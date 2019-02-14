namespace Own.Blockchain.Public.Crypto.Tests

open Xunit
open Swensen.Unquote
open Own.Common
open Own.Blockchain.Common
open Own.Blockchain.Public.Crypto
open Own.Blockchain.Public.Core.DomainTypes

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
            let (BlockchainAddress address) = (Signing.generateWallet ()).Address
            test <@ address.StartsWith("CH") @>

    [<Fact>]
    let ``Signing.addressFromPrivateKey`` () =
        for _ in [1 .. 100] do
            let wallet = Signing.generateWallet ()
            let derivedAddress = Signing.addressFromPrivateKey wallet.PrivateKey
            test <@ derivedAddress = wallet.Address @>

    [<Fact>]
    let ``Signing.addressFromPrivateKey keys from JS`` () =
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
            |> List.map (snd >> BlockchainAddress)

        let actual =
            keys
            |> List.map (fst >> PrivateKey >> Signing.addressFromPrivateKey)

        test <@ actual = expected @>

    [<Fact>]
    let ``Signing.signHash`` () =
        let messageHash = Conversion.stringToBytes "Chainium" |> Hashing.hash

        let expectedSignatures =
            [
                "2C6sgXHMLkwiWeUK4fpyVFa3XG59MBa221pkW7kq2KB8",
                "NpWshZ58Z5PjCYGt5ERd1X7LidvNkAHKKCs5CzVtTwBRrkJigcR9LcVtCtYeFByTqXEco9CT5jzyHho23d5ca2VTN"
                "3H2V8pM1h4wJEzCfuBHbNBC4w2FvXszKXx6nMEs3mUcC",
                "NUo5XKiMNfh1bKQDLQPAuBw9aS6RonNJwMKUKGgcwYTXHcz6XnjKiU6CqFNbkqmbx9NduVeLwe3JfCHL55ikWsKb5"
                "CpxNZ1YsPCmVrLwJzP7H88gHthSjBSySgVR3iK1c1VBk",
                "HViz1vmVcT6RN1PhTZGhEuo2LkK8wibYnSYcoCRiHysP7v1MfmhmjVraFhHvFdGqx41mYfwqKP3vS23csBRMRinB2"
                "GzsiWSoVZtDKwGeLELjpqRW618eBsWmFxJhE2wobkzmP",
                "MtmL3y4RV8QtXc2U9iQ5m6CkGtTdtjg9tWmdgjpHPYUUM6S3QVDNfg4J9tNvRedgorAFHUsSRijYDvWStSBdU3Aw2"
                "Ai6m6px88vHv9L3uVtqSGMRoRDatem7xYXdUyAgg7fon",
                "5z6EjhTdpTf5osbENz6UJU6r9NKThBF4gaBA23SyMnJvt6RbF577dW4nLYsaBHGZ8Xd54bmehEgbHwqapxt11pEaU"
                "DdJtweNMxs6vfL3dGUMzZHM3GM7gi6RbGyHHwDcQaxXT",
                "LgUETDP6czyLyZhaxqfR17bTtTUxEw2HdmARgzbpi6Yy8ePqwnaoZHUuBFdDCbyQBnZR79XiWKPXk2YWmiTakBfkB"
                "9hYD2Xsky8PUpQStvE8UhPaHmhaqxhJth8VuQT5TDTjA",
                "PMn2a8Cxq8KCe6sGS2F6tnV2y7K9d5FgJFL1U6h53u5dG27d8fiAgVpeDuMu7r8Bb6ZwPcVmdNHPrCDkB6uZKHXWP"
                "AAscexBi2v8agKdHwbDgfiKzs9eMbH8JQQB3vzvx5k7X",
                "GC7AbefoXdTAoj5U6fQpzdVpoqddxxbyuJjtd9nhKJYj2AEoFwLpo6L3c8J42bGbVpz8bgshpir11D5tkEuphZDJQ"
                "9exbLv213SGiHnSppnLYsRVTQqW96BHcMDg9ECZZEBCt",
                "6Ng73M2ReLhETgvmNvv8obg3eaEySZJkYdpyNBAMa6tsZ7q6WjPq4wNNQnWJ8Azr2FV7Rmw21cuLxLFJv7zMsmsFE"
                "AvLDKGB7SAqjjs4RhT87GCdBdxyyJHSqcALvWRrQnggd",
                "Cw6aqjNxfa2myJhP2VFhWH7NrjBrqNdHFJfGPUxAzRxnnPUTgX9tCq8JbAnNuxGy16VsVejAQV2CZ9K6e4jMZrJR6"
            ]
            |> List.map (fun (pk, signature) -> pk, (Signature signature))

        let actualSignatures =
            expectedSignatures
            |> List.map (fun (pk, _) -> pk, Signing.signHash Helpers.networkCode (PrivateKey pk) messageHash)

        let matches =
            List.zip actualSignatures expectedSignatures
            |> List.map (fun (a, e) -> a = e)

        test <@ matches = List.replicate matches.Length true @>

        for actual, expected in List.zip actualSignatures expectedSignatures do
            test <@ actual = expected @>

    [<Fact>]
    let ``Signing.signHash Helpers.networkCode same message for multiple users`` () =
        let numOfReps = 100
        let messageHash = Conversion.stringToBytes "Own" |> Hashing.hash

        let generateSignature () =
            let wallet = Signing.generateWallet ()
            Signing.signHash Helpers.networkCode wallet.PrivateKey messageHash

        let distinctMessages =
            [1 .. numOfReps]
            |> List.map (fun _ -> generateSignature ())
            |> List.distinct

        test <@ distinctMessages.Length = numOfReps @>

    [<Fact>]
    let ``Signing.verifyMessage sign, verify message and check if resulting adress is same`` () =
        let messageHash = Conversion.stringToBytes "Own" |> Hashing.hash
        let wallet = Signing.generateWallet ()

        let signature = Signing.signHash Helpers.networkCode wallet.PrivateKey messageHash
        let address = Signing.verifySignature Helpers.networkCode signature messageHash

        test <@ address = Some wallet.Address @>

    [<Fact>]
    let ``Signing.verifyMessage sign, verify mutiple messages and check if resulting adress is same`` () =
        let messagePrefix = "Own"

        let wallet = Signing.generateWallet ()

        for i in [1 .. 100] do
            let messageHash = sprintf "%s %i" messagePrefix i |> Conversion.stringToBytes |> Hashing.hash
            let signature = Signing.signHash Helpers.networkCode wallet.PrivateKey messageHash
            let address = Signing.verifySignature Helpers.networkCode signature messageHash

            test <@ address = Some wallet.Address @>

    [<Fact>]
    let ``Signing.verifyMessage sign, verify random generated longer messages`` () =
        let privateKey = PrivateKey "9DeKWSbveJnzgawry3SG6uby3xE1s26UR4X5uXwdG8WT"
        let expectedAddress = Some (BlockchainAddress "CHPvS1Hxs4oLcrbgKWYYmubSBjurjUHmRMG")

        let generateRandomMessageAndTest messageSize =
            let messageHash = Signing.generateRandomBytes messageSize |> Hashing.hash

            let signature = Signing.signHash Helpers.networkCode privateKey messageHash
            let address = Signing.verifySignature Helpers.networkCode signature messageHash

            test <@ address = expectedAddress @>

        [1 .. 100]
        |> List.map generateRandomMessageAndTest
