namespace Own.Blockchain.Public.Core.Tests

open System.Collections.Generic
open Own.Common.FSharp
open Own.Blockchain.Common
open Own.Blockchain.Public.Core
open Own.Blockchain.Public.Core.Consensus
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Core.Dtos
open Own.Blockchain.Public.Core.Events
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

    let isPropose (_, consensusMessageEnvelope : ConsensusMessageEnvelope) =
        match consensusMessageEnvelope.ConsensusMessage with
        | Propose _ -> true
        | _ -> false

    let isVoteForBlock (_, consensusMessageEnvelope : ConsensusMessageEnvelope) =
        match consensusMessageEnvelope.ConsensusMessage with
        | Vote (Some _) -> true
        | _ -> false

    let isVoteForNone (_, consensusMessageEnvelope : ConsensusMessageEnvelope) =
        match consensusMessageEnvelope.ConsensusMessage with
        | Vote None -> true
        | _ -> false

    let isCommitForBlock (_, consensusMessageEnvelope : ConsensusMessageEnvelope) =
        match consensusMessageEnvelope.ConsensusMessage with
        | Commit (Some _) -> true
        | _ -> false

    let isCommitForNone (_, consensusMessageEnvelope : ConsensusMessageEnvelope) =
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
        validators : BlockchainAddress list,
        ?proposeBlock : BlockchainAddress -> BlockNumber -> Result<Block, AppErrors> option,
        ?isValidBlock : BlockchainAddress -> Block -> bool,
        ?scheduleMessage : BlockchainAddress -> int -> (BlockchainAddress * ConsensusMessageEnvelope) -> unit,
        ?scheduleStateResponse : BlockchainAddress -> int -> (BlockNumber * ConsensusStateResponse) -> unit,
        ?schedulePropose : BlockchainAddress -> int -> (BlockNumber * ConsensusRound) -> unit,
        ?scheduleTimeout : BlockchainAddress -> (BlockNumber * ConsensusRound * ConsensusStep) -> unit,
        ?isValidatorBlacklisted : BlockchainAddress * BlockNumber * BlockNumber -> bool,
        ?lastAppliedBlockNumber : BlockNumber
        ) =

        let _state = new Dictionary<BlockchainAddress, ConsensusState>()
        let _decisions = new Dictionary<BlockchainAddress, Dictionary<BlockNumber, Block>>()
        let _messages = new List<BlockchainAddress * ConsensusMessageEnvelope>()
        let _events = new List<BlockchainAddress * AppEvent>()

        let _persistedState = new Dictionary<BlockchainAddress, ConsensusStateInfo>()
        let _persistedMessages =
            new Dictionary<
                BlockchainAddress,
                Dictionary<BlockNumber * ConsensusRound * ConsensusStep, ConsensusMessageEnvelope>
                >()

        member __.Validators
            with get () = validators

        member __.States
            with get () = _state

        member __.Decisions
            with get () = _decisions

        member __.Messages
            with get () = _messages

        member __.Events
            with get () = _events

        member __.StartConsensus() =
            for v in validators do
                __.StartValidator v

            for s in _state.Values do
                s.StartConsensus()

        member private __.StartValidator validatorAddress =
            let persistConsensusState s =
                if _persistedState.ContainsKey validatorAddress then
                    _persistedState.[validatorAddress] <- s
                else
                    _persistedState.Add(validatorAddress, s)

            let restoreConsensusState () =
                match _persistedState.TryGetValue validatorAddress with
                | true, s -> Some s
                | _ -> None

            let restoreConsensusMessages () =
                match _persistedMessages.TryGetValue validatorAddress with
                | true, ms ->
                    ms
                    |> List.ofDict
                    |> List.map snd
                | _ -> []

            _decisions.Add(validatorAddress, Dictionary<BlockNumber, Block>())

            let getLastAppliedBlockNumber () =
                lastAppliedBlockNumber |?> fun _ ->
                    _decisions.[validatorAddress].Keys
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

            let ensureBlockReady _ = true

            let isValidBlock =
                match isValidBlock with
                | Some f -> f validatorAddress
                | None -> fun _ -> true

            let sendConsensusMessage =
                __.SendConsensusMessage validatorAddress

            let sendConsensusState =
                __.SendConsensusState validatorAddress

            let requestConsensusState = ignore

            let publishEvent event =
                _events.Add (validatorAddress, event)

                match event with
                | BlockCommitted (blockNumber, envelopeDto) ->
                    let envelope = envelopeDto |> Mapping.blockEnvelopeFromDto
                    _decisions.[validatorAddress].Add(blockNumber, envelope.Block)

                    _persistedState.Remove validatorAddress |> ignore

                    _persistedMessages.[validatorAddress]
                    |> List.ofDict
                    |> List.filter (fun ((bn, _, _), _) -> bn <= blockNumber)
                    |> List.iter (fst >> _persistedMessages.[validatorAddress].Remove >> ignore)
                | _ -> ()

            let scheduleMessage =
                match scheduleMessage with
                | Some f -> f validatorAddress
                | None -> fun _ _ -> ()

            let scheduleStateResponse =
                match scheduleStateResponse with
                | Some f -> f validatorAddress
                | None -> fun _ _ -> ()

            let schedulePropose =
                match schedulePropose with
                | Some f -> f validatorAddress
                | None -> fun _ _ -> ()

            let scheduleTimeout =
                match scheduleTimeout with
                | Some f -> f validatorAddress
                | None -> fun _ -> ()

            let timeoutForRound _ _ =
                1000

            let verifyConsensusMessage =
                __.VerifyConsensusMessage

            let state =
                new ConsensusState(
                    persistConsensusState,
                    restoreConsensusState,
                    restoreConsensusMessages,
                    getLastAppliedBlockNumber,
                    getValidators,
                    isValidatorBlacklisted,
                    proposeBlock,
                    ensureBlockReady,
                    isValidBlock,
                    verifyConsensusMessage,
                    sendConsensusMessage,
                    sendConsensusState,
                    requestConsensusState,
                    publishEvent,
                    scheduleMessage,
                    scheduleStateResponse,
                    schedulePropose,
                    scheduleTimeout,
                    timeoutForRound,
                    0, // No need to pass in the value, because test will trigger the retry explicitly.
                    0, // No need to pass in the value, because test will trigger the retry explicitly.
                    0, // No need to pass in the value, because test will trigger the request explicitly.
                    validatorAddress
                )

            _state.Add(validatorAddress, state)

        member private __.VerifyConsensusMessage (envelope : ConsensusMessageEnvelopeDto) =
            let envelope = envelope |> Mapping.consensusMessageEnvelopeFromDto
            let consensusStep = envelope.ConsensusMessage |> Mapping.consensusStepFromMessage

            match envelope.Signature.Value.Split('_') with
            | [| b; r; s; a |] ->
                let signedBlockNumber = b |> int64 |> BlockNumber
                let signedRound = r |> int32 |> ConsensusRound
                let signedStep = s |> byte |> Mapping.consensusStepFromCode
                let signerAddress = a |> BlockchainAddress

                if envelope.BlockNumber <> signedBlockNumber then
                    sprintf "Different block number (%i vs signed %i)"
                        envelope.BlockNumber.Value
                        signedBlockNumber.Value
                    |> Result.appError
                elif envelope.Round <> signedRound then
                    sprintf "Different round (%i vs signed %i)" envelope.Round.Value signedRound.Value
                    |> Result.appError
                elif consensusStep <> signedStep then
                    sprintf "Different step (%A vs signed %A)" consensusStep signedStep
                    |> Result.appError
                else
                    Ok (signerAddress, envelope)
            | _ ->
                sprintf "Dummy signature must contain four components (%s)" envelope.Signature.Value
                |> Result.appError

        member private __.SendConsensusMessage validatorAddress blockNumber consensusRound consensusMessage =
            let dummySignature =
                sprintf "%i_%i_%i_%s"
                    blockNumber.Value
                    consensusRound.Value
                    (consensusMessage |> Mapping.consensusStepFromMessage |> Mapping.consensusStepToCode)
                    validatorAddress.Value
                |> Signature

            let consensusMessageEnvelope =
                {
                    ConsensusMessageEnvelope.BlockNumber = blockNumber
                    Round = consensusRound
                    ConsensusMessage = consensusMessage
                    Signature = dummySignature
                }

            _messages.Add(validatorAddress, consensusMessageEnvelope)

            if not (_persistedMessages.ContainsKey validatorAddress) then
                _persistedMessages.Add(
                    validatorAddress,
                    Dictionary<BlockNumber * ConsensusRound * ConsensusStep, ConsensusMessageEnvelope>()
                )

            let consensusStep = consensusMessage |> Mapping.consensusStepFromMessage
            _persistedMessages.[validatorAddress].Add(
                (blockNumber, consensusRound, consensusStep),
                consensusMessageEnvelope
            )

        member __.DeliverMessages
            (
            ?sendFilter : BlockchainAddress * BlockchainAddress * ConsensusMessageEnvelope -> bool,
            ?delayFilter : BlockchainAddress * BlockchainAddress * ConsensusMessageEnvelope -> bool
            ) =

            let messages = _messages |> Seq.toList
            _messages.Clear()

            let shouldSend = sendFilter |? fun _ -> true
            let shouldDelay = delayFilter |? fun _ -> false

            let states = _state |> Seq.ofDict

            seq {
                for (senderAddress, msg) in messages do
                    for address, state in states do
                        if shouldSend (senderAddress, address, msg) || address = senderAddress then
                            yield senderAddress, msg, state
                        if shouldDelay (senderAddress, address, msg) then
                            if not (_messages.Contains (senderAddress, msg)) then
                                _messages.Add (senderAddress, msg)
            }
            |> Seq.shuffle
            |> Seq.iter (fun (a, m, s) -> (a, m) |> ConsensusCommand.Message |> s.HandleConsensusCommand)

        member __.PropagateBlock validatorAddress blockNumber =
            let block = _decisions.[validatorAddress].[blockNumber]
            validators
            |> List.except [validatorAddress]
            |> List.iter (fun v ->
                if _decisions.ContainsKey v && not (_decisions.[v].ContainsKey blockNumber) then
                    _decisions.[v].Add(blockNumber, block)
            )

        member __.RemoveMessages condition =
            _messages
            |> Seq.filter condition
            |> Seq.toList
            |> List.iter (fun (s, m) ->
                if not (_messages.Remove (s, m)) then
                    failwithf "Didn't remove message from %s: %A" s.Value m
            )

        member __.ResetValidator validatorAddress =
            __.CrashValidator validatorAddress
            _persistedState.Remove validatorAddress |> ignore
            _persistedMessages.[validatorAddress].Clear()
            __.StartValidator validatorAddress
            _state.[validatorAddress].StartConsensus()

        member __.CrashValidator validatorAddress =
            if not (_state.Remove validatorAddress) then
                failwithf "Didn't remove state for crashed validator %s" validatorAddress.Value
            if not (_decisions.Remove validatorAddress) then
                failwithf "Didn't remove decisions for crashed validator %s" validatorAddress.Value
            __.RemoveMessages (fun (sender, message) -> sender = validatorAddress)

        member __.RecoverValidator validatorAddress =
            __.StartValidator validatorAddress

            // Mimicking the block synchronization process by getting the decided blocks from others.
            _decisions
            |> List.ofDict
            |> List.collect (snd >> List.ofDict)
            |> List.distinct
            |> List.sort
            |> List.iter _decisions.[validatorAddress].Add

            _state.[validatorAddress].HandleConsensusCommand Synchronize

            __.RequestConsensusState validatorAddress

        member __.RequestConsensusState validatorAddress =
            let consensusStateRequest =
                {
                    ConsensusStateRequest.ValidatorAddress = validatorAddress
                }

            let peerId =
                validatorAddress.Value
                |> Hashing.decode
                |> PeerNetworkIdentity

            for v in validators do
                if v <> validatorAddress && _state.ContainsKey v then
                    ConsensusCommand.StateRequested (consensusStateRequest, peerId)
                    |> _state.[v].HandleConsensusCommand

        member private __.SendConsensusState validatorAddress recipientPeerNetworkIdentity consensusStateResponse =
            let recipientValidatorAddress =
                recipientPeerNetworkIdentity.Value
                |> Hashing.encode
                |> BlockchainAddress

            if recipientValidatorAddress = validatorAddress then
                failwithf "Shouldn't send consensus state to self: %s" validatorAddress.Value

            ConsensusCommand.StateReceived consensusStateResponse
            |> _state.[recipientValidatorAddress].HandleConsensusCommand

        member __.PrintTheState(log) =
            for m in __.Messages do
                log (sprintf "MESSAGE: %A" m)
            for e in __.Events do
                log (sprintf "EVENT: %A" e)
            for s in __.States |> Seq.sortBy (fun s -> validators |> List.findIndex (fun v -> v = s.Key)) do
                log (sprintf "\nVALIDATOR %A STATE:" s.Key)
                for v in s.Value.PrintCurrentState() do
                    log (sprintf "%s" v)
