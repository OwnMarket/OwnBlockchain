namespace Chainium.Blockchain.Public.Core

open System
open System.Text
open Chainium.Common
open Chainium.Blockchain.Common
open Chainium.Blockchain.Common.Conversion
open Chainium.Blockchain.Public.Core
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Core.Dtos

module Blocks =

    let createTxResultHash decodeHash createHash (TxHash txHash, txStatus) =
        let txStatusCode =
            txStatus
            |> Processed
            |> Mapping.txStatusToCode

        [
            decodeHash txHash
            [| txStatusCode |]
        ]
        |> Array.concat
        |> createHash

    let createChxBalanceStateHash decodeHash createHash (ChainiumAddress address, state : ChxBalanceState) =
        let (ChxAmount amount) = state.Amount
        let (Nonce nonce) = state.Nonce

        [
            decodeHash address
            decimalToBytes amount
            int64ToBytes nonce
        ]
        |> Array.concat
        |> createHash

    let createHoldingStateHash
        decodeHash
        createHash
        (AccountHash accountHash, AssetCode assetCode, state : HoldingState)
        =

        let (AssetAmount amount) = state.Amount

        [
            decodeHash accountHash
            Encoding.UTF8.GetBytes assetCode
            decimalToBytes amount
        ]
        |> Array.concat
        |> createHash

    let createBlockHash
        decodeHash
        createHash
        (BlockNumber blockNumber)
        (BlockHash previousBlockHash)
        (Timestamp timestamp)
        (ChainiumAddress validator)
        (MerkleTreeRoot txSetRoot)
        (MerkleTreeRoot txResultSetRoot)
        (MerkleTreeRoot stateRoot)
        =

        [
            blockNumber |> int64ToBytes
            previousBlockHash |> decodeHash
            timestamp |> int64ToBytes
            validator |> decodeHash
            txSetRoot |> decodeHash
            txResultSetRoot |> decodeHash
            stateRoot |> decodeHash
        ]
        |> Array.concat
        |> createHash
        |> BlockHash

    let assembleBlock
        (decodeHash : string -> byte[])
        (createHash : byte[] -> string)
        (createMerkleTree : string list -> MerkleTreeRoot)
        (validator : ChainiumAddress)
        (blockNumber : BlockNumber)
        (timestamp : Timestamp)
        (previousBlockHash : BlockHash)
        (txSet : TxHash list)
        (output : ProcessingOutput)
        : Block
        =

        if txSet.Length <> output.TxResults.Count then
            failwith "Number of elements in ProcessingOutput.TxResults and TxSet must be equal"

        let txSetRoot =
            txSet
            |> List.map (fun (TxHash hash) -> hash)
            |> createMerkleTree

        let txResultSetRoot =
            txSet
            |> List.map (fun txHash -> createTxResultHash decodeHash createHash (txHash, output.TxResults.[txHash]))
            |> createMerkleTree

        let chxBalanceHashes =
            output.ChxBalances
            |> Map.toList
            |> List.sort // We need a predictable order
            |> List.map (createChxBalanceStateHash decodeHash createHash)

        let holdingHashes =
            output.Holdings
            |> Map.toList
            |> List.sort // We need a predictable order
            |> List.map (fun ((accountHash, assetCode), state) ->
                createHoldingStateHash decodeHash createHash (accountHash, assetCode, state)
            )

        let stateRoot =
            chxBalanceHashes @ holdingHashes
            |> createMerkleTree

        let blockHash =
            createBlockHash
                decodeHash
                createHash
                blockNumber
                previousBlockHash
                timestamp
                validator
                txSetRoot
                txResultSetRoot
                stateRoot

        let blockHeader =
            {
                BlockHeader.Number = blockNumber
                Hash = blockHash
                PreviousHash = previousBlockHash
                Timestamp = timestamp
                Validator = validator
                TxSetRoot = txSetRoot
                TxResultSetRoot = txResultSetRoot
                StateRoot = stateRoot
            }

        {
            Header = blockHeader
            TxSet = txSet
        }
