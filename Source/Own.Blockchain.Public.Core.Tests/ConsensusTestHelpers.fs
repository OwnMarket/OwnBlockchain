namespace Own.Blockchain.Public.Core.Tests

open System.Collections.Generic
open Own.Common.FSharp
open Own.Blockchain.Common
open Own.Blockchain.Public.Core.Consensus
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Core.Events

module ConsensusTestHelpers =

    let proposeDummyBlock proposerAddress blockNumber =
        {
            Block.Header =
                {
                    BlockHeader.Number = blockNumber
                    Hash = Helpers.randomString () |> BlockHash
                    PreviousHash = Helpers.randomString () |> BlockHash
                    ConfigurationBlockNumber = BlockNumber 0L
                    Timestamp = Utils.getNetworkTimestamp () |> Timestamp
                    ProposerAddress = proposerAddress
                    TxSetRoot = Helpers.randomString () |> MerkleTreeRoot
                    TxResultSetRoot = Helpers.randomString () |> MerkleTreeRoot
                    EquivocationProofsRoot = Helpers.randomString () |> MerkleTreeRoot
                    EquivocationProofResultsRoot = Helpers.randomString () |> MerkleTreeRoot
                    StateRoot = Helpers.randomString () |> MerkleTreeRoot
                    StakingRewardsRoot = Helpers.randomString () |> MerkleTreeRoot
                    ConfigurationRoot = Helpers.randomString () |> MerkleTreeRoot
                }
            TxSet =
                [
                    Helpers.randomString () |> TxHash
                ]
            EquivocationProofs = []
            StakingRewards = []
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

    let createEquivocationMessage
        consensusMessage
        (senderAddress, consensusMessageEnvelope : ConsensusMessageEnvelope)
        =

        { consensusMessageEnvelope with
            ConsensusMessage = consensusMessage
            Signature = Signature (consensusMessageEnvelope.Signature.Value + "_EQ")
        }

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Simulation Infrastructure
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    type ConsensusSimulationNetwork
        (
        ?proposeBlock : BlockchainAddress -> BlockNumber -> Result<Block, AppErrors> option,
        ?isValidBlock : BlockchainAddress -> Block -> bool,
        ?scheduleMessage : BlockchainAddress -> int -> (BlockchainAddress * ConsensusMessageEnvelope) -> unit,
        ?schedulePropose : BlockchainAddress -> int -> (BlockNumber * ConsensusRound) -> unit,
        ?scheduleTimeout : BlockchainAddress -> int -> (BlockNumber * ConsensusRound * ConsensusStep) -> unit,
        ?isValidatorBlacklisted : BlockchainAddress * BlockNumber * BlockNumber -> bool,
        ?lastAppliedBlockNumber : BlockNumber
        ) =

        let _state = new Dictionary<BlockchainAddress, ConsensusState>()
        let _messages = new List<BlockchainAddress * ConsensusMessageEnvelope>()
        let _events = new List<AppEvent>()

        member __.States
            with get () = _state

        member __.Messages
            with get () = _messages

        member __.Events
            with get () = _events

        member __.StartConsensus(validators : BlockchainAddress list) =
            for validatorAddress in validators do
                let persistConsensusState _ = ()

                let restoreConsensusState () = None

                let restoreConsensusMessages () = []

                let getLastAppliedBlockNumber () =
                    lastAppliedBlockNumber |?> fun _ ->
                        __.States.[validatorAddress].Decisions.Keys
                        |> Seq.sortDescending
                        |> Seq.tryHead
                        |? BlockNumber 0L

                let getValidators _ =
                    validators
                    |> Seq.map (fun a ->
                        {
                            ValidatorSnapshot.ValidatorAddress = a
                            NetworkAddress = NetworkAddress ""
                            SharedRewardPercent = 0m
                            TotalStake = ChxAmount 0m
                        }
                    )
                    |> Seq.toList

                let isValidatorBlacklisted =
                    match isValidatorBlacklisted with
                    | Some f -> f
                    | None -> fun _ -> false

                let proposeBlock =
                    let dummyFn validatorAddress blockNumber =
                        proposeDummyBlock validatorAddress blockNumber
                        |> Ok
                        |> Some

                    (proposeBlock |? dummyFn) validatorAddress

                let txExists _ = true

                let equivocationProofExists _ = true

                let requestTx _ = ()

                let requestEquivocationProof _ = ()

                let isValidBlock =
                    match isValidBlock with
                    | Some f -> f validatorAddress
                    | None -> fun _ -> true

                let sendConsensusMessage =
                    __.SendConsensusMessage validatorAddress

                let sendConsensusState =
                    __.SendConsensusState validatorAddress

                let publishEvent event =
                    _events.Add event

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

                let state =
                    new ConsensusState(
                        persistConsensusState,
                        restoreConsensusState,
                        restoreConsensusMessages,
                        getLastAppliedBlockNumber,
                        getValidators,
                        isValidatorBlacklisted,
                        proposeBlock,
                        txExists,
                        equivocationProofExists,
                        requestTx,
                        requestEquivocationProof,
                        isValidBlock,
                        sendConsensusMessage,
                        sendConsensusState,
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
                s.StartConsensus()

        member private __.SendConsensusMessage validatorAddress blockNumber consensusRound consensusMessage =
            _messages.Add(
                validatorAddress,
                {
                    ConsensusMessageEnvelope.BlockNumber = blockNumber
                    Round = consensusRound
                    ConsensusMessage = consensusMessage
                    Signature = Signature validatorAddress.Value // Just for testing convenience.
                }
            )

        member private __.SendConsensusState validatorAddress recipientValidatorAddress state =
            () // TODO: Implement

        member private __.RequestConsensusState validatorAddress =
            () // TODO: Implement

        member __.DeliverMessages(?filter : BlockchainAddress * BlockchainAddress * ConsensusMessageEnvelope -> bool) =
            let messages = _messages |> Seq.toList
            _messages.Clear()

            let shouldSend = filter |? fun _ -> true

            let states = _state |> Seq.ofDict

            seq {
                for (senderAddress, msg) in messages do
                    for address, state in states do
                        if shouldSend (senderAddress, address, msg) then
                            yield senderAddress, msg, state
            }
            |> Seq.shuffle
            |> Seq.iter (fun (a, m, s) -> (a, m) |> ConsensusCommand.Message |> s.HandleConsensusCommand)

        member __.PrintTheState(log) =
            for m in __.Messages do
                log (sprintf "MESSAGE: %A" m)
            for e in __.Events do
                log (sprintf "EVENT: %A" e)
            for s in __.States do
                log (sprintf "\nVALIDATOR %A STATE:" s.Key)
                for v in s.Value.PrintCurrentState() do
                    log (sprintf "%s" v)
