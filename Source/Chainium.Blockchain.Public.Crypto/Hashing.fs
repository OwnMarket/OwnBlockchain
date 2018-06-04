namespace Chainium.Blockchain.Public.Crypto

open System
open System.Security.Cryptography
open Multiformats.Base
open Chainium.Common
open Chainium.Blockchain.Public.Core.DomainTypes

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
        Multibase.Base58.Encode hash

    let decode hash =
        Multibase.Base58.Decode hash

    let hash (data : byte[]) =
        data
        |> hashBytes
        |> encode

    let chainiumAddress (publicKey : byte[]) =
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
        |> ChainiumAddress

    let merkleTree (hashes : string list) =
        hashes
        |> List.map decode
        |> MerkleTree.build hashBytes
        |> encode
        |> MerkleTreeRoot
