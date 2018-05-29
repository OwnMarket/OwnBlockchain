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
    let ``Signing.generateRandomSeed generates an array of 64 bytes`` () =
        let seed = Signing.generateRandomSeed ()

        test <@ seed.Length = 64 @>

    [<Fact>]
    let ``Signing.generateRandomSeed always returns a different value`` () =
        let allSeeds =
            [1 .. 10000]
            |> List.map (fun _ -> Signing.generateRandomSeed ())

        let distinctSeeds =
            allSeeds
            |> List.distinct

        test <@ distinctSeeds.Length = allSeeds.Length @>

    [<Fact>]
    let ``Signing.generateWallet using seed`` () =
        let seed =
            Signing.generateRandomSeed ()

        let numOfReps = 1000

        let distinctPairs =
            [1 .. numOfReps]
            |> List.map (fun _ -> Some seed |> Signing.generateWallet)
            |> List.distinct

        test <@ distinctPairs.Length = numOfReps @>

    [<Fact>]
    let ``Signing.generateWallet without using seed`` () =
        let numOfReps = 1000

        let walletInfoPairs =
            [1 .. numOfReps]
            |> List.map (fun _ -> Signing.generateWallet None)
            |> List.distinct

        test <@ walletInfoPairs.Length = numOfReps @>

    [<Fact>]
    let ``Signing.generateWallet produces address starting with "CH"`` () =
        for _ in [1 .. 100] do
            let (ChainiumAddress address) = (Signing.generateWallet None).Address
            test <@ address.StartsWith("CH") @>

    [<Fact>]
    let ``Signing.signMessage same message for multiple users`` () =
        let numOfReps = 100
        let messageToSign = Encoding.UTF8.GetBytes "Chainium"

        let generateSignature () =
            let wallet = Signing.generateWallet None
            Signing.signMessage wallet.PrivateKey messageToSign

        let distinctMessages =
            [1 .. numOfReps]
            |> List.map (fun _ -> generateSignature ())
            |> List.distinct

        test <@ distinctMessages.Length = numOfReps @>

    [<Fact>]
    let ``Signing.verifyMessage sign, verify message and check if resulting adress is same`` () =
        let messageToSign = Encoding.UTF8.GetBytes "Chainium"
        let wallet = Signing.generateWallet None

        let signature = Signing.signMessage wallet.PrivateKey messageToSign
        let address = Signing.verifySignature signature messageToSign

        test <@ address = Some wallet.Address @>


    [<Fact>]
    let ``Signing.verifyMessage sign, verify mutiple messages and check if resulting adress is same`` () =
        let messagePrefix = "Chainium"

        let wallet = Signing.generateWallet None

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

        [33 .. 230]
        |> List.map generateRandomMessageAndTest
