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

    let initBlockchainState
        (getLastAppliedBlockNumber : unit -> BlockNumber option)
        (getBlock : BlockNumber -> Result<BlockEnvelopeDto, AppErrors>)
        saveBlock
        applyNewState
        decodeHash
        createHash
        createMerkleTree
        zeroHash
        zeroAddress
        genesisChxSupply
        genesisAddress
        genesisValidators
        =

        if getLastAppliedBlockNumber () = None then
            let genesisValidators =
                genesisValidators
                |> List.map (fun (ba, na) -> BlockchainAddress ba, {ValidatorState.NetworkAddress = na})
                |> Map.ofList

            let genesisState = Blocks.createGenesisState genesisChxSupply genesisAddress genesisValidators

            let genesisBlock =
                Blocks.createGenesisBlock
                    decodeHash createHash createMerkleTree zeroHash zeroAddress genesisState

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
                    if not genesisBlockExists then
                        let! blockEnvelopeDto =
                            genesisBlock
                            |> Mapping.blockToDto
                            |> Serialization.serialize<BlockDto>
                            >>= fun blockBytes ->
                                {
                                    Block = blockBytes |> Convert.ToBase64String
                                    Signature = "" // TODO: Genesis block should be signed by genesis validators.
                                }
                                |> Ok
                        do! saveBlock genesisBlock.Header.Number blockEnvelopeDto
                    do! genesisState
                        |> Mapping.outputToDto
                        |> applyNewState blockInfoDto
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
        (getTopValidators : unit -> ValidatorSnapshot list)
        (getCurrentValidators : unit -> ValidatorSnapshot list)
        getBlock
        decodeHash
        createHash
        createMerkleTree
        (calculateConfigurationBlockNumberForNewBlock : BlockNumber -> BlockNumber)
        isConfigurationBlock
        (createNewBlockchainConfiguration : unit -> BlockchainConfiguration)
        minTxActionFee
        validatorAddress
        (previousBlockHash : BlockHash)
        blockNumber
        timestamp
        txSet
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

        let blockchainConfiguration =
            if isConfigurationBlock blockNumber then
                createNewBlockchainConfiguration ()
                |> Some
            else
                None

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
        createBlock
        getBlock
        getPendingTxs
        getChxBalanceStateFromStorage
        getAvailableChxBalanceFromStorage
        signBlock
        saveBlock
        applyNewState
        maxTxCountPerBlock
        addressFromPrivateKey
        validatorPrivateKey
        (previousBlockNumber : BlockNumber)
        : Result<BlockCreatedEventData, AppErrors> option
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
        | [] -> None // Nothing to process.
        | txSet ->
            result {
                let txSet =
                    txSet
                    |> Processing.orderTxSet

                let! previousBlock =
                    getBlock previousBlockNumber
                    >>= Blocks.extractBlockFromEnvelopeDto

                let block, output =
                    createBlock
                        validatorAddress
                        previousBlock.Header.Hash
                        (previousBlock.Header.Number + 1L)
                        timestamp
                        txSet

                do! block
                    |> Mapping.blockToDto
                    |> Serialization.serialize<BlockDto>
                    |> Result.map (fun blockBytes ->
                        let signature : Signature = signBlock validatorPrivateKey blockBytes
                        {
                            Block = blockBytes |> Convert.ToBase64String
                            Signature = signature |> fun (Signature s) -> s
                        }
                    )
                    >>= saveBlock block.Header.Number

                // TODO: Move this to consensus' COMMIT stage.
                Synchronization.setLastStoredBlock block
                Synchronization.resetLastKnownBlock ()

                return { BlockCreatedEventData.BlockNumber = block.Header.Number }
            }
            |> Some

    let storeReceivedBlock
        isValidAddress
        getBlock
        verifySignature
        blockExists
        saveBlock
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
                |> Option.map (fun c -> c.Validators)
                |? []

            if validators.IsEmpty then
                return!
                    sprintf "No validators found in configuration block %i to validate block %i."
                        (block.Header.ConfigurationBlockNumber |> fun (BlockNumber n) -> n)
                        (block.Header.Number |> fun (BlockNumber n) -> n)
                    |> Result.appError

            let expectedBlockProposer =
                validators
                |> Consensus.getBlockProposer block.Header.Number
                |> (fun v -> v.ValidatorAddress)

            let! signerAddress = Validation.verifyBlockSignature verifySignature blockEnvelope

            if signerAddress <> expectedBlockProposer then
                do! block.Header.Number
                    |> fun (BlockNumber n) -> n
                    |> fun n ->
                        sprintf "Block %i not signed by proper validator. Expected: %s / Actual: %s"
                            n
                            (expectedBlockProposer |> fun (BlockchainAddress a) -> a)
                            (signerAddress |> fun (BlockchainAddress a) -> a)
                    |> Result.appError

            if block.Header.Validator <> expectedBlockProposer then
                do! block.Header.Number
                    |> fun (BlockNumber n) -> n
                    |> fun n ->
                        sprintf "Block %i not proposed by proper validator. Expected: %s / Actual: %s"
                            n
                            (expectedBlockProposer |> fun (BlockchainAddress a) -> a)
                            (block.Header.Validator |> fun (BlockchainAddress a) -> a)
                    |> Result.appError

            do! saveBlock block.Header.Number blockEnvelopeDto

            Synchronization.setLastStoredBlock block

            return {BlockCreatedEventData.BlockNumber = block.Header.Number}
        }

    let persistTxResults saveTxResult txResults =
        result {
            do! txResults
                |> Map.toList
                |> List.fold (fun result (txHash, txResult) ->
                    result
                    >>= fun _ -> saveTxResult (TxHash txHash) txResult
                ) (Ok ())
        }

    let applyBlock
        isValidSuccessorBlock
        createBlock
        getBlock
        verifySignature
        persistTxResults
        saveBlock
        applyNewState
        blockNumber
        blockEnvelopeDto
        =

        result {
            let! previousBlock =
                getBlock (blockNumber - 1L)
                >>= Blocks.extractBlockFromEnvelopeDto

            let! block =
                getBlock blockNumber
                >>= Blocks.extractBlockFromEnvelopeDto

            if not (isValidSuccessorBlock previousBlock.Header.Hash block) then
                return!
                    blockNumber
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

            if block = createdBlock then
                let blockInfoDto = Mapping.blockHeaderToBlockInfoDto createdBlock.Header
                let outputDto = Mapping.outputToDto output
                do! persistTxResults outputDto.TxResults
                do! applyNewState blockInfoDto outputDto
            else
                Log.debugf "RECEIVED BLOCK:\n%A" block
                Log.debugf "CREATED BLOCK:\n%A" createdBlock
                return!
                    blockNumber
                    |> fun (BlockNumber n) ->
                        sprintf "Applying of block %i didn't result in expected blockchain state." n
                    |> Result.appError
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
            // TODO
            Ok None

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
        verifySignature
        getTxResult
        (txHash : TxHash)
        : Result<GetTxApiResponseDto, AppErrors>
        =

        result {
            let! txEnvelope =
                getTx txHash
                |> Result.map Mapping.txEnvelopeFromDto

            let! txDto = Serialization.deserializeTx txEnvelope.RawTx

            let! senderAddress = Validation.verifyTxSignature verifySignature txEnvelope

            let txResult =
                match getTxResult txHash with
                | Ok result -> Some result
                | _ -> None

            return Mapping.txToGetTxApiResponseDto txHash senderAddress txDto txResult
        }

    let getBlockApi
        getBlock
        (blockNumber : BlockNumber)
        : Result<GetBlockApiResponseDto, AppErrors>
        =

        match getBlock blockNumber >>= Blocks.extractBlockFromEnvelopeDto with
        | Ok block ->
            block
            |> Mapping.blockToDto
            |> Mapping.blockTxsToGetBlockApiResponseDto
            |> Ok
        | _ -> Result.appError (sprintf "Block %i does not exist" (blockNumber |> fun (BlockNumber b) -> b))

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
