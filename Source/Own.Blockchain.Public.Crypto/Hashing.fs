namespace Own.Blockchain.Public.Crypto

open System
open System.Security.Cryptography
open SimpleBase
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
        Base58.Bitcoin.Encode (ReadOnlySpan hash)

    let decode (hash : string) =
        (Base58.Bitcoin.Decode hash).ToArray()

    let encodeHex hash =
        Base16.EncodeLower (ReadOnlySpan hash)

    let decodeHex (hash : string) =
        (Base16.Decode hash).ToArray()

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

    let private addressPrefix = [| 6uy; 90uy |] // "CH"

    let blockchainAddress (publicKey : byte[]) =
        let publicKeyHashWithPrefix =
            publicKey
            |> sha256
            |> sha160
            |> Array.append addressPrefix

        let checksum =
            publicKeyHashWithPrefix
            |> sha256
            |> sha256
            |> Array.take 4

        [publicKeyHashWithPrefix; checksum]
        |> Array.concat
        |> encode
        |> BlockchainAddress

    let isValidBlockchainAddress (BlockchainAddress address) =
        if address.IsNullOrWhiteSpace() || not (address.StartsWith("CH")) then
            false
        else
            let addressBytes = decode address

            if addressBytes.Length <> 26 || addressBytes.[0 .. 1] <> addressPrefix then
                false
            else
                let publicKeyHashWithPrefix =
                    addressBytes
                    |> Array.take 22

                let checksum =
                    addressBytes
                    |> Array.skip 22

                let calculatedChecksum =
                    publicKeyHashWithPrefix
                    |> sha256
                    |> sha256
                    |> Array.take 4

                checksum = calculatedChecksum

    let merkleTree (hashes : string list) =
        hashes
        |> List.map decode
        |> MerkleTree.build hashBytes
        |> encode
        |> MerkleTreeRoot

    let networkId networkCode =
        networkCode
        |> Conversion.stringToBytes
        |> hash
        |> decode
        |> NetworkId
