namespace Own.Blockchain.Public.Core.Tests

open System
open Xunit
open Swensen.Unquote
open Own.Common
open Own.Blockchain.Common
open Own.Blockchain.Public.Core
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Core.Dtos
open Own.Blockchain.Public.Core.Events
open Own.Blockchain.Public.Crypto

module SubmissionTests =

    [<Theory>]
    [<InlineData (1, "Available CHX balance is insufficient to cover the fee.")>]
    [<InlineData (10, "Available CHX balance is insufficient to cover the fee for all pending transactions.")>]
    let ``Workflows.submitTx fails on insufficient CHX balance to cover Tx fee`` (balance : decimal, error : string) =
        // ARRANGE
        let senderWallet = Signing.generateWallet ()
        let recipientWallet = Signing.generateWallet ()
        let nonce = Nonce 5L
        let txFee = ChxAmount 1m
        let totalTxFee = txFee * 2m // Two txs
        let totalPendingTxsFee = ChxAmount 9m
        let senderBalance = ChxAmount balance

        let txHash, txEnvelopeDto =
            [
                {
                    ActionType = "TransferChx"
                    ActionData =
                        {
                            RecipientAddress = recipientWallet.Address |> fun (BlockchainAddress a) -> a
                            Amount = 10m
                        }
                } :> obj
                {
                    ActionType = "TransferChx"
                    ActionData =
                        {
                            RecipientAddress = recipientWallet.Address |> fun (BlockchainAddress a) -> a
                            Amount = 10m
                        }
                } :> obj
            ]
            |> Helpers.newTx senderWallet nonce txFee

        let expectedResult : Result<TxReceivedEventData, AppErrors> = Error [AppError error]

        // COMPOSE
        let getAvailableChxBalance =
            let data =
                [
                    senderWallet.Address, senderBalance
                ]
                |> Map.ofSeq

            fun (address : BlockchainAddress) ->
                data.[address]

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
                Hashing.isValidBlockchainAddress
                Hashing.hash
                getAvailableChxBalance
                getTotalFeeForPendingTxs
                saveTx
                saveTxToDb
                Helpers.minTxActionFee
                txEnvelopeDto

        // ASSERT
        test <@ result = expectedResult @>
