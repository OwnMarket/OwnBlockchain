namespace Chainium.Blockchain.Public.Core.Tests

open System
open Xunit
open Swensen.Unquote
open Chainium.Blockchain.Public.Core
open Chainium.Blockchain.Public.Core.DomainTypes

module DomainTypesTests =

    [<Fact>]
    let ``Total tx fee is calculated everywhere in the same way`` () =
        // ARRANGE
        let expectedFee = ChxAmount 4M

        let tx =
            {
                Tx.TxHash = TxHash ""
                Sender = ChainiumAddress ""
                Nonce = Nonce 0L
                Fee = ChxAmount 2M
                Actions =
                    [
                        TransferChx {
                            TransferChxTxAction.RecipientAddress = ChainiumAddress ""
                            Amount = ChxAmount 0M
                        }
                        TransferChx {
                            TransferChxTxAction.RecipientAddress = ChainiumAddress ""
                            Amount = ChxAmount 0M
                        }
                    ]
            }

        let pendingTxInfo =
            {
                PendingTxInfo.TxHash = tx.TxHash
                Sender = tx.Sender
                Nonce = tx.Nonce
                Fee = tx.Fee
                ActionCount = tx.Actions.Length |> Convert.ToInt16
                AppearanceOrder = 0L
            }

        // ASSERT
        test <@ tx.TotalFee = expectedFee @>
        test <@ pendingTxInfo.TotalFee = expectedFee @>
