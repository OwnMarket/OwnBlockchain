namespace Own.Blockchain.Public.Core

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
        txExists : TxHash -> bool,
        equivocationProofExists : EquivocationProofHash -> bool,
        requestTx : TxHash -> unit,
        requestEquivocationProof : EquivocationProofHash -> unit,
        isValidBlock : Block -> bool,
        sendConsensusMessage : BlockNumber -> ConsensusRound -> ConsensusMessage -> unit,
        sendConsensusState : BlockchainAddress -> ConsensusStateResponse -> unit,
        publishEvent : AppEvent -> unit,
        scheduleMessage : int -> BlockchainAddress * ConsensusMessageEnvelope -> unit,
        schedulePropose : int -> BlockNumber * ConsensusRound -> unit,
        scheduleTimeout : int -> BlockNumber * ConsensusRound * ConsensusStep -> unit,
        messageRetryingInterval : int,
        proposeRetryingInterval : int,
        timeoutPropose : int,
        timeoutVote : int,
        timeoutCommit : int,
        validatorAddress : BlockchainAddress
        ) =

        let mutable _validators = []
        let mutable _qualifiedMajority = 0
        let mutable _validQuorum = 0

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
                scheduleTimeout timeoutPropose (_blockNumber, _round, ConsensusStep.Propose)
            __.Synchronize()
            __.UpdateState()

        member __.HandleConsensusCommand(command : ConsensusCommand) =
            match command with
            | Synchronize ->
                __.Synchronize()
            | Message (senderAddress, envelope) ->
                __.ProcessConsensusMessage(senderAddress, envelope)
            | RetryPropose (blockNumber, consensusRound) ->
                __.RetryPropose(blockNumber, consensusRound)
            | Timeout (blockNumber, consensusRound, consensusStep) ->
                match consensusStep with
                | ConsensusStep.Propose -> __.OnTimeoutPropose(blockNumber, consensusRound)
                | ConsensusStep.Vote -> __.OnTimeoutVote(blockNumber, consensusRound)
                | ConsensusStep.Commit -> __.OnTimeoutCommit(blockNumber, consensusRound)
            | StateRequested stateRequest ->
                __.SendState(stateRequest.ValidatorAddress)

        member private __.ProcessConsensusMessage(senderAddress, envelope : ConsensusMessageEnvelope) =
            if isValidatorBlacklisted (senderAddress, _blockNumber, envelope.BlockNumber) then
                envelope.ConsensusMessage
                |> unionCaseName
                |> Log.warningf "Validator %s is blacklisted. %s consensus message ignored." senderAddress.Value
            elif envelope.BlockNumber >= _blockNumber then
                let key = envelope.BlockNumber, envelope.Round, senderAddress

                match envelope.ConsensusMessage with
                | ConsensusMessage.Propose (block, vr) ->
                    let missingTxs =
                        block.TxSet
                        |> List.filter (txExists >> not)

                    let missingEquivocationProofs =
                        block.EquivocationProofs
                        |> List.filter (equivocationProofExists >> not)

                    match missingTxs, missingEquivocationProofs with
                    | [], [] ->
                        if _proposals.TryAdd(key, (block, vr, envelope.Signature)) then
                            __.UpdateState()
                    | _ ->
                        if _blockNumber = block.Header.Number then
                            missingTxs |> List.iter requestTx
                            missingEquivocationProofs |> List.iter requestEquivocationProof
                            scheduleMessage messageRetryingInterval (senderAddress, envelope)
                | ConsensusMessage.Vote blockHash ->
                    if _votes.TryAdd(key, (blockHash, envelope.Signature)) then
                        __.UpdateState()
                    else
                        __.DetectEquivocation(envelope, senderAddress)
                | ConsensusMessage.Commit blockHash ->
                    if _commits.TryAdd(key, (blockHash, envelope.Signature)) then
                        __.UpdateState()
                    else
                        __.DetectEquivocation(envelope, senderAddress)

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

        member private __.TryPropose() =
            let block =
                _validBlock
                |> Option.orElseWith (fun _ ->
                    let lastAppliedBlockNumber = getLastAppliedBlockNumber ()
                    let nextBlockNumber = lastAppliedBlockNumber + 1
                    if _blockNumber < nextBlockNumber then
                        Log.warningf "Consensus is at block %i, while the state is at block %i."
                            _blockNumber.Value
                            lastAppliedBlockNumber.Value
                        __.Synchronize()
                        None
                    elif _blockNumber > nextBlockNumber then
                        Log.warningf "Cannot propose block %i at this time due to block %i being last applied block."
                            _blockNumber.Value
                            lastAppliedBlockNumber.Value
                        None
                    else
                        proposeBlock _blockNumber
                        |> Option.bind (fun r ->
                            match r with
                            | Ok b -> Some b
                            | Error e ->
                                Log.error "Failed to propose block."
                                Log.appErrors e
                                None
                        )
                )

            match block with
            | None ->
                Log.debug "Nothing to propose."
                schedulePropose proposeRetryingInterval (_blockNumber, _round)
            | Some b -> __.SendPropose(_round, b)

        member private __.RetryPropose(blockNumber, consensusRound) =
            if _blockNumber = blockNumber && _round = consensusRound && _step = ConsensusStep.Propose then
                if __.GetProposal() = None then
                    __.TryPropose()

        member private __.StartRound(r) =
            _round <- r
            _step <- ConsensusStep.Propose

            if Validators.getProposer _blockNumber _round _validators = validatorAddress then
                __.TryPropose()

            scheduleTimeout timeoutPropose (_blockNumber, _round, ConsensusStep.Propose)

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
                scheduleTimeout timeoutVote (_blockNumber, _round, ConsensusStep.Vote)

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
                scheduleTimeout timeoutCommit (_blockNumber, _round, ConsensusStep.Commit)

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

        member private __.SendState(requestValidatorAddress) =
            let key = _blockNumber, _round, validatorAddress
            {
                ConsensusStateResponse.ProposeMessage =
                    match _proposals.TryGetValue(key) with
                    | true, (b, vr, s) ->
                        Some {
                            ConsensusMessageEnvelope.BlockNumber = _blockNumber
                            Round = _round
                            ConsensusMessage = Propose (b, vr)
                            Signature = s
                        }
                    | _ -> None
                VoteMessage =
                    match _votes.TryGetValue(key) with
                    | true, (bh, s) ->
                        Some {
                            ConsensusMessageEnvelope.BlockNumber = _blockNumber
                            Round = _round
                            ConsensusMessage = Vote bh
                            Signature = s
                        }
                    | _ -> None
                CommitMessage =
                    match _votes.TryGetValue(key) with
                    | true, (bh, s) ->
                        Some {
                            ConsensusMessageEnvelope.BlockNumber = _blockNumber
                            Round = _round
                            ConsensusMessage = Commit bh
                            Signature = s
                        }
                    | _ -> None
                LockedBlockSignatures = _lockedBlockSignatures
            }
            |> sendConsensusState requestValidatorAddress

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
                failwithf "Consensus state doesn't contain enough commits for block %i. Expected (min): %i, Actual: %i"
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
                | ConsensusMessage.Propose _ -> failwith "Don't call IsTryingToEquivocate for Propose messages."
                | ConsensusMessage.Vote hash -> hash, _votes
                | ConsensusMessage.Commit hash -> hash, _commits

            match messages.TryGetValue((_blockNumber, consensusRound, validatorAddress)) with
            | true, (foundBlockHash, _) when foundBlockHash <> blockHash ->
                Log.warningf "EQUIVOCATION: This node tries to %s %A in round %i on hight %i, while already did for %A."
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
            | Propose _ -> failwith "Don't call DetectEquivocation for Propose messages."
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
        persistConsensusState
        restoreConsensusState
        persistConsensusMessage
        restoreConsensusMessages
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

        let isValidBlock = memoizeBy (fun (b : Block) -> b.Header.Hash) <| fun block ->
            block
            |> Mapping.blockToDto
            |> Validation.validateBlock isValidAddress
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

        let sendConsensusState requestValidatorAddress state =
            () // TODO: Implement

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

        let scheduleTimeout timeout (blockNumber : BlockNumber, consensusRound : ConsensusRound, consensusStep) =
            if canParticipateInConsensus blockNumber = Some true then
                async {
                    Log.debugf "Timeout scheduled: %i / %i / %s"
                        blockNumber.Value
                        consensusRound.Value
                        (unionCaseName consensusStep)

                    do! Async.Sleep (timeout + timeoutDelta * min consensusRound.Value timeoutIncrements)

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
            messageRetryingInterval,
            proposeRetryingInterval,
            timeoutPropose,
            timeoutVote,
            timeoutCommit,
            validatorAddress
            )
