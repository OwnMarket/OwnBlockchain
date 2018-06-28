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
        : Result<TxSubmittedEvent, AppErrors>
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
        getAccountControllerFromStorage
        getAssetControllerFromStorage
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
        : Result<BlockCreatedEvent, AppErrors> option
        =

        let getChxBalanceState = memoize (getChxBalanceStateFromStorage >> Option.map Mapping.chxBalanceStateFromDto)
        let getHoldingState = memoize (getHoldingStateFromStorage >> Option.map Mapping.holdingStateFromDto)
        let getAccountController = memoize getAccountControllerFromStorage
        let getAssetController = memoize getAssetControllerFromStorage

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
                        getChxBalanceState
                        getHoldingState
                        getAccountController
                        getAssetController
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

                return { BlockCreatedEvent.BlockNumber = block.Header.Number }
            }
            |> Some

    let processBlock
        getTx
        verifySignature
        isValidAddress
        getChxBalanceStateFromStorage
        getHoldingStateFromStorage
        getAccountControllerFromStorage
        getAssetControllerFromStorage
        decodeHash
        createHash
        createMerkleTree
        saveTxResult
        saveBlock
        applyNewState
        minTxActionFee
        (block : Block)
        : Result<BlockProcessedEvent, AppErrors>
        =

        let getChxBalanceState = memoize (getChxBalanceStateFromStorage >> Option.map Mapping.chxBalanceStateFromDto)
        let getHoldingState = memoize (getHoldingStateFromStorage >> Option.map Mapping.holdingStateFromDto)
        let getAccountController = memoize getAccountControllerFromStorage
        let getAssetController = memoize getAssetControllerFromStorage

        let output =
            block.TxSet
            |> Processing.processTxSet
                getTx
                verifySignature
                isValidAddress
                getChxBalanceState
                getHoldingState
                getAccountController
                getAssetController
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
                return { BlockProcessedEvent.BlockNumber = block.Header.Number }
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
        getAccountControllerFromStorage
        getAssetControllerFromStorage
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
                                getAccountControllerFromStorage
                                getAssetControllerFromStorage
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
        =

        if getLastAppliedBlockNumber () = None then
            let genesisState = Blocks.createGenesisState genesisChxSupply genesisAddress

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

    let propagateTx sendMessageToPeers (txHash : TxHash) =
        sprintf "%A" txHash
        |> sendMessageToPeers

    let propagateBlock sendMessageToPeers (blockNumber : BlockNumber) =
        sprintf "%A" blockNumber
        |> sendMessageToPeers

    let getAddressApi getChxBalanceState (chainiumAddress : ChainiumAddress)
        : Result<GetAddressApiResponseDto, AppErrors> =
        match getChxBalanceState chainiumAddress with
        | Some addressState ->
            Mapping.chxBalanceStateDtoToGetAddressApiResponseDto
                (chainiumAddress |> (fun (ChainiumAddress a) -> a))
                addressState
            |> Ok
        | None -> Result.appError "Chainium Address does not exist"

    let getAccountApi
        getAccountController
        getAccountHoldings
        (accountHash : AccountHash)
        (assetHash : string option)
        : Result<GetAccountApiResponseDto, AppErrors>
        =

        match getAccountController accountHash with
        | None -> Result.appError (sprintf "Account %A does not exists" accountHash)
        | Some (ChainiumAddress accountController) ->
            match getAccountHoldings accountHash assetHash with
            | None -> []
            | Some holdings -> holdings
            |> (fun v ->
                Mapping.accountHoldingsDtoToGetAccoungHoldingsResponseDto
                    (accountHash |> (fun (AccountHash h) -> h))
                    accountController
                    v
                |> Ok
            )

    let getBlockApi
        getBlock
        (blockNumber : BlockNumber)
        : Result<GetBlockApiResponseDto, AppErrors>
        =

        match getBlock blockNumber with
            | Ok block -> Ok (Mapping.blockTxsToGetBlockApiResponseDto block)
            | _ -> Result.appError (sprintf "Block %A does not exists" (blockNumber |> fun (BlockNumber b) -> b))

    let getTxApi
        getTxInfo
        getTx
        getTxResult
        (txHash : TxHash)
        : Result<GetTxApiResponseDto, AppErrors>
        =

        match getTxInfo txHash with
        | None -> Result.appError (sprintf "Tx %A does not exists" (txHash |> fun (TxHash t) -> t))
        | Some txInfo ->
            result {
                let! txDto =
                    getTx txHash
                    |> Result.map Mapping.txEnvelopeFromDto
                    >>= fun txEnvelope -> Serialization.deserializeTx txEnvelope.RawTx

                let txResult =
                    match getTxResult txHash with
                    | Ok result -> result
                    | _ ->
                        {
                        Status = int16 0
                        ErrorCode = Nullable()
                        FailedActionNumber = Nullable()
                        BlockNumber = int64 0
                        }

                return (Mapping.txToGetTxApiResponseDto txInfo txDto.Actions txResult)
            }
