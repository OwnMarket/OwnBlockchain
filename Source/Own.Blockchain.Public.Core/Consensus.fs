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
        restoreConsensusMessages : unit -> ConsensusMessageEnvelope list,
        getLastAppliedBlockNumber : unit -> BlockNumber,
        getValidatorsAtHeight : BlockNumber -> ValidatorSnapshot list,
        isValidatorBlacklisted : BlockchainAddress * BlockNumber * BlockNumber -> bool,
        proposeBlock : BlockNumber -> Result<Block, AppErrors> option,
        ensureBlockReady : Block -> bool,
        isValidBlock : Block -> bool,
        verifyConsensusMessage :
            ConsensusMessageEnvelopeDto -> Result<BlockchainAddress * ConsensusMessageEnvelope, AppErrors>,
        sendConsensusMessage : BlockNumber -> ConsensusRound -> ConsensusMessage -> unit,
        sendConsensusState : PeerNetworkIdentity -> ConsensusStateResponse -> unit,
        requestConsensusState : unit -> unit,
        publishEvent : AppEvent -> unit,
        scheduleMessage : int -> BlockchainAddress * ConsensusMessageEnvelope -> unit,
        scheduleStateResponse : int -> BlockNumber * ConsensusStateResponse -> unit,
        schedulePropose : int -> BlockNumber * ConsensusRound -> unit,
        scheduleTimeout : BlockNumber * ConsensusRound * ConsensusStep -> unit,
        timeoutForRound : ConsensusStep -> ConsensusRound -> int,
        messageRetryingInterval : int,
        proposeRetryingInterval : int,
        staleRoundDetectionInterval : int,
        validatorAddress : BlockchainAddress
        ) =

        let mutable _validators = []
        let mutable _qualifiedMajority = 0
        let mutable _validQuorum = 0

        let mutable _roundStartTime = 0L // Used for stale round detection. Updated upon requesting consensus state.

        let mutable _blockNumber = BlockNumber 0L
        let mutable _round = ConsensusRound 0
        let mutable _step = ConsensusStep.Propose
        let mutable _decisions = new Dictionary<BlockNumber, Block>()
        let mutable _lockedBlockSignatures = []
        let mutable _lockedBlock = None
        let mutable _lockedRound = ConsensusRound -1
        let mutable _validBlock = None
        let mutable _validRound = ConsensusRound -1

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
                scheduleTimeout (_blockNumber, _round, ConsensusStep.Propose)

            __.Synchronize()
            __.StartStaleRoundDetection()

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
                envelope.ConsensusMessage
                |> unionCaseName
                |> Log.warningf "Validator %s is blacklisted - %s consensus message ignored" senderAddress.Value
            elif envelope.BlockNumber >= _blockNumber then
                let key = envelope.BlockNumber, envelope.Round, senderAddress

                match envelope.ConsensusMessage with
                | ConsensusMessage.Propose (block, vr) ->
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
                                    if updateState then
                                        __.UpdateState()
                            else
                                scheduleMessage messageRetryingInterval (senderAddress, envelope)
                | ConsensusMessage.Vote blockHash ->
                    if _votes.TryAdd(key, (blockHash, envelope.Signature)) then
                        if updateState then
                            __.UpdateState()
                    else
                        __.DetectEquivocation(envelope, senderAddress)
                | ConsensusMessage.Commit blockHash ->
                    if _commits.TryAdd(key, (blockHash, envelope.Signature)) then
                        if updateState then
                            __.UpdateState()
                    else
                        __.DetectEquivocation(envelope, senderAddress)

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // State Management
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        member private __.PersistState() =
            {
                ConsensusStateInfo.BlockNumber = _blockNumber
                ConsensusRound = _round
                ConsensusStep = _step
                LockedBlockSignatures = _lockedBlockSignatures
                LockedBlock = _lockedBlock
                LockedRound = _lockedRound
                ValidBlock = _validBlock
                ValidRound = _validRound
            }
            |> persistConsensusState

        member private __.RestoreState() =
            restoreConsensusState ()
            |> Option.iter (fun s ->
                _blockNumber <- s.BlockNumber
                _round <- s.ConsensusRound
                _step <- s.ConsensusStep
                _lockedBlockSignatures <- s.LockedBlockSignatures
                _lockedBlock <- s.LockedBlock
                _lockedRound <- s.LockedRound
                _validBlock <- s.ValidBlock
                _validRound <- s.ValidRound
            )

            restoreConsensusMessages ()
            |> List.sortBy (fun e -> e.BlockNumber, e.Round, e.ConsensusMessage)
            |> List.iter (fun envelope ->
                let key = envelope.BlockNumber, envelope.Round, validatorAddress

                match envelope.ConsensusMessage with
                | Propose (b, r) -> _proposals.Add(key, (b, r, envelope.Signature))
                | Vote h -> _votes.Add(key, (h, envelope.Signature))
                | Commit h -> _commits.Add(key, (h, envelope.Signature))
            )

            __.SetValidators(_blockNumber - 1)

        member __.StartStaleRoundDetection() =
            let rec loop () =
                async {
                    do! Async.Sleep staleRoundDetectionInterval

                    let maxRoundDuration =
                        timeoutForRound ConsensusStep.Propose _round
                        + timeoutForRound ConsensusStep.Vote _round
                        + timeoutForRound ConsensusStep.Commit _round
                        |> int64

                    let currentTime = Utils.getMachineTimestamp ()
                    let roundDuration = currentTime - _roundStartTime

                    if roundDuration > maxRoundDuration then
                        try
                            _roundStartTime <- currentTime
                            requestConsensusState ()
                        with
                        | ex -> Log.error ex.AllMessagesAndStackTraces

                    return! loop ()
                }

            loop ()
            |> Async.Start

        member private __.ApplyReceivedState(response) =
            let messages =
                response.LatestMessages
                |> List.toArray
                |> Array.Parallel.map (Mapping.consensusMessageEnvelopeToDto >> verifyConsensusMessage)
                |> Array.choose (function
                    | Ok e -> Some e
                    | _ -> None
                )

            if messages.Length > 0 then
                for (s, e) in messages do
                    __.ProcessConsensusMessage(s, e, false)
                __.UpdateState()

            let response = {response with LatestMessages = []} // We're done with the messages - no need to keep them.

            if response.LockedRound > _lockedRound then
                let isValidMessage, lockedBlock =
                    match response.LockedProposal with
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
                        response.LockedVoteSignatures
                        |> List.toArray
                        |> Array.Parallel.map (fun s ->
                            {
                                ConsensusMessageEnvelope.BlockNumber = _blockNumber
                                Round = response.LockedRound
                                ConsensusMessage =
                                    lockedBlock
                                    |> Option.map (fun b -> b.Header.Hash)
                                    |> ConsensusMessage.Vote
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
                            match lockedBlock with
                            | None -> true
                            | Some b -> ensureBlockReady b

                        if not blockReady then
                            scheduleStateResponse messageRetryingInterval (_blockNumber, response)
                        else
                            response.LockedProposal
                            |> Option.iter (fun e ->
                                e
                                |> Mapping.consensusMessageEnvelopeToDto
                                |> verifyConsensusMessage
                                |> Result.handle
                                    (fun (a, _) -> __.ProcessConsensusMessage(a, e, false))
                                    Log.appErrors
                            )

                            for s, e in votes do
                                if signers.Contains s then
                                    __.ProcessConsensusMessage(s, e, false)

                            _lockedBlock <- lockedBlock
                            _lockedRound <- response.LockedRound
                            _round <- _lockedRound
                            _step <- ConsensusStep.Propose

                            __.UpdateState()

        member private __.SendState(request, peerIdentity) =
            if not (_validators |> List.contains request.ValidatorAddress) then
                Log.warningf "%s is not an active validator - consensus state request ignored"
                    request.ValidatorAddress.Value
            elif isValidatorBlacklisted (request.ValidatorAddress, _blockNumber, _blockNumber) then
                Log.warningf "Validator %s is blacklisted - consensus state request ignored"
                    request.ValidatorAddress.Value
            else
                let latestMessages =
                    [
                        let key = _blockNumber, _round, validatorAddress

                        if _proposals.ContainsKey(key) then
                            let (b, vr, s) = _proposals.[key]
                            yield {
                                ConsensusMessageEnvelope.BlockNumber = _blockNumber
                                Round = _round
                                ConsensusMessage = Propose (b, vr)
                                Signature = s
                            }

                        if _votes.ContainsKey(key) then
                            let (bh, s) = _votes.[key]
                            yield {
                                ConsensusMessageEnvelope.BlockNumber = _blockNumber
                                Round = _round
                                ConsensusMessage = Vote bh
                                Signature = s
                            }

                        if _commits.ContainsKey(key) then
                            let (bh, s) = _commits.[key]
                            yield {
                                ConsensusMessageEnvelope.BlockNumber = _blockNumber
                                Round = _round
                                ConsensusMessage = Commit bh
                                Signature = s
                            }
                    ]

                let lockedProposal =
                    if _lockedRound < ConsensusRound 0 then
                        None
                    else
                        _lockedBlock
                        |> Option.map (fun _ ->
                            let lockedRoundProposer = Validators.getProposer _blockNumber _lockedRound _validators
                            let key = _blockNumber, _lockedRound, lockedRoundProposer
                            match _proposals.TryGetValue(key) with
                            | true, (b, vr, s) ->
                                {
                                    ConsensusMessageEnvelope.BlockNumber = _blockNumber
                                    Round = _lockedRound
                                    ConsensusMessage = Propose (b, vr)
                                    Signature = s
                                }
                            | _ ->
                                failwithf "Cannot find proposal corresponding to locked consensus value (Key: %A)" key
                        )

                let lockedVoteSignatures =
                    if _lockedRound < ConsensusRound 0 then
                        []
                    elif _lockedBlockSignatures.Length < _qualifiedMajority then
                        failwithf "_lockedBlockSignatures has only %i entries - it should have at least %i"
                            _lockedBlockSignatures.Length
                            _qualifiedMajority
                    else
                        _lockedBlockSignatures

                {
                    ConsensusStateResponse.LatestMessages = latestMessages
                    LockedRound = _lockedRound
                    LockedProposal = lockedProposal
                    LockedVoteSignatures = lockedVoteSignatures
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
            _lockedBlockSignatures <- []
            _lockedBlock <- None
            _lockedRound <- ConsensusRound -1
            _validBlock <- None
            _validRound <- ConsensusRound -1

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
            _roundStartTime <- Utils.getMachineTimestamp ()
            _round <- r
            _step <- ConsensusStep.Propose

            if Validators.getProposer _blockNumber _round _validators = validatorAddress then
                __.TryPropose()

            scheduleTimeout (_blockNumber, _round, ConsensusStep.Propose)

        member private __.UpdateState() =
            // PROPOSE RULES
            __.GetProposal()
            |> Option.iter (fun ((_, _, _), (block, vr, _)) ->
                if _step = ConsensusStep.Propose then
                    _step <- ConsensusStep.Vote
                    if isValidBlock block && (_lockedRound = ConsensusRound -1 || _lockedBlock = Some block) then
                        __.SendVote(_round, Some block.Header.Hash)
                    else
                        __.SendVote(_round, None)

                if _step = ConsensusStep.Propose
                    && (vr >= ConsensusRound 0 && vr < _round)
                    && __.MajorityVoted(vr, Some block.Header.Hash)
                then
                    _step <- ConsensusStep.Vote
                    if isValidBlock block && (_lockedRound <= vr || _lockedBlock = Some block) then
                        __.SendVote(_round, Some block.Header.Hash)
                    else
                        __.SendVote(_round, None)
            )

            // VOTE RULES
            if _step = ConsensusStep.Vote && __.MajorityVoted(_round) then
                scheduleTimeout (_blockNumber, _round, ConsensusStep.Vote)

            if _step >= ConsensusStep.Vote then
                __.GetProposal()
                |> Option.iter (fun ((_, _, _), (block, _, _)) ->
                    if __.MajorityVoted(_round, Some block.Header.Hash) && isValidBlock block then
                        _validBlock <- Some block
                        _validRound <- _round
                        if _step = ConsensusStep.Vote then
                            _lockedBlockSignatures <- __.GetLockedBlockSignatures()
                            _lockedBlock <- Some block
                            _lockedRound <- _round
                            _step <- ConsensusStep.Commit
                            __.SendCommit(_round, Some block.Header.Hash)
                        else
                            __.PersistState()
                )

            if _step = ConsensusStep.Vote && __.MajorityVoted(_round, None) then
                _step <- ConsensusStep.Commit
                __.SendCommit(_round, None)

            // COMMIT RULES
            if __.MajorityCommitted(_round) then
                scheduleTimeout (_blockNumber, _round, ConsensusStep.Commit)

            if not (_decisions.ContainsKey _blockNumber) then
                __.GetProposalCommittedByMajority()
                |> Option.iter (fun ((blockNumber, r, _), (block, _, _)) ->
                    if (*__.MajorityCommitted(r, Some block.Header.Hash) &&*) isValidBlock block then
                        _decisions.[blockNumber] <- block
                        __.SaveBlock(block, r)
                        __.Synchronize()
                )

            __.LatestValidRound()
            |> Option.iter (fun r -> if r > _round then __.StartRound(r))

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
            _proposals
            |> Seq.ofDict
            |> Seq.filter (fun ((blockNumber, _, senderAddress), _) ->
                blockNumber = _blockNumber && senderAddress = proposerAddress
            )
            |> Seq.sortByDescending (fun ((_, consensusRound, _), _) -> consensusRound)
            |> Seq.tryHead

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

        member private __.GetLockedBlockSignatures() =
            let lockedBlockHash = _lockedBlock |> Option.map (fun b -> b.Header.Hash)

            _votes
            |> List.ofDict
            |> List.choose (fun ((bn, r, _), (bh, s)) ->
                if bn = _blockNumber && r = _lockedRound && bh = lockedBlockHash then
                    Some s
                else
                    None
            )

        member private __.MajorityVoted(consensusRound, ?blockHash : BlockHash option) : bool =
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
            __.PersistState()
            ConsensusMessage.Propose (block, _validRound)
            |> sendConsensusMessage _blockNumber consensusRound

        member private __.SendVote(consensusRound, blockHash) =
            let message = ConsensusMessage.Vote blockHash
            if not (__.IsTryingToEquivocate(consensusRound, message)) then
                __.PersistState()
                sendConsensusMessage _blockNumber consensusRound message

        member private __.SendCommit(consensusRound, blockHash) =
            let message = ConsensusMessage.Commit blockHash
            if not (__.IsTryingToEquivocate(consensusRound, message)) then
                __.PersistState()
                sendConsensusMessage _blockNumber consensusRound message

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
            async {
                while getLastAppliedBlockNumber () < block.Header.Number do
                    do! Async.Sleep 100
            }
            |> Async.RunSynchronously

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Equivocation
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        /// This is a safety measure to prevent accidental equivocation in outgoing messages for honest nodes.
        member private __.IsTryingToEquivocate(consensusRound, consensusMessage) =
            let blockHash, messages =
                match consensusMessage with
                | ConsensusMessage.Propose _ -> failwith "Don't call IsTryingToEquivocate for Propose messages"
                | ConsensusMessage.Vote hash -> hash, _votes
                | ConsensusMessage.Commit hash -> hash, _commits

            match messages.TryGetValue((_blockNumber, consensusRound, validatorAddress)) with
            | true, (foundBlockHash, _) when foundBlockHash <> blockHash ->
                Log.warningf
                    "EQUIVOCATION: This node tries to %s %A in round %i on hight %i (it already did that for %A)"
                    (unionCaseName consensusMessage)
                    blockHash
                    consensusRound.Value
                    _blockNumber.Value
                    foundBlockHash
                true
            | _ ->
                false

        member private __.CreateEquivocationProof
            (BlockNumber blockNumber)
            (ConsensusRound consensusRound)
            (consensusStep : ConsensusStep)
            (blockHash1 : BlockHash option)
            (blockHash2 : BlockHash option)
            (Signature signature1)
            (Signature signature2)
            =

            // Prevent the same two hashes being propagated as two distinct proofs (h1/h2 and h2/h1),
            // to avoid deposit being slashed twice for essentialy the same proof.
            let blockHash1, blockHash2, signature1, signature2 =
                if blockHash1 > blockHash2 then
                    blockHash2, blockHash1, signature2, signature1 // Swap
                else
                    blockHash1, blockHash2, signature1, signature2

            {
                BlockNumber = blockNumber
                ConsensusRound = consensusRound
                ConsensusStep = consensusStep |> Mapping.consensusStepToCode
                BlockHash1 = blockHash1 |> Option.map (fun h -> h.Value) |> Option.toObj
                BlockHash2 = blockHash2 |> Option.map (fun h -> h.Value) |> Option.toObj
                Signature1 = signature1
                Signature2 = signature2
            }

        /// Detects equivocation for incomming messages.
        member private __.DetectEquivocation(envelope, senderAddress) =
            match envelope.ConsensusMessage with
            | Propose _ -> failwith "Don't call DetectEquivocation for Propose messages"
            | Vote blockHash2 ->
                let blockHash1, signature1 = _votes.[envelope.BlockNumber, envelope.Round, senderAddress]
                if blockHash2 <> blockHash1 then
                    let equivocationProofDto =
                        __.CreateEquivocationProof
                            envelope.BlockNumber
                            envelope.Round
                            ConsensusStep.Vote
                            blockHash1
                            blockHash2
                            signature1
                            envelope.Signature
                    EquivocationProofDetected (equivocationProofDto, senderAddress)
                    |> publishEvent
            | Commit blockHash2 ->
                let blockHash1, signature1 = _commits.[envelope.BlockNumber, envelope.Round, senderAddress]
                if blockHash2 <> blockHash1 then
                    let equivocationProofDto =
                        __.CreateEquivocationProof
                            envelope.BlockNumber
                            envelope.Round
                            ConsensusStep.Commit
                            blockHash1
                            blockHash2
                            signature1
                            envelope.Signature
                    EquivocationProofDetected (equivocationProofDto, senderAddress)
                    |> publishEvent

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Test Helpers
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        member __.Decisions
            with get () = _decisions

        member __.PrintCurrentState() =
            [
                sprintf "_validators: %A" _validators
                sprintf "_qualifiedMajority: %A" _qualifiedMajority
                sprintf "_validQuorum: %A" _validQuorum

                sprintf "_blockNumber: %A" _blockNumber
                sprintf "_round: %A" _round
                sprintf "_step: %A" _step
                sprintf "_decisions: %A" _decisions
                sprintf "_lockedBlock: %A" _lockedBlock
                sprintf "_lockedRound: %A" _lockedRound
                sprintf "_validBlock: %A" _validBlock
                sprintf "_validRound: %A" _validRound
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
        |> sprintf "%s: %s" (unionCaseName consensusMessage)

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
        getValidatorsAtHeight
        (getValidatorState : BlockchainAddress -> ValidatorStateDto option)
        proposeBlock
        txExists
        equivocationProofExists
        requestTx
        requestEquivocationProof
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
        messageRetryingInterval
        proposeRetryingInterval
        timeoutPropose
        timeoutVote
        timeoutCommit
        timeoutDelta
        timeoutIncrements
        staleRoundDetectionInterval
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

        let canParticipateInConsensus = memoizeWhen (fun output -> output <> None) <| fun blockNumber ->
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
                missingTxs |> List.iter requestTx
                missingEquivocationProofs |> List.iter requestEquivocationProof
                false

        let isValidBlock = memoizeBy (fun (b : Block) -> b.Header.Hash) <| fun block ->
            block
            |> Mapping.blockToDto
            |> Validation.validateBlock decodeHash isValidAddress
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

        let sendConsensusMessage blockNumber consensusRound consensusMessage =
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

                persistConsensusMessage consensusMessageEnvelope
                |> Result.iterError (fun e ->
                    Log.appErrors e
                    failwith "persistConsensusMessage FAILED"
                )

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
                    Log.debugf "Consensus state response retry scheduled: LockedRound %i"
                        stateResponse.LockedRound.Value

                    do! Async.Sleep timeout

                    Log.debugf "Consensus state response retry triggered: LockedRound %i"
                        stateResponse.LockedRound.Value

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

            baseTimeout + timeoutDelta * min consensusRound timeoutIncrements

        let scheduleTimeout (blockNumber : BlockNumber, consensusRound : ConsensusRound, consensusStep) =
            if canParticipateInConsensus blockNumber = Some true then
                async {
                    Log.debugf "Timeout scheduled: %i / %i / %s"
                        blockNumber.Value
                        consensusRound.Value
                        (unionCaseName consensusStep)

                    do! Async.Sleep (timeoutForRound consensusStep consensusRound)

                    Log.debugf "Timeout elapsed: %i / %i / %s"
                        blockNumber.Value
                        consensusRound.Value
                        (unionCaseName consensusStep)

                    ConsensusCommand.Timeout (blockNumber, consensusRound, consensusStep)
                    |> ConsensusCommandInvoked
                    |> publishEvent
                }
                |> Async.Start

        new ConsensusState
            (
            persistConsensusState,
            restoreConsensusState,
            restoreConsensusMessages,
            getLastAppliedBlockNumber,
            getValidatorsAtHeight,
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
            messageRetryingInterval,
            proposeRetryingInterval,
            staleRoundDetectionInterval,
            validatorAddress
            )
