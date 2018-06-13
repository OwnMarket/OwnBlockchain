namespace Chainium.Blockchain.Public.Crypto.Tests

open System
open System.Text
open Xunit
open Swensen.Unquote
open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Crypto

module HashingTests =

    [<Fact>]
    let ``Hashing.zeroHash`` () =
        let randomHash = Signing.generateRandomBytes 64 |> Hashing.hash
        let randomHashBytes = randomHash |> Hashing.decode

        let zeroHash = Hashing.zeroHash
        let zeroHashBytes = zeroHash |> Hashing.decode

        test <@ zeroHashBytes.Length = randomHashBytes.Length @>
        test <@ zeroHash = String.replicate zeroHash.Length "1" @> // 0 => 1 in Base58

    [<Fact>]
    let ``Hashing.zeroAddress`` () =
        let randomAddress = Signing.generateWallet().Address |> fun (ChainiumAddress a) -> a
        let randomAddressBytes = randomAddress |> Hashing.decode

        let zeroAddress = Hashing.zeroAddress |> (fun (ChainiumAddress a) -> a)
        let zeroAddressBytes = zeroAddress |> Hashing.decode

        test <@ zeroAddressBytes.Length = randomAddressBytes.Length @>
        test <@ zeroAddress = String.replicate zeroAddress.Length "1" @> // 0 => 1 in Base58

    [<Fact>]
    let ``Hashing.hash calculates same hash when executed multiple times for same input`` () =
        let message = Conversion.stringToBytes "Chainium"

        let hashes =
            [1 .. 1000]
            |> List.map (fun _ -> Hashing.hash message)
            |> List.distinct

        test <@ hashes.Length = 1 @>

    [<Fact>]
    let ``Hashing.hash calculates different hash for different input`` () =
        let message = "Chainium"
        let hashCount = 1000

        let allHashes =
            [1 .. hashCount]
            |> List.map (fun i -> (sprintf "%s %i" message i) |> Conversion.stringToBytes |> Hashing.hash)

        let distinctHashes =
            allHashes
            |> List.distinct

        test <@ allHashes.Length = hashCount @>
        test <@ distinctHashes.Length = allHashes.Length @>

    [<Fact>]
    let ``Hashing.createChainiumAddress calculates same hash not longer than 26 bytes for same input`` () =
        let message = Conversion.stringToBytes "Chainium"
        let hashCount = 1000
        let hashes =
            [1 .. hashCount]
            |> List.map (fun _ ->
                Hashing.chainiumAddress message
                |> fun (ChainiumAddress a) -> Hashing.decode a
            )
            |> List.distinct
        test <@ hashes.Length = 1 @>

        let longerThan26Bytes =
            hashes
            |> List.where(fun a -> a.Length <> 26)
        test <@ longerThan26Bytes.Length = 0 @>

    [<Fact>]
    let ``Hashing.createChainiumAddress calculates different hash not longer than 26 bytes for different input`` () =
        let hashCount = 1000
        let hashes =
            [1 .. hashCount]
            |> List.map (fun i ->
                sprintf "Chainium %i" i
                |> Conversion.stringToBytes
                |> Hashing.chainiumAddress
                |> fun (ChainiumAddress a) -> Hashing.decode a
            )
            |> List.distinct
        test <@ hashes.Length = hashCount @>

        let longerThan26Bytes =
            hashes
            |> List.where(fun a -> a.Length <> 26)
        test <@ longerThan26Bytes.Length = 0 @>

    [<Theory>]
    [<InlineData ("CHPvS1Hxs4oLcrbgKWYYmubSBjurjUHmRMG", true)>]
    [<InlineData ("XRPvS1Hxs4oLcrbgKWYYmubSBjurjUHmRMG", false)>]
    [<InlineData ("CHPvS1Hxs4oLcgKccYmubSBjurjUHmRMG", false)>]
    [<InlineData ("CHPvS1Hxs4oLcrbgKccYmubSBjurjUHmRMG", false)>]
    let ``Hashing.isValidChainiumAddress validate various ChainiumAddress`` (chainiumAddress, expectedValid) =
        // ARRANGE
        let address = ChainiumAddress chainiumAddress;

        // ACT
        let isValid = Hashing.isValidChainiumAddress address

        // ASSERT
        test <@ isValid = expectedValid @>

    [<Fact>]
    let ``Hashing.isValidChainiumAddress valid address created with createChainiumAddress`` () =
        // ARRANGE
        let isAlwaysValid = Hashing.chainiumAddress >> Hashing.isValidChainiumAddress
        let bytes = Signing.generateRandomBytes 100

        // ACT
        let isValid = isAlwaysValid bytes

        // ASSERT
        test <@ isValid = true @>

    [<Fact>]
    let ``Hashing.merkleTree check if same root has been calculated for multiple runs`` () =
        let transactionMocks =
            [
                for i in 1 .. 100 ->
                    sprintf "%i" i
                    |> Conversion.stringToBytes
                    |> Hashing.encode
            ]

        let roots =
            [
                for _ in 1 .. 100 ->
                    Hashing.merkleTree transactionMocks
                    |> fun (MerkleTreeRoot r) -> r
            ]
            |> List.distinct

        test <@ roots.Length = 1 @>

        let bytes =
            roots.Head
            |> Hashing.decode

        test <@ bytes.Length = 32 @>
