namespace Own.Blockchain.Public.Core.Dtos

open System
open MessagePack

////////////////////////////////////////////////////////////////////////////////////////////////////
// Tx
////////////////////////////////////////////////////////////////////////////////////////////////////

[<CLIMutable>]
type TransferChxTxActionDto = {
    RecipientAddress : string
    Amount : decimal
}

[<CLIMutable>]
type TransferAssetTxActionDto = {
    FromAccountHash : string
    ToAccountHash : string
    AssetHash : string
    Amount : decimal
}

[<CLIMutable>]
type CreateAssetEmissionTxActionDto = {
    EmissionAccountHash : string
    AssetHash : string
    Amount : decimal
}

type CreateAccountTxActionDto () =
    class end // Using empty class to satisfy the deserialization logic (class because record cannot be empty).

type CreateAssetTxActionDto () =
    class end // Using empty class to satisfy the deserialization logic (class because record cannot be empty).

[<CLIMutable>]
type SetAccountControllerTxActionDto = {
    AccountHash : string
    ControllerAddress : string
}

[<CLIMutable>]
type SetAssetControllerTxActionDto = {
    AssetHash : string
    ControllerAddress : string
}

[<CLIMutable>]
type SetAssetCodeTxActionDto = {
    AssetHash : string
    AssetCode : string
}

[<CLIMutable>]
type ConfigureValidatorTxActionDto = {
    NetworkAddress : string
    SharedRewardPercent : decimal
}

[<CLIMutable>]
type DelegateStakeTxActionDto = {
    ValidatorAddress : string
    Amount : decimal
}

[<CLIMutable>]
type SubmitVoteTxActionDto = {
    AccountHash : string
    AssetHash : string
    ResolutionHash : string
    VoteHash : string
}

[<CLIMutable>]
type SubmitVoteWeightTxActionDto = {
    AccountHash : string
    AssetHash : string
    ResolutionHash : string
    VoteWeight : decimal
}

[<CLIMutable>]
type SetAccountEligibilityTxActionDto = {
    AccountHash : string
    AssetHash : string
    IsPrimaryEligible : bool
    IsSecondaryEligible : bool
}

[<CLIMutable>]
type SetAssetEligibilityTxActionDto = {
    AssetHash : string
    IsEligibilityRequired : bool
}

[<CLIMutable>]
type ChangeKycControllerAddressTxActionDto = {
    AccountHash : string
    AssetHash : string
    KycControllerAddress : string
}

[<CLIMutable>]
type AddKycProviderTxActionDto = {
    AssetHash : string
    ProviderAddress : string
}

[<CLIMutable>]
type RemoveKycProviderTxActionDto = {
    AssetHash : string
    ProviderAddress : string
}

[<CLIMutable>]
type TxActionDto = {
    ActionType : string
    ActionData : obj
}

[<CLIMutable>]
type TxDto = {
    SenderAddress : string
    Nonce : int64
    Fee : decimal
    Actions : TxActionDto list
}

[<CLIMutable>]
[<MessagePackObject>]
type TxEnvelopeDto = {
    [<Key(0)>] Tx : string
    [<Key(1)>] Signature : string
}

[<CLIMutable>]
type TxInfoDto = {
    TxHash : string
    SenderAddress : string
    Nonce : int64
    Fee : decimal
    ActionCount : int16
}

[<CLIMutable>]
type PendingTxInfoDto = {
    TxHash : string
    SenderAddress : string
    Nonce : int64
    Fee : decimal
    ActionCount : int16
    AppearanceOrder : int64
}

[<CLIMutable>]
[<MessagePackObject>]
type TxResultDto = {
    [<Key(0)>] Status : byte
    [<Key(1)>] ErrorCode : Nullable<int16>
    [<Key(2)>] FailedActionNumber : Nullable<int16>
    [<Key(3)>] BlockNumber : int64
}

////////////////////////////////////////////////////////////////////////////////////////////////////
// Blockchain Configuration
////////////////////////////////////////////////////////////////////////////////////////////////////

[<CLIMutable>]
[<MessagePackObject>]
type ValidatorSnapshotDto = {
    [<Key(0)>] ValidatorAddress : string
    [<Key(1)>] NetworkAddress : string
    [<Key(2)>] SharedRewardPercent : decimal
    [<Key(3)>] TotalStake : decimal
}

[<CLIMutable>]
[<MessagePackObject>]
type BlockchainConfigurationDto = {
    [<Key(0)>] Validators : ValidatorSnapshotDto list
}

////////////////////////////////////////////////////////////////////////////////////////////////////
// Block
////////////////////////////////////////////////////////////////////////////////////////////////////

[<CLIMutable>]
[<MessagePackObject>]
type BlockHeaderDto = {
    [<Key(0)>] Number : int64
    [<Key(1)>] Hash : string
    [<Key(2)>] PreviousHash : string
    [<Key(3)>] ConfigurationBlockNumber : int64
    [<Key(4)>] Timestamp : int64
    [<Key(5)>] ProposerAddress : string
    [<Key(6)>] TxSetRoot : string
    [<Key(7)>] TxResultSetRoot : string
    [<Key(8)>] EquivocationProofsRoot : string
    [<Key(9)>] EquivocationProofResultsRoot : string
    [<Key(10)>] StateRoot : string
    [<Key(11)>] StakingRewardsRoot : string
    [<Key(12)>] ConfigurationRoot : string
}

[<CLIMutable>]
[<MessagePackObject>]
type StakingRewardDto = {
    [<Key(0)>] StakerAddress : string
    [<Key(1)>] Amount : decimal
}

[<CLIMutable>]
[<MessagePackObject>]
type BlockDto = {
    [<Key(0)>] Header : BlockHeaderDto
    [<Key(1)>] TxSet : string list
    [<Key(2)>] EquivocationProofs : string list
    [<Key(3)>] StakingRewards : StakingRewardDto list
    [<Key(4)>] Configuration : BlockchainConfigurationDto
}

[<CLIMutable>]
[<MessagePackObject>]
type BlockEnvelopeDto = {
    [<Key(0)>] Block : BlockDto
    [<Key(1)>] ConsensusRound : int
    [<Key(2)>] Signatures : string list
}

[<CLIMutable>]
type BlockInfoDto = {
    BlockNumber : int64
    BlockHash : string
    BlockTimestamp : int64
    IsConfigBlock : bool
}

////////////////////////////////////////////////////////////////////////////////////////////////////
// Consensus
////////////////////////////////////////////////////////////////////////////////////////////////////

[<MessagePackObject>]
type ConsensusMessageDto = {
    [<Key(0)>] ConsensusMessageType : string
    [<Key(1)>] BlockHash : string
    [<Key(2)>] Block : BlockDto
    [<Key(3)>] ValidRound : Nullable<int>
}

[<MessagePackObject>]
type ConsensusMessageEnvelopeDto = {
    [<Key(0)>] BlockNumber : int64
    [<Key(1)>] Round : int
    [<Key(2)>] ConsensusMessage : ConsensusMessageDto
    [<Key(3)>] Signature : string
}

[<CLIMutable>]
[<MessagePackObject>]
type EquivocationProofDto = {
    [<Key(0)>] BlockNumber : int64
    [<Key(1)>] ConsensusRound : int
    [<Key(2)>] ConsensusStep : byte
    [<Key(3)>] BlockHash1 : string
    [<Key(4)>] BlockHash2 : string
    [<Key(5)>] Signature1 : string
    [<Key(6)>] Signature2 : string
}

[<CLIMutable>]
type EquivocationInfoDto = {
    EquivocationProofHash : string
    ValidatorAddress : string
    BlockNumber : int64
    ConsensusRound : int
    ConsensusStep : byte
}

[<CLIMutable>]
[<MessagePackObject>]
type EquivocationProofResultDto = {
    [<Key(0)>] DepositTaken : decimal
    [<Key(1)>] BlockNumber : int64
}

////////////////////////////////////////////////////////////////////////////////////////////////////
// State
////////////////////////////////////////////////////////////////////////////////////////////////////

[<CLIMutable>]
type ChxBalanceStateDto = {
    Amount : decimal
    Nonce : int64
}

[<CLIMutable>]
type HoldingStateDto = {
    Amount : decimal
    IsEmission : bool
}

[<CLIMutable>]
type VoteStateDto = {
    VoteHash : string
    VoteWeight : Nullable<decimal>
}

[<CLIMutable>]
type EligibilityStateDto = {
    IsPrimaryEligible : bool
    IsSecondaryEligible : bool
    KycControllerAddress : string
}

[<CLIMutable>]
type AccountStateDto = {
    ControllerAddress : string
}

[<CLIMutable>]
type AssetStateDto = {
    AssetCode : string
    ControllerAddress : string
    IsEligibilityRequired : bool
}

[<CLIMutable>]
type ValidatorStateDto = {
    NetworkAddress : string
    SharedRewardPercent : decimal
    TimeToLockDeposit : Nullable<int16>
    TimeToBlacklist : Nullable<int16>
}

[<CLIMutable>]
type StakeStateDto = {
    Amount : decimal
}

type ProcessingOutputDto = {
    TxResults : Map<string, TxResultDto>
    EquivocationProofResults : Map<string, EquivocationProofResultDto>
    ChxBalances : Map<string, ChxBalanceStateDto>
    Holdings : Map<string * string, HoldingStateDto>
    Votes : Map<string * string * string, VoteStateDto>
    Eligibilities : Map<string * string, EligibilityStateDto>
    KycProviders : Map<string, Map<string, bool>>
    Accounts : Map<string, AccountStateDto>
    Assets : Map<string, AssetStateDto>
    Validators : Map<string, ValidatorStateDto>
    Stakes : Map<string * string, StakeStateDto>
}

[<CLIMutable>]
type ChxBalanceInfoDto = {
    BlockchainAddress : string
    ChxBalanceState : ChxBalanceStateDto
}

[<CLIMutable>]
type HoldingInfoDto = {
    AccountHash : string
    AssetHash : string
    HoldingState : HoldingStateDto
}

[<CLIMutable>]
type VoteInfoDto = {
    AccountHash : string
    AssetHash : string
    ResolutionHash : string
    VoteState : VoteStateDto
}

[<CLIMutable>]
type EligibilityInfoDto = {
    AccountHash : string
    AssetHash : string
    EligibilityState : EligibilityStateDto
}

[<CLIMutable>]
type AccountInfoDto = {
    AccountHash : string
    ControllerAddress : string
}

[<CLIMutable>]
type AssetInfoDto = {
    AssetHash : string
    AssetCode : string
    ControllerAddress : string
    IsEligibilityRequired : bool
}

[<CLIMutable>]
type ValidatorInfoDto = {
    ValidatorAddress : string
    NetworkAddress : string
    SharedRewardPercent : decimal
    TimeToLockDeposit : Nullable<int16>
    TimeToBlacklist : Nullable<int16>
}

[<CLIMutable>]
type StakeInfoDto = {
    StakerAddress : string
    ValidatorAddress : string
    StakeState : StakeStateDto
}

[<CLIMutable>]
type StakerInfoDto = {
    StakerAddress : string
    Amount : decimal
}

[<CLIMutable>]
type AccountHoldingDto = {
    AssetHash : string
    Amount : decimal
}

////////////////////////////////////////////////////////////////////////////////////////////////////
// Network
////////////////////////////////////////////////////////////////////////////////////////////////////

[<CLIMutable>]
[<MessagePackObject>]
type GossipMemberDto = {
    [<Key(0)>] NetworkAddress : string
    [<Key(1)>] Heartbeat : int64
}

[<CLIMutable>]
[<MessagePackObject>]
type GossipDiscoveryMessageDto = {
    [<Key(0)>] ActiveMembers : GossipMemberDto list
}

[<CLIMutable>]
[<MessagePackObject>]
type GossipMessageDto = {
    [<Key(0)>] MessageType : string
    [<Key(1)>] MessageId : string
    [<Key(2)>] SenderAddress : string
    [<Key(3)>] Data : byte[]
}

[<CLIMutable>]
[<MessagePackObject>]
type MulticastMessageDto = {
    [<Key(0)>] MessageType : string
    [<Key(1)>] MessageId : string
    [<Key(2)>] Data : byte[]
}

[<CLIMutable>]
[<MessagePackObject>]
type RequestDataMessageDto = {
    [<Key(0)>] MessageType : string
    [<Key(1)>] MessageId : string
    [<Key(2)>] SenderAddress : string
}

[<CLIMutable>]
[<MessagePackObject>]
type ResponseDataMessageDto = {
    [<Key(0)>] MessageType : string
    [<Key(1)>] MessageId : string
    [<Key(2)>] Data : byte[]
}

[<CLIMutable>]
[<MessagePackObject>]
type PeerMessageDto = {
    [<Key(0)>] MessageType : string
    [<Key(1)>] MessageData : obj
}

////////////////////////////////////////////////////////////////////////////////////////////////////
// API
////////////////////////////////////////////////////////////////////////////////////////////////////

type ErrorResponseDto = {
    Errors : string list
}

type SubmitTxResponseDto = {
    TxHash : string
}

type GetTxApiResponseDto = {
    TxHash : string
    SenderAddress : string
    Nonce : int64
    Fee : decimal
    Actions : TxActionDto list
    Status : string
    // Result
    ErrorCode : string
    FailedActionNumber : Nullable<int16>
    IncludedInBlockNumber : Nullable<int64>
}

type GetEquivocationProofApiResponseDto = {
    EquivocationProofHash : string
    ValidatorAddress : string
    BlockNumber : int64
    ConsensusRound : int
    ConsensusStep : string
    BlockHash1 : string
    BlockHash2 : string
    Signature1 : string
    Signature2 : string
    // Result
    DepositTaken : Nullable<decimal>
    IncludedInBlockNumber : Nullable<int64>
}

type GetBlockApiResponseDto = {
    Number : int64
    Hash : string
    PreviousHash : string
    ConfigurationBlockNumber : int64
    Timestamp : int64
    ProposerAddress : string
    TxSetRoot : string
    TxResultSetRoot : string
    EquivocationProofsRoot : string
    EquivocationProofResultsRoot : string
    StateRoot : string
    StakingRewardsRoot : string
    ConfigurationRoot : string
    TxSet : string list
    EquivocationProofs : string list
    StakingRewards : StakingRewardDto list
    Configuration : BlockchainConfigurationDto
    ConsensusRound : int
    Signatures : string list
}

type GetAddressApiResponseDto = {
    BlockchainAddress : string
    Balance : decimal
    Nonce : int64
}

type GetAddressAccountsApiResponseDto = {
    Accounts : string list
}

type GetAccountApiHoldingDto = {
    AssetHash : string
    Balance : decimal
}

type GetAccountApiResponseDto = {
    AccountHash : string
    ControllerAddress : string
    Holdings : GetAccountApiHoldingDto list
}
