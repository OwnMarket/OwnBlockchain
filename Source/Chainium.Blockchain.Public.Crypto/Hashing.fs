namespace Chainium.Blockchain.Public.Crypto

open System
open System.Text
open System.Security.Cryptography
open Multiformats.Base
open Chainium.Common
open Chainium.Blockchain.Public.Core.DomainTypes

module Hashing =
    let hashBytes (data : byte[])=
        let sha256 = SHA256.Create()
        sha256.ComputeHash(data)

    let hash (data : byte[]) =
        data
        |> hashBytes
        |> Multibase.Base58.Encode

    let addressHash (data : byte[]) =
        let numOfBytesToTake = 20

        let sha160Hash (data : byte[]) =
            let sha512 = SHA512.Create()

            sha512.ComputeHash(data)
            |> Array.take(numOfBytesToTake)

        let sha256 = SHA256.Create()

        data
        |> sha256.ComputeHash
        |> sha160Hash
        |> Multibase.Base58.Encode

    let merkleTree (hashes : string list) =
        let hashes =
            hashes
            |> List.map Multibase.Base58.Decode

        // TODO: Calculate Merkle Tree

        MerkleTreeRoot ""
