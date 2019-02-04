namespace Own.Blockchain.Public.Core

open Own.Common
open Own.Blockchain.Common
open Own.Blockchain.Public.Core
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Core.Dtos
open Own.Blockchain.Public.Core.Events

module Workflows =

    let getAvailableChxBalance
        getChxBalanceState
        getTotalChxStaked
        getValidatorState
        validatorDeposit
        senderAddress
        : ChxAmount
        =

        let chxBalance =
            senderAddress
            |> getChxBalanceState
            |> Option.map (Mapping.chxBalanceStateFromDto >> fun state -> state.Amount)
            |? ChxAmount 0m

        let chxStaked = getTotalChxStaked senderAddress

        let validatorDeposit =
            getValidatorState(senderAddress)
            |> Option.map Mapping.validatorStateFromDto
            |> Option.filter (fun v -> v.TimeToLockDeposit > 0s)
            |> Option.map (fun _ -> validatorDeposit)
            |? ChxAmount 0m

        chxBalance - chxStaked - validatorDeposit

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Blockchain
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let createGenesisBlock
        decodeHash
        createHash
        createMerkleTree
        zeroHash
        zeroAddress
        genesisChxSupply
        genesisAddress
        genesisValidators
        validatorDepositLockTime
        =

        let genesisValidators =
            genesisValidators
            |> List.map (fun (ba, na) ->
                BlockchainAddress ba,
                    {
                        ValidatorState.NetworkAddress = NetworkAddress na
                        SharedRewardPercent = 0m
                        TimeToLockDeposit = validatorDepositLockTime
                        TimeToBlacklist = 0s
                    }
            )
            |> Map.ofList

        let genesisState = Blocks.createGenesisState genesisChxSupply genesisAddress genesisValidators

        let genesisBlock =
            Blocks.assembleGenesisBlock
                decodeHash createHash createMerkleTree zeroHash zeroAddress genesisState

        genesisBlock, genesisState

    let signGenesisBlock
        (createGenesisBlock : unit -> Block * ProcessingOutput)
        createConsensusMessageHash
        signHash
        privateKey
        : Signature
        =

        let block, _ = createGenesisBlock ()

        createConsensusMessageHash
            block.Header.Number
            (ConsensusRound 0)
            (block.Header.Hash |> Some |> ConsensusMessage.Commit)
        |> signHash privateKey

    let initBlockchainState
        (tryGetLastAppliedBlockNumber : unit -> BlockNumber option)
        (createGenesisBlock : unit -> Block * ProcessingOutput)
        (getBlock : BlockNumber -> Result<BlockEnvelopeDto, AppErrors>)
        saveBlock
        saveBlockToDb
        persistStateChanges
        createConsensusMessageHash
        verifySignature
        genesisSignatures
        =

        if tryGetLastAppliedBlockNumber () = None then
            let genesisBlock, genesisState = createGenesisBlock ()
            let genesisBlockFromDisk =
                getBlock genesisBlock.Header.Number
                |> Result.map Blocks.extractBlockFromEnvelopeDto

            let genesisBlockExists =
                match genesisBlockFromDisk with
                | Ok blockFromDisk ->
                    if blockFromDisk <> genesisBlock then
                        failwith "Stored genesis block is invalid."
                    true
                | Error _ -> false

            let result =
                result {
                    let blockEnvelopeDto =
                        {
                            Block = genesisBlock |> Mapping.blockToDto
                            ConsensusRound = 0
                            Signatures = genesisSignatures
                        }

                    let! genesisSigners =
                        blockEnvelopeDto
                        |> Mapping.blockEnvelopeFromDto
                        |> Blocks.verifyBlockSignatures createConsensusMessageHash verifySignature

                    match genesisBlock.Configuration with
                    | None -> return! Result.appError "Genesis block must have configuration."
                    | Some c ->
                        let validators = c.Validators |> List.map (fun v -> v.ValidatorAddress)
                        if (set validators) <> (set genesisSigners) then
                            return! Result.appError "Genesis signatures don't match genesis validators."

                    if not genesisBlockExists then
                        do! saveBlock genesisBlock.Header.Number blockEnvelopeDto
                        do! genesisBlock.Header
                            |> Mapping.blockHeaderToBlockInfoDto (genesisBlock.Configuration <> None)
                            |> saveBlockToDb

                    do! genesisState
                        |> Mapping.outputToDto
                        |> persistStateChanges genesisBlock.Header.Number
                }

            match result with
            | Ok _ ->
                Log.info "Blockchain state initialized."
            | Error errors ->
                Log.appErrors errors
                failwith "Cannot initialize blockchain state."

    let createBlock
        getTx
        getEquivocationProof
        verifySignature
        isValidAddress
        getChxBalanceStateFromStorage
        getHoldingStateFromStorage
        getVoteStateFromStorage
        getEligibilityStateFromStorage
        getKycProvidersStateFromStorage
        getAccountStateFromStorage
        getAssetStateFromStorage
        getAssetHashByCodeFromStorage
        getValidatorStateFromStorage
        getStakeStateFromStorage
        getTotalChxStakedFromStorage
        getTopStakersFromStorage
        (getValidatorsAtHeight : BlockNumber -> ValidatorSnapshot list)
        getLockedAndBlacklistedValidators
        deriveHash
        decodeHash
        createHash
        createConsensusMessageHash
        createMerkleTree
        (calculateConfigurationBlockNumberForNewBlock : BlockNumber -> BlockNumber)
        minTxActionFee
        validatorDeposit
        validatorDepositLockTime
        validatorBlacklistTime
        validatorAddress
        (previousBlockHash : BlockHash)
        (blockNumber : BlockNumber)
        timestamp
        txSet
        equivocationProofs
        blockchainConfiguration
        =

        let getChxBalanceState = memoize (getChxBalanceStateFromStorage >> Option.map Mapping.chxBalanceStateFromDto)
        let getHoldingState = memoize (getHoldingStateFromStorage >> Option.map Mapping.holdingStateFromDto)
        let getVoteState = memoize (getVoteStateFromStorage >> Option.map Mapping.voteStateFromDto)
        let getEligibilityState = memoize (getEligibilityStateFromStorage >> Option.map Mapping.eligibilityStateFromDto)
        let getKycProvidersState = memoize getKycProvidersStateFromStorage
        let getAccountState = memoize (getAccountStateFromStorage >> Option.map Mapping.accountStateFromDto)
        let getAssetHashByCode = memoize getAssetHashByCodeFromStorage
        let getAssetState = memoize (getAssetStateFromStorage >> Option.map Mapping.assetStateFromDto)
        let getValidatorState = memoize (getValidatorStateFromStorage >> Option.map Mapping.validatorStateFromDto)
        let getStakeState = memoize (getStakeStateFromStorage >> Option.map Mapping.stakeStateFromDto)
        let getTotalChxStaked = memoize getTotalChxStakedFromStorage

        let getTopStakers = getTopStakersFromStorage >> List.map Mapping.stakerInfoFromDto

        let validators = getValidatorsAtHeight (blockNumber - 1)

        let sharedRewardPercent =
            match validators |> List.filter (fun v -> v.ValidatorAddress = validatorAddress) with
            | [] ->
                failwithf "Validator %s not found to create block %i."
                    validatorAddress.Value
                    blockNumber.Value
            | [v] ->
                v.SharedRewardPercent
            | vs ->
                failwithf "%i entries found for validator %s while creating block %i."
                    vs.Length
                    validatorAddress.Value
                    blockNumber.Value

        let validators = validators |> List.map (fun v -> v.ValidatorAddress)

        let output =
            Processing.processChanges
                getTx
                getEquivocationProof
                verifySignature
                isValidAddress
                deriveHash
                decodeHash
                createHash
                createConsensusMessageHash
                getChxBalanceState
                getHoldingState
                getVoteState
                getEligibilityState
                getKycProvidersState
                getAccountState
                getAssetState
                getAssetHashByCode
                getValidatorState
                getStakeState
                getTotalChxStaked
                getTopStakers
                getLockedAndBlacklistedValidators
                validatorDeposit
                validatorDepositLockTime
                validatorBlacklistTime
                validators
                validatorAddress
                sharedRewardPercent
                blockNumber
                blockchainConfiguration
                equivocationProofs
                txSet

        let configurationBlockNumber =
            calculateConfigurationBlockNumberForNewBlock blockNumber

        let block =
            Blocks.assembleBlock
                decodeHash
                createHash
                createMerkleTree
                validatorAddress
                blockNumber
                timestamp
                previousBlockHash
                configurationBlockNumber
                txSet
                equivocationProofs
                output
                blockchainConfiguration

        (block, output)

    let proposeBlock
        (getLastAppliedBlockNumber : unit -> BlockNumber)
        createBlock
        isConfigurationBlock
        createNewBlockchainConfiguration
        getBlock
        getPendingTxs
        (getPendingEquivocationProofs : BlockNumber -> EquivocationInfoDto list)
        getChxBalanceStateFromStorage
        getAvailableChxBalanceFromStorage
        maxTxCountPerBlock
        addressFromPrivateKey
        validatorPrivateKey
        (blockNumber : BlockNumber)
        : Result<Block, AppErrors> option
        =

        let timestamp = Utils.getUnixTimestamp () |> Timestamp

        let getChxBalanceState = memoize (getChxBalanceStateFromStorage >> Option.map Mapping.chxBalanceStateFromDto)
        let getAvailableChxBalance = memoize getAvailableChxBalanceFromStorage

        let validatorAddress = validatorPrivateKey |> addressFromPrivateKey
        match
            Processing.getTxSetForNewBlock
                getPendingTxs
                getChxBalanceState
                getAvailableChxBalance
                maxTxCountPerBlock
            with
        | [] -> None // Nothing to propose.
        | txSet ->
            result {
                let lastAppliedBlockNumber = getLastAppliedBlockNumber ()

                if blockNumber <> lastAppliedBlockNumber + 1 then
                    return!
                        sprintf "Cannot propose block %i due to block %i being last applied block."
                            blockNumber.Value
                            lastAppliedBlockNumber.Value
                        |> Result.appError

                let! lastAppliedBlock =
                    getBlock lastAppliedBlockNumber
                    |> Result.map Blocks.extractBlockFromEnvelopeDto

                let txSet =
                    txSet
                    |> Processing.orderTxSet

                let equivocationProofs =
                    getPendingEquivocationProofs blockNumber
                    |> List.sortBy (fun p ->
                        p.BlockNumber, p.ConsensusRound, p.ConsensusStep, p.ValidatorAddress, p.EquivocationProofHash
                    )
                    |> List.map (fun p -> p.EquivocationProofHash |> EquivocationProofHash)

                let blockchainConfiguration =
                    if isConfigurationBlock blockNumber then
                        createNewBlockchainConfiguration ()
                        |> Some
                    else
                        None

                let block, _ =
                    createBlock
                        validatorAddress
                        lastAppliedBlock.Header.Hash
                        blockNumber
                        timestamp
                        txSet
                        equivocationProofs
                        blockchainConfiguration

                return block
            }
            |> Some

    let storeReceivedBlock
        isValidAddress
        getBlock
        createConsensusMessageHash
        verifySignature
        blockExists
        saveBlock
        saveBlockToDb
        minValidatorCount
        blockEnvelopeDto
        =

        result {
            let! blockEnvelope = Validation.validateBlockEnvelope isValidAddress blockEnvelopeDto
            let block = blockEnvelope.Block

            if not (blockExists block.Header.ConfigurationBlockNumber) then
                Synchronization.unverifiedBlocks.TryAdd(block.Header.Number, blockEnvelopeDto) |> ignore
                return!
                    sprintf "Missing configuration block %i for block %i."
                        block.Header.ConfigurationBlockNumber.Value
                        block.Header.Number.Value
                    |> Result.appError

            let! configBlock =
                getBlock block.Header.ConfigurationBlockNumber
                |> Result.map Blocks.extractBlockFromEnvelopeDto

            let validators =
                configBlock.Configuration
                |> Option.map (fun c -> c.Validators |> List.map (fun v -> v.ValidatorAddress) |> Set.ofList)
                |? Set.empty

            if validators.IsEmpty then
                return!
                    sprintf "No validators found in configuration block %i to validate block %i."
                        block.Header.ConfigurationBlockNumber.Value
                        block.Header.Number.Value
                    |> Result.appError

            if validators.Count < minValidatorCount then
                return!
                    sprintf "Configuration block %i must have at least %i validators in the configuration."
                        block.Header.ConfigurationBlockNumber.Value
                        minValidatorCount
                    |> Result.appError

            let! blockSigners =
                Blocks.verifyBlockSignatures createConsensusMessageHash verifySignature blockEnvelope
                |> Result.map (Set.ofList >> Set.intersect validators)

            let qualifiedMajority = Validators.calculateQualifiedMajority validators.Count
            if blockSigners.Count < qualifiedMajority then
                return!
                    sprintf "Block %i is not signed by qualified majority. Expected (min): %i / Actual: %i"
                        block.Header.Number.Value
                        qualifiedMajority
                        blockSigners.Count
                    |> Result.appError

            do! saveBlock block.Header.Number blockEnvelopeDto

            do! block.Header
                |> Mapping.blockHeaderToBlockInfoDto (block.Configuration <> None)
                |> saveBlockToDb

            Synchronization.unverifiedBlocks.TryRemove(block.Header.Number) |> ignore

            return block.Header.Number
        }

    let persistTxResults saveTxResult txResults =
        txResults
        |> Map.toList
        |> List.fold (fun result (txHash, txResult) ->
            result >>= fun _ ->
                Log.noticef "Saving TxResult %s" txHash
                saveTxResult (TxHash txHash) txResult
        ) (Ok ())

    let removeOrphanTxResults getAllPendingTxHashes txResultExists deleteTxResult =
        let pendingTxHashes = getAllPendingTxHashes ()
        for (h : TxHash) in pendingTxHashes do
            if txResultExists h then
                Log.warningf "Deleting orphan TxResult: %s" h.Value
                deleteTxResult h
                |> Result.iterError Log.appErrors

    let persistEquivocationProofResults saveEquivocationProofResult equivocationProofResults =
        equivocationProofResults
        |> Map.toList
        |> List.fold (fun result (equivocationProofHash, equivocationProofResult) ->
            result >>= fun _ ->
                Log.noticef "Saving EquivocationProofResult %s" equivocationProofHash
                saveEquivocationProofResult (EquivocationProofHash equivocationProofHash) equivocationProofResult
        ) (Ok ())

    let removeOrphanEquivocationProofResults
        getAllPendingEquivocationProofHashes
        equivocationProofResultExists
        deleteEquivocationProofResult
        =

        let pendingEquivocationProofHashes = getAllPendingEquivocationProofHashes ()
        for (h : EquivocationProofHash) in pendingEquivocationProofHashes do
            if equivocationProofResultExists h then
                Log.warningf "Deleting orphan EquivocationProofResult: %s" h.Value
                deleteEquivocationProofResult h
                |> Result.iterError Log.appErrors

    let applyBlockToCurrentState
        getBlock
        isValidSuccessorBlock
        txResultExists
        equivocationProofResultExists
        createBlock
        (block : Block)
        =

        result {
            let! previousBlock =
                getBlock (block.Header.Number - 1)
                |> Result.map Blocks.extractBlockFromEnvelopeDto

            if not (isValidSuccessorBlock previousBlock.Header.Hash block) then
                return!
                    sprintf "Block %i is not a valid successor of the previous block." block.Header.Number.Value
                    |> Result.appError

            for txHash in block.TxSet do
                if txResultExists txHash then
                    return!
                        sprintf
                            "Tx %s cannot be included in the block %i because it is already processed."
                            txHash.Value
                            block.Header.Number.Value
                        |> Result.appError

            for equivocationProofHash in block.EquivocationProofs do
                if equivocationProofResultExists equivocationProofHash then
                    return!
                        sprintf
                            "EquivocationProof %s cannot be included in the block %i because it is already processed."
                            equivocationProofHash.Value
                            block.Header.Number.Value
                        |> Result.appError

            let createdBlock, output =
                createBlock
                    block.Header.ProposerAddress
                    previousBlock.Header.Hash
                    block.Header.Number
                    block.Header.Timestamp
                    block.TxSet
                    block.EquivocationProofs
                    block.Configuration

            if block <> createdBlock then
                Log.debugf "RECEIVED BLOCK:\n%A" block
                Log.debugf "CREATED BLOCK:\n%A" createdBlock
                return!
                    sprintf "Applying of block %i didn't result in expected blockchain state." block.Header.Number.Value
                    |> Result.appError

            return output
        }

    let applyBlock
        getBlock
        applyBlockToCurrentState
        persistTxResults
        persistEquivocationProofResults
        persistStateChanges
        blockNumber
        =

        result {
            let! block =
                getBlock blockNumber
                |> Result.map Blocks.extractBlockFromEnvelopeDto

            let! output = applyBlockToCurrentState block

            #if DEBUG
            let outputFileName = sprintf "Data/Block_%i_output_apply" block.Header.Number.Value
            System.IO.File.WriteAllText(outputFileName, sprintf "%A" output)
            #endif

            let outputDto = Mapping.outputToDto output
            do! persistTxResults outputDto.TxResults
            do! persistEquivocationProofResults outputDto.EquivocationProofResults
            do! persistStateChanges blockNumber outputDto
        }

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Consensus
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let handleReceivedConsensusMessage
        decodeHash
        createHash
        (getValidators : unit -> ValidatorSnapshot list)
        (verifySignature : Signature -> string -> BlockchainAddress option)
        (envelopeDto : ConsensusMessageEnvelopeDto)
        =

        let envelope = Mapping.consensusMessageEnvelopeFromDto envelopeDto

        let messageHash =
            Consensus.createConsensusMessageHash
                decodeHash
                createHash
                envelope.BlockNumber
                envelope.Round
                envelope.ConsensusMessage

        result {
            let! senderAddress =
                match verifySignature (Signature envelopeDto.Signature) messageHash with
                | Some a -> Ok a
                | None ->
                    sprintf "Cannot verify signature for consensus message: %A" envelope
                    |> Result.appError

            let isSenderValidator =
                getValidators ()
                |> Seq.exists (fun v -> v.ValidatorAddress = senderAddress)

            if not isSenderValidator then
                return!
                    sprintf "%A is not a validator. Consensus message ignored: %A" senderAddress envelope
                    |> Result.appError

            return ConsensusCommand.Message (senderAddress, envelope)
        }

    let storeEquivocationProof
        verifySignature
        createConsensusMessageHash
        decodeHash
        createHash
        saveEquivocationProof
        saveEquivocationProofToDb
        equivocationProofDto
        =

        result {
            let! equivocationProof =
                Validation.validateEquivocationProof
                    verifySignature
                    createConsensusMessageHash
                    decodeHash
                    createHash
                    equivocationProofDto

            do! saveEquivocationProof equivocationProof.EquivocationProofHash equivocationProofDto
            do! equivocationProof
                |> Mapping.equivocationProofToEquivocationInfoDto
                |> saveEquivocationProofToDb

            return equivocationProof.EquivocationProofHash
        }

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Network
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let propagateTx sendMessageToPeers networkAddress getTx (txHash : TxHash) =
        match getTx txHash with
        | Ok (txEnvelopeDto : TxEnvelopeDto) ->
            GossipMessage {
                MessageId = Tx txHash
                SenderAddress = NetworkAddress networkAddress // TODO: move it into network code
                Data = Serialization.serializeBinary txEnvelopeDto
            }
            |> sendMessageToPeers
        | _ -> Log.errorf "Tx %s does not exist" txHash.Value

    let propagateEquivocationProof
        sendMessageToPeers
        networkAddress
        getEquivocationProof
        (equivocationProofHash : EquivocationProofHash)
        =

        match getEquivocationProof equivocationProofHash with
        | Ok (equivocationProofDto : EquivocationProofDto) ->
            GossipMessage {
                MessageId = EquivocationProof equivocationProofHash
                SenderAddress = NetworkAddress networkAddress // TODO: move it into network code
                Data = Serialization.serializeBinary equivocationProofDto
            }
            |> sendMessageToPeers
        | _ -> Log.errorf "EquivocationProof %s does not exist" equivocationProofHash.Value

    let propagateBlock
        sendMessageToPeers
        networkAddress
        getBlock
        (blockNumber : BlockNumber)
        =

        match getBlock blockNumber with
        | Ok (blockEnvelopeDto : BlockEnvelopeDto) ->
            GossipMessage {
                MessageId = Block blockNumber
                SenderAddress = NetworkAddress networkAddress // TODO: move it into network code
                Data = Serialization.serializeBinary blockEnvelopeDto
            }
            |> sendMessageToPeers
        | _ -> Log.errorf "Block %i does not exist." blockNumber.Value

    let processPeerMessage
        getTx
        getEquivocationProof
        getBlock
        getLastAppliedBlockNumber
        handleReceivedConsensusMessage
        respondToPeer
        peerMessage
        =

        let processTxFromPeer isResponse txHash data =
            match getTx txHash with
            | Ok _ -> None
            | _ ->
                data
                |> Serialization.deserializeBinary<TxEnvelopeDto>
                |> fun txEnvelopeDto ->
                    (txHash, txEnvelopeDto)
                    |> (if isResponse then TxFetched else TxReceived)
                    |> Some
            |> Ok

        let processEquivocationProofFromPeer isResponse equivocationProofHash data =
            match getEquivocationProof equivocationProofHash with
            | Ok _ -> None
            | _ ->
                data
                |> Serialization.deserializeBinary<EquivocationProofDto>
                |> fun equivocationProofDto ->
                    equivocationProofDto
                    |> (if isResponse then EquivocationProofFetched else EquivocationProofReceived)
                    |> Some
            |> Ok

        let processBlockFromPeer isResponse blockNr data =
            let blockEnvelopeDto = data |> Serialization.deserializeBinary<BlockEnvelopeDto>

            result {
                let receivedBlock = Blocks.extractBlockFromEnvelopeDto blockEnvelopeDto

                return
                    match getBlock receivedBlock.Header.Number with
                    | Ok _ -> None
                    | _ ->
                        (receivedBlock.Header.Number, blockEnvelopeDto)
                        |> (if isResponse then BlockFetched else BlockReceived)
                        |> Some
            }

        let processConsensusMessageFromPeer consensusMessageId data =
            data
            |> Serialization.deserializeBinary<ConsensusMessageEnvelopeDto>
            |> handleReceivedConsensusMessage
            |> Result.map (ConsensusMessageReceived >> Some)

        let processData isResponse messageId (data : byte[]) =
            match messageId with
            | Tx txHash -> processTxFromPeer isResponse txHash data
            | EquivocationProof proofHash -> processEquivocationProofFromPeer isResponse proofHash data
            | Block blockNr -> processBlockFromPeer isResponse blockNr data
            | Consensus consensusMessageId -> processConsensusMessageFromPeer consensusMessageId data

        let processRequest messageId senderAddress =
            match messageId with
            | Tx txHash ->
                match getTx txHash with
                | Ok txEvenvelopeDto ->
                    ResponseDataMessage {
                        MessageId = messageId
                        Data = txEvenvelopeDto |> Serialization.serializeBinary
                    }
                    |> respondToPeer senderAddress
                    Ok None
                | _ -> Result.appError (sprintf "Requested tx %s not found" txHash.Value)

            | EquivocationProof equivocationProofHash ->
                match getEquivocationProof equivocationProofHash with
                | Ok equivocationProofDto ->
                    ResponseDataMessage {
                        MessageId = messageId
                        Data = equivocationProofDto |> Serialization.serializeBinary
                    }
                    |> respondToPeer senderAddress
                    Ok None
                | _ -> Result.appError (sprintf "Requested equivocation proof %s not found" equivocationProofHash.Value)

            | Block blockNr ->
                let blockNr =
                    if blockNr = BlockNumber -1L then
                        getLastAppliedBlockNumber()
                    else
                        blockNr

                match getBlock blockNr with
                | Ok blockEnvelopeDto ->
                    ResponseDataMessage {
                        MessageId = messageId
                        Data = blockEnvelopeDto |> Serialization.serializeBinary
                    }
                    |> respondToPeer senderAddress
                    Ok None
                | _ -> Result.appError (sprintf "Requested block %i not found" blockNr.Value)

            | Consensus _ -> Result.appError ("Cannot request consensus message from Peer")

        match peerMessage with
        | GossipDiscoveryMessage _ -> None
        | GossipMessage m -> processData false m.MessageId m.Data |> Some
        | MulticastMessage m -> processData false m.MessageId m.Data |> Some
        | RequestDataMessage m -> processRequest m.MessageId m.SenderAddress |> Some
        | ResponseDataMessage m -> processData true m.MessageId m.Data |> Some

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // API
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let submitTx
        verifySignature
        isValidAddress
        createHash
        getAvailableChxBalance
        getTotalFeeForPendingTxs
        saveTx
        saveTxToDb
        minTxActionFee
        isIncludedInBlock
        txEnvelopeDto
        : Result<TxHash, AppErrors>
        =

        result {
            let! txEnvelope = Validation.validateTxEnvelope txEnvelopeDto
            let! senderAddress = Validation.verifyTxSignature createHash verifySignature txEnvelope
            let txHash = txEnvelope.RawTx |> createHash |> TxHash

            let! txDto = Serialization.deserializeTx txEnvelope.RawTx
            let! tx = Validation.validateTx isValidAddress senderAddress txHash txDto

            // Txs included in verified blocks are considered to be valid, hence shouldn't be rejected for fees.
            if not isIncludedInBlock then
                if tx.Fee < minTxActionFee then
                    return! Result.appError "Fee is too low."

                do!
                    Validation.checkIfBalanceCanCoverFees
                        getAvailableChxBalance
                        getTotalFeeForPendingTxs
                        senderAddress
                        tx.TotalFee

            do! saveTx txHash txEnvelopeDto
            do! tx
                |> Mapping.txToTxInfoDto
                |> saveTxToDb

            return txHash
        }

    let getTxApi
        getTx
        getTxInfo
        getTxResult
        createHash
        verifySignature
        (txHash : TxHash)
        : Result<GetTxApiResponseDto, AppErrors>
        =

        result {
            let! txEnvelope =
                getTx txHash
                |> Result.map Mapping.txEnvelopeFromDto

            let! senderAddress = Validation.verifyTxSignature createHash verifySignature txEnvelope

            let! txDto = Serialization.deserializeTx txEnvelope.RawTx

            let! txResult =
                // If the Tx is still in the pool (DB), we respond with Pending status and ignore the TxResult file.
                // This avoids the lag in the state changes (when TxResult file is persisted, but DB not yet updated).
                match getTxInfo txHash with
                | Some _ -> Ok None
                | None -> getTxResult txHash |> Result.map Some

            return Mapping.txToGetTxApiResponseDto txHash senderAddress txDto txResult
        }

    let getEquivocationProofApi
        getEquivocationProof
        getEquivocationInfo
        getEquivocationProofResult
        decodeHash
        createHash
        verifySignature
        (equivocationProofHash : EquivocationProofHash)
        : Result<GetEquivocationProofApiResponseDto, AppErrors>
        =

        result {
            let! equivocationProofDto = getEquivocationProof equivocationProofHash
            let! equivocationProof =
                Validation.validateEquivocationProof
                    verifySignature
                    Consensus.createConsensusMessageHash
                    decodeHash
                    createHash
                    equivocationProofDto

            let! equivocationProofResult =
                // If the EquivocationProof is still in the pool (DB),
                // we respond with Pending status and ignore the EquivocationProofResult file.
                // This avoids the lag in the state changes
                // (when EquivocationProofResult file is persisted, but DB not yet updated).
                match getEquivocationInfo equivocationProofHash with
                | Some _ -> Ok None
                | None -> getEquivocationProofResult equivocationProofHash |> Result.map Some

            return
                Mapping.equivocationProofToGetEquivocationProofApiResponseDto
                    equivocationProofHash
                    equivocationProof.ValidatorAddress
                    equivocationProofDto
                    equivocationProofResult
        }

    let getBlockApi
        getLastAppliedBlockNumber
        (getBlock : BlockNumber -> Result<BlockEnvelopeDto, AppErrors>)
        (blockNumber : BlockNumber)
        : Result<GetBlockApiResponseDto, AppErrors>
        =

        let lastAppliedBlockNumber = getLastAppliedBlockNumber ()
        if blockNumber > lastAppliedBlockNumber then
            sprintf "Last applied block on this node is %i" lastAppliedBlockNumber.Value
            |> Result.appError
        else
            getBlock blockNumber
            >>= (Mapping.blockEnvelopeDtoToGetBlockApiResponseDto >> Ok)

    let getAddressApi
        getChxBalanceState
        (blockchainAddress : BlockchainAddress)
        : Result<GetAddressApiResponseDto, AppErrors>
        =

        match getChxBalanceState blockchainAddress with
        | Some addressState ->
            addressState
            |> Mapping.chxBalanceStateDtoToGetAddressApiResponseDto blockchainAddress
            |> Ok
        | None ->
            {
                ChxBalanceStateDto.Amount = 0m
                Nonce = 0L
            }
            |> Mapping.chxBalanceStateDtoToGetAddressApiResponseDto blockchainAddress
            |> Ok

    let getAddressAccountsApi
        (getAddressAccounts : BlockchainAddress -> AccountHash list)
        (address : BlockchainAddress)
        : Result<GetAddressAccountsApiResponseDto, AppErrors>
        =

        let accounts =
            getAddressAccounts address
            |> List.map (fun (AccountHash h) -> h)

        Ok {GetAddressAccountsApiResponseDto.Accounts = accounts}

    let getAccountApi
        (getAccountState : AccountHash -> AccountStateDto option)
        getAccountHoldings
        (accountHash : AccountHash)
        (assetHash : AssetHash option)
        : Result<GetAccountApiResponseDto, AppErrors>
        =

        match getAccountState accountHash with
        | None ->
            sprintf "Account %s does not exist." accountHash.Value
            |> Result.appError
        | Some accountState ->
            getAccountHoldings accountHash assetHash
            |? []
            |> Mapping.accountHoldingDtosToGetAccoungHoldingsResponseDto accountHash accountState
            |> Ok
