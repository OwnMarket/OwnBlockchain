namespace Chainium.Blockchain.Public.Crypto

open System
open System.Text
open System.Security.Cryptography
open Multiformats.Base
open Chainium.Common
open Chainium.Blockchain.Public.Core.DomainTypes

module Hashing =
    open MerkleTree

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
    

    let private hashChildNodes 
        (left : MerkleNode option) 
        (right : MerkleNode option)
        =
        let nodehash x=
            match x with
            | Some l -> l.Hash
            | None -> Array.zeroCreate 0

        let lefthash = nodehash left
        let righthash = nodehash right

        righthash
        |> Array.append lefthash
        |> hashBytes
    
    let merkleTree (hashes : string list) =
        let leafNodes = 
            hashes
            |> List.map Multibase.Base58.Decode
            |> List.map
                (
                    fun h -> 
                    {
                        Hash = h
                        Left = None
                        Right = None
                    } 
                    |> Some
                )
        let root = MerkleTree.build hashChildNodes leafNodes

        let rootHash = 
            match root with
            | Some x -> Multibase.Base58.Encode x.Hash
            | None -> ""

            
        MerkleTreeRoot rootHash
