namespace Chainium.Blockchain.Public.Core.Tests

open System
open Xunit
open Swensen.Unquote
open Chainium.Common
open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Core
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Crypto

module ProcessingTests =

    [<Fact>]
    let ``Processing.excludeUnprocessableTxs`` () =
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
                {
                    PendingTxInfo.TxHash = TxHash "Tx2"
                    Sender = w1.Address
                    Nonce = Nonce 12L
                    Fee = ChxAmount 1M
                    AppearanceOrder = 2L
                }
                {
                    PendingTxInfo.TxHash = TxHash "Tx3"
                    Sender = w1.Address
                    Nonce = Nonce 10L
                    Fee = ChxAmount 1M
                    AppearanceOrder = 3L
                }
                {
                    PendingTxInfo.TxHash = TxHash "Tx4"
                    Sender = w1.Address
                    Nonce = Nonce 14L
                    Fee = ChxAmount 1M
                    AppearanceOrder = 4L
                }
                {
                    PendingTxInfo.TxHash = TxHash "Tx5"
                    Sender = w1.Address
                    Nonce = Nonce 11L
                    Fee = ChxAmount 1M
                    AppearanceOrder = 5L
                }
                {
                    PendingTxInfo.TxHash = TxHash "Tx1"
                    Sender = w2.Address
                    Nonce = Nonce 21L
                    Fee = ChxAmount 1M
                    AppearanceOrder = 1L
                }
            ]

        // ACT
        let txHashes =
            txSet
            |> Processing.excludeUnprocessableTxs getChxBalanceState
            |> List.map (fun tx -> tx.TxHash |> fun (TxHash hash) -> hash)

        test <@ txHashes = ["Tx1"; "Tx2"; "Tx3"; "Tx5"] @>
