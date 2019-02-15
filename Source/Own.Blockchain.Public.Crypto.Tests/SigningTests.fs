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
                "B6WNNx9oK8qRUU52PpzjXHZuv4NUb3Z33hdju3hhrceS", "CHc3pxkiUEZAFu1N54bxJFJWQc3vAqdWDtG"
                "BYeryGRWErwcHD6MDPYeUpYBH5Z2viXSDS827hPMmVvU", "CHZJY8KFVsRN3Km6MWQKtc9wb534qVDpKaR"
                "3uwbboWnx2BGcWGBASXQ6AAzooi4xKA6fV3psPgvt8Ja", "CHamNi4qk1QykonzgH4nHvvG9gZUAhX1RRg"
                "3UtEGN2Wbmm5jVE3W5iFgeCw9NJ5AueFTqfWcPbdFGMk", "CHLVMMaGe1r76jJNQTj1AzgN6GQWCq4ewEc"
                "7hYZ9bHuhbJZcGhPzxeRdFYVr24DFMExduLgcqF1U4k8", "CHfhy5m8Giz4QDR1UgiPKdqMf5DNaJXmbvx"
                "Hg4GsWvBDKxdZ76dYmjm6L39JoukD23acQS2KA7eoGLy", "CHJL383gLhi3GXXPjdMV5rtHPzPYT9q5wHF"
                "2bnW9tKokbneHvUzZ6SaUkwM8XwudDxdyWr6FnLtCHnT", "CHUYZSZSMH6pmJ4Sg3mh83mbsTyWK38io44"
                "BENFVdPfpb8e1jRKZkf7Wmo4Re71qg1Xzfu5cH73JFWG", "CHaKtHwMb6a9bxum1h4NWiidU9sRW59Gbbf"
                "BFRzCfhZFBq2mBtSSXz27i3SdNsPh4FtHe9QLeZchySg", "CHX677op16Bro38SqpPs51WYJgPERKdwQa8"
                "ZXXkM41yHhkzb2k5KjeWuGCzYj7AXAfJdMXqKM4TGKq", "CHGeQC23WjThKoDoSbKRuUKvq1EGkBaA5Gg"
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
                "B6WNNx9oK8qRUU52PpzjXHZuv4NUb3Z33hdju3hhrceS",
                    "6Hhxz2eP3AagR56mP4AAaKViUxHi3gM9c5weLDR48x4X4ynRBDfxsHGjhX9cni1mtCkNxbnZ783YPgMwVYV52X1w5"
                "BYeryGRWErwcHD6MDPYeUpYBH5Z2viXSDS827hPMmVvU",
                    "5d6ZJQPuQNWJNGcYhm2tNonWkqrMKinb9z39Lnhr7LYBJHMYQmkw5fejC32HMwV4FoLAxnkiYvoNyPog3fAYVo6Mu"
                "3uwbboWnx2BGcWGBASXQ6AAzooi4xKA6fV3psPgvt8Ja",
                    "HSfYVfXxk9mXK9Z5kDc3VW2yjFfzectdynznQb6ewNEEScpr5hhMnqje4CB6LAATU8nTpaS99ZUzXKRS7yFGEcicf"
                "3UtEGN2Wbmm5jVE3W5iFgeCw9NJ5AueFTqfWcPbdFGMk",
                    "6Rkmo83paLfRAmmCWH7VawZKzYzddSmrZCUGcD7nPhis8AfkDku9jwMQLv2vFUu8TdmXLHvYTPgdpWXk2giWnDmnG"
                "7hYZ9bHuhbJZcGhPzxeRdFYVr24DFMExduLgcqF1U4k8",
                    "PQhZNUmgA1BWPYLMUHKXstm2qLrA4PgTMb5S8oP2anQBB6Y53Gq7ZDMdtpRFU3iydotTsTXuQbSszzRbQ4SQNVaaL"
                "Hg4GsWvBDKxdZ76dYmjm6L39JoukD23acQS2KA7eoGLy",
                    "DrWbKDQB5sqBLXreyFifvFs1tteXGj6Gf6k3Wjh5pKXybVapmS3sZ1gN7eGrRPjQYRTNDBHWtv2j5yWtwcDkQTotU"
                "2bnW9tKokbneHvUzZ6SaUkwM8XwudDxdyWr6FnLtCHnT",
                    "858f2hxuMETR9Dtj3GzYGrF2C4BttBPMNwSdH5uHyCNUiWhGNkEYQNqRuG2utxLRb9To4ViLyFrrbaR4Q8ARgdri4"
                "BENFVdPfpb8e1jRKZkf7Wmo4Re71qg1Xzfu5cH73JFWG",
                    "CYFwokRvVJzR5sry3kzPEufeL9H4sf8undTEr1N7wWdij4pzHtTxtMnFWFGsoz7CG4FJdw3AFyvp71KfnW7ussr8P"
                "BFRzCfhZFBq2mBtSSXz27i3SdNsPh4FtHe9QLeZchySg",
                    "NtDNBubtvhmQf8UqFGfX2kFy4naNaH38aNDuNWbYDCUtAc6qxkQ4t39wx3zF599zJ4jdvNVncEPW55AYSoeJXekLX"
                "ZXXkM41yHhkzb2k5KjeWuGCzYj7AXAfJdMXqKM4TGKq",
                    "L6Wydc2igH5Ck6BJEeZNYavimG7CjWQFW7EVtbJt9QYdMNFXE6Gcqy4WF6YJtnx7eESvsG8HqMop8LZorskyyMq4s"
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
        let expectedAddress = Some (BlockchainAddress "CHPvS1Hxs4oLcrbgKWYYmubSBjurjUdvjg8")

        let generateRandomMessageAndTest messageSize =
            let messageHash = Signing.generateRandomBytes messageSize |> Hashing.hash

            let signature = Signing.signHash Helpers.networkCode privateKey messageHash
            let address = Signing.verifySignature Helpers.networkCode signature messageHash

            test <@ address = expectedAddress @>

        [1 .. 100]
        |> List.map generateRandomMessageAndTest
