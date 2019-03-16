namespace Own.Blockchain.Public.Core

open Own.Common.FSharp
open Own.Blockchain.Common
open Own.Blockchain.Common.Conversion
open Own.Blockchain.Public.Core
open Own.Blockchain.Public.Core.DomainTypes

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

    let createEquivocationProofResultHash
        decodeHash
        createHash
        (EquivocationProofHash equivocationProofHash, equivocationProofResult : EquivocationProofResult)
        =

        let depositDistribution =
            equivocationProofResult.DepositDistribution
            |> List.map (fun d ->
                [
                    d.ValidatorAddress.Value |> decodeHash
                    d.Amount.Value |> decimalToBytes
                ]
                |> Array.concat
            )

        [
            yield equivocationProofHash |> decodeHash
            yield equivocationProofResult.DepositTaken.Value |> decimalToBytes
            yield! depositDistribution
            yield equivocationProofResult.BlockNumber.Value |> int64ToBytes
        ]
        |> Array.concat
        |> createHash

    let createChxAddressStateHash decodeHash createHash (BlockchainAddress address, state : ChxAddressState) =
        [
            decodeHash address
            int64ToBytes state.Nonce.Value
            decimalToBytes state.Balance.Value
        ]
        |> Array.concat
        |> createHash

    let createHoldingStateHash
        decodeHash
        createHash
        (AccountHash accountHash, AssetHash assetHash, state : HoldingState)
        =

        [
            decodeHash accountHash
            decodeHash assetHash
            decimalToBytes state.Balance.Value
            boolToBytes state.IsEmission
        ]
        |> Array.concat
        |> createHash

    let createVoteStateHash
        decodeHash
        createHash
        (AccountHash accountHash, AssetHash assetHash, VotingResolutionHash resolutionHash, state : VoteState)
        =

        let voteWeightBytes =
            match state.VoteWeight with
            | Some (VoteWeight voteWeight) -> decimalToBytes voteWeight
            | None -> [| 0uy |]

        [
            decodeHash accountHash
            decodeHash assetHash
            decodeHash resolutionHash
            decodeHash state.VoteHash.Value
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

    let createKycProviderStateHash
        decodeHash
        createHash
        (AssetHash assetHash, state : Map<BlockchainAddress, KycProviderChange>)
        =

        let stateHash =
            state
            |> Map.toList
            |> List.collect (fun (k, v) -> [decodeHash k.Value; boolToBytes (v = KycProviderChange.Add)])

        decodeHash assetHash :: stateHash
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
            | None -> [| 0uy |]

        [
            decodeHash assetHash
            assetCodeBytes
            addressBytes
            boolToBytes state.IsEligibilityRequired
        ]
        |> Array.concat
        |> createHash

    let createValidatorStateHash
        decodeHash
        createHash
        (BlockchainAddress validatorAddress, (state : ValidatorState, change : ValidatorChange))
        =

        let validatorChangeCodeBytes = [| change |> Mapping.validatorChangeToCode |> byte |]

        [
            decodeHash validatorAddress
            stringToBytes state.NetworkAddress.Value
            decimalToBytes state.SharedRewardPercent
            int16ToBytes state.TimeToLockDeposit
            int16ToBytes state.TimeToBlacklist
            boolToBytes state.IsEnabled
            validatorChangeCodeBytes
        ]
        |> Array.concat
        |> createHash

    let createStakeStateHash
        decodeHash
        createHash
        (BlockchainAddress stakerAddress, BlockchainAddress validatorAddress, state : StakeState)
        =

        [
            decodeHash stakerAddress
            decodeHash validatorAddress
            decimalToBytes state.Amount.Value
        ]
        |> Array.concat
        |> createHash

    let createStakingRewardHash
        decodeHash
        createHash
        (stakingReward : StakingReward)
        =

        [
            decodeHash stakingReward.StakerAddress.Value
            decimalToBytes stakingReward.Amount.Value
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

    let createConfigurationMerkleRoot
        decodeHash
        createHash
        createMerkleTree
        (blockchainConfiguration : BlockchainConfiguration option)
        =

        match blockchainConfiguration with
        | None -> []
        | Some c ->
            let validatorSnapshotHashes =
                c.Validators
                |> List.sortBy (fun v -> v.ValidatorAddress) // Ensure a predictable order
                |> List.map (createValidatorSnapshotHash decodeHash createHash)

            [
                yield c.ConfigurationBlockDelta |> int32ToBytes |> createHash
                yield! validatorSnapshotHashes
                yield c.ValidatorDepositLockTime |> int16ToBytes |> createHash
                yield c.ValidatorBlacklistTime |> int16ToBytes |> createHash
                yield c.MaxTxCountPerBlock |> int32ToBytes |> createHash
            ]
        |> createMerkleTree

    let createBlockHash
        decodeHash
        createHash
        (BlockNumber blockNumber)
        (BlockHash previousBlockHash)
        (Timestamp timestamp)
        (BlockchainAddress proposerAddress)
        (MerkleTreeRoot txSetRoot)
        (MerkleTreeRoot txResultSetRoot)
        (MerkleTreeRoot equivocationProofsRoot)
        (MerkleTreeRoot equivocationProofResultsRoot)
        (MerkleTreeRoot stateRoot)
        (MerkleTreeRoot stakingRewardsRoot)
        (MerkleTreeRoot configurationRoot)
        =

        [
            blockNumber |> int64ToBytes
            previousBlockHash |> decodeHash
            timestamp |> int64ToBytes
            proposerAddress |> decodeHash
            txSetRoot |> decodeHash
            txResultSetRoot |> decodeHash
            equivocationProofsRoot |> decodeHash
            equivocationProofResultsRoot |> decodeHash
            stateRoot |> decodeHash
            stakingRewardsRoot |> decodeHash
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
        (equivocationProofs : EquivocationProofHash list)
        (output : ProcessingOutput)
        (blockchainConfiguration : BlockchainConfiguration option)
        : Block
        =

        if txSet.Length <> output.TxResults.Count then
            failwith "Number of elements in TxResults and TxSet must be equal"

        if equivocationProofs.Length <> output.EquivocationProofResults.Count then
            failwith "Number of elements in EquivocationProofResults and EquivocationProofs must be equal"

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

        let equivocationProofsRoot =
            equivocationProofs
            |> List.map (fun (EquivocationProofHash hash) -> hash)
            |> createMerkleTree

        let equivocationProofResultsRoot =
            equivocationProofs
            |> List.map (fun equivocationProofHash ->
                createEquivocationProofResultHash
                    decodeHash
                    createHash
                    (equivocationProofHash, output.EquivocationProofResults.[equivocationProofHash])
            )
            |> createMerkleTree

        let chxAddressHashes =
            output.ChxAddresses
            |> Map.toList
            |> List.sort // Ensure a predictable order
            |> List.map (createChxAddressStateHash decodeHash createHash)

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

        let kycProviderHashes =
            output.KycProviders
            |> Map.toList
            |> List.sort // Ensure a predictable order
            |> List.map (fun (hash, state) ->
                createKycProviderStateHash decodeHash createHash (hash, state)
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
            chxAddressHashes
            @ holdingHashes
            @ voteHashes
            @ eligibilityHashes
            @ kycProviderHashes
            @ accountHashes
            @ assetHashes
            @ validatorHashes
            @ stakeHashes
            |> createMerkleTree

        let stakingRewards =
            output.StakingRewards
            |> Map.toList
            |> List.sortBy (fun (stakerAddress, amount) -> -amount.Value, stakerAddress) // Ensure a predictable order
            |> List.map (fun (stakerAddress, amount) ->
                {
                    StakingReward.StakerAddress = stakerAddress
                    Amount = amount
                }
            )

        let stakingRewardsRoot =
            stakingRewards
            |> List.map (createStakingRewardHash decodeHash createHash)
            |> createMerkleTree

        let configurationRoot =
            blockchainConfiguration
            |> createConfigurationMerkleRoot decodeHash createHash createMerkleTree

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
                equivocationProofsRoot
                equivocationProofResultsRoot
                stateRoot
                stakingRewardsRoot
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
                EquivocationProofsRoot = equivocationProofsRoot
                EquivocationProofResultsRoot = equivocationProofResultsRoot
                StateRoot = stateRoot
                StakingRewardsRoot = stakingRewardsRoot
                ConfigurationRoot = configurationRoot
            }

        {
            Header = blockHeader
            TxSet = txSet
            EquivocationProofs = equivocationProofs
            StakingRewards = stakingRewards
            Configuration = blockchainConfiguration
        }

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Genesis block
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let createGenesisState
        genesisChxSupply
        genesisAddress
        (genesisValidators : Map<BlockchainAddress, ValidatorState * ValidatorChange>)
        : ProcessingOutput
        =

        let genesisChxAddressState =
            {
                ChxAddressState.Nonce = Nonce 0L
                Balance = genesisChxSupply
            }

        let chxAddresses =
            [
                genesisAddress, genesisChxAddressState
            ]
            |> Map.ofList

        {
            TxResults = Map.empty
            EquivocationProofResults = Map.empty
            ChxAddresses = chxAddresses
            Holdings = Map.empty
            Votes = Map.empty
            Eligibilities = Map.empty
            KycProviders = Map.empty
            Accounts = Map.empty
            Assets = Map.empty
            Validators = genesisValidators
            Stakes = Map.empty
            StakingRewards = Map.empty
        }

    let assembleGenesisBlock
        (decodeHash : string -> byte[])
        (createHash : byte[] -> string)
        (createMerkleTree : string list -> MerkleTreeRoot)
        zeroHash
        zeroAddress
        configurationBlockDelta
        validatorDepositLockTime
        validatorBlacklistTime
        maxTxCountPerBlock
        (output : ProcessingOutput)
        : Block
        =

        let blockNumber = BlockNumber 0L
        let timestamp = Timestamp 0L
        let previousBlockHash = zeroHash |> BlockHash
        let txSet = []
        let equivocationProofs = []

        let validatorSnapshots =
            output.Validators
            |> Map.toList
            |> List.map (fun (validatorAddress, (state, _)) ->
                {
                    ValidatorSnapshot.ValidatorAddress = validatorAddress
                    NetworkAddress = state.NetworkAddress
                    SharedRewardPercent = state.SharedRewardPercent
                    TotalStake = ChxAmount 0m
                }
            )

        let blockchainConfiguration =
            {
                BlockchainConfiguration.ConfigurationBlockDelta = configurationBlockDelta
                Validators = validatorSnapshots
                ValidatorDepositLockTime = validatorDepositLockTime
                ValidatorBlacklistTime = validatorBlacklistTime
                MaxTxCountPerBlock = maxTxCountPerBlock
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
            equivocationProofs
            output
            blockchainConfiguration

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Helpers
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let extractBlockFromEnvelopeDto blockEnvelopeDto =
        blockEnvelopeDto
        |> Mapping.blockEnvelopeFromDto
        |> fun e -> e.Block

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

        let equivocationProofsRoot =
            block.EquivocationProofs
            |> List.map (fun (EquivocationProofHash hash) -> hash)
            |> createMerkleTree

        let stakingRewardsRoot =
            block.StakingRewards
            |> List.map (createStakingRewardHash decodeHash createHash)
            |> createMerkleTree

        let configurationRoot =
            createConfigurationMerkleRoot
                decodeHash
                createHash
                createMerkleTree
                block.Configuration

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
                equivocationProofsRoot
                block.Header.EquivocationProofResultsRoot
                block.Header.StateRoot
                stakingRewardsRoot
                configurationRoot

        block.Header.Hash = blockHash

    let verifyBlockSignatures
        createConsensusMessageHash
        verifySignature
        (blockEnvelope : BlockEnvelope)
        : Result<BlockchainAddress list, AppErrors>
        =

        let block = blockEnvelope.Block

        let values, errors =
            blockEnvelope.Signatures
            |> List.toArray
            |> Array.Parallel.map (fun s ->
                let messageHash =
                    createConsensusMessageHash
                        block.Header.Number
                        blockEnvelope.ConsensusRound
                        (block.Header.Hash |> Some |> ConsensusMessage.Commit)

                match verifySignature s messageHash with
                | Some blockchainAddress ->
                    Ok blockchainAddress
                | None ->
                    sprintf "Cannot verify block signature %s" s.Value
                    |> Result.appError
            )
            |> Array.toList
            |> List.partition Result.isOk

        if errors.IsEmpty then
            values
            |> List.map (function | Ok a -> a | _ -> failwith "This shouldn't happen")
            |> Ok
        else
            errors
            |> List.collect (function | Error e -> e | _ -> failwith "This shouldn't happen either")
            |> Error

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Configuration blocks
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let createNewBlockchainConfiguration
        (getTopValidators : unit -> ValidatorSnapshot list)
        configurationBlockDelta
        validatorDepositLockTime
        validatorBlacklistTime
        maxTxCountPerBlock
        =

        let validators = getTopValidators ()

        {
            BlockchainConfiguration.ConfigurationBlockDelta = configurationBlockDelta
            Validators = validators
            ValidatorDepositLockTime = validatorDepositLockTime
            ValidatorBlacklistTime = validatorBlacklistTime
            MaxTxCountPerBlock = maxTxCountPerBlock
        }

    let getConfigBlockAtHeight getBlock blockNumber =
        getBlock blockNumber
        |> Result.map extractBlockFromEnvelopeDto
        >>= (fun b ->
            if b.Configuration.IsSome then
                Ok b // This block is the configuration block
            else
                getBlock b.Header.ConfigurationBlockNumber
                |> Result.map extractBlockFromEnvelopeDto
        )
        |> Result.handle
            id
            (fun _ -> failwithf "Cannot get configuration block at height %i" blockNumber.Value)

    let getConfigurationAtHeight getBlock blockNumber =
        let configBlock = getConfigBlockAtHeight getBlock blockNumber
        match configBlock.Configuration with
        | None -> failwithf "Cannot find configuration in configuration block %i" configBlock.Header.Number.Value
        | Some config -> configBlock.Header.Number, config
