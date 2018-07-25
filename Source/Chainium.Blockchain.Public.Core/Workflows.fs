namespace Chainium.Blockchain.Public.Core

open System
open Chainium.Common
open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Core
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Core.Dtos
open Chainium.Blockchain.Public.Core.Events

module Workflows =

    let submitTx
        verifySignature
        isValidAddress
        createHash
        getChxBalanceState
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
                    getChxBalanceState
                    getTotalFeeForPendingTxs
                    senderAddress
                    tx.TotalFee

            do! saveTx txHash txEnvelopeDto
            do! tx
                |> Mapping.txToTxInfoDto
                |> saveTxToDb

            return { TxHash = txHash }
        }

    let createNewBlock
        getPendingTxs
        getTx
        verifySignature
        isValidAddress
        getChxBalanceStateFromStorage
        getHoldingStateFromStorage
        getAccountStateFromStorage
        getAssetStateFromStorage
        getValidatorStateFromStorage
        (getLastAppliedBlockNumber : unit -> BlockNumber option)
        getBlock
        decodeHash
        createHash
        createMerkleTree
        saveTxResult
        saveBlock
        applyNewState
        minTxActionFee
        maxTxCountPerBlock
        validatorAddress
        : Result<BlockCreatedEventData, AppErrors> option
        =

        let getChxBalanceState = memoize (getChxBalanceStateFromStorage >> Option.map Mapping.chxBalanceStateFromDto)
        let getHoldingState = memoize (getHoldingStateFromStorage >> Option.map Mapping.holdingStateFromDto)
        let getAccountState = memoize (getAccountStateFromStorage >> Option.map Mapping.accountStateFromDto)
        let getAssetState = memoize (getAssetStateFromStorage >> Option.map Mapping.assetStateFromDto)
        let getValidatorState = memoize (getValidatorStateFromStorage >> Option.map Mapping.validatorStateFromDto)

        match Processing.getTxSetForNewBlock getPendingTxs getChxBalanceState maxTxCountPerBlock with
        | [] -> None // Nothing to process.
        | txSet ->
            result {
                let! previousBlockDto =
                    match getLastAppliedBlockNumber () with
                    | Some blockNumber -> getBlock blockNumber
                    | None -> failwith "Blockchain state is not initialized."
                let previousBlock = Mapping.blockFromDto previousBlockDto
                let blockNumber = previousBlock.Header.Number |> fun (BlockNumber n) -> BlockNumber (n + 1L)
                let timestamp = Utils.getUnixTimestamp () |> Timestamp

                let txSet =
                    txSet
                    |> Processing.orderTxSet

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
                        minTxActionFee
                        validatorAddress
                        blockNumber

                let block =
                    Blocks.assembleBlock
                        decodeHash
                        createHash
                        createMerkleTree
                        validatorAddress
                        blockNumber
                        timestamp
                        previousBlock.Header.Hash
                        txSet
                        output

                let outputDto = Mapping.outputToDto output
                let blockDto = Mapping.blockToDto block
                let blockInfoDto = Mapping.blockInfoDtoFromBlockHeaderDto blockDto.Header

                do! outputDto.TxResults
                    |> Map.toList
                    |> List.fold (fun result (txHash, txResult) ->
                        result
                        >>= fun _ -> saveTxResult (TxHash txHash) txResult
                    ) (Ok ())

                do! saveBlock blockDto

                do! applyNewState blockInfoDto outputDto

                return { BlockCreatedEventData.BlockNumber = block.Header.Number }
            }
            |> Some

    let processBlock
        getTx
        verifySignature
        isValidAddress
        getChxBalanceStateFromStorage
        getHoldingStateFromStorage
        getAccountStateFromStorage
        getAssetStateFromStorage
        getValidatorStateFromStorage
        decodeHash
        createHash
        createMerkleTree
        saveTxResult
        saveBlock
        applyNewState
        minTxActionFee
        (block : Block)
        : Result<BlockProcessedEventData, AppErrors>
        =

        let getChxBalanceState = memoize (getChxBalanceStateFromStorage >> Option.map Mapping.chxBalanceStateFromDto)
        let getHoldingState = memoize (getHoldingStateFromStorage >> Option.map Mapping.holdingStateFromDto)
        let getAccountState = memoize (getAccountStateFromStorage >> Option.map Mapping.accountStateFromDto)
        let getAssetState = memoize (getAssetStateFromStorage >> Option.map Mapping.assetStateFromDto)
        let getValidatorState = memoize (getValidatorStateFromStorage >> Option.map Mapping.validatorStateFromDto)

        let output =
            block.TxSet
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
                minTxActionFee
                block.Header.Validator
                block.Header.Number

        let resultingBlock =
            Blocks.assembleBlock
                decodeHash
                createHash
                createMerkleTree
                block.Header.Validator
                block.Header.Number
                block.Header.Timestamp
                block.Header.PreviousHash
                block.TxSet
                output

        if resultingBlock = block then
            let outputDto = Mapping.outputToDto output
            let blockDto = Mapping.blockToDto block
            let blockInfoDto = Mapping.blockInfoDtoFromBlockHeaderDto blockDto.Header

            result {
                do! applyNewState blockInfoDto outputDto
                return { BlockProcessedEventData.BlockNumber = block.Header.Number }
            }
        else
            let message =
                block.Header.Number
                |> fun (BlockNumber n) -> n
                |> sprintf "Processing of block %i didn't result in expected blockchain state."

            Log.error message
            Result.appError message

    let advanceToLastKnownBlock
        getTx
        verifySignature
        isValidAddress
        getChxBalanceStateFromStorage
        getHoldingStateFromStorage
        getAccountStateFromStorage
        getAssetStateFromStorage
        getValidatorStateFromStorage
        decodeHash
        createHash
        createMerkleTree
        saveTxResult
        saveBlock
        applyNewState
        (getLastAppliedBlockNumber : unit -> BlockNumber option)
        (blockExists : BlockNumber -> bool)
        (getBlock : BlockNumber -> Result<BlockDto, AppErrors>)
        minTxActionFee
        =

        let rec processNextBlock (previousBlockNumber : BlockNumber, previousBlockHash : BlockHash) =
            let nextBlockNumber = previousBlockNumber + 1L
            if blockExists nextBlockNumber then
                result {
                    let! blockDto = getBlock nextBlockNumber
                    let block = Mapping.blockFromDto blockDto
                    if Blocks.isValidBlock decodeHash createHash createMerkleTree previousBlockHash block then
                        let! event =
                            processBlock
                                getTx
                                verifySignature
                                isValidAddress
                                getChxBalanceStateFromStorage
                                getHoldingStateFromStorage
                                getAccountStateFromStorage
                                getAssetStateFromStorage
                                getValidatorStateFromStorage
                                decodeHash
                                createHash
                                createMerkleTree
                                saveTxResult
                                saveBlock
                                applyNewState
                                minTxActionFee
                                block

                        event
                        |> fun ({BlockNumber = (BlockNumber n)}) -> n
                        |> Log.infof "Block %i applied"

                        return! processNextBlock (block.Header.Number, block.Header.Hash)
                    else
                        let message =
                            nextBlockNumber
                            |> fun (BlockNumber n) -> n
                            |> sprintf "Block %i is not valid."

                        Log.error message
                        return! Result.appError message
                }
            else
                Ok previousBlockNumber

        match getLastAppliedBlockNumber () with
        | None -> failwith "Blockchain state is not initialized."
        | Some blockNumber ->
            getBlock blockNumber
            >>= (fun lastBlock ->
                processNextBlock (BlockNumber lastBlock.Header.Number, BlockHash lastBlock.Header.Hash)
            )

    let initBlockchainState
        (getLastAppliedBlockNumber : unit -> BlockNumber option)
        (getBlock : BlockNumber -> Result<BlockDto, AppErrors>)
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
                |> List.map (fun (ca, na) -> ChainiumAddress ca, {ValidatorState.NetworkAddress = na})
                |> Map.ofList

            let genesisState = Blocks.createGenesisState genesisChxSupply genesisAddress genesisValidators

            let genesisBlockDto =
                Blocks.createGenesisBlock
                    decodeHash createHash createMerkleTree zeroHash zeroAddress genesisState
                |> Mapping.blockToDto

            let genesisBlockExists =
                match getBlock (BlockNumber 0L) with
                | Ok genesisBlockDtoFromDisk ->
                    if genesisBlockDtoFromDisk <> genesisBlockDto then
                        failwith "Stored genesis block is invalid."
                    true
                | _ ->
                    false

            let blockInfoDto = Mapping.blockInfoDtoFromBlockHeaderDto genesisBlockDto.Header

            let result =
                result {
                    if not genesisBlockExists then
                        do! saveBlock genesisBlockDto
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

    let propagateTx sendMessageToPeers networkHost networkPort getTx (txHash : TxHash) =
        match getTx txHash with
        | Ok (txEnvelopeDto : TxEnvelopeDto) ->
            let peerMessage = GossipMessage {
                MessageId = Tx txHash
                SenderId = GossipMemberId (sprintf "%s:%i" networkHost networkPort) // TODO: move it into network code
                Data = txEnvelopeDto
            }

            peerMessage
            |> sendMessageToPeers
        | _ -> Log.errorf "Tx %s does not exist" (txHash |> fun (TxHash hash) -> hash)

    let propagateBlock sendMessageToPeers networkHost networkPort getBlock (blockNumber : BlockNumber) =
        match getBlock blockNumber with
        | Ok (blockDto : BlockDto) ->
            let peerMessage = GossipMessage {
                MessageId = Block blockNumber
                SenderId = GossipMemberId (sprintf "%s:%i" networkHost networkPort) // TODO: move it into network code
                Data = blockDto
            }
            peerMessage
            |> ignore // TODO: sendMessageToPeers
        | _ -> Log.errorf "Block %i does not exist." (blockNumber |> fun (BlockNumber b) -> b)

    let processPeerMessage getTx submitTx peerMessage =
        let processData messageId (data : obj) =
            match messageId with
            | Tx txHash ->
                let txEnvelopeDto = Serialization.deserializeJObject data
                match getTx txHash with
                | Ok _ -> Result.appError (sprintf "%A already exists" txHash)
                | Error _ -> submitTx txEnvelopeDto

            | Block blockNr ->
                // TODO
                failwith "TODO: Implemented process block"

        match peerMessage with
        | GossipDiscoveryMessage _ -> None
        | GossipMessage m -> processData m.MessageId m.Data |> Some
        | MulticastMessage m -> processData m.MessageId m.Data |> Some

    let getAddressApi getChxBalanceState (chainiumAddress : ChainiumAddress)
        : Result<GetAddressApiResponseDto, AppErrors> =
        match getChxBalanceState chainiumAddress with
        | Some addressState ->
            addressState
            |> Mapping.chxBalanceStateDtoToGetAddressApiResponseDto chainiumAddress
            |> Ok
        | None ->
            {
                ChxBalanceStateDto.Amount = 0M
                Nonce = 0L
            }
            |> Mapping.chxBalanceStateDtoToGetAddressApiResponseDto chainiumAddress
            |> Ok

    let getAddressAccountsApi
        (getAddressAccounts : ChainiumAddress -> AccountHash list)
        (address : ChainiumAddress)
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

    let getBlockApi
        getBlock
        (blockNumber : BlockNumber)
        : Result<GetBlockApiResponseDto, AppErrors>
        =

        match getBlock blockNumber with
        | Ok block -> Ok (Mapping.blockTxsToGetBlockApiResponseDto block)
        | _ -> Result.appError (sprintf "Block %i does not exist" (blockNumber |> fun (BlockNumber b) -> b))

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
