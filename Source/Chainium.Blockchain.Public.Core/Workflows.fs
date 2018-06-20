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
        txEnvelopeDto
        : Result<TxSubmittedEvent, AppErrors>
        =

        result {
            let! txEnvelope = Validation.validateTxEnvelope txEnvelopeDto
            let! senderAddress = Validation.verifyTxSignature verifySignature txEnvelope
            let txHash = txEnvelope.RawTx |> createHash |> TxHash

            let! txDto = Serialization.deserializeTx txEnvelope.RawTx
            let! tx = Validation.validateTx isValidAddress senderAddress txHash txDto

            do! Validation.validateTxFee getChxBalanceState getTotalFeeForPendingTxs senderAddress tx.TotalFee

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
        (getLastAppliedBlockNumber : unit -> BlockNumber option)
        getBlock
        decodeHash
        createHash
        createMerkleTree
        saveTxResult
        saveBlock
        applyNewState
        maxTxCountPerBlock
        validatorAddress
        : Result<BlockCreatedEvent, AppErrors> option
        =

        let getChxBalanceState = memoize (getChxBalanceStateFromStorage >> Option.map Mapping.chxBalanceStateFromDto)
        let getHoldingState = memoize (getHoldingStateFromStorage >> Option.map Mapping.holdingStateFromDto)
        let getAccountController = memoize getAccountControllerFromStorage

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

                let blockDto = Mapping.blockToDto block
                let blockInfoDto = Mapping.blockInfoDtoFromBlockHeaderDto blockDto.Header
                let outputDto = Mapping.outputToDto output

                do! outputDto.TxResults
                    |> Map.toList
                    |> List.fold (fun result (txHash, txResult) ->
                        result
                        >>= fun _ -> saveTxResult (TxHash txHash) txResult
                    ) (Ok ())

                do! saveBlock blockDto
                do! applyNewState blockInfoDto outputDto

                return { BlockNumber = block.Header.Number }
            }
            |> Some

    let initBlockchainState
        (getLastAppliedBlockNumber : unit -> BlockNumber option)
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
            let genesisBlock =
                Blocks.createGenesisBlock
                    decodeHash createHash createMerkleTree zeroHash zeroAddress genesisState
            let genesisBlockDto = Mapping.blockToDto genesisBlock
            let blockInfoDto = Mapping.blockInfoDtoFromBlockHeaderDto genesisBlockDto.Header

            let result =
                result {
                    do! saveBlock genesisBlockDto
                    do! genesisState
                        |> Mapping.outputToDto
                        |> applyNewState blockInfoDto
                }

            match result with
            | Ok _ ->
                Log.info "Genesis block created."
            | Error errors ->
                for AppError e in errors do
                    Log.error e
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
            Ok (Mapping.chxBalanceStateDtoToGetAddressApiResponseDto
                    (chainiumAddress |> (fun (ChainiumAddress a) -> a))
                    addressState
            )
        | None -> Error [AppError "Chainium Address does not exist"]

    let getAccountApi
        getAccountController
        getAccountHoldings
        (accountHash : AccountHash)
        (assetCode : string option)
        : Result<GetAccountApiResponseDto, AppErrors>
        =

        match getAccountController accountHash with
        | None -> Error [AppError (sprintf "Account %A does not exists" accountHash)]
        | Some (ChainiumAddress accountController) ->
            match getAccountHoldings accountHash assetCode with
                | None -> []
                | Some holdings -> holdings
            |> (fun v ->
                Ok (Mapping.accountHoldingsDtoToGetAccoungHoldingsResponseDto
                        (accountHash |> (fun (AccountHash h) -> h))
                        accountController
                        v)
                )

    let getBlockApi
        getBlock
        (blockNumber : BlockNumber)
        : Result<GetBlockApiResponseDto, AppErrors>
        =

        match getBlock blockNumber with
            | Ok block -> Ok (Mapping.blockTxsToGetBlockApiResponseDto block)
            | _ -> Error [AppError (sprintf "Block %A does not exists" (blockNumber |> fun (BlockNumber b) -> b))]

    let getTxApi
        getTxInfo
        getTx
        getTxResult
        (txHash : TxHash)
        : Result<GetTxApiResponseDto, AppErrors>
        =

        match getTxInfo txHash with
        | None -> Error [AppError (sprintf "Tx %A does not exists" (txHash |> fun (TxHash t) -> t))]
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
