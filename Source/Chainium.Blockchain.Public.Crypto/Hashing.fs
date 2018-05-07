namespace Chainium.Blockchain.Public.Crypto

open System
open System.Text
open System.Security.Cryptography
open Chainium.Common
open Chainium.Blockchain.Public.Core.DomainTypes

module Hashing =

    let baseHash()=SHA256.Create() :> HashAlgorithm

    let hash (data : byte[]) =
        baseHash().ComputeHash(data)

    let addressHash (dataToHash : byte[]) =
        let numOfBytesToTake = 20
        
        let sha160Hash = fun (data : byte[]) -> 
            let sha512=SHA512.Create() in () 
            ;  sha512.ComputeHash(data) 
            |> Array.take(numOfBytesToTake)
        
        let sha256=SHA256.Create()
        
        dataToHash 
        |> sha256.ComputeHash 
        |> sha160Hash

    let merkleTree _ =
        MerkleTree ""
        
    let txHash _ =
        TxHash ""

    let blockHash _ =
        BlockHash ""
