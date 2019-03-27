namespace Own.Blockchain.Public.Core

open System
open Own.Common.FSharp
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Core.Dtos

module Mapping =

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Tx
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let txStatusNumberToString (txStatusNumber : byte) =
        match txStatusNumber with
        | 0uy -> "Pending"
        | 1uy -> "Success"
        | 2uy -> "Failure"
        | s -> failwithf "Unknown tx status: %i" s

    let txErrorCodeNumberToString (txErrorCodeNumber : Nullable<int16>) : string =
        if not txErrorCodeNumber.HasValue then
            None
        elif Enum.IsDefined(typeof<TxErrorCode>, txErrorCodeNumber.Value) then
            let txErrorCode : TxErrorCode = LanguagePrimitives.EnumOfValue txErrorCodeNumber.Value
            txErrorCode.ToString() |> Some
        else
            failwithf "Unknown tx error code: %s" (txErrorCodeNumber.ToString())
        |> Option.toObj

    let txEnvelopeFromDto (dto : TxEnvelopeDto) : TxEnvelope =
        {
            RawTx = dto.Tx |> Convert.FromBase64String
            Signature = Signature dto.Signature
        }

    let txActionFromDto (action : TxActionDto) =
        match action.ActionData with
        | :? TransferChxTxActionDto as a ->
            {
                TransferChxTxAction.RecipientAddress = BlockchainAddress a.RecipientAddress
                Amount = ChxAmount a.Amount
            }
            |> TransferChx
        | :? TransferAssetTxActionDto as a ->
            {
                TransferAssetTxAction.FromAccountHash = AccountHash a.FromAccountHash
                ToAccountHash = AccountHash a.ToAccountHash
                AssetHash = AssetHash a.AssetHash
                Amount = AssetAmount a.Amount
            }
            |> TransferAsset
        | :? CreateAssetEmissionTxActionDto as a ->
            {
                CreateAssetEmissionTxAction.EmissionAccountHash = AccountHash a.EmissionAccountHash
                AssetHash = AssetHash a.AssetHash
                Amount = AssetAmount a.Amount
            }
            |> CreateAssetEmission
        | :? CreateAccountTxActionDto ->
            CreateAccount
        | :? CreateAssetTxActionDto ->
            CreateAsset
        | :? SetAccountControllerTxActionDto as a ->
            {
                SetAccountControllerTxAction.AccountHash = AccountHash a.AccountHash
                ControllerAddress = BlockchainAddress a.ControllerAddress
            }
            |> SetAccountController
        | :? SetAssetControllerTxActionDto as a ->
            {
                SetAssetControllerTxAction.AssetHash = AssetHash a.AssetHash
                ControllerAddress = BlockchainAddress a.ControllerAddress
            }
            |> SetAssetController
        | :? SetAssetCodeTxActionDto as a ->
            {
                SetAssetCodeTxAction.AssetHash = AssetHash a.AssetHash
                AssetCode = AssetCode a.AssetCode
            }
            |> SetAssetCode
        | :? ConfigureValidatorTxActionDto as a ->
            {
                ConfigureValidatorTxAction.NetworkAddress = NetworkAddress a.NetworkAddress
                SharedRewardPercent = a.SharedRewardPercent
                IsEnabled = a.IsEnabled
            }
            |> ConfigureValidator
        | :? RemoveValidatorTxActionDto ->
            RemoveValidator
        | :? DelegateStakeTxActionDto as a ->
            {
                DelegateStakeTxAction.ValidatorAddress = BlockchainAddress a.ValidatorAddress
                Amount = ChxAmount a.Amount
            }
            |> DelegateStake
        | :? SubmitVoteTxActionDto as a ->
            {
                VoteId =
                    {
                        AccountHash = AccountHash a.AccountHash
                        AssetHash = AssetHash a.AssetHash
                        ResolutionHash = VotingResolutionHash a.ResolutionHash
                    }
                VoteHash = VoteHash a.VoteHash
            }
            |> SubmitVote
        | :? SubmitVoteWeightTxActionDto as a ->
            {
                VoteId =
                    {
                        AccountHash = AccountHash a.AccountHash
                        AssetHash = AssetHash a.AssetHash
                        ResolutionHash = VotingResolutionHash a.ResolutionHash
                    }
                VoteWeight = VoteWeight a.VoteWeight
            }
            |> SubmitVoteWeight
        | :? SetAccountEligibilityTxActionDto as a ->
            {
                AccountHash = AccountHash a.AccountHash
                AssetHash = AssetHash a.AssetHash
                Eligibility =
                    {
                        IsPrimaryEligible = a.IsPrimaryEligible
                        IsSecondaryEligible = a.IsSecondaryEligible
                    }
            }
            |> SetAccountEligibility
        | :? SetAssetEligibilityTxActionDto as a ->
            {
                SetAssetEligibilityTxAction.AssetHash = AssetHash a.AssetHash
                IsEligibilityRequired = a.IsEligibilityRequired
            }
            |> SetAssetEligibility
        | :? AddKycProviderTxActionDto as a ->
            {
                AddKycProviderTxAction.AssetHash = AssetHash a.AssetHash
                ProviderAddress = BlockchainAddress a.ProviderAddress
            }
            |> AddKycProvider
        | :? ChangeKycControllerAddressTxActionDto as a ->
            {
                ChangeKycControllerAddressTxAction.AccountHash = AccountHash a.AccountHash
                AssetHash = AssetHash a.AssetHash
                KycControllerAddress = BlockchainAddress a.KycControllerAddress
            }
            |> ChangeKycControllerAddress
        | :? RemoveKycProviderTxActionDto as a ->
            {
                RemoveKycProviderTxAction.AssetHash = AssetHash a.AssetHash
                ProviderAddress = BlockchainAddress a.ProviderAddress
            }
            |> RemoveKycProvider
        | _ ->
            failwith "Invalid action type to map"

    let txFromDto sender hash (dto : TxDto) : Tx =
        {
            TxHash = hash
            Sender = sender
            Nonce = Nonce dto.Nonce
            ExpirationTime = Timestamp dto.ExpirationTime
            ActionFee = ChxAmount dto.ActionFee
            Actions = dto.Actions |> List.map txActionFromDto
        }

    let txToTxInfoDto (tx : Tx) : TxInfoDto =
        {
            TxHash = tx.TxHash.Value
            SenderAddress = tx.Sender.Value
            Nonce = tx.Nonce.Value
            ActionFee = tx.ActionFee.Value
            ActionCount = Convert.ToInt16 tx.Actions.Length
        }

    let pendingTxInfoFromDto (dto : PendingTxInfoDto) : PendingTxInfo =
        {
            TxHash = TxHash dto.TxHash
            Sender = BlockchainAddress dto.SenderAddress
            Nonce = Nonce dto.Nonce
            ActionFee = ChxAmount dto.ActionFee
            ActionCount = dto.ActionCount
            AppearanceOrder = dto.AppearanceOrder
        }

    let txResultToDto (txResult : TxResult) =
        let status, errorCode, failedActionNumber =
            match txResult.Status with
            | Success -> 1uy, Nullable (), Nullable ()
            | Failure txError ->
                let statusNumber = 2uy
                match txError with
                | TxError errorCode ->
                    let errorNumber = errorCode |> LanguagePrimitives.EnumToValue
                    statusNumber, Nullable errorNumber, Nullable ()
                | TxActionError (TxActionNumber actionNumber, errorCode) ->
                    let errorNumber = errorCode |> LanguagePrimitives.EnumToValue
                    statusNumber, Nullable errorNumber, Nullable actionNumber

        {
            Status = status
            ErrorCode = errorCode
            FailedActionNumber = failedActionNumber
            BlockNumber = txResult.BlockNumber.Value
        }

    let txResultFromDto (dto : TxResultDto) : TxResult =
        let status =
            match dto.Status with
            | 1uy -> Success
            | 2uy ->
                match dto.ErrorCode.HasValue, dto.FailedActionNumber.HasValue with
                | true, false ->
                    let errorCode : TxErrorCode = dto.ErrorCode.Value |> LanguagePrimitives.EnumOfValue
                    TxError errorCode
                | true, true ->
                    let errorCode : TxErrorCode = dto.ErrorCode.Value |> LanguagePrimitives.EnumOfValue
                    TxActionError (TxActionNumber dto.FailedActionNumber.Value, errorCode)
                | _, _ -> failwith "Invalid error code and action number state in TxResult"
                |> Failure
            | c -> failwithf "Unknown TxStatus code %i" c

        {
            Status = status
            BlockNumber = BlockNumber dto.BlockNumber
        }

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Equivocation
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let consensusStepToCode consensusStep =
        match consensusStep with
        | ConsensusStep.Propose -> 0uy
        | ConsensusStep.Vote -> 1uy
        | ConsensusStep.Commit -> 2uy

    let consensusStepFromCode consensusStepCode =
        match consensusStepCode with
        | 0uy -> ConsensusStep.Propose
        | 1uy -> ConsensusStep.Vote
        | 2uy -> ConsensusStep.Commit
        | c -> failwithf "Unknown consensus step code %i" c

    let consensusStepFromMessage consensusMessage =
        match consensusMessage with
        | Propose _ -> ConsensusStep.Propose
        | Vote _ -> ConsensusStep.Vote
        | Commit _ -> ConsensusStep.Commit

    let equivocationProofToEquivocationInfoDto (equivocationProof : EquivocationProof) =
        {
            EquivocationProofHash = equivocationProof.EquivocationProofHash.Value
            ValidatorAddress = equivocationProof.ValidatorAddress.Value
            BlockNumber = equivocationProof.BlockNumber.Value
            ConsensusRound = equivocationProof.ConsensusRound.Value
            ConsensusStep = equivocationProof.ConsensusStep |> consensusStepToCode
        }

    let distributedDepositToDto (distributedDeposit : DistributedDeposit) : DistributedDepositDto =
        {
            DistributedDepositDto.ValidatorAddress = distributedDeposit.ValidatorAddress.Value
            Amount = distributedDeposit.Amount.Value
        }

    let distributedDepositFromDto (dto : DistributedDepositDto) : DistributedDeposit =
        {
            DistributedDeposit.ValidatorAddress = dto.ValidatorAddress |> BlockchainAddress
            Amount = dto.Amount |> ChxAmount
        }

    let equivocationProofResultToDto (equivocationProofResult : EquivocationProofResult) : EquivocationProofResultDto =
        {
            DepositTaken = equivocationProofResult.DepositTaken.Value
            DepositDistribution = equivocationProofResult.DepositDistribution |> List.map distributedDepositToDto
            BlockNumber = equivocationProofResult.BlockNumber.Value
        }

    let equivocationProofResultFromDto (dto : EquivocationProofResultDto) : EquivocationProofResult =
        {
            DepositTaken = ChxAmount dto.DepositTaken
            DepositDistribution = dto.DepositDistribution |> List.map distributedDepositFromDto
            BlockNumber = BlockNumber dto.BlockNumber
        }

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Block
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let blockHeaderFromDto (dto : BlockHeaderDto) : BlockHeader =
        {
            BlockHeader.Number = BlockNumber dto.Number
            Hash = BlockHash dto.Hash
            PreviousHash = BlockHash dto.PreviousHash
            ConfigurationBlockNumber = BlockNumber dto.ConfigurationBlockNumber
            Timestamp = Timestamp dto.Timestamp
            ProposerAddress = BlockchainAddress dto.ProposerAddress
            TxSetRoot = MerkleTreeRoot dto.TxSetRoot
            TxResultSetRoot = MerkleTreeRoot dto.TxResultSetRoot
            EquivocationProofsRoot = MerkleTreeRoot dto.EquivocationProofsRoot
            EquivocationProofResultsRoot = MerkleTreeRoot dto.EquivocationProofResultsRoot
            StateRoot = MerkleTreeRoot dto.StateRoot
            StakingRewardsRoot = MerkleTreeRoot dto.StakingRewardsRoot
            ConfigurationRoot = MerkleTreeRoot dto.ConfigurationRoot
        }

    let blockHeaderToDto (block : BlockHeader) : BlockHeaderDto =
        {
            BlockHeaderDto.Number = block.Number.Value
            Hash = block.Hash.Value
            PreviousHash = block.PreviousHash.Value
            ConfigurationBlockNumber = block.ConfigurationBlockNumber.Value
            Timestamp = block.Timestamp.Value
            ProposerAddress = block.ProposerAddress.Value
            TxSetRoot = block.TxSetRoot.Value
            TxResultSetRoot = block.TxResultSetRoot.Value
            EquivocationProofsRoot = block.EquivocationProofsRoot.Value
            EquivocationProofResultsRoot = block.EquivocationProofResultsRoot.Value
            StateRoot = block.StateRoot.Value
            StakingRewardsRoot = block.StakingRewardsRoot.Value
            ConfigurationRoot = block.ConfigurationRoot.Value
        }

    let validatorSnapshotFromDto (dto : ValidatorSnapshotDto) : ValidatorSnapshot =
        {
            ValidatorAddress = BlockchainAddress dto.ValidatorAddress
            NetworkAddress = NetworkAddress dto.NetworkAddress
            SharedRewardPercent = dto.SharedRewardPercent
            TotalStake = ChxAmount dto.TotalStake
        }

    let validatorSnapshotToDto (snapshot : ValidatorSnapshot) : ValidatorSnapshotDto =
        {
            ValidatorAddress = snapshot.ValidatorAddress.Value
            NetworkAddress = snapshot.NetworkAddress.Value
            SharedRewardPercent = snapshot.SharedRewardPercent
            TotalStake = snapshot.TotalStake.Value
        }

    let stakingRewardFromDto (dto : StakingRewardDto) : StakingReward =
        {
            StakingReward.StakerAddress = BlockchainAddress dto.StakerAddress
            Amount = ChxAmount dto.Amount
        }

    let stakingRewardToDto (stakingReward : StakingReward) : StakingRewardDto =
        {
            StakingRewardDto.StakerAddress = stakingReward.StakerAddress.Value
            Amount = stakingReward.Amount.Value
        }

    let blockchainConfigurationFromDto (dto : BlockchainConfigurationDto) : BlockchainConfiguration =
        {
            BlockchainConfiguration.ConfigurationBlockDelta = dto.ConfigurationBlockDelta
            Validators = dto.Validators |> List.map validatorSnapshotFromDto
            ValidatorDepositLockTime = dto.ValidatorDepositLockTime
            ValidatorBlacklistTime = dto.ValidatorBlacklistTime
            MaxTxCountPerBlock = dto.MaxTxCountPerBlock
        }

    let blockchainConfigurationToDto (config : BlockchainConfiguration) : BlockchainConfigurationDto =
        {
            BlockchainConfigurationDto.ConfigurationBlockDelta = config.ConfigurationBlockDelta
            Validators = config.Validators |> List.map validatorSnapshotToDto
            ValidatorDepositLockTime = config.ValidatorDepositLockTime
            ValidatorBlacklistTime = config.ValidatorBlacklistTime
            MaxTxCountPerBlock = config.MaxTxCountPerBlock
        }

    let blockFromDto (dto : BlockDto) : Block =
        let config =
            if isNull (box dto.Configuration) then
                None
            else
                dto.Configuration |> blockchainConfigurationFromDto |> Some

        {
            Header = blockHeaderFromDto dto.Header
            TxSet = dto.TxSet |> List.map TxHash
            EquivocationProofs = dto.EquivocationProofs |> List.map EquivocationProofHash
            StakingRewards = dto.StakingRewards |> List.map stakingRewardFromDto
            Configuration = config
        }

    let blockToDto (block : Block) : BlockDto =
        {
            Header = blockHeaderToDto block.Header
            TxSet = block.TxSet |> List.map (fun (TxHash h) -> h)
            EquivocationProofs = block.EquivocationProofs |> List.map (fun (EquivocationProofHash h) -> h)
            StakingRewards = block.StakingRewards |> List.map stakingRewardToDto
            Configuration =
                match block.Configuration with
                | None -> Unchecked.defaultof<_>
                | Some c -> blockchainConfigurationToDto c
        }

    let blockEnvelopeFromDto (dto : BlockEnvelopeDto) : BlockEnvelope =
        {
            Block = dto.Block |> blockFromDto
            ConsensusRound = ConsensusRound dto.ConsensusRound
            Signatures = dto.Signatures |> List.map Signature
        }

    let blockEnvelopeToDto (envelope : BlockEnvelope) : BlockEnvelopeDto =
        {
            Block = envelope.Block |> blockToDto
            ConsensusRound = envelope.ConsensusRound.Value
            Signatures = envelope.Signatures |> List.map (fun (Signature s) -> s)
        }

    let blockHeaderToBlockInfoDto isConfigBlock (blockHeader : BlockHeader) : BlockInfoDto =
        {
            BlockNumber = blockHeader.Number.Value
            BlockHash = blockHeader.Hash.Value
            BlockTimestamp = blockHeader.Timestamp.Value
            IsConfigBlock = isConfigBlock
        }

    let blockchainHeadInfoToDto (info : BlockchainHeadInfo) : BlockchainHeadInfoDto =
        {
            BlockNumber = info.BlockNumber.Value
        }

    let blockchainHeadInfoFromDto (dto : BlockchainHeadInfoDto) : BlockchainHeadInfo =
        {
            BlockNumber = BlockNumber dto.BlockNumber
        }

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // State
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let chxAddressStateFromDto (dto : ChxAddressStateDto) : ChxAddressState =
        {
            Nonce = Nonce dto.Nonce
            Balance = ChxAmount dto.Balance
        }

    let chxAddressStateToDto (state : ChxAddressState) : ChxAddressStateDto =
        {
            Nonce = state.Nonce.Value
            Balance = state.Balance.Value
        }

    let holdingStateFromDto (dto : HoldingStateDto) : HoldingState =
        {
            Balance = AssetAmount dto.Balance
            IsEmission = dto.IsEmission
        }

    let holdingStateToDto (state : HoldingState) : HoldingStateDto =
        {
            Balance = state.Balance.Value
            IsEmission = state.IsEmission
        }

    let voteStateFromDto (dto : VoteStateDto) : VoteState =
        {
            VoteHash = VoteHash dto.VoteHash
            VoteWeight =
                if dto.VoteWeight.HasValue then
                    dto.VoteWeight.Value |> VoteWeight |> Some
                else
                    None
        }

    let voteStateToDto (state : VoteState) : VoteStateDto =
        {
            VoteHash = state.VoteHash.Value
            VoteWeight =
                match state.VoteWeight with
                | None -> Nullable ()
                | Some (VoteWeight voteWeight) -> Nullable voteWeight
        }

    let eligibilityStateFromDto (dto : EligibilityStateDto) =
        {
            Eligibility =
                {
                    IsPrimaryEligible = dto.IsPrimaryEligible
                    IsSecondaryEligible = dto.IsSecondaryEligible
                }
            KycControllerAddress = BlockchainAddress dto.KycControllerAddress
        }

    let eligibilityStateToDto (state : EligibilityState) =
        {
            IsPrimaryEligible = state.Eligibility.IsPrimaryEligible
            IsSecondaryEligible = state.Eligibility.IsSecondaryEligible
            KycControllerAddress = state.KycControllerAddress.Value
        }

    let accountStateFromDto (dto : AccountStateDto) : AccountState =
        {
            ControllerAddress = BlockchainAddress dto.ControllerAddress
        }

    let accountStateToDto (state : AccountState) : AccountStateDto =
        {
            ControllerAddress = state.ControllerAddress.Value
        }

    let assetStateFromDto (dto : AssetStateDto) : AssetState =
        {
            AssetCode =
                if dto.AssetCode.IsNullOrWhiteSpace() then
                    None
                else
                    dto.AssetCode |> AssetCode |> Some
            ControllerAddress = BlockchainAddress dto.ControllerAddress
            IsEligibilityRequired = dto.IsEligibilityRequired
        }

    let assetStateToDto (state : AssetState) : AssetStateDto =
        {
            AssetCode = state.AssetCode |> Option.map (fun (AssetCode c) -> c) |> Option.toObj
            ControllerAddress = state.ControllerAddress.Value
            IsEligibilityRequired = state.IsEligibilityRequired
        }

    let validatorStateFromDto (dto : ValidatorStateDto) : ValidatorState =
        {
            NetworkAddress = NetworkAddress dto.NetworkAddress
            SharedRewardPercent = dto.SharedRewardPercent
            TimeToLockDeposit = dto.TimeToLockDeposit
            TimeToBlacklist = dto.TimeToBlacklist
            IsEnabled = dto.IsEnabled
        }

    let validatorStateToDto (state : ValidatorState) : ValidatorStateDto =
        {
            NetworkAddress = state.NetworkAddress.Value
            SharedRewardPercent = state.SharedRewardPercent
            TimeToLockDeposit = state.TimeToLockDeposit
            TimeToBlacklist = state.TimeToBlacklist
            IsEnabled = state.IsEnabled
        }

    let validatorChangeToCode (change : ValidatorChange) =
        match change with
        | Add -> ValidatorChangeCode.Add
        | Remove -> ValidatorChangeCode.Remove
        | Update -> ValidatorChangeCode.Update

    let stakeStateFromDto (dto : StakeStateDto) : StakeState =
        {
            Amount = ChxAmount dto.Amount
        }

    let stakeStateToDto (state : StakeState) : StakeStateDto =
        {
            Amount = state.Amount.Value
        }

    let stakerInfoFromDto (dto : StakerInfoDto) : StakerInfo =
        {
            StakerInfo.StakerAddress = BlockchainAddress dto.StakerAddress
            Amount = ChxAmount dto.Amount
        }

    let outputToDto (output : ProcessingOutput) : ProcessingOutputDto =
        let txResults =
            output.TxResults
            |> Map.remap (fun (TxHash h, s : TxResult) -> h, txResultToDto s)

        let equivocationProofResults =
            output.EquivocationProofResults
            |> Map.remap (fun (EquivocationProofHash h, s : EquivocationProofResult) ->
                h, equivocationProofResultToDto s
            )

        let chxAddresses =
            output.ChxAddresses
            |> Map.remap (fun (BlockchainAddress a, s : ChxAddressState) -> a, chxAddressStateToDto s)

        let holdings =
            output.Holdings
            |> Map.remap (fun ((AccountHash ah, AssetHash ac), s : HoldingState) -> (ah, ac), holdingStateToDto s)

        let votes =
            output.Votes
            |> Map.remap (fun (voteId : VoteId, s : VoteState) ->
                (voteId.AccountHash.Value, voteId.AssetHash.Value, voteId.ResolutionHash.Value), voteStateToDto s
            )

        let eligibilities =
            output.Eligibilities
            |> Map.remap (fun ((AccountHash ah, AssetHash ac), s : EligibilityState) ->
                (ah, ac), eligibilityStateToDto s)

        let kycProviders =
            output.KycProviders
            |> Map.remap (fun (AssetHash assetHash, providers) ->
                assetHash, providers |> Map.remap (fun (BlockchainAddress a, c) -> a, c = KycProviderChange.Add)
            )

        let accounts =
            output.Accounts
            |> Map.remap (fun (AccountHash ah, s : AccountState) -> ah, accountStateToDto s)

        let assets =
            output.Assets
            |> Map.remap (fun (AssetHash ah, s : AssetState) -> ah, assetStateToDto s)

        let validators =
            output.Validators
            |> Map.remap (fun (BlockchainAddress a, (s, c)) -> a, (validatorStateToDto s, validatorChangeToCode c))

        let stakes =
            output.Stakes
            |> Map.remap (fun ((BlockchainAddress sa, BlockchainAddress va), s : StakeState) ->
                (sa, va), stakeStateToDto s
            )

        {
            ProcessingOutputDto.TxResults = txResults
            EquivocationProofResults = equivocationProofResults
            ChxAddresses = chxAddresses
            Holdings = holdings
            Votes = votes
            Eligibilities = eligibilities
            KycProviders = kycProviders
            Accounts = accounts
            Assets = assets
            Validators = validators
            Stakes = stakes
        }

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // API
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let accountHoldingDtosToGetAccoungHoldingsResponseDto
        (AccountHash accountHash)
        (accountState : AccountStateDto)
        (holdings : AccountHoldingDto list)
        =

        let mapFn (holding : AccountHoldingDto) : GetAccountApiHoldingDto =
            {
                AssetHash = holding.AssetHash
                Balance = holding.Balance
            }

        {
            AccountHash = accountHash
            ControllerAddress = accountState.ControllerAddress
            Holdings = List.map mapFn holdings
        }

    let blockEnvelopeDtoToGetBlockApiResponseDto (blockEnvelopeDto : BlockEnvelopeDto) =
        let blockDto = blockEnvelopeDto.Block

        {
            Number = blockDto.Header.Number
            Hash = blockDto.Header.Hash
            PreviousHash = blockDto.Header.PreviousHash
            ConfigurationBlockNumber = blockDto.Header.ConfigurationBlockNumber
            Timestamp = blockDto.Header.Timestamp
            ProposerAddress = blockDto.Header.ProposerAddress
            TxSetRoot = blockDto.Header.TxSetRoot
            TxResultSetRoot = blockDto.Header.TxResultSetRoot
            EquivocationProofsRoot = blockDto.Header.EquivocationProofsRoot
            EquivocationProofResultsRoot = blockDto.Header.EquivocationProofResultsRoot
            StateRoot = blockDto.Header.StateRoot
            StakingRewardsRoot = blockDto.Header.StakingRewardsRoot
            ConfigurationRoot = blockDto.Header.ConfigurationRoot
            TxSet = blockDto.TxSet
            EquivocationProofs = blockDto.EquivocationProofs
            StakingRewards = blockDto.StakingRewards
            Configuration = blockDto.Configuration
            ConsensusRound = blockEnvelopeDto.ConsensusRound
            Signatures = blockEnvelopeDto.Signatures
        }

    let txToGetTxApiResponseDto
        (TxHash txHash)
        (BlockchainAddress senderAddress)
        (txDto : TxDto)
        (txResult : TxResultDto option)
        =

        let txStatus, txErrorCode, failedActionNumber, blockNumber =
            match txResult with
            | Some r -> r.Status, r.ErrorCode, r.FailedActionNumber, Nullable r.BlockNumber
            | None -> 0uy, Nullable(), Nullable(), Nullable()

        {
            TxHash = txHash
            SenderAddress = senderAddress
            Nonce = txDto.Nonce
            ExpirationTime = txDto.ExpirationTime
            ActionFee = txDto.ActionFee
            Actions = txDto.Actions
            Status = txStatus |> txStatusNumberToString
            ErrorCode = txErrorCode |> txErrorCodeNumberToString
            FailedActionNumber = failedActionNumber
            IncludedInBlockNumber = blockNumber
        }

    let equivocationProofToGetEquivocationProofApiResponseDto
        (EquivocationProofHash equivocationProofHash)
        (BlockchainAddress validatorAddress)
        (equivocationProofDto : EquivocationProofDto)
        (equivocationProofResult : EquivocationProofResultDto option)
        =

        let status, depositTaken, depositDistribution, blockNumber =
            match equivocationProofResult with
            | Some r -> "Processed", Nullable r.DepositTaken, r.DepositDistribution, Nullable r.BlockNumber
            | None -> "Pending", Nullable(), [], Nullable()

        {
            EquivocationProofHash = equivocationProofHash
            ValidatorAddress = validatorAddress
            BlockNumber = equivocationProofDto.BlockNumber
            ConsensusRound = equivocationProofDto.ConsensusRound
            ConsensusStep = equivocationProofDto.ConsensusStep |> consensusStepFromCode |> unionCaseName
            BlockHash1 = equivocationProofDto.BlockHash1
            BlockHash2 = equivocationProofDto.BlockHash2
            Signature1 = equivocationProofDto.Signature1
            Signature2 = equivocationProofDto.Signature2
            Status = status
            DepositTaken = depositTaken
            DepositDistribution = depositDistribution
            IncludedInBlockNumber = blockNumber
        }

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Consensus
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let consensusMessageFromDto (dto : ConsensusMessageDto) : ConsensusMessage =
        match dto.ConsensusMessageType with
        | "Propose" ->
            let block = dto.Block |> blockFromDto
            let validRound = dto.ValidRound.Value |> ConsensusRound
            Propose (block, validRound)
        | "Vote" ->
            if dto.BlockHash = (None |> unionCaseName) then
                None
            else
                dto.BlockHash |> BlockHash |> Some
            |> Vote
        | "Commit" ->
            if dto.BlockHash = (None |> unionCaseName) then
                None
            else
                dto.BlockHash |> BlockHash |> Some
            |> Commit
        | mt ->
            failwithf "Unknown consensus message type: %s" mt

    let consensusMessageToDto (consensusMessage : ConsensusMessage) : ConsensusMessageDto =
        match consensusMessage with
        | Propose (block, validRound) ->
            {
                ConsensusMessageType = "Propose"
                Block = block |> blockToDto
                ValidRound = Nullable(validRound.Value)
                BlockHash = Unchecked.defaultof<_>
            }
        | Vote blockHash ->
            let blockHash =
                match blockHash with
                | Some (BlockHash h) -> h
                | None -> None |> unionCaseName
            {
                ConsensusMessageType = "Vote"
                Block = Unchecked.defaultof<_>
                ValidRound = Unchecked.defaultof<_>
                BlockHash = blockHash
            }
        | Commit blockHash ->
            let blockHash =
                match blockHash with
                | Some (BlockHash h) -> h
                | None -> None |> unionCaseName
            {
                ConsensusMessageType = "Commit"
                Block = Unchecked.defaultof<_>
                ValidRound = Unchecked.defaultof<_>
                BlockHash = blockHash
            }

    let consensusMessageEnvelopeFromDto (dto : ConsensusMessageEnvelopeDto) : ConsensusMessageEnvelope =
        {
            BlockNumber = BlockNumber dto.BlockNumber
            Round = ConsensusRound dto.Round
            ConsensusMessage = consensusMessageFromDto dto.ConsensusMessage
            Signature = Signature dto.Signature
        }

    let consensusMessageEnvelopeToDto (envelope : ConsensusMessageEnvelope) : ConsensusMessageEnvelopeDto =
        {
            BlockNumber = envelope.BlockNumber.Value
            Round = envelope.Round.Value
            ConsensusMessage = consensusMessageToDto envelope.ConsensusMessage
            Signature = envelope.Signature.Value
        }

    let consensusStateRequestFromDto (dto : ConsensusStateRequestDto) : ConsensusStateRequest =
        {
            ConsensusStateRequest.ValidatorAddress = dto.ValidatorAddress |> BlockchainAddress
            ConsensusRound = dto.ConsensusRound |> ConsensusRound
        }

    let consensusStateRequestToDto (request : ConsensusStateRequest) : ConsensusStateRequestDto =
        {
            ConsensusStateRequestDto.ValidatorAddress = request.ValidatorAddress.Value
            ConsensusRound = request.ConsensusRound.Value
        }

    let consensusStateResponseFromDto (dto : ConsensusStateResponseDto) : ConsensusStateResponse =
        {
            ConsensusStateResponse.Messages =
                dto.Messages
                |> List.map consensusMessageEnvelopeFromDto
            LockedRound = dto.LockedRound |> ConsensusRound
            LockedProposal =
                if isNull (box dto.LockedProposal) then
                    None
                else
                    dto.LockedProposal |> consensusMessageEnvelopeFromDto |> Some
            LockedVoteSignatures =
                dto.LockedVoteSignatures
                |> List.map Signature
        }

    let consensusStateResponseToDto (response : ConsensusStateResponse) : ConsensusStateResponseDto =
        {
            ConsensusStateResponseDto.Messages =
                response.Messages
                |> List.map consensusMessageEnvelopeToDto
            LockedRound = response.LockedRound.Value
            LockedProposal =
                match response.LockedProposal with
                | None -> Unchecked.defaultof<_>
                | Some e -> consensusMessageEnvelopeToDto e
            LockedVoteSignatures =
                response.LockedVoteSignatures
                |> List.map (fun s -> s.Value)
        }

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Network
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let private networkMessageIdToIdTypeTuple networkMessageId =
        match networkMessageId with
        | Tx (TxHash txHash) -> "Tx", txHash
        | EquivocationProof (EquivocationProofHash proofHash) -> "EquivocationProof", proofHash
        | Block (BlockNumber blockNr) -> "Block", blockNr |> Convert.ToString
        | Consensus (ConsensusMessageId msgId) -> "Consensus", msgId
        | ConsensusState -> "ConsensusState", ""
        | BlockchainHead -> "BlockchainHead", ""
        | PeerList -> "PeerList", ""

    let private messageTypeToNetworkMessageId (messageType : string) (messageId : string) =
        match messageType with
        | "Tx" -> messageId |> TxHash |> Tx
        | "EquivocationProof" -> messageId |> EquivocationProofHash |> EquivocationProof
        | "Block" -> messageId |> Convert.ToInt64 |> BlockNumber |> Block
        | "Consensus" -> messageId |> ConsensusMessageId |> Consensus
        | "ConsensusState" -> ConsensusState
        | "BlockchainHead" -> BlockchainHead
        | "PeerList" -> PeerList
        | _ -> failwithf "Invalid network message type %s" messageType

    let gossipMemberFromDto (dto: GossipMemberDto) : GossipMember =
        {
            NetworkAddress = NetworkAddress dto.NetworkAddress
            Heartbeat = dto.Heartbeat
        }

    let gossipMemberToDto (gossipMember : GossipMember) : GossipMemberDto =
        {
            NetworkAddress = gossipMember.NetworkAddress.Value
            Heartbeat = gossipMember.Heartbeat
        }

    let gossipDiscoveryMessageFromDto (dto : GossipDiscoveryMessageDto) : GossipDiscoveryMessage =
        {
            ActiveMembers = dto.ActiveMembers |> List.map gossipMemberFromDto
        }

    let gossipDiscoveryMessageToDto (gossipDiscoveryMessage: GossipDiscoveryMessage) =
        {
            ActiveMembers = gossipDiscoveryMessage.ActiveMembers |> List.map gossipMemberToDto
        }

    let gossipMessageFromDto (dto : GossipMessageDto) =
        let gossipMessageId = messageTypeToNetworkMessageId dto.MessageType dto.MessageId

        {
            MessageId = gossipMessageId
            SenderAddress =
                if dto.SenderAddress.IsNullOrEmpty() then
                    None
                else
                    NetworkAddress dto.SenderAddress |> Some
            Data = dto.Data
        }

    let gossipMessageToDto (gossipMessage : GossipMessage) =
        let messageType, messageId = networkMessageIdToIdTypeTuple gossipMessage.MessageId

        {
            MessageId = messageId
            MessageType = messageType
            SenderAddress =
                match gossipMessage.SenderAddress with
                | Some a -> a.Value
                | None -> null
            Data = gossipMessage.Data
        }

    let multicastMessageFromDto (dto : MulticastMessageDto) : MulticastMessage =
        let multicastMessageId = messageTypeToNetworkMessageId dto.MessageType dto.MessageId

        {
            MessageId = multicastMessageId
            SenderIdentity =
                if isNull (box dto.SenderIdentity) then
                    None
                else
                    dto.SenderIdentity |> PeerNetworkIdentity |> Some
            Data = dto.Data
        }

    let multicastMessageToDto (multicastMessage : MulticastMessage) =
        let messageType, messageId = networkMessageIdToIdTypeTuple multicastMessage.MessageId

        {
            MessageId = messageId
            MessageType = messageType
            SenderIdentity =
                match multicastMessage.SenderIdentity with
                | None -> Unchecked.defaultof<_>
                | Some id -> id.Value
            Data = multicastMessage.Data
        }

    let requestDataMessageFromDto (dto : RequestDataMessageDto) : RequestDataMessage =
        let requestDataMessageId = messageTypeToNetworkMessageId dto.MessageType dto.MessageId

        {
            MessageId = requestDataMessageId
            SenderIdentity = PeerNetworkIdentity dto.SenderIdentity
        }

    let requestDataMessageToDto (requestDataMessage : RequestDataMessage) =
        let messageType, messageId = networkMessageIdToIdTypeTuple requestDataMessage.MessageId

        {
            MessageId = messageId
            MessageType = messageType
            SenderIdentity = requestDataMessage.SenderIdentity.Value
        }

    let responseDataMessageFromDto (dto : ResponseDataMessageDto) : ResponseDataMessage =
        let responseDataMessageId = messageTypeToNetworkMessageId dto.MessageType dto.MessageId

        {
            MessageId = responseDataMessageId
            Data = dto.Data
        }

    let responseDataMessageToDto (responseDataMessage : ResponseDataMessage) =
        let messageType, messageId = networkMessageIdToIdTypeTuple responseDataMessage.MessageId

        {
            MessageId = messageId
            MessageType = messageType
            Data = responseDataMessage.Data
        }

    let private peerMessageFromDto (dto : PeerMessageDto) =
        match dto.MessageData with
        | :? GossipDiscoveryMessageDto as m ->
            gossipDiscoveryMessageFromDto m |> GossipDiscoveryMessage
        | :? GossipMessageDto as m ->
            gossipMessageFromDto m |> GossipMessage
        | :? MulticastMessageDto as m ->
            multicastMessageFromDto m |> MulticastMessage
        | :? RequestDataMessageDto as m ->
            requestDataMessageFromDto m |> RequestDataMessage
        | :? ResponseDataMessageDto as m ->
            responseDataMessageFromDto m |> ResponseDataMessage
        | _ -> failwith "Invalid message type to map"

    let private peerMessageToDto (serialize : obj -> byte[]) peerMessage : PeerMessageDto =
        let messageType, data =
            match peerMessage with
            | GossipDiscoveryMessage m -> "GossipDiscoveryMessage", m |> gossipDiscoveryMessageToDto |> serialize
            | GossipMessage m -> "GossipMessage", m |> gossipMessageToDto |> serialize
            | MulticastMessage m -> "MulticastMessage", m |> multicastMessageToDto |> serialize
            | RequestDataMessage m -> "RequestDataMessage", m |> requestDataMessageToDto |> serialize
            | ResponseDataMessage m -> "ResponseDataMessage", m |> responseDataMessageToDto |> serialize

        {
            MessageType = messageType
            MessageData = data
        }

    let peerMessageEnvelopeFromDto (dto : PeerMessageEnvelopeDto) =
        {
            PeerMessageEnvelope.NetworkId = NetworkId dto.NetworkId
            PeerMessage = peerMessageFromDto dto.PeerMessage
        }

    let peerMessageEnvelopeToDto (serialize : obj -> byte[]) (peerMessageEnvelope : PeerMessageEnvelope) =
        {
            NetworkId = peerMessageEnvelope.NetworkId.Value
            ProtocolVersion = 0s // TODO: Take from domain type upon implementing protocol versioning.
            PeerMessage = peerMessageToDto serialize peerMessageEnvelope.PeerMessage
        }
