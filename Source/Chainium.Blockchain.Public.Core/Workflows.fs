namespace Chainium.Blockchain.Public.Core

open System
open Chainium.Common
open Chainium.Blockchain.Public.Core
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Core.Events

module Workflows =

    let submitTx verifySignature createHash saveTx txEnvelopeDto : Result<TxSubmittedEvent, AppErrors> =
        result {
            let! txEnvelope = Validation.validateTxEnvelope txEnvelopeDto
            let! senderAddress = Validation.verifyTxSignature verifySignature txEnvelope
            let txHash = txEnvelope.RawTx |> createHash |> TxHash

            let! txDto = Serialization.deserializeTx txEnvelope.RawTx
            let! _ = Validation.validateTx senderAddress txHash txDto

            do! saveTx txHash txEnvelopeDto

            return { TxHash = txHash }
        }

    let createNewBlock
        getPendingTxs
        getTx
        verifySignature
        getChxBalanceStateFromStorage
        getHoldingStateFromStorage
        getAccountControllerFromStorage
        getLastBlockNumber
        getBlock
        createHash
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
                let output =
                    txSet
                    |> Processing.orderTxSet
                    |> Processing.processTxSet
                        getTx
                        verifySignature
                        getChxBalanceState
                        getHoldingState
                        getAccountController
                        validatorAddress

                let! previousBlockDto = getLastBlockNumber () |> getBlock
                let previousBlock = Mapping.blockFromDto previousBlockDto
                let blockNumber = previousBlock.Header.Number |> fun (BlockNumber n) -> BlockNumber (n + 1L)
                let block = Blocks.assembleBlock createHash blockNumber previousBlock.Header.Hash txSet output

                do! block |> Mapping.blockToDto |> saveBlock
                do! applyNewState block.Header.Number output

                return { BlockNumber = block.Header.Number }
            }
            |> Some

    let propagateTx sendMessageToPeers (txHash : TxHash) =
        sprintf "%A" txHash
        |> sendMessageToPeers

    let propagateBlock sendMessageToPeers (blockNumber : BlockNumber) =
        sprintf "%A" blockNumber
        |> sendMessageToPeers
