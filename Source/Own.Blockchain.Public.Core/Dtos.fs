﻿namespace rec Own.Blockchain.Public.Core.Dtos

open System
open MessagePack

////////////////////////////////////////////////////////////////////////////////////////////////////
// TX
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
    IsEnabled : bool
}

type RemoveValidatorTxActionDto () =
    class end // Using empty class to satisfy the deserialization logic (class because record cannot be empty).

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
type ConfigureTradingPairTxActionDto = {
    BaseAssetHash : string
    QuoteAssetHash : string
    IsEnabled : bool
    MaxTradeOrderDuration : int16 // Hours
}

[<CLIMutable>]
type PlaceTradeOrderTxActionDto = {
    AccountHash : string
    BaseAssetHash : string
    QuoteAssetHash : string
    Side : string // BUY, SELL
    Amount : decimal
    OrderType : string // MARKET, LIMIT, STOP_MARKET, STOP_LIMIT, TRAILING_STOP_MARKET, TRAILING_STOP_LIMIT
    LimitPrice : decimal
    StopPrice : decimal
    TrailingOffset : decimal
    TrailingOffsetIsPercentage : bool
    TimeInForce : string // GTC, IOC
}

[<CLIMutable>]
type CancelTradeOrderTxActionDto = {
    TradeOrderHash : string
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
    ExpirationTime : int64
    ActionFee : decimal
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
    ActionFee : decimal
    ActionCount : int16
}

[<CLIMutable>]
type PendingTxInfoDto = {
    TxHash : string
    SenderAddress : string
    Nonce : int64
    ActionFee : decimal
    ActionCount : int16
    AppearanceOrder : int64
}

[<CLIMutable>]
type TxByAddressInfoDto = {
    TxHash : string
    Nonce : int64
    ActionFee : decimal
    ActionCount : int16
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
    [<Key(0)>] ConfigurationBlockDelta : int
    [<Key(1)>] Validators : ValidatorSnapshotDto list
    [<Key(2)>] ValidatorsBlacklist : string list
    [<Key(3)>] ValidatorDepositLockTime : int16
    [<Key(4)>] ValidatorBlacklistTime : int16
    [<Key(5)>] MaxTxCountPerBlock : int
    [<Key(6)>] DormantValidators : string list
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
    [<Key(13)>] TradesRoot : string
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
    [<Key(5)>] Trades : TradeDto list
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

[<CLIMutable>]
[<MessagePackObject>]
type BlockchainHeadInfoDto = {
    [<Key(0)>] BlockNumber : int64
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
type ConsensusMessageInfoDto = {
    BlockNumber : int64
    ConsensusRound : int
    ConsensusStep : int16
    MessageEnvelope : string
}

[<CLIMutable>]
type ConsensusStateInfoDto = {
    BlockNumber : int64
    ConsensusRound : int
    ConsensusStep : int16
    LockedBlock : string
    LockedRound : int
    ValidBlock : string
    ValidRound : int
    ValidBlockSignatures : string
}

[<MessagePackObject>]
type ConsensusStateRequestDto = {
    [<Key(0)>] ValidatorAddress : string
    [<Key(1)>] ConsensusRound : int
    [<Key(2)>] TargetValidatorAddress : string
}

[<MessagePackObject>]
type ConsensusStateResponseDto = {
    [<Key(0)>] Messages : ConsensusMessageEnvelopeDto list
    [<Key(1)>] ValidRound : int
    [<Key(2)>] ValidProposal : ConsensusMessageEnvelopeDto
    [<Key(3)>] ValidVoteSignatures : string list
}

[<CLIMutable>]
[<MessagePackObject>]
type EquivocationProofDto = {
    [<Key(0)>] BlockNumber : int64
    [<Key(1)>] ConsensusRound : int
    [<Key(2)>] ConsensusStep : byte
    [<Key(3)>] EquivocationValue1 : string
    [<Key(4)>] EquivocationValue2 : string
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
type DistributedDepositDto = {
    [<Key(0)>] ValidatorAddress : string
    [<Key(1)>] Amount : decimal
}

[<CLIMutable>]
[<MessagePackObject>]
type EquivocationProofResultDto = {
    [<Key(0)>] DepositTaken : decimal
    [<Key(1)>] DepositDistribution : DistributedDepositDto list
    [<Key(2)>] BlockNumber : int64
}

////////////////////////////////////////////////////////////////////////////////////////////////////
// State
////////////////////////////////////////////////////////////////////////////////////////////////////

[<CLIMutable>]
type ChxAddressStateDto = {
    Nonce : int64
    Balance : decimal
}

[<CLIMutable>]
type HoldingStateDto = {
    Balance : decimal
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
type AccountEligibilityInfoDto = {
    AssetHash : string
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
    TimeToLockDeposit : int16
    TimeToBlacklist : int16
    IsEnabled : bool
    LastProposedBlockNumber : Nullable<int64>
    LastProposedBlockTimestamp : Nullable<int64>
}

[<RequireQualifiedAccess>]
type ValidatorChangeCode =
    | Add = 0uy
    | Remove = 1uy
    | Update = 2uy

[<CLIMutable>]
type StakeStateDto = {
    Amount : decimal
}

[<CLIMutable>]
type TradingPairStateDto = {
    IsEnabled : bool
    MaxTradeOrderDuration : int16
    LastPrice : decimal
    PriceChange : decimal
}

[<CLIMutable>]
type TradeOrderStateDto = {
    BlockTimestamp : int64
    BlockNumber : int64
    TxPosition : int
    ActionNumber : int16
    AccountHash : string
    BaseAssetHash : string
    QuoteAssetHash : string
    Side : byte
    Amount : decimal
    OrderType : byte
    LimitPrice : decimal
    StopPrice : decimal
    TrailingOffset : decimal
    TrailingOffsetIsPercentage : bool
    TimeInForce : byte
    ExpirationTimestamp : int64
    IsExecutable : bool
    AmountFilled : decimal
}

[<RequireQualifiedAccess>]
type TradeOrderChangeCode =
    | Add = 0uy
    | Remove = 1uy
    | Update = 2uy

[<CLIMutable>]
[<MessagePackObject>]
type TradeDto = {
    [<Key(0)>] Direction : byte
    [<Key(1)>] BuyOrderHash : string
    [<Key(2)>] SellOrderHash : string
    [<Key(3)>] Amount : decimal
    [<Key(4)>] Price : decimal
}

type ProcessingOutputDto = {
    TxResults : Map<string, TxResultDto>
    EquivocationProofResults : Map<string, EquivocationProofResultDto>
    ChxAddresses : Map<string, ChxAddressStateDto>
    Holdings : Map<string * string, HoldingStateDto>
    Votes : Map<string * string * string, VoteStateDto>
    Eligibilities : Map<string * string, EligibilityStateDto>
    KycProviders : Map<string, Map<string, bool>>
    Accounts : Map<string, AccountStateDto>
    Assets : Map<string, AssetStateDto>
    Validators : Map<string, ValidatorStateDto * ValidatorChangeCode>
    Stakes : Map<string * string, StakeStateDto>
    TradingPairs : Map<string * string, TradingPairStateDto>
    TradeOrders : Map<string, TradeOrderStateDto * TradeOrderChangeCode>
    ClosedTradeOrders : Map<string, ClosedTradeOrderDto>
    Trades : TradeDto list
}

[<CLIMutable>]
type ChxAddressInfoDto = {
    BlockchainAddress : string
    ChxAddressState : ChxAddressStateDto
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
    TimeToLockDeposit : int16
    TimeToBlacklist : int16
    IsEnabled : bool
    LastProposedBlockNumber : Nullable<int64>
    LastProposedBlockTimestamp : Nullable<int64>
}

[<CLIMutable>]
type StakeInfoDto = {
    StakerAddress : string
    ValidatorAddress : string
    StakeState : StakeStateDto
}

[<CLIMutable>]
type AddressStakeInfoDto = {
    ValidatorAddress : string
    Amount : decimal
}

[<CLIMutable>]
type ValidatorStakeInfoDto = {
    StakerAddress : string
    Amount : decimal
}

[<CLIMutable>]
type StakerInfoDto = {
    StakerAddress : string
    Amount : decimal
}

[<CLIMutable>]
type AccountHoldingDto = {
    AssetHash : string
    Balance : decimal
}

[<CLIMutable>]
type AccountVoteDto = {
    AssetHash : string
    ResolutionHash : string
    VoteHash : string
    VoteWeight: decimal
}

[<CLIMutable>]
type TradingPairInfoDto = {
    BaseAssetHash : string
    QuoteAssetHash : string
    IsEnabled : bool
    MaxTradeOrderDuration : int16
    LastPrice : decimal
    PriceChange : decimal
}

[<CLIMutable>]
type TradeOrderAggregatedDto = {
    Side : byte
    LimitPrice : decimal
    Amount : decimal
}

[<CLIMutable>]
type TradeOrderInfoDto = {
    TradeOrderHash : string
    BlockTimestamp : int64
    BlockNumber : int64
    TxPosition : int
    ActionNumber : int16
    AccountHash : string
    BaseAssetHash : string
    QuoteAssetHash : string
    Side : byte
    Amount : decimal
    OrderType : byte
    LimitPrice : decimal
    StopPrice : decimal
    TrailingOffset : decimal
    TrailingOffsetIsPercentage : bool
    TimeInForce : byte
    ExpirationTimestamp : int64
    IsExecutable : bool
    AmountFilled : decimal
}

[<CLIMutable>]
[<MessagePackObject>]
type ClosedTradeOrderDto = {
    [<Key(0)>] BlockTimestamp : int64
    [<Key(1)>] BlockNumber : int64
    [<Key(2)>] TxPosition : int
    [<Key(3)>] ActionNumber : int16
    [<Key(4)>] AccountHash : string
    [<Key(5)>] BaseAssetHash : string
    [<Key(6)>] QuoteAssetHash : string
    [<Key(7)>] Side : byte
    [<Key(8)>] Amount : decimal
    [<Key(9)>] OrderType : byte
    [<Key(10)>] LimitPrice : decimal
    [<Key(11)>] StopPrice : decimal
    [<Key(12)>] TrailingOffset : decimal
    [<Key(13)>] TrailingOffsetIsPercentage : bool
    [<Key(14)>] TimeInForce : byte
    [<Key(15)>] ExpirationTimestamp : int64
    [<Key(16)>] IsExecutable : bool
    [<Key(17)>] AmountFilled : decimal
    [<Key(18)>] Status : byte
    [<Key(19)>] ClosedInBlockNumber : int64
}

////////////////////////////////////////////////////////////////////////////////////////////////////
// Network
////////////////////////////////////////////////////////////////////////////////////////////////////

[<CLIMutable>]
[<MessagePackObject>]
type GossipPeerDto = {
    [<Key(0)>] NetworkAddress : string
    [<Key(1)>] Heartbeat : int64
    [<Key(2)>] SessionTimestamp : int64
}

[<CLIMutable>]
type GossipPeerInfoDto = {
    NetworkAddress : string
    SessionTimestamp : int64
    IsDead : bool
    DeadTimestamp : Nullable<int64>
}

[<CLIMutable>]
[<MessagePackObject>]
type GossipDiscoveryMessageDto = {
    [<Key(0)>] ActiveMembers : GossipPeerDto list
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
    [<Key(2)>] SenderIdentity : byte[]
    [<Key(3)>] Data : byte[]
}

[<CLIMutable>]
[<MessagePackObject>]
type NetworkMessageItemDto = {
    [<Key(0)>] MessageType : string
    [<Key(1)>] MessageId : string
}

[<CLIMutable>]
[<MessagePackObject>]
type RequestDataMessageDto = {
    [<Key(0)>] Items : NetworkMessageItemDto list
    [<Key(1)>] SenderIdentity : byte[]
}

[<CLIMutable>]
[<MessagePackObject>]
type ResponseItemMessageDto = {
    [<Key(0)>] MessageType : string
    [<Key(1)>] MessageId : string
    [<Key(2)>] Data : byte[]
}

[<CLIMutable>]
[<MessagePackObject>]
type ResponseDataMessageDto = {
    [<Key(0)>] Items : ResponseItemMessageDto list
}

[<CLIMutable>]
[<MessagePackObject>]
type PeerMessageDto = {
    [<Key(0)>] MessageType : string
    [<Key(1)>] MessageData : byte[]
}

[<CLIMutable>]
[<MessagePackObject>]
type PeerMessageEnvelopeDto = {
    [<Key(0)>] NetworkId : byte[]
    [<Key(1)>] ProtocolVersion : int16
    [<Key(2)>] PeerMessage : PeerMessageDto
}

////////////////////////////////////////////////////////////////////////////////////////////////////
// API
////////////////////////////////////////////////////////////////////////////////////////////////////

type ErrorResponseDto = {
    Errors : string list
}

type GetNodeInfoApiDto = {
    VersionNumber : string
    VersionHash : string
    NetworkCode : string
    PublicAddress : string
    ValidatorAddress : string
    MinTxActionFee : decimal
}

type GetConsensusInfoApiDto = {
    BlockNumber : int64
    ConsensusRound : int
    ConsensusStep : string
    LockedBlock : string
    LockedRound : int
    ValidBlock : string
    ValidRound : int
    ValidBlockSignatures : string list
    Proposals : string list
    Votes : string list
    Commits : string list
}

type GetPeerListApiDto = {
    Peers : string list
}

[<CLIMutable>]
type GetTxPoolInfoApiDto = {
    PendingTxs : int64
}

type GetTxPoolByAddressApiDto = {
    SenderAddress : string
    PendingTxCount : int64
    PendingTxs : TxByAddressInfoDto list
}

type SubmitTxResponseDto = {
    TxHash : string
}

type GetTxApiResponseDto = {
    TxHash : string
    SenderAddress : string
    Nonce : int64
    ExpirationTime : int64
    ActionFee : decimal
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
    EquivocationValue1 : string
    EquivocationValue2 : string
    Signature1 : string
    Signature2 : string
    // Result
    Status : string
    DepositTaken : Nullable<decimal>
    DepositDistribution : DistributedDepositDto list
    IncludedInBlockNumber : Nullable<int64>
}

[<CLIMutable>]
type TradeApiDto = {
    Direction : string
    BuyOrderHash : string
    SellOrderHash : string
    Amount : decimal
    Price : decimal
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
    TradesRoot : string
    TxSet : string list
    EquivocationProofs : string list
    StakingRewards : StakingRewardDto list
    Configuration : BlockchainConfigurationDto
    Trades : TradeApiDto list
    ConsensusRound : int
    Signatures : string list
}

type DetailedChxBalanceDto = {
    Total : decimal
    Staked : decimal
    Deposit : decimal
    Available : decimal
}

type GetAddressApiResponseDto = {
    BlockchainAddress : string
    Nonce : int64
    Balance : DetailedChxBalanceDto
}

type GetAddressAccountsApiResponseDto = {
    BlockchainAddress : string
    Accounts : string list
}

type GetAddressAssetsApiResponseDto = {
    BlockchainAddress : string
    Assets : string list
}

type GetAddressStakesApiResponseDto = {
    BlockchainAddress : string
    Stakes : AddressStakeInfoDto list
}

type GetValidatorStakesApiResponseDto = {
    ValidatorAddress : string
    Stakes : ValidatorStakeInfoDto list
}

[<CLIMutable>]
type GetValidatorApiResponseDto = {
    ValidatorAddress : string
    NetworkAddress : string
    SharedRewardPercent : decimal
    IsDepositLocked : bool
    IsBlacklisted : bool
    IsEnabled : bool
    IsActive : bool
    LastProposedBlockNumber : Nullable<int64>
    LastProposedBlockTimestamp : Nullable<int64>
}

type GetValidatorsApiDto = {
    Validators : GetValidatorApiResponseDto list
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

type GetAccountApiVoteDto = {
    AccountHash : string
    Votes : AccountVoteDto list
}

type GetAccountApiEligibilitiesDto = {
    AccountHash : string
    Eligibilities : AccountEligibilityInfoDto list
}

type GetAccountApiKycProvidersDto = {
    AccountHash : string
    KycProviders : string list
}

type GetAssetApiKycProvidersDto = {
    AssetHash : string
    KycProviders : string list
}

[<CLIMutable>]
type TradingPairApiDto = {
    BaseAssetHash : string
    BaseAssetCode : string
    QuoteAssetHash : string
    QuoteAssetCode : string
    IsEnabled : bool
    MaxTradeOrderDuration : int16
    LastPrice : decimal
    PriceChange : decimal
} with
    member __.PriceChangePercent =
        if __.PriceChange = 0m || __.LastPrice = __.PriceChange then
            0m
        else
            __.PriceChange / (__.LastPrice - __.PriceChange) * 100m
            |> fun n -> Decimal.Round(n, 2, MidpointRounding.AwayFromZero)

type TradeOrderApiDto = {
    TradeOrderHash : string
    BlockTimestamp : int64
    BlockNumber : int64
    TxPosition : int
    ActionNumber : int16
    AccountHash : string
    BaseAssetHash : string
    QuoteAssetHash : string
    Side : string
    Amount : decimal
    OrderType : string
    LimitPrice : decimal
    StopPrice : decimal
    TrailingOffset : decimal
    TrailingOffsetIsPercentage : bool
    TimeInForce : string
    ExpirationTimestamp : int64
    IsExecutable : bool
    AmountFilled : decimal
    Status : string
    ClosedInBlockNumber : Nullable<int64>
}
