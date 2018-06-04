namespace Chainium.Blockchain.Public.Crypto.Tests

open System
open Xunit
open Swensen.Unquote
open Chainium.Common
open Chainium.Blockchain.Public.Crypto
open Chainium.Blockchain.Public.Core.DomainTypes

module SigningTests =
    open System.Text

    [<Fact>]
    let ``Signing.generateWallet`` () =
        let numOfReps = 100

        let walletInfoPairs =
            [1 .. numOfReps]
            |> List.map (fun _ -> Signing.generateWallet ())
            |> List.distinct

        test <@ walletInfoPairs.Length = numOfReps @>

    [<Fact>]
    let ``Signing.generateWallet produces address starting with "CH"`` () =
        for _ in [1 .. 100] do
            let (ChainiumAddress address) = (Signing.generateWallet ()).Address
            test <@ address.StartsWith("CH") @>

    [<Fact>]
    let ``Signing.signMessage same message for multiple users`` () =
        let numOfReps = 100
        let messageToSign = Encoding.UTF8.GetBytes "Chainium"

        let generateSignature () =
            let wallet = Signing.generateWallet ()
            Signing.signMessage wallet.PrivateKey messageToSign

        let distinctMessages =
            [1 .. numOfReps]
            |> List.map (fun _ -> generateSignature ())
            |> List.distinct

        test <@ distinctMessages.Length = numOfReps @>

    [<Fact>]
    let ``Signing.verifyMessage sign, verify message and check if resulting adress is same`` () =
        let messageToSign = Encoding.UTF8.GetBytes "Chainium"
        let wallet = Signing.generateWallet ()

        let signature = Signing.signMessage wallet.PrivateKey messageToSign
        let address = Signing.verifySignature signature messageToSign

        test <@ address = Some wallet.Address @>

    [<Fact>]
    let ``Signing.verifyMessage sign, verify mutiple messages and check if resulting adress is same`` () =
        let messagePrefix = "Chainium"

        let wallet = Signing.generateWallet ()

        for i in [1 .. 100] do
            let message = sprintf "%s %i" messagePrefix i |> Encoding.UTF8.GetBytes
            let signature = Signing.signMessage wallet.PrivateKey message
            let address = Signing.verifySignature signature message

            test <@ address = Some wallet.Address @>

    [<Fact>]
    let ``Signing.verifyMessage sign, verify random generated longer messages`` () =
        let privateKey = PrivateKey "9DeKWSbveJnzgawry3SG6uby3xE1s26UR4X5uXwdG8WT"
        let expectedAddress = Some (ChainiumAddress "CHPvS1Hxs4oLcrbgKWYYmubSBjurjUHmRMG")

        let generateRandomMessageAndTest messageSize =
            let message = Signing.generateRandomBytes messageSize

            let signature = Signing.signMessage privateKey message
            let address = Signing.verifySignature signature message

            test <@ address = expectedAddress @>

        [1 .. 100]
        |> List.map generateRandomMessageAndTest
