namespace Chainium.Blockchain.Public.Crypto

open System
open System.Security.Cryptography
open Multiformats.Base
open Chainium.Common
open Chainium.Blockchain.Public.Core.DomainTypes

module Hashing =
    let private sha256 (data : byte[]) =
        let sha256 = SHA256.Create()
        sha256.ComputeHash(data)

    let private sha512 (data : byte[]) =
        let sha512 = SHA512.Create()
        sha512.ComputeHash(data)

    let private sha160 =
        sha512 >> Array.take 20

    let internal hashBytes (data : byte[]) =
        sha256 data

    let hash (data : byte[]) =
        data
        |> hashBytes
        |> Multibase.Base58.Encode

    let chainiumAddress (publicKey : byte[]) =
        let prefix = "CH"

        let hash =
            publicKey
            |> sha256
            |> sha160

        let checksum =
            hash
            |> sha256
            |> sha256
            |> Array.take 4

        [hash; checksum]
        |> Array.concat
        |> Multibase.Base58.Encode
        |> sprintf "%s%s" prefix
        |> ChainiumAddress

    let merkleTree (hashes : string list) =
        hashes
        |> List.map Multibase.Base58.Decode
        |> MerkleTree.build hashBytes
        |> Multibase.Base58.Encode
        |> MerkleTreeRoot
