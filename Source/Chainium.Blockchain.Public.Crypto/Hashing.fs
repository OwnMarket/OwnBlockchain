namespace Chainium.Blockchain.Public.Crypto

open System
open System.Text
open System.Security.Cryptography
open Chainium.Common
open Chainium.Blockchain.Public.Core.DomainTypes

module Hashing =

    let hash (data : string) =
        // TODO: Implement properly
        let content = Encoding.UTF8.GetBytes(data)
        let hash = SHA256.Create().ComputeHash(content)
        BitConverter.ToString(hash).Replace("-", "")

    let merkleTree _ =
        MerkleTree ""
        
    let txHash _ =
        TxHash ""

    let blockHash _ =
        BlockHash ""
