namespace Own.Blockchain.Public.Core.Tests

open System
open System.Collections.Generic
open Own.Common
open Own.Blockchain.Common
open Own.Blockchain.Public.Core
open Own.Blockchain.Public.Core.Consensus
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Core.Dtos
open Own.Blockchain.Public.Crypto

module ConsensusTestHelpers =

    let proposeDummyBlock proposerAddress blockNumber =
        {
            Block.Header =
                {
                    BlockHeader.Number = blockNumber
                    Hash = Helpers.randomString () |> BlockHash
                    PreviousHash = Helpers.randomString () |> BlockHash
                    ConfigurationBlockNumber = BlockNumber 0L
                    Timestamp = Utils.getUnixTimestamp () |> Timestamp
                    ProposerAddress = proposerAddress
                    TxSetRoot = Helpers.randomString () |> MerkleTreeRoot
                    TxResultSetRoot = Helpers.randomString () |> MerkleTreeRoot
                    StateRoot = Helpers.randomString () |> MerkleTreeRoot
                    ConfigurationRoot = Helpers.randomString () |> MerkleTreeRoot
                }
            TxSet =
                [
                    Helpers.randomString () |> TxHash
                ]
            Configuration = None
        }

    let isPropose (consensusMessageEnvelope : ConsensusMessageEnvelope) =
        match consensusMessageEnvelope.ConsensusMessage with
        | Propose _ -> true
        | _ -> false

    let isVoteForBlock (consensusMessageEnvelope : ConsensusMessageEnvelope) =
        match consensusMessageEnvelope.ConsensusMessage with
        | Vote (Some _) -> true
        | _ -> false

    let isVoteForNone (consensusMessageEnvelope : ConsensusMessageEnvelope) =
        match consensusMessageEnvelope.ConsensusMessage with
        | Vote None -> true
        | _ -> false

    let isCommitForBlock (consensusMessageEnvelope : ConsensusMessageEnvelope) =
        match consensusMessageEnvelope.ConsensusMessage with
        | Commit (Some _) -> true
        | _ -> false

    let isCommitForNone (consensusMessageEnvelope : ConsensusMessageEnvelope) =
        match consensusMessageEnvelope.ConsensusMessage with
        | Commit None -> true
        | _ -> false

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Simulation Infrastructure
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    type ConsensusSimulationNetwork
        (
        ?proposeBlock : BlockchainAddress -> BlockNumber -> Result<Block, AppErrors> option,
        ?isValidBlock : BlockchainAddress -> Block -> bool,
        ?saveBlock : BlockNumber -> BlockEnvelopeDto -> Result<unit, AppErrors>,
        ?saveBlockToDb : BlockInfoDto -> Result<unit, AppErrors>,
        ?scheduleMessage : BlockchainAddress -> int -> (BlockchainAddress * ConsensusMessageEnvelope) -> unit,
        ?schedulePropose : BlockchainAddress -> int -> (BlockNumber * ConsensusRound) -> unit,
        ?scheduleTimeout : BlockchainAddress -> int -> (BlockNumber * ConsensusRound * ConsensusStep) -> unit,
        ?lastAppliedBlockNumber : BlockNumber
        ) =

        let _state = new Dictionary<BlockchainAddress, ConsensusState>()
        let _messages = new List<ConsensusMessageEnvelope>()

        member __.States
            with get () = _state

        member __.Messages
            with get () = _messages

        member __.StartConsensus(validators : BlockchainAddress list) =
            for validatorAddress in validators do
                let getValidators _ =
                    validators
                    |> Seq.map (fun a ->
                        {
                            ValidatorSnapshot.ValidatorAddress = a
                            NetworkAddress = ""
                            TotalStake = ChxAmount 0m
                        }
                    )
                    |> Seq.toList

                let getProposer blockNumber consensusRound =
                    getValidators ()
                    |> Validators.getProposer blockNumber consensusRound
                    |> fun vs -> vs.ValidatorAddress

                let getQualifiedMajority () =
                    Validators.calculateQualifiedMajority validators.Length

                let getValidQuorum () =
                    Validators.calculateValidQuorum validators.Length

                let sendConsensusMessage =
                    __.SendConsensusMessage validatorAddress

                let publishEvent _ =
                    ()

                let proposeBlock =
                    let dummyFn validatorAddress blockNumber =
                        proposeDummyBlock validatorAddress blockNumber
                        |> Ok
                        |> Some

                    (proposeBlock |? dummyFn) validatorAddress

                let txExists _ = true

                let requestTx _ = ()

                let isValidBlock =
                    match isValidBlock with
                    | Some f -> f validatorAddress
                    | None -> fun _ -> true

                let saveBlock =
                    saveBlock |? fun _ _ -> Ok ()

                let saveBlockToDb =
                    saveBlockToDb |? fun _ -> Ok ()

                let applyBlock _ =
                    Ok ()

                let scheduleMessage =
                    match scheduleMessage with
                    | Some f -> f validatorAddress
                    | None -> fun _ _ -> ()

                let schedulePropose =
                    match schedulePropose with
                    | Some f -> f validatorAddress
                    | None -> fun _ _ -> ()

                let scheduleTimeout =
                    match scheduleTimeout with
                    | Some f -> f validatorAddress
                    | None -> fun _ _ -> ()

                let getLastAppliedBlockNumber () =
                    lastAppliedBlockNumber |?> fun _ ->
                        __.States.[validatorAddress].Decisions.Keys
                        |> Seq.sortDescending
                        |> Seq.tryHead
                        |? BlockNumber 0L

                let state =
                    new ConsensusState(
                        getLastAppliedBlockNumber,
                        getValidators,
                        proposeBlock,
                        txExists,
                        requestTx,
                        isValidBlock,
                        saveBlock,
                        saveBlockToDb,
                        applyBlock,
                        sendConsensusMessage,
                        publishEvent,
                        scheduleMessage,
                        schedulePropose,
                        scheduleTimeout,
                        0, // No need to pass in the value, because test will trigger the retry explicitly.
                        0, // No need to pass in the value, because test will trigger the retry explicitly.
                        0, // No need to pass in the value, because test will trigger the timout explicitly.
                        0, // No need to pass in the value, because test will trigger the timout explicitly.
                        0, // No need to pass in the value, because test will trigger the timout explicitly.
                        validatorAddress
                    )

                _state.Add(validatorAddress, state)

            for s in _state.Values do
                s.HandleConsensusCommand ConsensusCommand.Synchronize

        member private __.SendConsensusMessage validatorAddress blockNumber consensusRound consensusMessage =
            {
                ConsensusMessageEnvelope.BlockNumber = blockNumber
                Round = consensusRound
                ConsensusMessage = consensusMessage
                Signature = Signature validatorAddress.Value // Just testing convenience.
            }
            |> _messages.Add

        member __.DeliverMessages(?recipientAddresses : BlockchainAddress seq) =
            let messages = _messages |> Seq.toList
            _messages.Clear()

            let states =
                _state
                |> Seq.ofDict
                |> Seq.filter (fun (a, _) ->
                    match recipientAddresses with
                    | None -> true
                    | Some rs -> rs |> Seq.contains a
                )

            seq {
                for m in messages do
                    let senderAddress =
                        m.Signature.Value // Just testing convenience.
                        |> BlockchainAddress

                    for address, state in states do
                        yield senderAddress, m, state
            }
            |> Seq.shuffle
            |> Seq.iter (fun (a, m, s) -> (a, m) |> ConsensusCommand.Message |> s.HandleConsensusCommand)

        member __.PrintTheState(log) =
            for m in __.Messages do
                log (sprintf "MESSAGE: %A" m)
            for s in __.States do
                log (sprintf "\nVALIDATOR %A STATE:" s.Key)
                for v in s.Value.PrintCurrentState() do
                    log (sprintf "%s" v)
