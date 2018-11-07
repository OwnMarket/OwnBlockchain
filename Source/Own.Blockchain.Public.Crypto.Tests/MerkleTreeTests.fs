namespace Chainium.Blockchain.Public.Crypto.Tests

open System.Security.Cryptography
open Xunit
open Swensen.Unquote
open Chainium.Blockchain.Public.Crypto
open Chainium.Blockchain.Public.Crypto.MerkleTree

module MerkleTreeTests =

    let hashFunc h = h

    [<Fact>]
    let ``MerkleTree.build - check if correct number of nodes has been added`` () =
        let leafs =
            [
                for i in 1 .. 8 -> [| byte(i) |]
            ]

        let root = MerkleTree.build hashFunc leafs

        test <@ leafs.Length = root.Length @>

    [<Fact>]
    let ``MerkleTree.build - check empty list creation`` () =
        let root = MerkleTree.build hashFunc []
        test <@ root.Length = 0 @>

    [<Fact>]
    let ``MerkleTree.calculateProof - get proof`` () =
        let leafs =
            [
                for i in 1 .. 4 do yield [| byte(i) |]
            ]
        let record = [| byte(2) |]
        let expected =
            [
                LeftHash [| byte(1) |]
                RightHash [| byte(3); byte(4) |]
            ]

        let actual = MerkleTree.calculateProof hashFunc leafs record

        test <@ expected = actual @>

    [<Fact>]
    let ``MerkleTree.calculateProof - uneven nodes get proof`` () =
        let leafs =
            [
                for i in 1 .. 5 do yield [| byte(i) |]
            ]
        let record = [| byte(1) |]
        let expected =
            [
                RightHash [| byte(2) |]
                RightHash [| byte(3); byte(4) |]
                RightHash [| byte(5); byte(5); byte(5); byte(5) |]
            ]

        let actual = MerkleTree.calculateProof hashFunc leafs record

        test <@ expected = actual @>

    [<Fact>]
    let ``MerkleTree.calculateProof - non existing leaf hash`` () =
        let leafs =
            [
                for i in 1 .. 4 do yield [| byte(i) |]
            ]
        let record = [| byte(0) |]

        let expected = List.Empty
        let actual = MerkleTree.calculateProof hashFunc leafs record

        test <@ expected = actual @>

    [<Fact>]
    let ``MerkleTree.verifyProof - leaf hash`` () =
        let root = [| for i in 1 .. 4 do yield byte(i) |]
        let record = [| byte(2) |]
        let proof =
            [
                LeftHash [| byte(1) |]
                RightHash [| byte(3); byte(4) |]
            ]

        let isVerified = MerkleTree.verifyProof hashFunc root record proof

        test <@ isVerified @>

    [<Fact>]
    let ``MerkleTree.verifyProof - invalid leaf hash`` () =
        let root = [| for i in 1 .. 4 do yield byte(i) |]
        let record = [| byte(5) |]
        let proof =
            [
                LeftHash [| byte(1) |]
                RightHash [| byte(3); byte(4) |]
            ]

        let isVerified = MerkleTree.verifyProof hashFunc root record proof

        test <@ isVerified = false @>

    [<Fact>]
    let ``MerkleTree - test basic functionalities using real hashing function`` () =
        let shaHash (data : byte[]) =
            let sha256 = SHA256.Create()
            sha256.ComputeHash(data)

        let concatAndHash left right =
            Array.append left right
            |> shaHash

        let leafs =
            [
                [|
                    75uy; 245uy; 18uy; 47uy; 52uy; 69uy; 84uy; 197uy;
                    59uy; 222uy; 46uy; 187uy; 140uy; 210uy; 183uy; 227uy;
                    209uy; 96uy; 10uy; 214uy; 49uy; 195uy; 133uy; 165uy;
                    215uy; 204uy; 226uy; 60uy; 119uy; 133uy; 69uy; 154uy
                |]
                [|
                    219uy; 193uy; 180uy; 201uy; 0uy; 255uy; 228uy; 141uy;
                    87uy; 91uy; 93uy; 165uy; 198uy; 56uy; 4uy; 1uy;
                    37uy; 246uy; 93uy; 176uy; 254uy; 62uy; 36uy; 73uy;
                    75uy; 118uy; 234uy; 152uy; 100uy; 87uy; 217uy; 134uy
                |]
                [|
                    8uy; 79uy; 237uy; 8uy; 185uy; 120uy; 175uy; 77uy;
                    125uy; 25uy; 106uy; 116uy; 70uy; 168uy; 107uy; 88uy;
                    0uy; 158uy; 99uy; 107uy; 97uy; 29uy; 177uy; 98uy;
                    17uy; 182uy; 90uy; 154uy; 173uy; 255uy; 41uy; 197uy
                |]
                [|
                    229uy; 45uy; 156uy; 80uy; 140uy; 80uy; 35uy; 71uy;
                    52uy; 77uy; 140uy; 7uy; 173uy; 145uy; 203uy; 214uy;
                    6uy; 138uy; 252uy; 117uy; 255uy; 98uy; 146uy; 240uy;
                    98uy; 160uy; 156uy; 163uy; 129uy; 200uy; 158uy; 113uy
                |]
            ]

        let expectedRoot =
            concatAndHash
                (
                    concatAndHash leafs.[0] leafs.[1]
                )
                (
                    concatAndHash leafs.[2] leafs.[3]
                )

        let actualroot = MerkleTree.build shaHash leafs
        test <@ actualroot = expectedRoot @>

        let record = leafs.[1]
        let expectedProof =
            [
                LeftHash leafs.[0]
                RightHash
                    (
                        concatAndHash
                            leafs.[2]
                            leafs.[3]
                    )
            ]

        let actualProof = MerkleTree.calculateProof shaHash leafs record
        test <@ actualProof = expectedProof @>

        let isVerified = MerkleTree.verifyProof shaHash expectedRoot record expectedProof
        test <@ isVerified @>

        let nonExistingHash = Array.zeroCreate 32
        let proof = MerkleTree.calculateProof shaHash leafs nonExistingHash
        test <@ proof = [] @>
