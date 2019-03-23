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
        ?isValidatorBlacklisted : BlockchainAddress * BlockNumber * BlockNumber -> bool,
        ?proposeBlock : BlockchainAddress -> BlockNumber -> Result<Block, AppErrors> option,
        ?isValidBlock : BlockchainAddress -> Block -> bool
        ) =

        let _states = new Dictionary<BlockchainAddress, ConsensusState>()
        let _decisions = new Dictionary<BlockchainAddress, Dictionary<BlockNumber, Block>>()
        let _scheduledTimeouts = new Dictionary<BlockchainAddress, List<BlockNumber * ConsensusRound * ConsensusStep>>()
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
            with get () = _states

        member __.Decisions
            with get () = _decisions
        member __.DecisionCount
            with get () = _decisions |> Seq.sumBy (fun d -> d.Value.Count)

        member __.ScheduledTimeouts
            with get () = _scheduledTimeouts

        member __.Messages
            with get () = _messages

        member __.Events
            with get () = _events

        member __.PersistedStates
            with get () = _persistedState

        member __.PersistedMessages
            with get () = _persistedMessages

        member __.StartConsensus() =
            for v in validators do
                __.InstantiateValidator v

            for s in _states.Values do
                s.StartConsensus()

        member private __.InstantiateValidator validatorAddress =
            let persistConsensusState =
                __.PersistConsensusState validatorAddress

            let restoreConsensusState () =
                match _persistedState.TryGetValue validatorAddress with
                | true, s -> Some s
                | _ -> None

            let persistConsensusMessage =
                __.PersistConsensusMessage validatorAddress

            let restoreConsensusMessages () =
                match _persistedMessages.TryGetValue validatorAddress with
                | true, ms ->
                    ms
                    |> List.ofDict
                    |> List.map snd
                | _ -> []

            if not (_decisions.ContainsKey validatorAddress) then
                _decisions.Add(validatorAddress, Dictionary<BlockNumber, Block>())

            let getLastAppliedBlockNumber () =
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

            let scheduleMessage _ _ = ()

            let scheduleStateResponse _ _ = ()

            let schedulePropose _ _ = ()

            _scheduledTimeouts.Add(validatorAddress, new List<BlockNumber * ConsensusRound * ConsensusStep>())

            let scheduleTimeout =
                _scheduledTimeouts.[validatorAddress].Add

            let timeoutForRound _ _ = 1000

            let verifyConsensusMessage =
                __.VerifyConsensusMessage

            let state =
                new ConsensusState(
                    persistConsensusState,
                    restoreConsensusState,
                    persistConsensusMessage,
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

            _states.Add(validatorAddress, state)

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

        member private __.PersistConsensusState validatorAddress s =
            if _persistedState.ContainsKey validatorAddress then
                _persistedState.[validatorAddress] <- s
            else
                _persistedState.Add(validatorAddress, s)

        member private __.PersistConsensusMessage validatorAddress envelope =
            if not (_persistedMessages.ContainsKey validatorAddress) then
                _persistedMessages.Add(
                    validatorAddress,
                    Dictionary<BlockNumber * ConsensusRound * ConsensusStep, ConsensusMessageEnvelope>()
                )

            let consensusStep = envelope.ConsensusMessage |> Mapping.consensusStepFromMessage
            _persistedMessages.[validatorAddress].Add(
                (envelope.BlockNumber, envelope.Round, consensusStep),
                envelope
            )

        member private __.SendConsensusMessage
            validatorAddress
            blockNumber
            consensusRound
            consensusVariables
            consensusMessage
            =

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

            __.PersistConsensusMessage validatorAddress consensusMessageEnvelope
            __.PersistConsensusState validatorAddress consensusVariables

            _messages.Add(validatorAddress, consensusMessageEnvelope)

        member __.DeliverMessages
            (
            ?sendFilter : BlockchainAddress * BlockchainAddress * ConsensusMessageEnvelope -> bool,
            ?delayFilter : BlockchainAddress * BlockchainAddress * ConsensusMessageEnvelope -> bool
            ) =

            let messages = _messages |> Seq.toList
            _messages.Clear()

            let shouldSend = sendFilter |? fun _ -> true
            let shouldDelay = delayFilter |? fun _ -> false

            let states = _states |> Seq.ofDict

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
            |> List.filter (fun v -> v <> validatorAddress && _states.ContainsKey v) // Ignore crashed validators
            |> List.iter (fun v ->
                if _states.ContainsKey v && not (_decisions.[v].ContainsKey blockNumber) then
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

        member __.IsTimeoutScheduled(validatorAddress, blockNumber, consensusRound, consensusStep) =
            _scheduledTimeouts.[validatorAddress]
            |> Seq.contains (blockNumber, consensusRound, consensusStep)

        member __.TriggerScheduledTimeout(validatorAddress, blockNumber, consensusRound, consensusStep) =
            let timeoutKey = blockNumber, consensusRound, consensusStep
            if _scheduledTimeouts.[validatorAddress].Remove timeoutKey then
                ConsensusCommand.Timeout timeoutKey |> _states.[validatorAddress].HandleConsensusCommand
            else
                failwithf "Didn't remove scheduled timeout: %A" timeoutKey

            if _scheduledTimeouts.[validatorAddress].Contains timeoutKey then
                failwithf "Scheduled timeout stil in the list: %A" timeoutKey

        member __.ResetValidator validatorAddress =
            __.CrashValidator validatorAddress
            _decisions.Remove validatorAddress |> ignore
            _persistedState.Remove validatorAddress |> ignore
            _persistedMessages.[validatorAddress].Clear()
            __.InstantiateValidator validatorAddress
            _states.[validatorAddress].StartConsensus()

        member __.CrashValidator validatorAddress =
            if not (_states.Remove validatorAddress) then
                failwithf "Didn't remove state for crashed validator %s" validatorAddress.Value
            if not (_scheduledTimeouts.Remove validatorAddress) then
                failwithf "Didn't remove scheduled timeouts for crashed validator %s" validatorAddress.Value
            __.RemoveMessages (fun (sender, message) -> sender = validatorAddress)

        member __.RecoverValidator validatorAddress =
            __.InstantiateValidator validatorAddress
            _states.[validatorAddress].StartConsensus()

            // Mimicking the block synchronization process by getting the decided blocks from others.
            _decisions
            |> List.ofDict
            |> List.filter (fun (v, _) -> v <> validatorAddress && _states.ContainsKey v) // Ignore crashed validators
            |> List.collect (snd >> List.ofDict)
            |> List.distinct
            |> List.sort
            |> List.iter _decisions.[validatorAddress].Add

            _states.[validatorAddress].HandleConsensusCommand Synchronize

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
                if v <> validatorAddress && _states.ContainsKey v then
                    ConsensusCommand.StateRequested (consensusStateRequest, peerId)
                    |> _states.[v].HandleConsensusCommand

        member private __.SendConsensusState validatorAddress recipientPeerNetworkIdentity consensusStateResponse =
            let recipientValidatorAddress =
                recipientPeerNetworkIdentity.Value
                |> Hashing.encode
                |> BlockchainAddress

            if recipientValidatorAddress = validatorAddress then
                failwithf "Shouldn't send consensus state to self: %s" validatorAddress.Value

            ConsensusCommand.StateReceived consensusStateResponse
            |> _states.[recipientValidatorAddress].HandleConsensusCommand

        member __.PrintTheState(log) =
            __.Messages
            |> Seq.iter (sprintf "MESSAGE: %A" >> log)

            __.Events
            |> Seq.iter (sprintf "EVENT: %A" >> log)

            __.Decisions
            |> Seq.sortBy (fun s -> validators |> List.findIndex (fun v -> v = s.Key))
            |> List.ofDict
            |> List.iter (sprintf "DECISIONS: %A" >> log)

            __.ScheduledTimeouts
            |> Seq.sortBy (fun s -> validators |> List.findIndex (fun v -> v = s.Key))
            |> List.ofDict
            |> List.iter (sprintf "SCHEDULED TIMEOUTS: %A" >> log)

            __.States
            |> Seq.sortBy (fun s -> validators |> List.findIndex (fun v -> v = s.Key))
            |> Seq.iter (fun s ->
                log (sprintf "\nVALIDATOR %A STATE:" s.Key)
                for v in s.Value.PrintCurrentState() do
                    log (sprintf "%s" v)
            )
