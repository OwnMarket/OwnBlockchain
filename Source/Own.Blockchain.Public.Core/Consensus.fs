namespace Own.Blockchain.Public.Core

open System
open System.Collections.Generic
open Own.Common
open Own.Blockchain.Common
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Core.Dtos
open Own.Blockchain.Public.Core.Events

module Consensus =

    type ConsensusState
        (
        getLastAppliedBlockNumber : unit -> BlockNumber,
        getCurrentValidators : unit -> ValidatorSnapshot list,
        proposeBlock : BlockNumber -> Result<Block, AppErrors> option,
        txExists : TxHash -> bool,
        requestTx : TxHash -> unit,
        isValidBlock : Block -> bool,
        saveBlock : BlockNumber -> BlockEnvelopeDto -> Result<unit, AppErrors>,
        applyBlock : BlockNumber -> Result<unit, AppErrors>,
        sendConsensusMessage : BlockNumber -> ConsensusRound -> ConsensusMessage -> unit,
        publishEvent : AppEvent -> unit,
        scheduleMessage : int -> (BlockchainAddress * ConsensusMessageEnvelope) -> unit,
        schedulePropose : int -> (BlockNumber * ConsensusRound) -> unit,
        scheduleTimeout : int -> (BlockNumber * ConsensusRound * ConsensusStep) -> unit,
        messageRetryingInterval : int,
        proposeRetryingInterval : int,
        timeoutPropose : int,
        timeoutVote : int,
        timeoutCommit : int,
        validatorAddress : BlockchainAddress
        )
        =

        let mutable _validators = []
        let mutable _qualifiedMajority = 0
        let mutable _validQuorum = 0

        let mutable _blockNumber = BlockNumber 0L
        let mutable _round = ConsensusRound 0
        let mutable _step = ConsensusStep.Propose
        let mutable _decisions = new Dictionary<BlockNumber, Block>()
        let mutable _lockedBlock = None
        let mutable _lockedRound = ConsensusRound -1
        let mutable _validBlock = None
        let mutable _validRound = ConsensusRound -1
        let _proposals = new Dictionary<BlockNumber * ConsensusRound * BlockchainAddress, Block * ConsensusRound>()
        let _votes = new Dictionary<BlockNumber * ConsensusRound * BlockchainAddress, BlockHash option>()
        let _commits = new Dictionary<BlockNumber * ConsensusRound * BlockchainAddress, BlockHash option * Signature>()

        member __.HandleConsensusCommand(command : ConsensusCommand) =
            match command with
            | Synchronize -> __.Synchronize(false)
            | Message (senderAddress, message) -> __.ProcessConsensusMessage (senderAddress, message)
            | RetryPropose (blockNumber, consensusRound) -> __.RetryPropose(blockNumber, consensusRound)
            | Timeout (blockNumber, consensusRound, consensusStep) ->
                match consensusStep with
                | ConsensusStep.Propose -> __.OnTimeoutPropose(blockNumber, consensusRound)
                | ConsensusStep.Vote -> __.OnTimeoutVote(blockNumber, consensusRound)
                | ConsensusStep.Commit -> __.OnTimeoutCommit(blockNumber, consensusRound)

        member private __.ProcessConsensusMessage(senderAddress, envelope : ConsensusMessageEnvelope) =
            let key = (envelope.BlockNumber, envelope.Round, senderAddress)

            match envelope.ConsensusMessage with
            | ConsensusMessage.Propose (block, vr) ->
                let missingTxs = block.TxSet |> List.filter (txExists >> not)
                if missingTxs.IsEmpty then
                    if _proposals.TryAdd(key, (block, vr)) then
                        __.UpdateState()
                else
                    if _blockNumber = block.Header.Number then
                        missingTxs |> List.iter requestTx
                        scheduleMessage messageRetryingInterval (senderAddress, envelope)
            | ConsensusMessage.Vote blockHash ->
                if _votes.TryAdd(key, blockHash) then
                    __.UpdateState()
            | ConsensusMessage.Commit blockHash ->
                if _commits.TryAdd(key, (blockHash, envelope.Signature)) then
                    __.UpdateState()

        member private __.Synchronize(deleteMessageLogs) =
            let nextBlockNumber = getLastAppliedBlockNumber () + 1
            if _blockNumber <> nextBlockNumber then
                _blockNumber <- nextBlockNumber
                _validators <- getCurrentValidators ()
                _qualifiedMajority <- Validators.calculateQualifiedMajority _validators.Length
                _validQuorum <- Validators.calculateValidQuorum _validators.Length
                __.ResetState(deleteMessageLogs)
                __.StartRound(ConsensusRound 0)

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Core Logic
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        member private __.ResetState(deleteMessageLogs) =
            _lockedRound <- ConsensusRound -1
            _lockedBlock <- None
            _validRound <- ConsensusRound -1
            _validBlock <- None

            if deleteMessageLogs then
                _proposals.Clear()
                _votes.Clear()
                _commits.Clear()

        member private __.TryPropose() =
            let block =
                _validBlock
                |> Option.orElseWith (fun _ ->
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

            if Validators.getProposerAddress _blockNumber _round _validators = validatorAddress then
                __.TryPropose()
            else
                scheduleTimeout timeoutPropose (_blockNumber, _round, ConsensusStep.Propose)

        member private __.UpdateState() =
            // PROPOSE RULES
            __.GetProposal()
            |> Option.iter (fun ((_, _, _), (block, vr)) ->
                if _step = ConsensusStep.Propose then
                    if isValidBlock block && (_lockedRound = ConsensusRound -1 || _lockedBlock = Some block) then
                        __.SendVote(_round, Some block.Header.Hash)
                    else
                        __.SendVote(_round, None)
                    _step <- ConsensusStep.Vote

                if _step = ConsensusStep.Propose
                    && (vr >= ConsensusRound 0 && vr < _round)
                    && __.MajorityVoted(vr, Some block.Header.Hash)
                then
                    if isValidBlock block && (_lockedRound <= vr || _lockedBlock = Some block) then
                        __.SendVote(_round, Some block.Header.Hash)
                    else
                        __.SendVote(_round, None)
                    _step <- ConsensusStep.Vote
            )

            // VOTE RULES
            if _step = ConsensusStep.Vote && __.MajorityVoted(_round) then
                scheduleTimeout timeoutVote (_blockNumber, _round, ConsensusStep.Vote)

            if _step >= ConsensusStep.Vote then
                __.GetProposal()
                |> Option.iter (fun ((_, _, _), (block, _)) ->
                    if __.MajorityVoted(_round, Some block.Header.Hash) && isValidBlock block then
                        if _step = ConsensusStep.Vote then
                            _lockedBlock <- Some block
                            _lockedRound <- _round
                            __.SendCommit(_round, Some block.Header.Hash)
                            _step <- ConsensusStep.Commit

                        _validBlock <- Some block
                        _validRound <- _round
                )

            if _step = ConsensusStep.Vote && __.MajorityVoted(_round, None) then
                __.SendCommit(_round, None)
                _step <- ConsensusStep.Commit

            // COMMIT RULES
            if __.MajorityCommitted(_round) then
                scheduleTimeout timeoutCommit (_blockNumber, _round, ConsensusStep.Commit)

            if not (_decisions.ContainsKey _blockNumber) then
                __.GetProposalCommittedByMajority()
                |> Option.iter (fun ((blockNumber, r, _), (block, _)) ->
                    if (*__.MajorityCommitted(r, Some block.Header.Hash) &&*) isValidBlock block then
                        _decisions.[blockNumber] <- block
                        __.SaveBlock(block, r)
                        __.Synchronize(true)
                )

            __.LatestValidRound()
            |> Option.iter (fun r -> if r > _round then __.StartRound(r))

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Timeout Handlers
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        member private __.OnTimeoutPropose(blockNumber, consensusRound) =
            if _blockNumber = blockNumber && _round = consensusRound && _step = ConsensusStep.Propose then
                __.SendVote(_round, None)
                _step <- ConsensusStep.Vote

        member private __.OnTimeoutVote(blockNumber, consensusRound) =
            if _blockNumber = blockNumber && _round = consensusRound && _step = ConsensusStep.Vote then
                __.SendCommit(_round, None)
                _step <- ConsensusStep.Commit

        member private __.OnTimeoutCommit(blockNumber, consensusRound) =
            if _blockNumber = blockNumber && _round = consensusRound then
                __.StartRound(_round + 1)

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Helpers
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        member private __.GetProposal()
            : ((BlockNumber * ConsensusRound * BlockchainAddress) * (Block * ConsensusRound)) option
            =

            let proposerAddress = Validators.getProposerAddress _blockNumber _round _validators
            _proposals
            |> Seq.ofDict
            |> Seq.filter (fun ((blockNumber, _, senderAddress), _) ->
                blockNumber = _blockNumber && senderAddress = proposerAddress
            )
            |> Seq.sortByDescending (fun ((_, consensusRound, _), _) -> consensusRound)
            |> Seq.tryHead

        member private __.GetProposalCommittedByMajority()
            : ((BlockNumber * ConsensusRound * BlockchainAddress) * (Block * ConsensusRound)) option
            =

            _proposals
            |> Seq.ofDict
            |> Seq.filter (fun ((blockNumber, r, senderAddress), (block, _)) ->
                blockNumber = _blockNumber
                && senderAddress = Validators.getProposerAddress _blockNumber r _validators
                && __.MajorityCommitted(r, Some block.Header.Hash)
            )
            |> Seq.sortBy (fun ((_, consensusRound, _), _) -> consensusRound)
            |> Seq.tryHead

        member private __.MajorityVoted(consensusRound, ?blockHash : BlockHash option) : bool =
            let count =
                _votes
                |> Seq.ofDict
                |> Seq.filter (fun ((bn, r, _), bh) ->
                    bn = _blockNumber
                    && r = consensusRound
                    && (blockHash.IsNone || bh = blockHash.Value))
                |> Seq.length

            count >= _qualifiedMajority

        member private __.MajorityCommitted(consensusRound, ?blockHash : BlockHash option) : bool =
            let count =
                _commits
                |> Seq.ofDict
                |> Seq.filter (fun ((bn, r, _), (bh, _)) ->
                    bn = _blockNumber
                    && r = consensusRound
                    && (blockHash.IsNone || bh = blockHash.Value))
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
            (block, _validRound)
            |> ConsensusMessage.Propose
            |> sendConsensusMessage _blockNumber consensusRound

        member private __.SendVote(consensusRound, blockHash) =
            blockHash
            |> ConsensusMessage.Vote
            |> sendConsensusMessage _blockNumber consensusRound

        member private __.SendCommit(consensusRound, blockHash) =
            blockHash
            |> ConsensusMessage.Commit
            |> sendConsensusMessage _blockNumber consensusRound

        member private __.SaveBlock(block, consensusRound) =
            let signatures =
                _commits
                |> List.ofDict
                |> List.choose (fun ((b, r, _), (h, Signature s)) ->
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

            block
            |> Mapping.blockToDto
            |> Serialization.serialize<BlockDto>
            |> Result.map (fun blockBytes ->
                {
                    Block = blockBytes |> Convert.ToBase64String
                    Signatures = signatures |> List.toArray
                }
            )
            >>= saveBlock block.Header.Number
            >>= fun _ -> applyBlock block.Header.Number // TODO: This should be done by applier worker...
            |> Result.handle
                (fun _ ->
                    Synchronization.setLastStoredBlock block
                    Synchronization.resetLastKnownBlock ()

                    BlockCreated { BlockCreatedEventData.BlockNumber = block.Header.Number }
                    |> publishEvent
                )
                (fun errors ->
                    Log.appErrors errors
                    failwithf "Cannot store the block %i created in consensus round %i."
                        block.Header.Number.Value
                        consensusRound.Value
                )

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
        zeroHash
        (BlockNumber blockNumber)
        (ConsensusRound consensusRound)
        (consensusMessage : ConsensusMessage)
        =

        match consensusMessage with
        | Propose (block, ConsensusRound validConsensusRound) ->
            [
                [| 0uy |] // Message type discriminator
                blockNumber |> Conversion.int64ToBytes
                consensusRound |> Conversion.int32ToBytes
                block.Header.Hash.Value |> decodeHash
                validConsensusRound |> Conversion.int32ToBytes
            ]
            |> Array.concat
            |> createHash
        | Vote blockHash ->
            [
                [| 1uy |] // Message type discriminator
                blockNumber |> Conversion.int64ToBytes
                consensusRound |> Conversion.int32ToBytes
                (blockHash |? BlockHash zeroHash).Value |> decodeHash
            ]
            |> Array.concat
            |> createHash
        | Commit blockHash ->
            match blockHash with
            | Some h -> h.Value // Simplifies verification of block signatures.
            | None ->
                [
                    [| 2uy |] // Message type discriminator
                    blockNumber |> Conversion.int64ToBytes
                    consensusRound |> Conversion.int32ToBytes
                    zeroHash |> decodeHash
                ]
                |> Array.concat
                |> createHash

    let createConsensusStateInstance
        getLastAppliedBlockNumber
        getCurrentValidators
        proposeBlock
        txExists
        requestTx
        applyBlockToCurrentState
        saveBlock
        applyBlock
        decodeHash
        createHash
        zeroHash
        signHash
        sendPeerMessage
        publishEvent
        addressFromPrivateKey
        (validatorPrivateKey : PrivateKey)
        messageRetryingInterval
        proposeRetryingInterval
        timeoutPropose
        timeoutVote
        timeoutCommit
        =

        let validatorAddress =
            addressFromPrivateKey validatorPrivateKey

        let getLastAppliedBlockNumber () =
            match getLastAppliedBlockNumber () with
            | None -> failwith "Cannot get last applied block number."
            | Some blockNumber -> blockNumber

        let isValidBlock block =
            match applyBlockToCurrentState block with
            | Ok _ ->
                true
            | Error e ->
                #if DEBUG
                Log.appErrors e
                #endif
                false

        // TODO: Move to Workflows module.
        let sendConsensusMessage blockNumber consensusRound consensusMessage =
            let consensusMessageHash =
                createConsensusMessageHash
                    decodeHash
                    createHash
                    zeroHash
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

            ConsensusCommand.Message (validatorAddress, consensusMessageEnvelope)
            |> ConsensusCommandInvoked
            |> publishEvent // Send message to self

            {
                MulticastMessage.MessageId =
                    sprintf "Consensus_%s" consensusMessageHash
                    |> ConsensusMessageId
                    |> NetworkMessageId.Consensus
                Data = consensusMessageEnvelope |> Mapping.consensusMessageEnvelopeToDto
            }
            |> MulticastMessage
            |> sendPeerMessage

            Log.debugf "Consensus message sent: %i / %i / %s"
                consensusMessageEnvelope.BlockNumber.Value
                consensusMessageEnvelope.Round.Value
                (consensusMessageEnvelope.ConsensusMessage |> consensusMessageDisplayFormat)

        let scheduleMessage timeout (senderAddress : BlockchainAddress, envelope : ConsensusMessageEnvelope) =
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
            async {
                Log.debugf "Timeout scheduled: %i / %i / %s"
                    blockNumber.Value
                    consensusRound.Value
                    (unionCaseName consensusStep)

                do! Async.Sleep timeout

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
            getLastAppliedBlockNumber,
            getCurrentValidators,
            proposeBlock,
            txExists,
            requestTx,
            isValidBlock,
            saveBlock,
            applyBlock,
            sendConsensusMessage,
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
