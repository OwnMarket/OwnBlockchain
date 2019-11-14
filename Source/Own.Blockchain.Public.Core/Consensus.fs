namespace Own.Blockchain.Public.Core

open System
open System.Collections.Generic
open Own.Common.FSharp
open Own.Blockchain.Common
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Core.Dtos
open Own.Blockchain.Public.Core.Events

module Consensus =

    type ConsensusState
        (
        persistConsensusState : ConsensusStateInfo -> unit,
        restoreConsensusState : unit -> ConsensusStateInfo option,
        persistConsensusMessage : ConsensusMessageEnvelope -> unit,
        restoreConsensusMessages : unit -> ConsensusMessageEnvelope list,
        getLastAppliedBlockNumber : unit -> BlockNumber,
        getLastAppliedBlockTimestamp : unit -> Timestamp,
        getValidatorsAtHeight : BlockNumber -> ValidatorSnapshot list,
        isValidatorBlacklisted : BlockchainAddress * BlockNumber * BlockNumber -> bool,
        proposeBlock : BlockNumber -> Result<Block, AppErrors> option,
        ensureBlockReady : Block -> bool,
        isValidBlock : Block -> bool,
        verifyConsensusMessage :
            ConsensusMessageEnvelopeDto -> Result<BlockchainAddress * ConsensusMessageEnvelope, AppErrors>,
        sendConsensusMessage : BlockNumber -> ConsensusRound -> ConsensusStateInfo -> ConsensusMessage -> unit,
        sendConsensusState : PeerNetworkIdentity -> ConsensusStateResponse -> unit,
        requestConsensusState : ConsensusRound -> BlockchainAddress option -> unit,
        canParticipateInConsensus : BlockNumber -> bool option,
        publishEvent : AppEvent -> unit,
        scheduleMessage : int -> BlockchainAddress * ConsensusMessageEnvelope -> unit,
        scheduleStateResponse : int -> BlockNumber * ConsensusStateResponse -> unit,
        schedulePropose : int -> BlockNumber * ConsensusRound -> unit,
        scheduleTimeout : BlockNumber * ConsensusRound * ConsensusStep -> unit,
        timeoutForRound : ConsensusStep -> ConsensusRound -> int,
        messageRetryingInterval : int,
        proposeRetryingInterval : int,
        staleConsensusDetectionInterval : int,
        emptyBlocksEnabled : bool,
        minEmptyBlockTime : int,
        validatorAddress : BlockchainAddress
        ) =

        let mutable _validators = []
        let mutable _qualifiedMajority = 0
        let mutable _validQuorum = 0

        let mutable _roundStartTime = 0L // Used for stale round detection. Updated upon requesting consensus state.

        let mutable _blockNumber = BlockNumber 0L
        let mutable _round = ConsensusRound 0
        let mutable _step = ConsensusStep.Propose
        let mutable _lockedBlock = None
        let mutable _lockedRound = ConsensusRound -1
        let mutable _validBlock = None
        let mutable _validRound = ConsensusRound -1
        let mutable _validBlockSignatures = []

        let _decisions = new Dictionary<BlockNumber, Block>()
        let _scheduledTimeouts = new HashSet<ConsensusRound * ConsensusStep>()

        let _proposals =
            new Dictionary<BlockNumber * ConsensusRound * BlockchainAddress, Block * ConsensusRound * Signature>()
        let _votes =
            new Dictionary<BlockNumber * ConsensusRound * BlockchainAddress, BlockHash option * Signature>()
        let _commits =
            new Dictionary<BlockNumber * ConsensusRound * BlockchainAddress, BlockHash option * Signature>()

        member __.StartConsensus() =
            __.RestoreState()

            if _blockNumber > BlockNumber 0L && _step = ConsensusStep.Propose then
                if Validators.getProposer _blockNumber _round _validators <> validatorAddress
                    || not (_proposals.ContainsKey(_blockNumber, _round, validatorAddress))
                then
                    __.StartRound(_round)

                if _scheduledTimeouts.Add(_round, ConsensusStep.Propose) then
                    scheduleTimeout (_blockNumber, _round, ConsensusStep.Propose)

            __.Synchronize()
            __.StartStaleConsensusDetection()

        member __.HandleConsensusCommand(command : ConsensusCommand) =
            match command with
            | Synchronize ->
                __.Synchronize()
            | Message (senderAddress, envelope) ->
                __.ProcessConsensusMessage(senderAddress, envelope, true)
            | RetryPropose (blockNumber, consensusRound) ->
                __.RetryPropose(blockNumber, consensusRound)
            | Timeout (blockNumber, consensusRound, consensusStep) ->
                match consensusStep with
                | ConsensusStep.Propose -> __.OnTimeoutPropose(blockNumber, consensusRound)
                | ConsensusStep.Vote -> __.OnTimeoutVote(blockNumber, consensusRound)
                | ConsensusStep.Commit -> __.OnTimeoutCommit(blockNumber, consensusRound)
            | StateRequested (stateRequest, peerIdentity) ->
                __.SendState(stateRequest, peerIdentity)
            | StateReceived stateResponse ->
                __.ApplyReceivedState(stateResponse)

        member private __.ProcessConsensusMessage(senderAddress, envelope : ConsensusMessageEnvelope, updateState) =
            if isValidatorBlacklisted (senderAddress, _blockNumber, envelope.BlockNumber) then
                envelope.ConsensusMessage.CaseName
                |> Log.warningf "Validator %s is blacklisted - %s consensus message ignored" senderAddress.Value
            elif envelope.BlockNumber >= _blockNumber then
                let key = envelope.BlockNumber, envelope.Round, senderAddress

                match envelope.ConsensusMessage with
                | Propose (block, vr) ->
                    if block.Header.Number = envelope.BlockNumber then
                        let networkTime = Utils.getNetworkTimestamp ()
                        let proposeTimeout = timeoutForRound ConsensusStep.Propose _round |> int64
                        let maxValidBlockTime = _roundStartTime + proposeTimeout
                        if block.Header.Number = _blockNumber
                            && block.Header.Timestamp.Value > networkTime
                            && block.Header.Timestamp.Value < maxValidBlockTime
                        then
                            let timeToPostpone = block.Header.Timestamp.Value - networkTime |> Convert.ToInt32
                            scheduleMessage timeToPostpone (senderAddress, envelope)
                        else
                            if ensureBlockReady block then
                                if _proposals.TryAdd(key, (block, vr, envelope.Signature)) then
                                    if senderAddress <> validatorAddress then
                                        persistConsensusMessage envelope
                                    if updateState then
                                        __.UpdateState()
                                else
                                    __.DetectEquivocation(envelope, senderAddress)
                            elif envelope.Round >= _round then
                                scheduleMessage messageRetryingInterval (senderAddress, envelope)
                | Vote blockHash ->
                    if _votes.TryAdd(key, (blockHash, envelope.Signature)) then
                        Log.debugf "Vote added: %s / %i / %i / %A"
                            senderAddress.Value
                            envelope.BlockNumber.Value
                            envelope.Round.Value
                            blockHash
                        if updateState then
                            __.UpdateState()
                    else
                        __.DetectEquivocation(envelope, senderAddress)
                | Commit blockHash ->
                    if _commits.TryAdd(key, (blockHash, envelope.Signature)) then
                        Log.debugf "Commit added: %s / %i / %i / %A"
                            senderAddress.Value
                            envelope.BlockNumber.Value
                            envelope.Round.Value
                            blockHash
                        if updateState then
                            __.UpdateState()
                    else
                        __.DetectEquivocation(envelope, senderAddress)

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // State Management
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        member private __.GetConsensusVariables() =
            {
                ConsensusStateInfo.BlockNumber = _blockNumber
                ConsensusRound = _round
                ConsensusStep = _step
                LockedBlock = _lockedBlock
                LockedRound = _lockedRound
                ValidBlock = _validBlock
                ValidRound = _validRound
                ValidBlockSignatures = _validBlockSignatures
            }

        member private __.PersistState() =
            __.GetConsensusVariables()
            |> persistConsensusState

        member private __.RestoreState() =
            let validValueCertificateVotes =
                restoreConsensusState ()
                |> Option.map (fun state ->
                    Log.info "Restoring consensus state variables..."

                    _blockNumber <- state.BlockNumber
                    _round <- state.ConsensusRound
                    _step <- state.ConsensusStep
                    _lockedBlock <- state.LockedBlock
                    _lockedRound <- state.LockedRound
                    _validBlock <- state.ValidBlock
                    _validRound <- state.ValidRound
                    _validBlockSignatures <- state.ValidBlockSignatures

                    // Restore votes from the valid value certificate.
                    if not state.ValidBlockSignatures.IsEmpty then
                        Log.info "Restoring votes from the valid value certificate..."
                    state.ValidBlockSignatures
                    |> List.toArray
                    |> Array.Parallel.map (fun signature ->
                        {
                            ConsensusMessageEnvelope.BlockNumber = state.BlockNumber
                            Round = state.ValidRound
                            ConsensusMessage =
                                state.ValidBlock
                                |> Option.map (fun b -> b.Header.Hash)
                                |> Vote
                            Signature = signature
                        }
                        |> Mapping.consensusMessageEnvelopeToDto
                        |> verifyConsensusMessage
                    )
                    |> Array.choose (function
                        | Ok v -> Some v
                        | Error e ->
                            Log.appErrors e
                            None
                    )
                    |> Array.toList
                )
                |? []

            restoreConsensusMessages ()
            |> tee (fun ms -> if not ms.IsEmpty then Log.info "Restoring persisted consensus messages...")
            |> List.sortBy (fun e -> e.BlockNumber, e.Round, e.ConsensusMessage)
            |> List.iter (fun envelope ->
                let senderAddress =
                    envelope
                    |> Mapping.consensusMessageEnvelopeToDto
                    |> verifyConsensusMessage
                    |> Result.handle
                        fst
                        (fun e ->
                            Log.appErrors e
                            failwithf "Cannot verify persisted consensus message: %A" envelope
                        )

                let key = envelope.BlockNumber, envelope.Round, senderAddress

                match envelope.ConsensusMessage with
                | Propose (b, r) -> _proposals.Add(key, (b, r, envelope.Signature))
                | Vote h -> _votes.Add(key, (h, envelope.Signature))
                | Commit h -> _commits.Add(key, (h, envelope.Signature))
            )

            __.SetValidators(_blockNumber - 1)

            if not validValueCertificateVotes.IsEmpty then
                Log.info "Adding restored valid value certificate votes..."

                let signers =
                    validValueCertificateVotes
                    |> List.map fst
                    |> Set.ofList
                    |> Set.intersect (_validators |> Set.ofList)

                for s, e in validValueCertificateVotes do
                    if signers.Contains s then
                        let key = e.BlockNumber, e.Round, s
                        match e.ConsensusMessage with
                        | Vote h ->
                            if not (_votes.TryAdd(key, (h, e.Signature))) && s <> validatorAddress then
                                Log.warningf "Cannot add restored valid value certificate vote: %s / %i / %i / %A"
                                    s.Value
                                    e.BlockNumber.Value
                                    e.Round.Value
                                    h
                        | m -> failwithf "Unexpected message created from the stored valid value certificate: %A" m

        member private __.StartStaleConsensusDetection() =
            let maxRoundDuration r =
                timeoutForRound ConsensusStep.Propose r
                + timeoutForRound ConsensusStep.Vote r
                + timeoutForRound ConsensusStep.Commit r
                |> int64

            let maxHeightDuration =
                int64 (minEmptyBlockTime * 1000)
                + maxRoundDuration (ConsensusRound 0) // Give one round time to commit an empty block.

            let rec loop () =
                async {
                    do! Async.Sleep staleConsensusDetectionInterval

                    if canParticipateInConsensus _blockNumber = Some true then
                        let currentTime = Utils.getNetworkTimestamp ()

                        // Detect stale round
                        let roundDuration = currentTime - _roundStartTime
                        if roundDuration > maxRoundDuration _round then
                            Log.warning "Stale consensus round detected"
                            requestConsensusState _round None
                        elif emptyBlocksEnabled then
                            // Detect stale height (relies on empty block pace)
                            let heightDuration = currentTime - (getLastAppliedBlockTimestamp ()).Value
                            if heightDuration > maxHeightDuration then
                                Log.warning "Stale consensus height detected"
                                requestConsensusState _round None

                        return! loop ()
                }

            loop ()
            |> Async.Start

        member private __.ApplyReceivedState(response) =
            let mutable shouldUpdateState = false

            let messages =
                response.Messages
                |> List.toArray
                |> Array.Parallel.map (Mapping.consensusMessageEnvelopeToDto >> verifyConsensusMessage)
                |> Array.choose (function
                    | Ok e -> Some e
                    | _ -> None
                )

            if messages.Length > 0 then
                for (s, e) in messages do
                    __.ProcessConsensusMessage(s, e, false)
                    shouldUpdateState <- true

            let response = {response with Messages = []} // We're done with the messages - no need to keep them.

            if response.ValidRound > _validRound then
                let isValidMessage, validBlock =
                    match response.ValidProposal with
                    | None -> true, None
                    | Some e ->
                        if e.BlockNumber = _blockNumber then
                            match e.ConsensusMessage with
                            | Propose (b, _) when b.Header.Number = e.BlockNumber ->
                                true, Some b
                            | _ ->
                                false, None
                        else
                            false, None

                if isValidMessage then
                    let votes =
                        response.ValidVoteSignatures
                        |> List.toArray
                        |> Array.Parallel.map (fun s ->
                            {
                                ConsensusMessageEnvelope.BlockNumber = _blockNumber
                                Round = response.ValidRound
                                ConsensusMessage =
                                    validBlock
                                    |> Option.map (fun b -> b.Header.Hash)
                                    |> Vote
                                Signature = s
                            }
                            |> Mapping.consensusMessageEnvelopeToDto
                            |> verifyConsensusMessage
                        )
                        |> Array.choose (function
                            | Ok v -> Some v
                            | Error e ->
                                Log.appErrors e
                                None
                        )

                    let signers =
                        votes
                        |> Array.map fst
                        |> Set.ofArray
                        |> Set.intersect (_validators |> Set.ofList)

                    if signers.Count >= _qualifiedMajority then
                        let blockReady =
                            match validBlock with
                            | None -> true
                            | Some b -> ensureBlockReady b

                        if not blockReady then
                            validBlock
                            |> Option.map (fun b -> b.Header.Hash.Value)
                            |> Log.debugf "Block %A not ready for valid value certificate"
                            scheduleStateResponse messageRetryingInterval (_blockNumber, response)
                        else
                            response.ValidProposal
                            |> Option.iter (fun e ->
                                e
                                |> Mapping.consensusMessageEnvelopeToDto
                                |> verifyConsensusMessage
                                |> Result.handle
                                    (fun (a, _) ->
                                        __.ProcessConsensusMessage(a, e, false)
                                        shouldUpdateState <- true
                                    )
                                    Log.appErrors
                            )

                            votes
                            |> Array.map (fun (_, e) -> e.BlockNumber, e.Round)
                            |> Array.distinct
                            |> Array.exactlyOne
                            |> Log.debugf "Received certificate with %i votes for %A" votes.Length

                            for s, e in votes do
                                if signers.Contains s then
                                    __.ProcessConsensusMessage(s, e, false)
                                    shouldUpdateState <- true
                    else
                        Log.warningf "Not enough valid value certificate signers (%i):\n%A" signers.Count signers
                else
                    Log.warningf "Incoming valid value certificate: %A / %A\nLocal state: %A / %A"
                        response.ValidRound
                        (validBlock |> Option.map (fun b -> b.Header.Hash))
                        _validRound
                        (_validBlock |> Option.map (fun b -> b.Header.Hash))

            if shouldUpdateState then
                __.UpdateState()

        member private __.SendState(request, peerIdentity) =
            if not (_validators |> List.contains request.ValidatorAddress) then
                Log.warningf "%s is not an active validator - consensus state request ignored"
                    request.ValidatorAddress.Value
            elif isValidatorBlacklisted (request.ValidatorAddress, _blockNumber, _blockNumber) then
                Log.warningf "Validator %s is blacklisted - consensus state request ignored"
                    request.ValidatorAddress.Value
            elif request.TargetValidatorAddress.IsNone || request.TargetValidatorAddress = Some validatorAddress then
                let messages =
                    [
                        // Send messages from requested round, as well as from current round.
                        let keys =
                            [
                                _blockNumber, request.ConsensusRound, validatorAddress
                                _blockNumber, _round, validatorAddress
                            ]
                            |> List.distinct

                        for key in keys do
                            if _proposals.ContainsKey(key) then
                                let (b, vr, s) = _proposals.[key]
                                yield {
                                    ConsensusMessageEnvelope.BlockNumber = _blockNumber
                                    Round = request.ConsensusRound
                                    ConsensusMessage = Propose (b, vr)
                                    Signature = s
                                }

                            if _votes.ContainsKey(key) then
                                let (bh, s) = _votes.[key]
                                yield {
                                    ConsensusMessageEnvelope.BlockNumber = _blockNumber
                                    Round = request.ConsensusRound
                                    ConsensusMessage = Vote bh
                                    Signature = s
                                }

                            if _commits.ContainsKey(key) then
                                let (bh, s) = _commits.[key]
                                yield {
                                    ConsensusMessageEnvelope.BlockNumber = _blockNumber
                                    Round = request.ConsensusRound
                                    ConsensusMessage = Commit bh
                                    Signature = s
                                }
                    ]

                let validProposal =
                    if _validRound < ConsensusRound 0 then
                        None
                    else
                        _validBlock
                        |> Option.map (fun _ ->
                            let validRoundProposer = Validators.getProposer _blockNumber _validRound _validators
                            let key = _blockNumber, _validRound, validRoundProposer
                            match _proposals.TryGetValue(key) with
                            | true, (b, vr, s) ->
                                {
                                    ConsensusMessageEnvelope.BlockNumber = _blockNumber
                                    Round = _validRound
                                    ConsensusMessage = Propose (b, vr)
                                    Signature = s
                                }
                            | _ ->
                                failwithf "Cannot find proposal corresponding to valid consensus value (Key: %A)" key
                        )

                let validVoteSignatures =
                    if _validRound < ConsensusRound 0 then
                        []
                    elif _validBlockSignatures.Length < _qualifiedMajority then
                        failwithf
                            "_validBlockSignatures has only %i entries - it should have at least %i for validRound %i"
                            _validBlockSignatures.Length
                            _qualifiedMajority
                            _validRound.Value
                    else
                        _validBlockSignatures

                {
                    ConsensusStateResponse.Messages = messages
                    ValidRound = _validRound
                    ValidProposal = validProposal
                    ValidVoteSignatures = validVoteSignatures
                }
                |> sendConsensusState peerIdentity

        member private __.Synchronize() =
            let lastAppliedBlockNumber = getLastAppliedBlockNumber ()
            let nextBlockNumber = lastAppliedBlockNumber + 1

            if _blockNumber <> nextBlockNumber then
                Log.notice "Synchronizing the consensus"
                _blockNumber <- nextBlockNumber
                __.SetValidators(lastAppliedBlockNumber)
                __.ResetState()
                __.StartRound(ConsensusRound 0)

        member private __.SetValidators(blockNumber : BlockNumber) =
            _validators <-
                blockNumber
                |> max (BlockNumber 0L)
                |> getValidatorsAtHeight
                |> List.map (fun v -> v.ValidatorAddress)
            _qualifiedMajority <- Validators.calculateQualifiedMajority _validators.Length
            _validQuorum <- Validators.calculateValidQuorum _validators.Length

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Core Logic
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        member private __.ResetState() =
            _lockedBlock <- None
            _lockedRound <- ConsensusRound -1
            _validBlock <- None
            _validRound <- ConsensusRound -1
            _validBlockSignatures <- []

            _scheduledTimeouts.Clear()

            _proposals
            |> List.ofDict
            |> List.filter (fun ((b, _, _), _) -> b < _blockNumber)
            |> List.iter (fun (key, _) -> _proposals.Remove(key) |> ignore)

            _votes
            |> List.ofDict
            |> List.filter (fun ((b, _, _), _) -> b < _blockNumber)
            |> List.iter (fun (key, _) -> _votes.Remove(key) |> ignore)

            _commits
            |> List.ofDict
            |> List.filter (fun ((b, _, _), _) -> b < _blockNumber)
            |> List.iter (fun (key, _) -> _commits.Remove(key) |> ignore)

            _decisions
            |> List.ofDict
            |> List.filter (fun (b, _) -> b < _blockNumber)
            |> List.iter (fun (key, _) -> _decisions.Remove(key) |> ignore)

        member private __.TryPropose() =
            let block =
                _validBlock
                |> Option.orElseWith (fun _ ->
                    let lastAppliedBlockNumber = getLastAppliedBlockNumber ()
                    let nextBlockNumber = lastAppliedBlockNumber + 1
                    if _blockNumber < nextBlockNumber then
                        Log.warningf "Consensus is at block %i - state is at block %i"
                            _blockNumber.Value
                            lastAppliedBlockNumber.Value
                        __.Synchronize()
                        None
                    elif _blockNumber > nextBlockNumber then
                        Log.warningf "Cannot propose block %i at this time due to block %i being last applied block"
                            _blockNumber.Value
                            lastAppliedBlockNumber.Value
                        None
                    else
                        proposeBlock _blockNumber
                        |> Option.bind (fun r ->
                            match r with
                            | Ok b -> Some b
                            | Error e ->
                                Log.error "Failed to propose block"
                                Log.appErrors e
                                None
                        )
                )

            match block with
            | None ->
                Log.debug "Nothing to propose"
                schedulePropose proposeRetryingInterval (_blockNumber, _round)
            | Some b -> __.SendPropose(_round, b)

        member private __.RetryPropose(blockNumber, consensusRound) =
            if _blockNumber = blockNumber && _round = consensusRound && _step = ConsensusStep.Propose then
                if __.GetProposal() = None then
                    __.TryPropose()

        member private __.StartRound(r) =
            _roundStartTime <- Utils.getNetworkTimestamp ()
            _round <- r
            _step <- ConsensusStep.Propose

            if Validators.getProposer _blockNumber _round _validators = validatorAddress then
                __.TryPropose()

            if _scheduledTimeouts.Add(_round, ConsensusStep.Propose) then
                scheduleTimeout (_blockNumber, _round, ConsensusStep.Propose)

        member private __.UpdateState() =
            // PROPOSE RULES
            __.GetProposal()
            |> Option.iter (fun ((_, _, senderAddress), (block, vr, _)) ->
                if _step = ConsensusStep.Propose && vr.Value = -1 then
                    Log.debug ">>>>>>>>>> A1"
                    _step <- ConsensusStep.Vote
                    if isValidBlock block && (_lockedRound.Value = -1 || _lockedBlock = Some block) then
                        Log.debug ">>>>>>>>>> A2"
                        __.SendVote(_round, Some block.Header.Hash)
                    else
                        Log.debug ">>>>>>>>>> A3"
                        __.SendVote(_round, None)

                if _step = ConsensusStep.Propose && (vr.Value >= 0 && vr < _round) then
                    Log.debug ">>>>>>>>>> B1"
                    if __.MajorityVoted(vr, Some block.Header.Hash) then
                        Log.debug ">>>>>>>>>> B2"
                        _step <- ConsensusStep.Vote
                        if isValidBlock block && (_lockedRound <= vr || _lockedBlock = Some block) then
                            Log.debug ">>>>>>>>>> B3"
                            __.SendVote(_round, Some block.Header.Hash)
                        else
                            Log.debug ">>>>>>>>>> B4"
                            __.SendVote(_round, None)
                    else
                        Log.debug ">>>>>>>>>> B5"
                        if isValidBlock block && (_lockedRound < vr || _lockedBlock = Some block) then
                            // We have a proposal which looks good, but we're missing the corresponding votes.
                            Log.warningf "Missing votes for Propose %s in valid round %i sent by %s"
                                block.Header.Hash.Value
                                vr.Value
                                senderAddress.Value

                            requestConsensusState vr (Some senderAddress)
            )

            // VOTE RULES
            if _step = ConsensusStep.Vote && __.MajorityVoted(_round) then
                Log.debug ">>>>>>>>>> C1"
                if _scheduledTimeouts.Add(_round, ConsensusStep.Vote) then
                    Log.debug ">>>>>>>>>> C2"
                    scheduleTimeout (_blockNumber, _round, ConsensusStep.Vote)

            if _step >= ConsensusStep.Vote then
                Log.debug ">>>>>>>>>> D1"
                __.GetProposal()
                |> Option.iter (fun ((_, _, _), (block, _, _)) ->
                    Log.debug ">>>>>>>>>> D2"
                    let blockHash = Some block.Header.Hash
                    if __.MajorityVoted(_round, blockHash) && isValidBlock block then
                        Log.debug ">>>>>>>>>> D3"
                        _validBlock <- Some block
                        _validRound <- _round
                        _validBlockSignatures <- __.GetVoteSignatures(_blockNumber, _round, blockHash)
                        if _step = ConsensusStep.Vote then
                            Log.debug ">>>>>>>>>> D4"
                            _lockedBlock <- Some block
                            _lockedRound <- _round
                            _step <- ConsensusStep.Commit
                            __.SendCommit(_round, blockHash)
                        else
                            Log.debug ">>>>>>>>>> D5"
                            __.PersistState()
                )

            if _step = ConsensusStep.Vote && __.MajorityVoted(_round, None) then
                Log.debug ">>>>>>>>>> E1"
                _step <- ConsensusStep.Commit
                __.SendCommit(_round, None)

            // COMMIT RULES
            if __.MajorityCommitted(_round, None) then
                Log.debug ">>>>>>>>>> F1"
                __.StartRound(_round + 1)

            if __.MajorityCommitted(_round) then
                Log.debug ">>>>>>>>>> G1"
                if _scheduledTimeouts.Add(_round, ConsensusStep.Commit) then
                    Log.debug ">>>>>>>>>> G2"
                    scheduleTimeout (_blockNumber, _round, ConsensusStep.Commit)

            if not (_decisions.ContainsKey _blockNumber) then
                Log.debug ">>>>>>>>>> H1"
                __.GetProposalCommittedByMajority()
                |> Option.iter (fun ((blockNumber, r, _), (block, _, _)) ->
                    Log.debug ">>>>>>>>>> H2"
                    if (*__.MajorityCommitted(r, Some block.Header.Hash) &&*) isValidBlock block then
                        Log.debug ">>>>>>>>>> H3"
                        _decisions.[blockNumber] <- block
                        __.SaveBlock(block, r)
                        __.Synchronize()
                )

            __.LatestValidRound()
            |> Option.iter (fun r ->
                Log.debugf ">>>>>>>>>> LatestValidRound vs current: %A / %A" r _round
                if r > _round then __.StartRound(r)
            )

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Timeout Handlers
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        member private __.OnTimeoutPropose(blockNumber, consensusRound) =
            if _blockNumber = blockNumber && _round = consensusRound && _step = ConsensusStep.Propose then
                _step <- ConsensusStep.Vote
                __.SendVote(_round, None)

        member private __.OnTimeoutVote(blockNumber, consensusRound) =
            if _blockNumber = blockNumber && _round = consensusRound && _step = ConsensusStep.Vote then
                _step <- ConsensusStep.Commit
                __.SendCommit(_round, None)

        member private __.OnTimeoutCommit(blockNumber, consensusRound) =
            if _blockNumber = blockNumber && _round = consensusRound then
                __.StartRound(_round + 1)

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Helpers
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        member private __.GetProposal()
            : ((BlockNumber * ConsensusRound * BlockchainAddress) * (Block * ConsensusRound * Signature)) option
            =

            let proposerAddress = Validators.getProposer _blockNumber _round _validators
            let key = _blockNumber, _round, proposerAddress
            match _proposals.TryGetValue(key) with
            | true, v -> Some (key, v)
            | _ -> None

        member private __.GetProposalCommittedByMajority()
            : ((BlockNumber * ConsensusRound * BlockchainAddress) * (Block * ConsensusRound * Signature)) option
            =

            _proposals
            |> Seq.ofDict
            |> Seq.filter (fun ((blockNumber, r, senderAddress), (block, _, _)) ->
                blockNumber = _blockNumber
                && senderAddress = Validators.getProposer _blockNumber r _validators
                && __.MajorityCommitted(r, Some block.Header.Hash)
            )
            |> Seq.sortBy (fun ((_, consensusRound, _), _) -> consensusRound)
            |> Seq.tryHead

        member private __.GetVoteSignatures(blockNumber, consensusRound, blockHash : BlockHash option) =
            _votes
            |> List.ofDict
            |> List.choose (fun ((bn, r, _), (bh, s)) ->
                if bn = blockNumber && r = consensusRound && bh = blockHash then
                    Some s
                else
                    None
            )

        member private __.MajorityVoted(consensusRound, ?blockHash : BlockHash option) : bool =
            (*
            blockHash is optional parameter with nested Option<BlockHash> value, which is evaluated as follows:

            match blockHash with
            | None -> take all votes
            | Some v ->
                match v with
                | None -> take only votes for None
                | Some h -> take only votes for block hash h
            *)

            let count =
                _votes
                |> Seq.ofDict
                |> Seq.filter (fun ((bn, r, _), (bh, _)) ->
                    bn = _blockNumber
                    && r = consensusRound
                    && (blockHash = None || blockHash = Some bh)
                )
                |> Seq.length

            count >= _qualifiedMajority

        member private __.MajorityCommitted(consensusRound, ?blockHash : BlockHash option) : bool =
            (*
            blockHash is optional parameter with nested Option<BlockHash> value, which is evaluated as follows:

            match blockHash with
            | None -> take all commits
            | Some v ->
                match v with
                | None -> take only commits for None
                | Some h -> take only commits for block hash h
            *)

            let count =
                _commits
                |> Seq.ofDict
                |> Seq.filter (fun ((bn, r, _), (bh, _)) ->
                    bn = _blockNumber
                    && r = consensusRound
                    && (blockHash = None || blockHash = Some bh)
                )
                |> Seq.length

            count >= _qualifiedMajority

        member private __.LatestValidRound() =
            _votes
            |> List.ofDict
            |> List.choose (fun ((bn, r, _), _) -> if bn = _blockNumber && r > _round then Some r else None)
            |> List.groupBy id
            |> List.choose (fun (k, vs) -> if vs.Length >= _validQuorum then Some k else None)
            |> List.sortDescending
            |> List.tryHead
            |> Option.orElseWith (fun _ ->
                _commits
                |> List.ofDict
                |> List.choose (fun ((bn, r, _), _) -> if bn = _blockNumber && r > _round then Some r else None)
                |> List.groupBy id
                |> List.choose (fun (k, vs) -> if vs.Length >= _validQuorum then Some k else None)
                |> List.sortDescending
                |> List.tryHead
            )

        member private __.SendPropose(consensusRound, block) =
            let variables = __.GetConsensusVariables()
            let message = Propose (block, _validRound)
            if not (__.IsTryingToEquivocate(consensusRound, message)) then
                sendConsensusMessage _blockNumber consensusRound variables message

        member private __.SendVote(consensusRound, blockHash) =
            let variables = __.GetConsensusVariables()
            let message = Vote blockHash
            if not (__.IsTryingToEquivocate(consensusRound, message)) then
                sendConsensusMessage _blockNumber consensusRound variables message

        member private __.SendCommit(consensusRound, blockHash) =
            let variables = __.GetConsensusVariables()
            let message = Commit blockHash
            if not (__.IsTryingToEquivocate(consensusRound, message)) then
                sendConsensusMessage _blockNumber consensusRound variables message

        member private __.SaveBlock(block, consensusRound) =
            let signatures =
                _commits
                |> List.ofDict
                |> List.choose (fun ((b, r, _), (h, s)) ->
                    if b = block.Header.Number && h = Some block.Header.Hash && r = consensusRound then
                        Some s
                    else
                        None
                )
                |> List.distinct

            if signatures.Length < _qualifiedMajority then
                failwithf "Consensus state doesn't contain enough commits for block %i (Expected (min): %i, Actual: %i)"
                    block.Header.Number.Value
                    _qualifiedMajority
                    signatures.Length

            let blockEnvelopeDto =
                {
                    BlockEnvelope.Block = block
                    ConsensusRound = consensusRound
                    Signatures = signatures
                }
                |> Mapping.blockEnvelopeToDto

            (block.Header.Number, blockEnvelopeDto)
            |> BlockCommitted
            |> publishEvent

            // Wait for the state to get updated before proceeding.
            while Synchronization.appliedBlocks.Take() < block.Header.Number do
                () // Nothing to do - we rely on the blocking nature of the appliedBlocks queue.

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Equivocation
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        member private __.TryDetectEquivocation(blockNumber, consensusRound, consensusMessage, senderAddress) =
            let key = blockNumber, consensusRound, senderAddress

            match consensusMessage with
            | Propose (incomingBlock, incomingVr) ->
                match _proposals.TryGetValue(key) with
                | true, (existingBlock, existingVr, signature1) ->
                    let ev1 = (existingBlock.Header.Hash, existingVr) |> EquivocationValue.BlockHashAndValidRound
                    let ev2 = (incomingBlock.Header.Hash, incomingVr) |> EquivocationValue.BlockHashAndValidRound
                    Some (ev1, ev2, signature1)
                | _ -> None
            | Vote incomingHash ->
                match _votes.TryGetValue(key) with
                | true, (existingHash, signature1) ->
                    let ev1 = existingHash |> EquivocationValue.BlockHash
                    let ev2 = incomingHash |> EquivocationValue.BlockHash
                    Some (ev1, ev2, signature1)
                | _ -> None
            | Commit incomingHash ->
                match _commits.TryGetValue(key) with
                | true, (existingHash, signature1) ->
                    let ev1 = existingHash |> EquivocationValue.BlockHash
                    let ev2 = incomingHash |> EquivocationValue.BlockHash
                    Some (ev1, ev2, signature1)
                | _ -> None
            |> Option.filter (fun (ev1, ev2, _) -> ev1 <> ev2)

        /// This is a safety measure to prevent accidental equivocation in outgoing messages for honest nodes.
        member private __.IsTryingToEquivocate(consensusRound, consensusMessage) =
            match __.TryDetectEquivocation(_blockNumber, consensusRound, consensusMessage, validatorAddress) with
            | Some (ev1, ev2, _) ->
                Log.warningf
                    "EQUIVOCATION: This node is trying to %s %A in round %i on hight %i (it already did that for %A)"
                    consensusMessage.CaseName
                    ev2
                    consensusRound.Value
                    _blockNumber.Value
                    ev1
                true
            | _ ->
                false

        member private __.CreateEquivocationProof
            (BlockNumber blockNumber)
            (ConsensusRound consensusRound)
            (consensusStep : ConsensusStep)
            (equivocationValue1 : EquivocationValue)
            (equivocationValue2 : EquivocationValue)
            (Signature signature1)
            (Signature signature2)
            =

            // Prevent the same two values being propagated as two distinct proofs (v1/v2 and v2/v1),
            // to avoid deposit being slashed twice for essentialy the same proof.
            let equivocationValue1, equivocationValue2, signature1, signature2 =
                if equivocationValue1 > equivocationValue2 then
                    equivocationValue2, equivocationValue1, signature2, signature1 // Swap
                else
                    equivocationValue1, equivocationValue2, signature1, signature2

            {
                BlockNumber = blockNumber
                ConsensusRound = consensusRound
                ConsensusStep = consensusStep |> Mapping.consensusStepToCode
                EquivocationValue1 = equivocationValue1 |> Mapping.equivocationValueToString
                EquivocationValue2 = equivocationValue2 |> Mapping.equivocationValueToString
                Signature1 = signature1
                Signature2 = signature2
            }

        /// Detects equivocation for incomming messages.
        member private __.DetectEquivocation(envelope, senderAddress) =
            __.TryDetectEquivocation(envelope.BlockNumber, envelope.Round, envelope.ConsensusMessage, senderAddress)
            |> Option.iter (fun (ev1, ev2, signature1) ->
                let consensusStep = envelope.ConsensusMessage |> Mapping.consensusStepFromMessage

                let equivocationProofDto =
                    __.CreateEquivocationProof
                        envelope.BlockNumber
                        envelope.Round
                        consensusStep
                        ev1
                        ev2
                        signature1
                        envelope.Signature

                EquivocationProofDetected (equivocationProofDto, senderAddress)
                |> publishEvent
            )

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Test Helpers
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        member __.Variables
            with get () = __.GetConsensusVariables()

        member __.Proposals
            with get () = _proposals |> List.ofDict

        member __.Votes
            with get () = _votes |> List.ofDict

        member __.Commits
            with get () = _commits |> List.ofDict

        member __.MessageCounts
            with get () = _proposals.Count, _votes.Count, _commits.Count

        member __.MessageCountsInRound consensusRound =
            let proposeCount =
                _proposals
                |> Seq.ofDict
                |> Seq.filter (fun ((b, r, _), _) -> b = _blockNumber && r = consensusRound)
                |> Seq.length
            let voteCount =
                _votes
                |> Seq.ofDict
                |> Seq.filter (fun ((b, r, _), _) -> b = _blockNumber && r = consensusRound)
                |> Seq.length
            let commitCount =
                _commits
                |> Seq.ofDict
                |> Seq.filter (fun ((b, r, _), _) -> b = _blockNumber && r = consensusRound)
                |> Seq.length

            proposeCount, voteCount, commitCount

        member __.PrintCurrentState() =
            [
                sprintf "_validators: %A" _validators
                sprintf "_qualifiedMajority: %A" _qualifiedMajority
                sprintf "_validQuorum: %A" _validQuorum

                sprintf "_blockNumber: %A" _blockNumber
                sprintf "_round: %A" _round
                sprintf "_step: %A" _step
                sprintf "_lockedBlock: %A" _lockedBlock
                sprintf "_lockedRound: %A" _lockedRound
                sprintf "_validBlock: %A" _validBlock
                sprintf "_validRound: %A" _validRound
                sprintf "_validBlockSignatures: %A" _validBlockSignatures
                sprintf "_decisions: %A" _decisions
                sprintf "_scheduledTimeouts: %A" (_scheduledTimeouts |> Seq.toList)
                sprintf "_proposals: %A" (_proposals |> List.ofDict)
                sprintf "_votes: %A" (_votes |> List.ofDict)
                sprintf "_commits: %A" (_commits |> List.ofDict)
            ]

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Helpers
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let consensusMessageDisplayFormat consensusMessage =
        match consensusMessage with
        | Propose (block, validRound) ->
            sprintf "%s, %i" block.Header.Hash.Value validRound.Value
        | Vote blockHash
        | Commit blockHash ->
            blockHash
            |> Option.map (fun (BlockHash h) -> h)
            |> sprintf "%A"
        |> sprintf "%s: %s" consensusMessage.CaseName

    let createConsensusMessageHash
        decodeHash
        createHash
        (BlockNumber blockNumber)
        (ConsensusRound consensusRound)
        (consensusMessage : ConsensusMessage)
        =

        match consensusMessage with
        | Propose (block, ConsensusRound validConsensusRound) ->
            [
                [| 0uy |] // Message type discriminator
                block.Header.Hash.Value |> decodeHash
                validConsensusRound |> Conversion.int32ToBytes
            ]
        | Vote blockHash ->
            [
                [| 1uy |] // Message type discriminator
                blockHash |> Option.map (fun h -> decodeHash h.Value) |? Array.empty
            ]
        | Commit blockHash ->
            [
                [| 2uy |] // Message type discriminator
                blockHash |> Option.map (fun h -> decodeHash h.Value) |? Array.empty
            ]
        |> List.append
            [
                blockNumber |> Conversion.int64ToBytes
                consensusRound |> Conversion.int32ToBytes
            ]
        |> Array.concat
        |> createHash

    let createConsensusStateInstance
        (getLastAppliedBlockNumber : unit -> BlockNumber)
        (getLastAppliedBlockTimestamp : unit -> Timestamp)
        getValidatorsAtHeight
        (getValidatorState : BlockchainAddress -> ValidatorStateDto option)
        proposeBlock
        txExists
        equivocationProofExists
        requestTxs
        requestEquivocationProofs
        isValidHash
        isValidAddress
        applyBlockToCurrentState
        decodeHash
        createHash
        signHash
        verifyConsensusMessage
        persistConsensusState
        restoreConsensusState
        persistConsensusMessage
        restoreConsensusMessages
        requestConsensusState
        sendConsensusState
        sendPeerMessage
        getNetworkId
        publishEvent
        addressFromPrivateKey
        (validatorPrivateKey : PrivateKey)
        emptyBlocksEnabled
        minEmptyBlockTime
        staleConsensusDetectionInterval
        messageRetryingInterval
        proposeRetryingInterval
        timeoutPropose
        timeoutVote
        timeoutCommit
        timeoutDelta
        =

        let validatorAddress =
            addressFromPrivateKey validatorPrivateKey

        let isValidatorBlacklisted = memoize <| fun (validatorAddress, currentBlockNumber, incomingMsgBlockNumber) ->
            (*
            currentBlockNumber and incomingMsgBlockNumber parameters ensure proper caching per block.
            Since function is relying on last applied state in DB, having only currentBlock parameter would result in
            incorrect response. For example:

            IF TimeToBlacklist = 1 (meaning validator will be removed from blacklist in next config block, e.g. #10)
            AND current block number being voted on in consensus is one block before next config block (e.g. #9)
            AND incoming message is for future block (e.g. #10, because node might be catching up)
            THEN function would be called with block number #10 and would return TRUE,
                which would cause all messages for block #10 from previously blacklisted validator,
                to be ignored for all rounds in block #10, although validator is not on blacklist at block #10.

            Having currentBlockNumber and incomingMsgBlockNumber parameters ensures this incorrect behavior will
            stop once this node catches up and applies block #9 to the state, because the function will be called
            with #10/#10 (which is different from previous #9/#10) and will return fresh result from the DB state.

            This reduces incorrect behaviour to the level equal to non-cached version of the function,
            while still benefiting from caching.
            *)
            match getValidatorState validatorAddress with
            | Some s when s.TimeToBlacklist > 0s -> true
            | _ -> false

        let canParticipateInConsensus = memoizeWhen Option.isSome <| fun blockNumber ->
            let lastAppliedBlockNumber = getLastAppliedBlockNumber ()
            if blockNumber = lastAppliedBlockNumber + 1 then
                // Participation in consensus is relevant only relative to the current blockchain state,
                // in which case the information about participation eligibility is cached for efficiency.
                getValidatorsAtHeight lastAppliedBlockNumber
                |> List.exists (fun (v : ValidatorSnapshot) -> v.ValidatorAddress = validatorAddress)
                |> Some
            else
                None

        let ensureBlockReady (block : Block) =
            let missingTxs =
                block.TxSet
                |> List.filter (txExists >> not)

            let missingEquivocationProofs =
                block.EquivocationProofs
                |> List.filter (equivocationProofExists >> not)

            match missingTxs, missingEquivocationProofs with
            | [], [] ->
                true
            | _ ->
                let proposer =
                    getValidatorsAtHeight (block.Header.Number - 1)
                    |> List.find (fun v -> v.ValidatorAddress = block.Header.ProposerAddress)

                requestTxs proposer.NetworkAddress missingTxs
                requestEquivocationProofs proposer.NetworkAddress missingEquivocationProofs
                false

        let isValidBlock = memoizeBy (fun (b : Block) -> b.Header.Hash) <| fun block ->
            block
            |> Mapping.blockToDto
            |> Validation.validateBlock isValidHash isValidAddress
            >>= Blocks.validateEmptyBlockTimestamp minEmptyBlockTime (getLastAppliedBlockTimestamp ())
            >>= applyBlockToCurrentState
            |> Result.handle
                (fun _ -> true)
                (fun e ->
                    Log.appErrors e
                    false
                )

        let persistConsensusState =
            persistConsensusState
            >> Result.iterError (fun e ->
                Log.appErrors e
                failwith "persistConsensusState FAILED"
            )

        let persistConsensusMessage =
            persistConsensusMessage
            >> Result.iterError (fun e ->
                Log.appErrors e
                failwith "persistConsensusMessage FAILED"
            )

        let sendConsensusMessage blockNumber consensusRound consensusVariables consensusMessage =
            if canParticipateInConsensus blockNumber = Some true then
                let consensusMessageHash =
                    createConsensusMessageHash
                        decodeHash
                        createHash
                        blockNumber
                        consensusRound
                        consensusMessage

                let signature = signHash validatorPrivateKey consensusMessageHash

                let consensusMessageEnvelope =
                    {
                        ConsensusMessageEnvelope.BlockNumber = blockNumber
                        Round = consensusRound
                        ConsensusMessage = consensusMessage
                        Signature = signature
                    }

                // If message is not persisted, variables shouldn't be persisted either.
                persistConsensusMessage consensusMessageEnvelope
                persistConsensusState consensusVariables

                ConsensusCommand.Message (validatorAddress, consensusMessageEnvelope)
                |> ConsensusCommandInvoked
                |> publishEvent // Send message to self

                {
                    PeerMessageEnvelope.NetworkId = getNetworkId ()
                    PeerMessage =
                        {
                            MulticastMessage.MessageId =
                                sprintf "Consensus_%s" consensusMessageHash
                                |> ConsensusMessageId
                                |> NetworkMessageId.Consensus
                            SenderIdentity = None
                            Data =
                                consensusMessageEnvelope
                                |> Mapping.consensusMessageEnvelopeToDto
                                |> Serialization.serializeBinary
                        }
                        |> MulticastMessage
                }
                |> sendPeerMessage

                Stats.increment Stats.Counter.SentConsensusMessages
                Log.debugf "Consensus message sent: %i / %i / %s"
                    consensusMessageEnvelope.BlockNumber.Value
                    consensusMessageEnvelope.Round.Value
                    (consensusMessageEnvelope.ConsensusMessage |> consensusMessageDisplayFormat)

        let scheduleMessage timeout (senderAddress : BlockchainAddress, envelope : ConsensusMessageEnvelope) =
            if canParticipateInConsensus envelope.BlockNumber = Some true then
                async {
                    let displayFormat = consensusMessageDisplayFormat envelope.ConsensusMessage

                    Log.debugf "Message retry from %s scheduled: %i / %i / %s"
                        senderAddress.Value
                        envelope.BlockNumber.Value
                        envelope.Round.Value
                        displayFormat

                    do! Async.Sleep timeout

                    Log.debugf "Message retry from %s triggered: %i / %i / %s"
                        senderAddress.Value
                        envelope.BlockNumber.Value
                        envelope.Round.Value
                        displayFormat

                    ConsensusCommand.Message (senderAddress, envelope)
                    |> ConsensusCommandInvoked
                    |> publishEvent
                }
                |> Async.Start

        let scheduleStateResponse timeout (blockNumber, stateResponse : ConsensusStateResponse) =
            if canParticipateInConsensus blockNumber = Some true then
                async {
                    Log.debugf "Consensus state response retry scheduled: ValidRound %i"
                        stateResponse.ValidRound.Value

                    do! Async.Sleep timeout

                    Log.debugf "Consensus state response retry triggered: ValidRound %i"
                        stateResponse.ValidRound.Value

                    ConsensusCommand.StateReceived stateResponse
                    |> ConsensusCommandInvoked
                    |> publishEvent
                }
                |> Async.Start

        let schedulePropose timeout (blockNumber : BlockNumber, consensusRound : ConsensusRound) =
            if canParticipateInConsensus blockNumber = Some true then
                async {
                    Log.debugf "Propose retry scheduled: %i / %i"
                        blockNumber.Value
                        consensusRound.Value

                    do! Async.Sleep timeout

                    Log.debugf "Propose retry triggered: %i / %i"
                        blockNumber.Value
                        consensusRound.Value

                    ConsensusCommand.RetryPropose (blockNumber, consensusRound)
                    |> ConsensusCommandInvoked
                    |> publishEvent
                }
                |> Async.Start

        let timeoutForRound consensusStep (ConsensusRound consensusRound) =
            let baseTimeout =
                match consensusStep with
                | ConsensusStep.Propose -> timeoutPropose
                | ConsensusStep.Vote -> timeoutVote
                | ConsensusStep.Commit -> timeoutCommit

            baseTimeout + timeoutDelta * consensusRound

        let scheduleTimeout
            (blockNumber : BlockNumber, consensusRound : ConsensusRound, consensusStep : ConsensusStep)
            =

            if canParticipateInConsensus blockNumber = Some true then
                async {
                    Log.debugf "Timeout scheduled: %i / %i / %s"
                        blockNumber.Value
                        consensusRound.Value
                        consensusStep.CaseName

                    do! Async.Sleep (timeoutForRound consensusStep consensusRound)

                    Log.debugf "Timeout elapsed: %i / %i / %s"
                        blockNumber.Value
                        consensusRound.Value
                        consensusStep.CaseName

                    ConsensusCommand.Timeout (blockNumber, consensusRound, consensusStep)
                    |> ConsensusCommandInvoked
                    |> publishEvent
                }
                |> Async.Start

        new ConsensusState
            (
            persistConsensusState,
            restoreConsensusState,
            persistConsensusMessage,
            restoreConsensusMessages,
            getLastAppliedBlockNumber,
            getLastAppliedBlockTimestamp,
            getValidatorsAtHeight,
            isValidatorBlacklisted,
            proposeBlock,
            ensureBlockReady,
            isValidBlock,
            verifyConsensusMessage,
            sendConsensusMessage,
            sendConsensusState,
            requestConsensusState,
            canParticipateInConsensus,
            publishEvent,
            scheduleMessage,
            scheduleStateResponse,
            schedulePropose,
            scheduleTimeout,
            timeoutForRound,
            messageRetryingInterval,
            proposeRetryingInterval,
            staleConsensusDetectionInterval,
            emptyBlocksEnabled,
            minEmptyBlockTime,
            validatorAddress
            )
