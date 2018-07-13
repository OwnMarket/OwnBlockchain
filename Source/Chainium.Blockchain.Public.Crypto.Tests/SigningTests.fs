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
    let ``Signing.signMessage`` () =
        let messageBytes = Conversion.stringToBytes "Chainium"

        let expectedSignatures =
            [
                "2C6sgXHMLkwiWeUK4fpyVFa3XG59MBa221pkW7kq2KB8",
                    "1", "HeBRVYhpjgrc1DgyxgBZJj7YPsgc5pYh2gAxQX6imSw2", "A9m7q528reZGoATB1SwjnMjiqYFPTTA8JEDHuBQccd6m"
                "3H2V8pM1h4wJEzCfuBHbNBC4w2FvXszKXx6nMEs3mUcC",
                    "1", "HP95LoxHV9B1bSAahTyNFereLdhP7jFhykBeKRjxE3Xh", "G6AVurMM9CDqFt5zPNQmRCjnLfEp6PWoJ587Xq7Zge4"
                "CpxNZ1YsPCmVrLwJzP7H88gHthSjBSySgVR3iK1c1VBk",
                    "2", "DafxKcuoczzbxo7rw7aVvaKyZDSKdqRJesMntUDVnHuj", "2UtKBjCJfzf9cSq6nxgEUMmaPDAd93j5xpvxj4Bnza2v"
                "GzsiWSoVZtDKwGeLELjpqRW618eBsWmFxJhE2wobkzmP",
                    "2", "GwBjy1FLwSfcjUGZA66JzZjHDdHZPSc4o72Jx9y4pU5J", "4KQcT4oXgukyDSxFaE3ExMQQC4FqKBkWMS7TQ3zbwK8K"
                "Ai6m6px88vHv9L3uVtqSGMRoRDatem7xYXdUyAgg7fon",
                    "1", "4oUzXs8wb1gwbD3VjpSMD1T7TwLBa4d5ZRPWNfTEs9Pd", "AMKcue6nperyfHzVL9a9G4m6m7Eo3upmwcffi3wYqxa3"
                "DdJtweNMxs6vfL3dGUMzZHM3GM7gi6RbGyHHwDcQaxXT",
                    "2", "G1aERxvq4GiXWSbFpfqWUmxw9WbxFyyRcqdJCHPTdqB7", "G6sM4kyW3eA8fQz4QghWSeakeNFuqFhaqKms3z6AQP27"
                "9hYD2Xsky8PUpQStvE8UhPaHmhaqxhJth8VuQT5TDTjA",
                    "1", "J42GqMsdoxSC8bDnmDNP6yTrkDUdTZZRmdApjnUq2zNf", "JqM6oVHiGqnw2ck1n5kof2H7okxMwPzLJRQB1LpUzWF"
                "AAscexBi2v8agKdHwbDgfiKzs9eMbH8JQQB3vzvx5k7X",
                    "2", "CazzRXDKrzqoVsgm46sT7PmaaYUMgngpCNz111h2nCD5", "2TYq8RgJYpyneTLJvTPXpgohPqUfRaTBMrgmZZB76Zbd"
                "9exbLv213SGiHnSppnLYsRVTQqW96BHcMDg9ECZZEBCt",
                    "1", "56hztYT5Jw31AH36xaDAyxi3MKG4xXtjduu2RWo3jaa4", "DSdapmpcR4Y4Rpq7zSmsHgzmu41PpdAeoAXFVpsiwNcV"
                "AvLDKGB7SAqjjs4RhT87GCdBdxyyJHSqcALvWRrQnggd",
                    "2", "A6r5TmuuqP8foTMmfnPWUPoMH3FT55ebYR5N4KxFFuja", "2Usa4YfjD9Rv5L2p8b5ig8LTsp4kbFYd8JWTGMKWGGLV"
            ]
            |> List.map (fun (pk, v, r, s) -> pk, {V = v; R = r; S = s})

        let actualSignatures =
            expectedSignatures
            |> List.map (fun (pk, _) -> pk, Signing.signMessage (PrivateKey pk) messageBytes)

        let matches =
            List.zip actualSignatures expectedSignatures
            |> List.map (fun (a, e) -> a = e)

        test <@ matches = List.replicate matches.Length true @>

        for actual, expected in List.zip actualSignatures expectedSignatures do
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
