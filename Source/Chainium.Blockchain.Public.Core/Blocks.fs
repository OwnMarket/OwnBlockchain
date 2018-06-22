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
        (AccountHash accountHash, AssetHash assetHash, state : HoldingState)
        =

        let (AssetAmount amount) = state.Amount

        [
            decodeHash accountHash
            decodeHash assetHash
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

    let createAssetControllerStateHash
        decodeHash
        createHash
        (AssetHash assetHash, controllerAddress : ChainiumAddress option)
        =

        let addressBytes =
            match controllerAddress with
            | Some (ChainiumAddress a) -> decodeHash a
            | None -> [| 0uy |]

        [
            decodeHash assetHash
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
            |> List.map (fun ((accountHash, assetHash), state) ->
                createHoldingStateHash decodeHash createHash (accountHash, assetHash, state)
            )

        let accountControllerHashes =
            output.AccountControllers
            |> Map.toList
            |> List.sort // We need a predictable order
            |> List.map (createAccountControllerStateHash decodeHash createHash)

        let assetControllerHashes =
            output.AssetControllers
            |> Map.toList
            |> List.sort // We need a predictable order
            |> List.map (createAssetControllerStateHash decodeHash createHash)

        let stateRoot =
            chxBalanceHashes @ holdingHashes @ accountControllerHashes @ assetControllerHashes
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

    /// Checks if the block is a valid potential successor of a previous block identified by previousBlockHash argument.
    let isValidBlock
        decodeHash
        createHash
        createMerkleTree
        previousBlockHash
        (block : Block)
        : bool
        =

        let txSetRoot =
            block.TxSet
            |> List.map (fun (TxHash hash) -> hash)
            |> createMerkleTree

        let blockHash =
            createBlockHash
                decodeHash
                createHash
                block.Header.Number
                previousBlockHash
                block.Header.Timestamp
                block.Header.Validator
                txSetRoot
                block.Header.TxResultSetRoot
                block.Header.StateRoot

        block.Header.Hash = blockHash

    let createGenesisState genesisChxSupply genesisAddress : ProcessingOutput =
        let genesisChxBalanceState =
            {
                Amount = genesisChxSupply
                Nonce = Nonce 0L
            }

        let chxBalances =
            [
                genesisAddress, genesisChxBalanceState
            ]
            |> Map.ofList

        {
            TxResults = Map.empty
            ChxBalances = chxBalances
            Holdings = Map.empty
            AccountControllers = Map.empty
            AssetControllers = Map.empty
        }

    let createGenesisBlock
        (decodeHash : string -> byte[])
        (createHash : byte[] -> string)
        (createMerkleTree : string list -> MerkleTreeRoot)
        zeroHash
        zeroAddress
        (output : ProcessingOutput)
        : Block
        =

        let blockNumber = BlockNumber 0L
        let timestamp = Timestamp 0L
        let previousBlockHash = zeroHash |> BlockHash
        let txSet = []

        assembleBlock
            decodeHash
            createHash
            createMerkleTree
            zeroAddress
            blockNumber
            timestamp
            previousBlockHash
            txSet
            output
