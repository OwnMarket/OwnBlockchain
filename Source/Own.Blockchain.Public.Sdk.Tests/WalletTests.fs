namespace Own.Blockchain.Public.Sdk.Tests

open System
open Xunit
open Swensen.Unquote
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Crypto
open Own.Blockchain.Public.Sdk

module WalletTests =

    [<Fact>]
    let ``Wallet constructor creates valid address from private key`` () =
        let privateKey = "BYVTKPwrqUKkeawu1RQKhfNBvxYPhTncaqpz5Ai4Dg3j"
        let expectedAddress = "CHXiknwhYcsVFEExx9yPccf1gkTdLrfBSom"
        let wallet = Wallet(privateKey)

        test <@ wallet.PrivateKey = privateKey @>
        test <@ wallet.Address = expectedAddress @>
        test <@ wallet.Address |> BlockchainAddress |> Hashing.isValidBlockchainAddress @>

    [<Fact>]
    let ``Wallet constructor creates expected address in both overloads`` () =
        let wallet1 = Wallet()
        let wallet2 = Wallet(wallet1.PrivateKey)

        test <@ wallet2.PrivateKey = wallet1.PrivateKey @>
        test <@ wallet2.Address = wallet1.Address @>
