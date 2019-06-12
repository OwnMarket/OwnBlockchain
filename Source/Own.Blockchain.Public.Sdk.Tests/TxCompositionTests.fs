namespace Own.Blockchain.Public.Sdk.Tests

open System
open Xunit
open Swensen.Unquote
open Own.Blockchain.Common
open Own.Blockchain.Public.Crypto
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Sdk

module TxCompositionTests =

    [<Fact>]
    let ``TX composition produces correct JSON structure`` () =
        let tx = new Tx("xxx", 1L)
        tx.SenderAddress <- "yyy"
        tx.Nonce <- 2L
        tx.ActionFee <- 0.02m
        tx.ExpirationTime <- 12345L
        tx.AddTransferChxAction("zzz", 1m)

        let expected =
            sprintf
                """{"senderAddress":"yyy","nonce":2,"actionFee":0.02,"expirationTime":12345,"actions":[%s]}"""
                """{"actionType":"TransferChx","actionData":{"recipientAddress":"zzz","amount":1.0}}"""

        let actual = tx.ToJson()
        printfn "%s" actual

        test <@ actual = expected @>

    [<Fact>]
    let ``TX signing produces correct signature`` () =
        let wallet = new Wallet()
        let expectedAddress = wallet.Address |> BlockchainAddress

        let tx = new Tx(wallet.Address, 1L)
        tx.AddTransferChxAction("zzz", 1m)

        let json = tx.ToJson()
        let txHash = json |> Conversion.stringToBytes |> Hashing.hash

        let signature = tx.Sign(Helpers.networkCode, wallet.PrivateKey).Signature
        let signerAddress = Helpers.verifySignature (Signature signature) txHash

        test <@ signerAddress = Some expectedAddress @>
