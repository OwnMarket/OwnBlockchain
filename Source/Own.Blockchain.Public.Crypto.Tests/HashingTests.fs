namespace Own.Blockchain.Public.Crypto.Tests

open Xunit
open Swensen.Unquote
open Own.Blockchain.Common
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Crypto

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
        let randomAddress = Signing.generateWallet().Address.Value
        let randomAddressBytes = randomAddress |> Hashing.decode

        let zeroAddress = Hashing.zeroAddress.Value
        let zeroAddressBytes = zeroAddress |> Hashing.decode

        test <@ zeroAddressBytes.Length = randomAddressBytes.Length @>
        test <@ zeroAddress = String.replicate zeroAddress.Length "1" @> // 0 => 1 in Base58

    [<Fact>]
    let ``Hashing.hash calculates same hash when executed multiple times for same input`` () =
        let message = Conversion.stringToBytes "Own"

        let hashes =
            [1 .. 1000]
            |> List.map (fun _ -> Hashing.hash message)
            |> List.distinct

        test <@ hashes.Length = 1 @>

    [<Fact>]
    let ``Hashing.hash calculates different hash for different input`` () =
        let message = "Own"
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
    let ``Hashing.createBlockchainAddress calculates same hash not longer than 26 bytes for same input`` () =
        let message = Conversion.stringToBytes "Own"
        let hashCount = 1000
        let hashes =
            [1 .. hashCount]
            |> List.map (fun _ ->
                Hashing.blockchainAddress message
                |> fun (BlockchainAddress a) -> Hashing.decode a
            )
            |> List.distinct
        test <@ hashes.Length = 1 @>

        let longerThan26Bytes =
            hashes
            |> List.where(fun a -> a.Length <> 26)
        test <@ longerThan26Bytes.Length = 0 @>

    [<Fact>]
    let ``Hashing.createBlockchainAddress calculates different hash not longer than 26 bytes for different input`` () =
        let hashCount = 1000
        let hashes =
            [1 .. hashCount]
            |> List.map (fun i ->
                sprintf "Own %i" i
                |> Conversion.stringToBytes
                |> Hashing.blockchainAddress
                |> fun (BlockchainAddress a) -> Hashing.decode a
            )
            |> List.distinct
        test <@ hashes.Length = hashCount @>

        let longerThan26Bytes =
            hashes
            |> List.where(fun a -> a.Length <> 26)
        test <@ longerThan26Bytes.Length = 0 @>

    [<Theory>]
    [<InlineData ("CHPvS1Hxs4oLcrbgKWYYmubSBjurjUdvjg8", true)>]
    [<InlineData ("XRPvS1Hxs4oLcrbgKWYYmubSBjurjUdvjg8", false)>]
    [<InlineData ("CHPvS1Hxs4oLcgKccYmubSBjurjUdvjg8", false)>]
    [<InlineData ("CHPvS1Hxs4oLcrbgKccYmubSBjurjUdvjg8", false)>]
    let ``Hashing.isValidBlockchainAddress validate various BlockchainAddress`` (blockchainAddress, expectedValid) =
        // ARRANGE
        let address = BlockchainAddress blockchainAddress;

        // ACT
        let isValid = Hashing.isValidBlockchainAddress address

        // ASSERT
        test <@ isValid = expectedValid @>

    [<Fact>]
    let ``Hashing.isValidBlockchainAddress valid address created with createBlockchainAddress`` () =
        // ARRANGE
        let isAlwaysValid = Hashing.blockchainAddress >> Hashing.isValidBlockchainAddress
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
            ]
            |> List.distinct

        test <@ roots.Length = 1 @>

        let bytes =
            roots.Head.Value
            |> Hashing.decode

        test <@ bytes.Length = 32 @>
