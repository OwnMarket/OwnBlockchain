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
                    "HzzxUvKq3ZRghq9q51zwVnFE7KKjG2KNTrh3qKRjK3WLPghxB1S6MNSZbp6HWyVeqonH9BPQcDpsqMApKg8SLNvKZ"
                "3H2V8pM1h4wJEzCfuBHbNBC4w2FvXszKXx6nMEs3mUcC",
                    "DXKvHdvyzhUh4hJKEAsYB7sKLmpmjqCUQ4Wjdp4FUgnNBeNnv2ZD5EGUuaSyvE6XGqHWmxetWDmFHx9joDViP18EL"
                "CpxNZ1YsPCmVrLwJzP7H88gHthSjBSySgVR3iK1c1VBk",
                    "9LbveLpmtFDx4bi81wrsyw4XvuKzAi6VDHis4hD6dnYCPLhh4cxcfC9wr52zEGpuvLWvGyUmAJouHZ7r1qyQvkaun"
                "GzsiWSoVZtDKwGeLELjpqRW618eBsWmFxJhE2wobkzmP",
                    "6zpUK6L5EW6LMAHwguUrvkStXA8W2FN6MV9rFXF31rBTM4to3zTsGPnCZLawbHV1sMGCptPUQmVbyVBWTsra2YCfJ"
                "Ai6m6px88vHv9L3uVtqSGMRoRDatem7xYXdUyAgg7fon",
                    "C6vhP9zxGRrVCmncFm3tsKaRC1Big21rY67Lry4HxdJzHXAYYckW6SJLjoqjCMF5BKXxLLLCcKNocfRXSraHwqbew"
                "DdJtweNMxs6vfL3dGUMzZHM3GM7gi6RbGyHHwDcQaxXT",
                    "99vvuzR94rwLJx28pa5PYXHi2gJyKS4Hpsv2FtUuPu5UmM6RNJKUvrFv4hwj13Xu6zRfacsegR7HHTWJHkT1LNytT"
                "9hYD2Xsky8PUpQStvE8UhPaHmhaqxhJth8VuQT5TDTjA",
                    "4x1t4GJcF5GGAYmR3No1pkD9UxTSPjwECyNJoqmDV1P5Z3tTJntuYXq2SUeb7GPrLF2mKbfbAw9SGuaXxML9UV7iF"
                "AAscexBi2v8agKdHwbDgfiKzs9eMbH8JQQB3vzvx5k7X",
                    "P5w79pZ4VicHuALKZWvoAQ9a8zSrMQpnJR5kVx1jsWwHoq5KpCHKVvrYKgKpyY3bdedGe3YPJeBuxDUEkJGEdBHaQ"
                "9exbLv213SGiHnSppnLYsRVTQqW96BHcMDg9ECZZEBCt",
                    "KV16M4ZZV45cCZt2sWcZj28YAiCH8UJtGKMJPd6547XM5Sfdd6huhyFvwtLZBU721DfEcT9i2d6eaijH5tvUkf4Fi"
                "AvLDKGB7SAqjjs4RhT87GCdBdxyyJHSqcALvWRrQnggd",
                    "LoPRxU8s1gwMZVEmc3qtGhzxB2qM4q4qJoTzDLDVFPdstN4sYFwNoxnF9i6YPCZmDhStfUC7GZic1TPXWKtQA981N"
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
