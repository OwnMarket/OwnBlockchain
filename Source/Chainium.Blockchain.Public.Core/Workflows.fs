namespace Chainium.Blockchain.Public.Core

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
                |> Mapping.txToTxInfoDto Pending
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
        getLastBlockNumber
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
                let txSet =
                    txSet
                    |> Processing.orderTxSet

                let! previousBlockDto =
                    getLastBlockNumber ()
                    |? BlockNumber 0L // TODO: Once genesis block init is added, this should throw.
                    |> getBlock
                let previousBlock = Mapping.blockFromDto previousBlockDto
                let blockNumber = previousBlock.Header.Number |> fun (BlockNumber n) -> BlockNumber (n + 1L)
                let timestamp = Utils.getUnixTimestamp () |> Timestamp

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

                let saveTxResultAcc result (txHash, txResult) =
                    result
                    >>= fun _ -> saveTxResult (TxHash txHash) txResult

                do! outputDto.TxResults
                    |> Map.toList
                    |> List.fold saveTxResultAcc (Ok())

                do! saveBlock blockDto
                do! applyNewState blockInfoDto outputDto

                return { BlockNumber = block.Header.Number }
            }
            |> Some

    let propagateTx sendMessageToPeers (txHash : TxHash) =
        sprintf "%A" txHash
        |> sendMessageToPeers

    let propagateBlock sendMessageToPeers (blockNumber : BlockNumber) =
        sprintf "%A" blockNumber
        |> sendMessageToPeers
