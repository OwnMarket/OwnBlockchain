namespace Own.Blockchain.Public.Sdk

open System
open Own.Common.FSharp
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Crypto

type Wallet (privateKey : string) =
    member val PrivateKey = privateKey
    member val Address = (privateKey |> PrivateKey |> Signing.addressFromPrivateKey).Value

    new () =
        let wallet = Signing.generateWallet ()
        new Wallet(wallet.PrivateKey.Value)
