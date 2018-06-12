namespace Chainium.Blockchain.Public.Core

open System.Text
open Chainium.Common
open Chainium.Blockchain.Common.Conversion
open Chainium.Blockchain.Public.Core
open Chainium.Blockchain.Public.Core.DomainTypes

module Blocks =

    let createTxResultHash decodeHash createHash (TxHash txHash, txResult : TxResult) =
        let txResult = Mapping.txResultToDto txResult

        [
            decodeHash txHash
            txResult.Status |> int16ToBytes
            txResult.ErrorCode |?? 0s |> int16ToBytes
            txResult.FailedActionNumber |?? 0s |> int16ToBytes
            txResult.BlockNumber |> int64ToBytes
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

    let createAccountControllerStateHash
        decodeHash
        createHash
        (AccountHash accountHash, controllerAddress : ChainiumAddress option)
        =

        let addressBytes =
            match controllerAddress with
            | Some (ChainiumAddress a) -> decodeHash a
            | None -> [| 0uy |]

        [
            decodeHash accountHash
            addressBytes
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
            |> List.map (fun txHash ->
                createTxResultHash
                    decodeHash
                    createHash
                    (txHash, output.TxResults.[txHash])
            )
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

        let accountControllerHashes =
            output.AccountControllers
            |> Map.toList
            |> List.sort // We need a predictable order
            |> List.map (createAccountControllerStateHash decodeHash createHash)

        let stateRoot =
            chxBalanceHashes @ holdingHashes @ accountControllerHashes
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
