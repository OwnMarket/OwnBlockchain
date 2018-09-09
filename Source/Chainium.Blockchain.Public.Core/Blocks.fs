namespace Chainium.Blockchain.Public.Core

open Chainium.Common
open Chainium.Blockchain.Common
open Chainium.Blockchain.Common.Conversion
open Chainium.Blockchain.Public.Core
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Core.Dtos

module Blocks =

    let createTxResultHash decodeHash createHash (TxHash txHash, txResult : TxResult) =
        let txResult = Mapping.txResultToDto txResult

        [
            decodeHash txHash
            [| txResult.Status |]
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

    let createAccountStateHash
        decodeHash
        createHash
        (AccountHash accountHash, state : AccountState)
        =

        let addressBytes = state.ControllerAddress |> fun (ChainiumAddress a) -> decodeHash a

        [
            decodeHash accountHash
            addressBytes
        ]
        |> Array.concat
        |> createHash

    let createAssetStateHash
        decodeHash
        createHash
        (AssetHash assetHash, state : AssetState)
        =

        let addressBytes = state.ControllerAddress |> fun (ChainiumAddress a) -> decodeHash a
        let assetCodeBytes =
            match state.AssetCode with
            | Some (AssetCode code) -> code |> stringToBytes |> createHash |> decodeHash
            | None -> Array.empty

        [
            decodeHash assetHash
            assetCodeBytes
            addressBytes
        ]
        |> Array.concat
        |> createHash

    let createValidatorStateHash
        decodeHash
        createHash
        (ChainiumAddress validatorAddress, state : ValidatorState)
        =

        [
            decodeHash validatorAddress
            stringToBytes state.NetworkAddress
        ]
        |> Array.concat
        |> createHash

    let createValidatorSnapshotHash
        decodeHash
        createHash
        (validatorSnapshot : ValidatorSnapshot)
        =

        let (ChainiumAddress validatorAddress) = validatorSnapshot.ValidatorAddress
        let (ChxAmount totalStake) = validatorSnapshot.TotalStake

        [
            decodeHash validatorAddress
            stringToBytes validatorSnapshot.NetworkAddress
            decimalToBytes totalStake
        ]
        |> Array.concat
        |> createHash

    let createStakeStateHash
        decodeHash
        createHash
        (ChainiumAddress stakeholderAddress, ChainiumAddress validatorAddress, state : StakeState)
        =

        let (ChxAmount amount) = state.Amount

        [
            decodeHash stakeholderAddress
            decodeHash validatorAddress
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

        let accountHashes =
            output.Accounts
            |> Map.toList
            |> List.sort // We need a predictable order
            |> List.map (createAccountStateHash decodeHash createHash)

        let assetHashes =
            output.Assets
            |> Map.toList
            |> List.sort // We need a predictable order
            |> List.map (createAssetStateHash decodeHash createHash)

        let validatorHashes =
            output.Validators
            |> Map.toList
            |> List.sort // We need a predictable order
            |> List.map (createValidatorStateHash decodeHash createHash)

        let validatorSnapshotHashes =
            output.ValidatorSnapshots
            |> List.sort // We need a predictable order
            |> List.map (createValidatorSnapshotHash decodeHash createHash)

        let stakeHashes =
            output.Stakes
            |> Map.toList
            |> List.sort // We need a predictable order
            |> List.map (fun ((stakeholderAddress, validatorAddress), state) ->
                createStakeStateHash decodeHash createHash (stakeholderAddress, validatorAddress, state)
            )

        let stateRoot =
            chxBalanceHashes
            @ holdingHashes
            @ accountHashes
            @ assetHashes
            @ validatorHashes
            @ validatorSnapshotHashes
            @ stakeHashes
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
    let isValidSuccessorBlock
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

    let createGenesisState
        genesisChxSupply
        genesisAddress
        (genesisValidators : Map<ChainiumAddress, ValidatorState>)
        : ProcessingOutput
        =

        let genesisChxBalanceState =
            {
                ChxBalanceState.Amount = genesisChxSupply
                Nonce = Nonce 0L
            }

        let chxBalances =
            [
                genesisAddress, genesisChxBalanceState
            ]
            |> Map.ofList

        let validatorSnapshots =
            genesisValidators
            |> Map.toList
            |> List.map (fun (a, s) ->
                {
                    ValidatorSnapshot.ValidatorAddress = a
                    NetworkAddress = s.NetworkAddress
                    TotalStake = ChxAmount 0m
                }
            )

        {
            TxResults = Map.empty
            ChxBalances = chxBalances
            Holdings = Map.empty
            Accounts = Map.empty
            Assets = Map.empty
            Validators = genesisValidators
            ValidatorSnapshots = validatorSnapshots
            Stakes = Map.empty
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

    let extractBlockFromEnvelopeDto blockEnvelopeDto =
        blockEnvelopeDto
        |> Mapping.blockEnvelopeFromDto
        |> fun envelope -> Serialization.deserialize<BlockDto> envelope.RawBlock
        |> Result.map Mapping.blockFromDto
