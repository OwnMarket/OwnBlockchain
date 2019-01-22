namespace Own.Blockchain.Public.Core

open Own.Common
open Own.Blockchain.Common
open Own.Blockchain.Common.Conversion
open Own.Blockchain.Public.Core
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Core.Dtos

module Blocks =

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Assembling the block
    ////////////////////////////////////////////////////////////////////////////////////////////////////

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

    let createChxBalanceStateHash decodeHash createHash (BlockchainAddress address, state : ChxBalanceState) =
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

    let createVoteStateHash
        decodeHash
        createHash
        (AccountHash accountHash, AssetHash assetHash, VotingResolutionHash resolutionHash, state : VoteState)
        =

        let (VoteHash voteHash) = state.VoteHash
        let voteWeightBytes =
            match state.VoteWeight with
            | None -> Array.empty
            | Some (VoteWeight voteWeight) -> decimalToBytes voteWeight

        [
            decodeHash accountHash
            decodeHash assetHash
            decodeHash resolutionHash
            decodeHash voteHash
            voteWeightBytes
        ]
        |> Array.concat
        |> createHash

    let createEligibilityStateHash
        decodeHash
        createHash
        (AccountHash accountHash, AssetHash assetHash, state : EligibilityState)
        =

        [
            decodeHash accountHash
            decodeHash assetHash
            boolToBytes state.Eligibility.IsPrimaryEligible
            boolToBytes state.Eligibility.IsSecondaryEligible
            decodeHash state.KycControllerAddress.Value
        ]
        |> Array.concat
        |> createHash

    let createKycControllerHash
        decodeHash
        createHash
        (state : KycControllerState)
        (change : KycControllerChange)
        =

        [
            decodeHash state.AssetHash.Value
            decodeHash state.ControllerAddress.Value
            boolToBytes (change = Add)
        ]
        |> Array.concat
        |> createHash

    let createAccountStateHash
        decodeHash
        createHash
        (AccountHash accountHash, state : AccountState)
        =

        let addressBytes = decodeHash state.ControllerAddress.Value

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

        let addressBytes = decodeHash state.ControllerAddress.Value
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
        (BlockchainAddress validatorAddress, state : ValidatorState)
        =

        [
            decodeHash validatorAddress
            stringToBytes state.NetworkAddress.Value
            decimalToBytes state.SharedRewardPercent
        ]
        |> Array.concat
        |> createHash

    let createValidatorSnapshotHash
        decodeHash
        createHash
        (validatorSnapshot : ValidatorSnapshot)
        =

        [
            decodeHash validatorSnapshot.ValidatorAddress.Value
            stringToBytes validatorSnapshot.NetworkAddress.Value
            decimalToBytes validatorSnapshot.SharedRewardPercent
            decimalToBytes validatorSnapshot.TotalStake.Value
        ]
        |> Array.concat
        |> createHash

    let createStakeStateHash
        decodeHash
        createHash
        (BlockchainAddress stakerAddress, BlockchainAddress validatorAddress, state : StakeState)
        =

        let (ChxAmount amount) = state.Amount

        [
            decodeHash stakerAddress
            decodeHash validatorAddress
            decimalToBytes amount
        ]
        |> Array.concat
        |> createHash

    let createStakerRewardHash
        decodeHash
        createHash
        (stakerReward : StakerReward)
        =

        [
            decodeHash stakerReward.StakerAddress.Value
            decimalToBytes stakerReward.Amount.Value
        ]
        |> Array.concat
        |> createHash

    let createBlockHash
        decodeHash
        createHash
        (BlockNumber blockNumber)
        (BlockHash previousBlockHash)
        (Timestamp timestamp)
        (BlockchainAddress proposerAddress)
        (MerkleTreeRoot txSetRoot)
        (MerkleTreeRoot txResultSetRoot)
        (MerkleTreeRoot stateRoot)
        (MerkleTreeRoot stakerRewardsRoot)
        (MerkleTreeRoot configurationRoot)
        =

        [
            blockNumber |> int64ToBytes
            previousBlockHash |> decodeHash
            timestamp |> int64ToBytes
            proposerAddress |> decodeHash
            txSetRoot |> decodeHash
            txResultSetRoot |> decodeHash
            stateRoot |> decodeHash
            stakerRewardsRoot |> decodeHash
            configurationRoot |> decodeHash
        ]
        |> Array.concat
        |> createHash
        |> BlockHash

    let assembleBlock
        (decodeHash : string -> byte[])
        (createHash : byte[] -> string)
        (createMerkleTree : string list -> MerkleTreeRoot)
        (proposerAddress : BlockchainAddress)
        (blockNumber : BlockNumber)
        (timestamp : Timestamp)
        (previousBlockHash : BlockHash)
        (configurationBlockNumber : BlockNumber)
        (txSet : TxHash list)
        (output : ProcessingOutput)
        (blockchainConfiguration : BlockchainConfiguration option)
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
            |> List.sort // Ensure a predictable order
            |> List.map (createChxBalanceStateHash decodeHash createHash)

        let holdingHashes =
            output.Holdings
            |> Map.toList
            |> List.sort // Ensure a predictable order
            |> List.map (fun ((accountHash, assetHash), state) ->
                createHoldingStateHash decodeHash createHash (accountHash, assetHash, state)
            )

        let voteHashes =
            output.Votes
            |> Map.toList
            |> List.sort // Ensure a predictable order
            |> List.map (fun (voteId, state) ->
                createVoteStateHash
                    decodeHash
                    createHash
                    (voteId.AccountHash, voteId.AssetHash, voteId.ResolutionHash, state)
            )

        let eligibilityHashes =
            output.Eligibilities
            |> Map.toList
            |> List.sort // Ensure a predictable order
            |> List.map (fun ((accountHash, assetHash), state) ->
                createEligibilityStateHash decodeHash createHash (accountHash, assetHash, state)
            )

        let kycControllerHashes =
            output.KycControllers
            |> Map.toList
            |> List.sort // Ensure a predictable order
            |> List.map (fun (state, change) ->
                createKycControllerHash decodeHash createHash state change
            )

        let accountHashes =
            output.Accounts
            |> Map.toList
            |> List.sort // Ensure a predictable order
            |> List.map (createAccountStateHash decodeHash createHash)

        let assetHashes =
            output.Assets
            |> Map.toList
            |> List.sort // Ensure a predictable order
            |> List.map (createAssetStateHash decodeHash createHash)

        let validatorHashes =
            output.Validators
            |> Map.toList
            |> List.sort // Ensure a predictable order
            |> List.map (createValidatorStateHash decodeHash createHash)

        let stakeHashes =
            output.Stakes
            |> Map.toList
            |> List.sort // Ensure a predictable order
            |> List.map (fun ((stakerAddress, validatorAddress), state) ->
                createStakeStateHash decodeHash createHash (stakerAddress, validatorAddress, state)
            )

        let stateRoot =
            chxBalanceHashes
            @ holdingHashes
            @ voteHashes
            @ eligibilityHashes
            @ kycControllerHashes
            @ accountHashes
            @ assetHashes
            @ validatorHashes
            @ stakeHashes
            |> createMerkleTree

        let stakerRewards =
            output.StakerRewards
            |> Map.toList
            |> List.sortBy (fun (stakerAddress, amount) -> -amount.Value, stakerAddress) // Ensure a predictable order
            |> List.map (fun (stakerAddress, amount) ->
                {
                    StakerReward.StakerAddress = stakerAddress
                    Amount = amount
                }
            )

        let stakerRewardsRoot =
            stakerRewards
            |> List.map (createStakerRewardHash decodeHash createHash)
            |> createMerkleTree

        let configurationRoot =
            match blockchainConfiguration with
            | None -> []
            | Some c ->
                let validatorSnapshotHashes =
                    c.Validators
                    |> List.sortBy (fun v -> v.ValidatorAddress) // Ensure a predictable order
                    |> List.map (createValidatorSnapshotHash decodeHash createHash)
                validatorSnapshotHashes
            |> createMerkleTree

        let blockHash =
            createBlockHash
                decodeHash
                createHash
                blockNumber
                previousBlockHash
                timestamp
                proposerAddress
                txSetRoot
                txResultSetRoot
                stateRoot
                stakerRewardsRoot
                configurationRoot

        let blockHeader =
            {
                BlockHeader.Number = blockNumber
                Hash = blockHash
                PreviousHash = previousBlockHash
                ConfigurationBlockNumber = configurationBlockNumber
                Timestamp = timestamp
                ProposerAddress = proposerAddress
                TxSetRoot = txSetRoot
                TxResultSetRoot = txResultSetRoot
                StateRoot = stateRoot
                StakerRewardsRoot = stakerRewardsRoot
                ConfigurationRoot = configurationRoot
            }

        {
            Header = blockHeader
            TxSet = txSet
            StakerRewards = stakerRewards
            Configuration = blockchainConfiguration
        }

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Genesis block
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let createGenesisState
        genesisChxSupply
        genesisAddress
        (genesisValidators : Map<BlockchainAddress, ValidatorState>)
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

        {
            TxResults = Map.empty
            ChxBalances = chxBalances
            Holdings = Map.empty
            Votes = Map.empty
            Eligibilities = Map.empty
            KycControllers = Map.empty
            Accounts = Map.empty
            Assets = Map.empty
            Validators = genesisValidators
            Stakes = Map.empty
            StakerRewards = Map.empty
        }

    let assembleGenesisBlock
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

        let validatorSnapshots =
            output.Validators
            |> Map.toList
            |> List.map (fun (validatorAddress, state) ->
                {
                    ValidatorSnapshot.ValidatorAddress = validatorAddress
                    NetworkAddress = state.NetworkAddress
                    SharedRewardPercent = state.SharedRewardPercent
                    TotalStake = ChxAmount 0m
                }
            )

        let blockchainConfiguration =
            {
                BlockchainConfiguration.Validators = validatorSnapshots
            }
            |> Some

        assembleBlock
            decodeHash
            createHash
            createMerkleTree
            zeroAddress
            blockNumber
            timestamp
            previousBlockHash
            blockNumber
            txSet
            output
            blockchainConfiguration

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Configuration blocks
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let calculateConfigurationBlockNumberForNewBlock configurationBlockDelta (BlockNumber blockNumber) =
        let offset =
            match blockNumber % configurationBlockDelta with
            | 0L -> configurationBlockDelta
            | o -> o

        BlockNumber (blockNumber - offset)

    let isConfigurationBlock configurationBlockDelta (BlockNumber blockNumber) =
        blockNumber % configurationBlockDelta = 0L

    let createNewBlockchainConfiguration
        (getTopValidators : unit -> ValidatorSnapshot list)
        =

        let validators = getTopValidators ()

        {
            BlockchainConfiguration.Validators = validators
        }

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Helpers
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let extractBlockFromEnvelopeDto blockEnvelopeDto =
        blockEnvelopeDto
        |> Mapping.blockEnvelopeFromDto
        |> fun envelope -> Serialization.deserialize<BlockDto> envelope.RawBlock
        |> Result.map Mapping.blockFromDto

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
                block.Header.ProposerAddress
                txSetRoot
                block.Header.TxResultSetRoot
                block.Header.StateRoot
                block.Header.StakerRewardsRoot
                block.Header.ConfigurationRoot

        block.Header.Hash = blockHash

    let verifyBlockSignatures
        createConsensusMessageHash
        verifySignature
        (blockEnvelope : BlockEnvelope)
        : Result<BlockchainAddress list, AppErrors>
        =

        result {
            let! block =
                blockEnvelope.RawBlock
                |> Serialization.deserialize<BlockDto>
                |> Result.map Mapping.blockFromDto

            let values, errors =
                blockEnvelope.Signatures
                |> List.map (fun s ->
                    let messageHash =
                        createConsensusMessageHash
                            block.Header.Number
                            blockEnvelope.ConsensusRound
                            (block.Header.Hash |> Some |> ConsensusMessage.Commit)

                    match verifySignature s messageHash with
                    | Some blockchainAddress ->
                        Ok blockchainAddress
                    | None ->
                        sprintf "Cannot verify block signature %s." s.Value
                        |> Result.appError
                )
                |> List.partition (function | Ok _ -> true | _ -> false)

            return!
                if errors.IsEmpty then
                    values
                    |> List.map (function | Ok a -> a | _ -> failwith "This shouldn't happen")
                    |> Ok
                else
                    errors
                    |> List.collect (function | Error e -> e | _ -> failwith "This shouldn't happen")
                    |> Error
        }
