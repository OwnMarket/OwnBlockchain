namespace Own.Blockchain.Public.Core

open System
open Own.Common
open Own.Blockchain.Common
open Own.Blockchain.Public.Core
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Core.Dtos
open Own.Blockchain.Public.Core.Events

module Workflows =

    let getAvailableChxBalance getChxBalanceState getTotalChxStaked senderAddress : ChxAmount =
        let chxBalance =
            senderAddress
            |> getChxBalanceState
            |> Option.map (Mapping.chxBalanceStateFromDto >> fun state -> state.Amount)
            |? ChxAmount 0m

        let chxStaked = getTotalChxStaked senderAddress

        chxBalance - chxStaked

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
        =

        let genesisValidators =
            genesisValidators
            |> List.map (fun (ba, na) -> BlockchainAddress ba, {ValidatorState.NetworkAddress = na})
            |> Map.ofList

        let genesisState = Blocks.createGenesisState genesisChxSupply genesisAddress genesisValidators

        let genesisBlock =
            Blocks.assembleGenesisBlock
                decodeHash createHash createMerkleTree zeroHash zeroAddress genesisState

        genesisBlock, genesisState

    let signGenesisBlock
        (createGenesisBlock : unit -> Block * ProcessingOutput)
        decodeHash
        signBlock
        privateKey
        : Signature
        =

        createGenesisBlock ()
        |> fun (b, _) -> b.Header.Hash |> (fun (BlockHash h) -> h) |> decodeHash
        |> signBlock privateKey

    let initBlockchainState
        (getLastAppliedBlockNumber : unit -> BlockNumber option)
        (createGenesisBlock : unit -> Block * ProcessingOutput)
        (getBlock : BlockNumber -> Result<BlockEnvelopeDto, AppErrors>)
        saveBlock
        persistStateChanges
        verifySignature
        genesisSignatures
        =

        if getLastAppliedBlockNumber () = None then
            let genesisBlock, genesisState = createGenesisBlock ()

            let genesisBlockExists =
                match getBlock genesisBlock.Header.Number >>= Blocks.extractBlockFromEnvelopeDto with
                | Ok genesisBlockFromDisk ->
                    if genesisBlockFromDisk <> genesisBlock then
                        failwith "Stored genesis block is invalid."
                    true
                | _ ->
                    false

            let blockInfoDto = Mapping.blockHeaderToBlockInfoDto genesisBlock.Header

            let result =
                result {
                    let! blockEnvelopeDto =
                        genesisBlock
                        |> Mapping.blockToDto
                        |> Serialization.serialize<BlockDto>
                        |> Result.map (fun blockBytes ->
                            {
                                Block = blockBytes |> Convert.ToBase64String
                                Signatures = genesisSignatures |> List.toArray
                            }
                        )

                    let! genesisSigners =
                        blockEnvelopeDto
                        |> Mapping.blockEnvelopeFromDto
                        |> Validation.verifyBlockSignatures verifySignature

                    match genesisBlock.Configuration with
                    | None -> return! Result.appError "Genesis block must have configuration."
                    | Some c ->
                        let validators = c.Validators |> List.map (fun v -> v.ValidatorAddress)
                        if (set validators) <> (set genesisSigners) then
                            return! Result.appError "Genesis signatures don't match genesis validators."

                    if not genesisBlockExists then
                        do! saveBlock genesisBlock.Header.Number blockEnvelopeDto
                    do! genesisState
                        |> Mapping.outputToDto
                        |> persistStateChanges blockInfoDto
                }

            match result with
            | Ok _ ->
                Log.info "Blockchain state initialized."
            | Error errors ->
                Log.appErrors errors
                failwith "Cannot initialize blockchain state."

    let createBlock
        getTx
        verifySignature
        isValidAddress
        getChxBalanceStateFromStorage
        getHoldingStateFromStorage
        getAccountStateFromStorage
        getAssetStateFromStorage
        getValidatorStateFromStorage
        getStakeStateFromStorage
        getTotalChxStakedFromStorage
        decodeHash
        createHash
        createMerkleTree
        (calculateConfigurationBlockNumberForNewBlock : BlockNumber -> BlockNumber)
        minTxActionFee
        validatorAddress
        (previousBlockHash : BlockHash)
        blockNumber
        timestamp
        txSet
        blockchainConfiguration
        =

        let getChxBalanceState = memoize (getChxBalanceStateFromStorage >> Option.map Mapping.chxBalanceStateFromDto)
        let getHoldingState = memoize (getHoldingStateFromStorage >> Option.map Mapping.holdingStateFromDto)
        let getAccountState = memoize (getAccountStateFromStorage >> Option.map Mapping.accountStateFromDto)
        let getAssetState = memoize (getAssetStateFromStorage >> Option.map Mapping.assetStateFromDto)
        let getValidatorState = memoize (getValidatorStateFromStorage >> Option.map Mapping.validatorStateFromDto)
        let getStakeState = memoize (getStakeStateFromStorage >> Option.map Mapping.stakeStateFromDto)
        let getTotalChxStaked = memoize getTotalChxStakedFromStorage

        let output =
            txSet
            |> Processing.processTxSet
                getTx
                verifySignature
                isValidAddress
                decodeHash
                createHash
                getChxBalanceState
                getHoldingState
                getAccountState
                getAssetState
                getValidatorState
                getStakeState
                getTotalChxStaked
                minTxActionFee
                validatorAddress
                blockNumber

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
                output
                blockchainConfiguration

        (block, output)

    let proposeBlock
        (getLastAppliedBlockNumber : unit -> BlockNumber option)
        createBlock
        isConfigurationBlock
        (createNewBlockchainConfiguration : unit -> BlockchainConfiguration)
        getBlock
        getPendingTxs
        getChxBalanceStateFromStorage
        getAvailableChxBalanceFromStorage
        signBlock
        saveBlock
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
                let lastAppliedBlockNumber =
                    getLastAppliedBlockNumber () |?> fun _ -> failwith "Cannot get last applied block number."

                if blockNumber <> lastAppliedBlockNumber + 1 then
                    return!
                        sprintf "Cannot propose block %i due to block %i being last applied block."
                            (blockNumber |> fun (BlockNumber n) -> n)
                            (lastAppliedBlockNumber |> fun (BlockNumber n) -> n)
                        |> Result.appError

                let! lastAppliedBlock =
                    getBlock lastAppliedBlockNumber
                    >>= Blocks.extractBlockFromEnvelopeDto

                let txSet =
                    txSet
                    |> Processing.orderTxSet

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
                        blockchainConfiguration

                return block
            }
            |> Some

    let storeReceivedBlock
        isValidAddress
        getBlock
        verifySignature
        blockExists
        saveBlock
        minValidatorCount
        blockEnvelopeDto
        =

        result {
            let! blockEnvelope = Validation.validateBlockEnvelope blockEnvelopeDto

            let! blockDto =
                blockEnvelope.RawBlock
                |> Serialization.deserialize<Dtos.BlockDto>

            let! block = Validation.validateBlock isValidAddress blockDto

            Synchronization.setLastKnownBlock block

            if not (blockExists block.Header.ConfigurationBlockNumber) then
                return!
                    sprintf "Missing configuration block %i for block %i."
                        (block.Header.ConfigurationBlockNumber |> fun (BlockNumber n) -> n)
                        (block.Header.Number |> fun (BlockNumber n) -> n)
                    |> Result.appError

            let! configBlock =
                getBlock block.Header.ConfigurationBlockNumber
                >>= Blocks.extractBlockFromEnvelopeDto

            let validators =
                configBlock.Configuration
                |> Option.map (fun c -> c.Validators |> List.map (fun v -> v.ValidatorAddress) |> Set.ofList)
                |? Set.empty

            if validators.IsEmpty then
                return!
                    sprintf "No validators found in configuration block %i to validate block %i."
                        (block.Header.ConfigurationBlockNumber |> fun (BlockNumber n) -> n)
                        (block.Header.Number |> fun (BlockNumber n) -> n)
                    |> Result.appError

            if validators.Count < minValidatorCount then
                return!
                    sprintf "Configuration block %i must have at least %i validators in the configuration."
                        (block.Header.ConfigurationBlockNumber |> fun (BlockNumber n) -> n)
                        minValidatorCount
                    |> Result.appError

            let! blockSigners =
                Validation.verifyBlockSignatures verifySignature blockEnvelope
                |> Result.map (Set.ofList >> Set.intersect validators)

            let qualifiedMajority = Validators.calculateQualifiedMajority validators.Count
            if blockSigners.Count < qualifiedMajority then
                return!
                    sprintf "Block %i is not signed by qualified majority. Expected (min): %i / Actual: %i"
                        (block.Header.Number |> fun (BlockNumber n) -> n)
                        qualifiedMajority
                        blockSigners.Count
                    |> Result.appError

            do! saveBlock block.Header.Number blockEnvelopeDto

            Synchronization.setLastStoredBlock block

            return {BlockCreatedEventData.BlockNumber = block.Header.Number}
        }

    let persistTxResults saveTxResult txResults =
        txResults
        |> Map.toList
        |> List.fold (fun result (txHash, txResult) ->
            result
            >>= fun _ -> saveTxResult (TxHash txHash) txResult
        ) (Ok ())

    let applyBlockToCurrentState
        getBlock
        isValidSuccessorBlock
        createBlock
        (block : Block)
        =

        result {
            let! previousBlock =
                getBlock (block.Header.Number - 1L)
                >>= Blocks.extractBlockFromEnvelopeDto

            if not (isValidSuccessorBlock previousBlock.Header.Hash block) then
                return!
                    block.Header.Number
                    |> fun (BlockNumber n) ->
                        sprintf "Block %i is not a valid successor of the previous block." n
                    |> Result.appError

            let createdBlock, output =
                createBlock
                    block.Header.Validator
                    previousBlock.Header.Hash
                    block.Header.Number
                    block.Header.Timestamp
                    block.TxSet
                    block.Configuration

            if block <> createdBlock then
                Log.debugf "RECEIVED BLOCK:\n%A" block
                Log.debugf "CREATED BLOCK:\n%A" createdBlock
                return!
                    block.Header.Number
                    |> fun (BlockNumber n) ->
                        sprintf "Applying of block %i didn't result in expected blockchain state." n
                    |> Result.appError

            return output
        }

    let applyBlock
        getBlock
        applyBlockToCurrentState
        persistTxResults
        persistStateChanges
        blockNumber
        =

        result {
            let! block =
                getBlock blockNumber
                >>= Blocks.extractBlockFromEnvelopeDto

            let! output = applyBlockToCurrentState block

            #if DEBUG
            let outputFileName =
                block.Header.Number
                |> fun (BlockNumber n) -> n
                |> sprintf "Data/Block_%i_output_apply"
            System.IO.File.WriteAllText(outputFileName, sprintf "%A" output)
            #endif

            let blockInfoDto = Mapping.blockHeaderToBlockInfoDto block.Header
            let outputDto = Mapping.outputToDto output
            do! persistTxResults outputDto.TxResults
            do! persistStateChanges blockInfoDto outputDto
        }

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Consensus
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let handleReceivedConsensusMessage
        decodeHash
        createHash
        zeroHash
        (getValidators : unit -> ValidatorSnapshot list)
        (verifySignature : Signature -> byte[] -> BlockchainAddress option)
        (envelopeDto : ConsensusMessageEnvelopeDto)
        =

        let envelope = Mapping.consensusMessageEnvelopeFromDto envelopeDto

        let messageHash =
            Consensus.createConsensusMessageHash
                decodeHash
                createHash
                zeroHash
                envelope.BlockNumber
                envelope.Round
                envelope.ConsensusMessage

        result {
            let! senderAddress =
                match verifySignature (Signature envelopeDto.Signature) (decodeHash messageHash) with
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

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Network
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let propagateTx sendMessageToPeers networkAddress getTx (txHash : TxHash) =
        match getTx txHash with
        | Ok (txEnvelopeDto : TxEnvelopeDto) ->
            GossipMessage {
                MessageId = Tx txHash
                // TODO: move it into network code
                SenderAddress = NetworkAddress networkAddress
                Data = txEnvelopeDto
            }
            |> sendMessageToPeers
        | _ -> Log.errorf "Tx %s does not exist" (txHash |> fun (TxHash hash) -> hash)

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
                // TODO: move it into network code
                SenderAddress = NetworkAddress networkAddress
                Data = blockEnvelopeDto
            }
            |> sendMessageToPeers
        | _ -> Log.errorf "Block %i does not exist." (blockNumber |> fun (BlockNumber b) -> b)

    let processPeerMessage
        getTx
        getBlock
        getLastAppliedBlockNumber
        handleReceivedTx
        handleReceivedBlock
        handleReceivedConsensusMessage
        respondToPeer
        peerMessage
        =

        let processTxFromPeer txHash data =
            match getTx txHash with
            | Ok _ -> Ok None
            | _ ->
                data
                |> Serialization.deserializeJObject
                |> handleReceivedTx
                |> Result.map (TxReceived >> Some)

        let processBlockFromPeer blockNr data =
            let blockEnvelopeDto = data |> Serialization.deserializeJObject

            let existingBlock =
                Blocks.extractBlockFromEnvelopeDto blockEnvelopeDto
                >>= fun receivedBlock -> getBlock receivedBlock.Header.Number

            match existingBlock with
            | Ok _ -> Ok None
            | _ ->
                blockEnvelopeDto
                |> handleReceivedBlock
                |> Result.map (BlockReceived >> Some)

        let processConsensusMessageFromPeer consensusMessageId data =
            data
            |> Serialization.deserializeJObject
            |> handleReceivedConsensusMessage
            |> Result.map (ConsensusMessageReceived >> Some)

        let processData messageId (data : obj) =
            match messageId with
            | Tx txHash -> processTxFromPeer txHash data
            | Block blockNr -> processBlockFromPeer blockNr data
            | Consensus consensusMessageId -> processConsensusMessageFromPeer consensusMessageId data

        let processRequest messageId senderAddress =
            match messageId with
            | Tx txHash ->
                match getTx txHash with
                | Ok txEvenvelopeDto ->
                    ResponseDataMessage {
                        MessageId = messageId
                        Data = txEvenvelopeDto
                    }
                    |> respondToPeer senderAddress
                    Ok None
                | _ -> Result.appError (sprintf "Requested tx %A not found" txHash)

            | Block blockNr ->
                let blockNr =
                    match blockNr with
                    | BlockNumber -1L -> getLastAppliedBlockNumber()
                    | _ -> Some blockNr

                match blockNr with
                | Some blockNr ->
                    match getBlock blockNr with
                    | Ok blockEnvelopeDto ->
                        ResponseDataMessage {
                            MessageId = messageId
                            Data = blockEnvelopeDto
                        }
                        |> respondToPeer senderAddress
                        Ok None
                    | _ -> Result.appError (sprintf "Requested block %A not found" blockNr)
                | None -> Result.appError "Error retrieving last block"
            | Consensus _ -> Result.appError ("Cannot request consensus message from Peer")

        match peerMessage with
        | GossipDiscoveryMessage _ -> None
        | GossipMessage m -> processData m.MessageId m.Data |> Some
        | MulticastMessage m -> processData m.MessageId m.Data |> Some
        | RequestDataMessage m -> processRequest m.MessageId m.SenderAddress |> Some
        | ResponseDataMessage m -> processData m.MessageId m.Data |> Some

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
        txEnvelopeDto
        : Result<TxReceivedEventData, AppErrors>
        =

        result {
            let! txEnvelope = Validation.validateTxEnvelope txEnvelopeDto
            let! senderAddress = Validation.verifyTxSignature verifySignature txEnvelope
            let txHash = txEnvelope.RawTx |> createHash |> TxHash

            let! txDto = Serialization.deserializeTx txEnvelope.RawTx
            let! tx = Validation.validateTx isValidAddress minTxActionFee senderAddress txHash txDto

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

            return { TxHash = txHash }
        }

    let getTxApi
        getTx
        getTxInfo
        getTxResult
        verifySignature
        (txHash : TxHash)
        : Result<GetTxApiResponseDto, AppErrors>
        =

        result {
            let! txEnvelope =
                getTx txHash
                |> Result.map Mapping.txEnvelopeFromDto

            let! senderAddress = Validation.verifyTxSignature verifySignature txEnvelope

            let! txDto = Serialization.deserializeTx txEnvelope.RawTx

            let! txResult =
                // If the Tx is still in the pool (DB), we respond with Pending status and ignore the TxResult file.
                // This avoids the lag in the state changes (when TxResult file is persisted, but DB not yet updated).
                match getTxInfo txHash with
                | Some _ -> Ok None
                | None -> getTxResult txHash |> Result.map Some

            return Mapping.txToGetTxApiResponseDto txHash senderAddress txDto txResult
        }

    let getBlockApi
        getBlock
        (blockNumber : BlockNumber)
        : Result<GetBlockApiResponseDto, AppErrors>
        =

        match getBlock blockNumber with
        | Ok blockEnvelopeDto ->
            Blocks.extractBlockFromEnvelopeDto blockEnvelopeDto
            >>= fun block ->
                block
                |> Mapping.blockToDto
                |> Mapping.blockDtosToGetBlockApiResponseDto blockEnvelopeDto
                |> Ok
        | _ ->
            sprintf "Block %i does not exist" (blockNumber |> fun (BlockNumber b) -> b)
            |> Result.appError

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
            accountHash
            |> fun (AccountHash h) -> sprintf "Account %s does not exist." h
            |> Result.appError
        | Some accountState ->
            getAccountHoldings accountHash assetHash
            |? []
            |> Mapping.accountHoldingDtosToGetAccoungHoldingsResponseDto accountHash accountState
            |> Ok
