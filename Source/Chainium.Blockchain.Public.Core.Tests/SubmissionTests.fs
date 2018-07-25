namespace Chainium.Blockchain.Public.Core.Tests

open System
open Xunit
open Swensen.Unquote
open Chainium.Common
open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Core
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Core.Dtos
open Chainium.Blockchain.Public.Core.Events
open Chainium.Blockchain.Public.Crypto

module SubmissionTests =

    [<Theory>]
    [<InlineData (1, "CHX balance is insufficient to cover the fee.")>]
    [<InlineData (10, "CHX balance is insufficient to cover the fee for all pending transactions.")>]
    let ``Workflows.submitTx fails on insufficient CHX balance to cover Tx fee`` (balance : int, error : string) =
        // ARRANGE
        let senderWallet = Signing.generateWallet ()
        let recipientWallet = Signing.generateWallet ()
        let nonce = Nonce 5L
        let txFee = ChxAmount 1M
        let totalTxFee = txFee * 2M // Two txs
        let totalPendingTxsFee = ChxAmount 9M
        let senderBalance = balance |> decimal |> ChxAmount

        let txHash, txEnvelopeDto =
            [
                {
                    ActionType = "TransferChx"
                    ActionData =
                        {
                            RecipientAddress = recipientWallet.Address |> fun (ChainiumAddress a) -> a
                            Amount = 10M
                        }
                } :> obj
                {
                    ActionType = "TransferChx"
                    ActionData =
                        {
                            RecipientAddress = recipientWallet.Address |> fun (ChainiumAddress a) -> a
                            Amount = 10M
                        }
                } :> obj
            ]
            |> Helpers.newTx senderWallet nonce txFee

        let expectedResult : Result<TxReceivedEventData, AppErrors> = Error [AppError error]

        // COMPOSE
        let getChxBalanceState =
            let data =
                [
                    senderWallet.Address, { ChxBalanceState.Amount = senderBalance; Nonce = Nonce 1L }
                ]
                |> Map.ofSeq

            fun (address : ChainiumAddress) ->
                data
                |> Map.tryFind address
                |> Option.map Mapping.chxBalanceStateToDto

        let getTotalFeeForPendingTxs _ =
            totalPendingTxsFee

        let saveTx _ =
            failwith "saveTx shouldn't be called"

        let saveTxToDb _ =
            failwith "saveTxToDb shouldn't be called"

        // ACT
        let result =
            Workflows.submitTx
                Signing.verifySignature
                Hashing.isValidChainiumAddress
                Hashing.hash
                getChxBalanceState
                getTotalFeeForPendingTxs
                saveTx
                saveTxToDb
                Helpers.minTxActionFee
                txEnvelopeDto

        // ASSERT
        test <@ result = expectedResult @>
