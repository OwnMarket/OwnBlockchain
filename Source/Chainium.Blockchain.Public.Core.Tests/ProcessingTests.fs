namespace Chainium.Blockchain.Public.Core.Tests

open System
open Xunit
open Swensen.Unquote
open Chainium.Common
open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Core
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Core.Dtos
open Chainium.Blockchain.Public.Crypto

module ProcessingTests =

    [<Fact>]
    let ``Processing.excludeUnprocessableTxs excludes txs after nonce gap`` () =
        let w1 = Signing.generateWallet None
        let w2 = Signing.generateWallet None

        let getChxBalanceState =
            let data =
                [
                    w1.Address, { ChxBalanceState.Amount = ChxAmount 100M; Nonce = Nonce 10L }
                    w2.Address, { ChxBalanceState.Amount = ChxAmount 200M; Nonce = Nonce 20L }
                ]
                |> Map.ofSeq

            fun (address : ChainiumAddress) -> data.[address]

        let txSet =
            [
                Helpers.newPendingTxInfo (TxHash "Tx2") w1.Address (Nonce 12L) (ChxAmount 1M) 2L
                Helpers.newPendingTxInfo (TxHash "Tx3") w1.Address (Nonce 10L) (ChxAmount 1M) 3L
                Helpers.newPendingTxInfo (TxHash "Tx4") w1.Address (Nonce 14L) (ChxAmount 1M) 4L
                Helpers.newPendingTxInfo (TxHash "Tx5") w1.Address (Nonce 11L) (ChxAmount 1M) 5L
                Helpers.newPendingTxInfo (TxHash "Tx1") w2.Address (Nonce 21L) (ChxAmount 1M) 1L
            ]

        // ACT
        let txHashes =
            txSet
            |> Processing.excludeUnprocessableTxs getChxBalanceState
            |> List.map (fun tx -> tx.TxHash |> fun (TxHash hash) -> hash)

        test <@ txHashes = ["Tx1"; "Tx2"; "Tx3"; "Tx5"] @>

    [<Fact>]
    let ``Processing.orderTxSet puts txs in correct order`` () =
        let w1 = Signing.generateWallet None
        let w2 = Signing.generateWallet None

        let getChxBalanceState =
            let data =
                [
                    w1.Address, { ChxBalanceState.Amount = ChxAmount 100M; Nonce = Nonce 10L }
                    w2.Address, { ChxBalanceState.Amount = ChxAmount 200M; Nonce = Nonce 20L }
                ]
                |> Map.ofSeq

            fun (address : ChainiumAddress) -> data.[address]

        let txSet =
            [
                Helpers.newPendingTxInfo (TxHash "Tx1") w2.Address (Nonce 21L) (ChxAmount 1M) 1L
                Helpers.newPendingTxInfo (TxHash "Tx2") w1.Address (Nonce 12L) (ChxAmount 1M) 2L
                Helpers.newPendingTxInfo (TxHash "Tx3") w1.Address (Nonce 10L) (ChxAmount 1M) 3L
                Helpers.newPendingTxInfo (TxHash "Tx6") w2.Address (Nonce 21L) (ChxAmount 2M) 6L
                Helpers.newPendingTxInfo (TxHash "Tx5") w1.Address (Nonce 11L) (ChxAmount 1M) 5L
            ]

        // ACT
        let txHashes =
            txSet
            |> Processing.orderTxSet
            |> List.map (fun (TxHash hash) -> hash)

        test <@ txHashes = ["Tx6"; "Tx1"; "Tx3"; "Tx5"; "Tx2"] @>

    [<Fact>]
    let ``Processing.processTxSet transfers CHX correctly`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet None
        let recipientWallet = Signing.generateWallet None
        let validatorWallet = Signing.generateWallet None

        let initialChxState =
            [
                senderWallet.Address, (ChxAmount 100M, Nonce 10L)
                recipientWallet.Address, (ChxAmount 100M, Nonce 20L)
                validatorWallet.Address, (ChxAmount 100M, Nonce 30L)
            ]

        // TX
        let nonce = Nonce 11L
        let fee = 1M
        let amountToTransfer = 10M

        let txHash, txEnvelope =
            [
                {
                    ActionType = "ChxTransfer"
                    ActionData =
                        {
                            RecipientAddress = recipientWallet.Address |> fun (ChainiumAddress a) -> a
                            Amount = amountToTransfer
                        }
                } :> obj
            ]
            |> Helpers.newTx senderWallet.PrivateKey nonce (ChxAmount fee)

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxBalanceState =
            Helpers.mockGetChxBalanceState initialChxState

        let getHoldingState _ =
            failwith "getHoldingState should not be called"

        // ACT
        let output =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                getChxBalanceState
                getHoldingState
                validatorWallet.Address
                txSet

        // ASSERT
        let senderBalance =
            getChxBalanceState senderWallet.Address
            |> fun { Amount = (ChxAmount balance); Nonce = _ } -> ChxAmount (balance - amountToTransfer - fee)
        let recipientBalance =
            getChxBalanceState recipientWallet.Address
            |> fun { Amount = (ChxAmount balance); Nonce = _ } -> ChxAmount (balance + amountToTransfer)
        let validatorBalance =
            getChxBalanceState validatorWallet.Address
            |> fun { Amount = (ChxAmount balance); Nonce = _ } -> ChxAmount (balance + fee)

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash] = Success @>
        test <@ output.ChxBalances.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxBalances.[senderWallet.Address].Amount = senderBalance @>
        test <@ output.ChxBalances.[recipientWallet.Address].Amount = recipientBalance @>
        test <@ output.ChxBalances.[validatorWallet.Address].Amount = validatorBalance @>
