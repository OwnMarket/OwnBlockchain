namespace Chainium.Blockchain.Public.Crypto.Tests

open System
open System.Text
open Xunit
open Swensen.Unquote
open Chainium.Common
open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Crypto
open Chainium.Blockchain.Public.Core.DomainTypes

module SigningTests =

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
    let ``Signing.addressFromPrivateKey`` () =
        for _ in [1 .. 100] do
            let wallet = Signing.generateWallet ()
            let derivedAddress = Signing.addressFromPrivateKey wallet.PrivateKey
            test <@ derivedAddress = wallet.Address @>

    [<Fact>]
    let ``Signing.addressFromPrivateKey keys from JS`` () =
        // These keys are generated using Chainium's crypto JS library, to ensure consistent results.
        let keys =
            [
                "2C6sgXHMLkwiWeUK4fpyVFa3XG59MBa221pkW7kq2KB8", "CHLsVaYSPJGFi8BNGd6tP1VvB8UdKbVRDKD"
                "3H2V8pM1h4wJEzCfuBHbNBC4w2FvXszKXx6nMEs3mUcC", "CHfDeuB1y1eJnWd6aWfYaRvpS9Qgrh1eqe7"
                "CpxNZ1YsPCmVrLwJzP7H88gHthSjBSySgVR3iK1c1VBk", "CHb5Sgdq1MNDVDUG8UPLHBKzUGZZ7ZtAuzy"
                "GzsiWSoVZtDKwGeLELjpqRW618eBsWmFxJhE2wobkzmP", "CHJeYbKnr8icRezrdrEQsLXrxpDbXxri6j4"
                "Ai6m6px88vHv9L3uVtqSGMRoRDatem7xYXdUyAgg7fon", "CHTjSrn385LBC7rzbqRvE9csKPJS8BT725y"
                "DdJtweNMxs6vfL3dGUMzZHM3GM7gi6RbGyHHwDcQaxXT", "CHZzZNYPCGfyC5zZyjpJAV68njzcqK3YYsE"
                "9hYD2Xsky8PUpQStvE8UhPaHmhaqxhJth8VuQT5TDTjA", "CHWcs2fFSaPbDYuMosuUYRazZEahVEV96nc"
                "AAscexBi2v8agKdHwbDgfiKzs9eMbH8JQQB3vzvx5k7X", "CHfRWRaiVVcQvb8CpNmPfBhRX1BLgXURDWg"
                "9exbLv213SGiHnSppnLYsRVTQqW96BHcMDg9ECZZEBCt", "CHM1QepZLdazpGpVVPEmcmMP2mQn1HeMniJ"
                "AvLDKGB7SAqjjs4RhT87GCdBdxyyJHSqcALvWRrQnggd", "CHTEKKKTSCt32C9yEaKHvckgKYNKcKXuryw"
            ]

        let expected =
            keys
            |> List.map (snd >> ChainiumAddress)

        let actual =
            keys
            |> List.map (fst >> PrivateKey >> Signing.addressFromPrivateKey)

        test <@ actual = expected @>

    [<Fact>]
    let ``Signing.signMessage same message for multiple users`` () =
        let numOfReps = 100
        let messageToSign = Conversion.stringToBytes "Chainium"

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
        let messageToSign = Conversion.stringToBytes "Chainium"
        let wallet = Signing.generateWallet ()

        let signature = Signing.signMessage wallet.PrivateKey messageToSign
        let address = Signing.verifySignature signature messageToSign

        test <@ address = Some wallet.Address @>

    [<Fact>]
    let ``Signing.verifyMessage sign, verify mutiple messages and check if resulting adress is same`` () =
        let messagePrefix = "Chainium"

        let wallet = Signing.generateWallet ()

        for i in [1 .. 100] do
            let message = sprintf "%s %i" messagePrefix i |> Conversion.stringToBytes
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
