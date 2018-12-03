namespace Own.Blockchain.Public.Crypto

open System
open System.Security.Cryptography
open Base58Check
open Own.Common
open Own.Blockchain.Common
open Own.Blockchain.Public.Core.DomainTypes

module Hashing =

    let private sha256 (data : byte[]) =
        let sha256 = SHA256.Create()
        sha256.ComputeHash data

    let private sha512 (data : byte[]) =
        let sha512 = SHA512.Create()
        sha512.ComputeHash data

    let private sha160 =
        sha512 >> Array.take 20

    let internal hashBytes (data : byte[]) =
        sha256 data

    let encode hash =
        Base58CheckEncoding.EncodePlain hash

    let decode hash =
        Base58CheckEncoding.DecodePlain hash

    let zeroHash =
        Array.zeroCreate<byte> 32 |> encode

    let zeroAddress =
        Array.zeroCreate<byte> 26 |> encode |> BlockchainAddress

    let hash (data : byte[]) =
        data
        |> hashBytes
        |> encode

    let deriveHash (BlockchainAddress address) (Nonce nonce) (TxActionNumber actionNumber) =
        [
            decode address
            nonce |> Conversion.int64ToBytes
            actionNumber |> Conversion.int16ToBytes
        ]
        |> Array.concat
        |> hash

    let blockchainAddress (publicKey : byte[]) =
        let prefix = [| 6uy; 90uy |] // "CH"

        let hash =
            publicKey
            |> sha256
            |> sha160

        let checksum =
            hash
            |> sha256
            |> sha256
            |> Array.take 4

        [prefix; hash; checksum]
        |> Array.concat
        |> encode
        |> BlockchainAddress

    let isValidBlockchainAddress (BlockchainAddress address) =
        if address.IsNullOrWhiteSpace() || not (address.StartsWith("CH")) then
            false
        else
            let hash = decode address

            if Array.length hash <> 26 then
                false
            else
                let addressChecksum =
                    hash
                    |> Array.skip 22

                let address20ByteHash =
                    hash
                    |> Array.skip 2
                    |> Array.take 20

                let address20ByteHashChecksum =
                    address20ByteHash
                    |> sha256
                    |> sha256
                    |> Array.take 4

                addressChecksum = address20ByteHashChecksum

    let merkleTree (hashes : string list) =
        hashes
        |> List.map decode
        |> MerkleTree.build hashBytes
        |> encode
        |> MerkleTreeRoot
