namespace rec Own.Blockchain.Public.Core.DomainTypes

open Own.Blockchain.Common

////////////////////////////////////////////////////////////////////////////////////////////////////
// Wallet
////////////////////////////////////////////////////////////////////////////////////////////////////

type PrivateKey = PrivateKey of string
type BlockchainAddress = BlockchainAddress of string

type WalletInfo = {
    PrivateKey : PrivateKey
    Address : BlockchainAddress
}

type Signature = Signature of string

////////////////////////////////////////////////////////////////////////////////////////////////////
// Accounts
////////////////////////////////////////////////////////////////////////////////////////////////////

type AccountHash = AccountHash of string
type AssetHash = AssetHash of string
type AssetCode = AssetCode of string

type Nonce = Nonce of int64
type ChxAmount = ChxAmount of decimal
type AssetAmount = AssetAmount of decimal

////////////////////////////////////////////////////////////////////////////////////////////////////
// Voting
////////////////////////////////////////////////////////////////////////////////////////////////////

type VotingResolutionHash = VotingResolutionHash of string

type VoteHash = VoteHash of string
type VoteWeight = VoteWeight of decimal

type VoteId = {
    AccountHash : AccountHash
    AssetHash : AssetHash
    ResolutionHash : VotingResolutionHash
}

type VoteInfo = {
    VoteId : VoteId
    VoteHash : VoteHash
    VoteWeight : VoteWeight option
}

////////////////////////////////////////////////////////////////////////////////////////////////////
// Eligibility
////////////////////////////////////////////////////////////////////////////////////////////////////

type Eligibility = {
    IsPrimaryEligible : bool
    IsSecondaryEligible : bool
}

type EligibilityInfo = {
    AccountHash : AccountHash
    AssetHash : AssetHash
    Eligibility : Eligibility
    KycControllerAddress : BlockchainAddress
}

type KycProvider = {
    AssetHash : AssetHash
    ProviderAddress : BlockchainAddress
}

////////////////////////////////////////////////////////////////////////////////////////////////////
// Trading
////////////////////////////////////////////////////////////////////////////////////////////////////

type TradingPairInfo = {
    BaseAssetHash : AssetHash
    QuoteAssetHash : AssetHash
    IsEnabled : bool
    LastPrice : AssetAmount
    PriceChange : AssetAmount
}

type TradeOrderHash = TradeOrderHash of string

type TradeOrderSide =
    | Buy
    | Sell

[<RequireQualifiedAccess>]
type TradeOrderType =
    | Market
    | Limit
    | StopMarket
    | StopLimit
    | TrailingStopMarket
    | TrailingStopLimit

[<RequireQualifiedAccess>]
type ExecTradeOrderType =
    | Market
    | Limit

type TradeOrderTimeInForce =
    | GoodTilCancelled
    | ImmediateOrCancel

[<RequireQualifiedAccess>]
type TradeOrderChange =
    | Add
    | Remove
    | Update

type TradeOrderInfo = {
    TradeOrderHash : TradeOrderHash
    BlockTimestamp : Timestamp
    BlockNumber : BlockNumber
    TxPosition : int
    ActionNumber : TxActionNumber
    AccountHash : AccountHash
    BaseAssetHash : AssetHash
    QuoteAssetHash : AssetHash
    Side : TradeOrderSide
    Amount : AssetAmount
    OrderType : TradeOrderType
    LimitPrice : AssetAmount
    StopPrice : AssetAmount
    TrailingOffset : AssetAmount
    TrailingOffsetIsPercentage : bool
    TimeInForce : TradeOrderTimeInForce
    ExpirationTimestamp : Timestamp
    IsExecutable : bool
    AmountFilled : AssetAmount
    Status : TradeOrderStatus
}

type TradeOrderBook = {
    BuyOrders : TradeOrderInfo list
    SellOrders : TradeOrderInfo list
}

[<RequireQualifiedAccess>]
type TradeOrderCancelReason =
    | TriggeredByUser
    | TriggeredByTimeInForce
    | Expired
    | InsufficientQuoteAssetBalance
    | NotEligible

[<RequireQualifiedAccess>]
type TradeOrderStatus =
    | Open
    | Filled
    | Cancelled of TradeOrderCancelReason

type Trade = {
    Direction : TradeOrderSide
    BuyOrderHash : TradeOrderHash
    SellOrderHash : TradeOrderHash
    Amount : AssetAmount
    Price : AssetAmount
}

////////////////////////////////////////////////////////////////////////////////////////////////////
// TX
////////////////////////////////////////////////////////////////////////////////////////////////////

type TransferChxTxAction = {
    RecipientAddress : BlockchainAddress
    Amount : ChxAmount
}

type TransferAssetTxAction = {
    FromAccountHash : AccountHash
    ToAccountHash : AccountHash
    AssetHash : AssetHash
    Amount : AssetAmount
}

type CreateAssetEmissionTxAction = {
    EmissionAccountHash : AccountHash
    AssetHash : AssetHash
    Amount : AssetAmount
}

type SetAccountControllerTxAction = {
    AccountHash : AccountHash
    ControllerAddress : BlockchainAddress
}

type SetAssetControllerTxAction = {
    AssetHash : AssetHash
    ControllerAddress : BlockchainAddress
}

type SetAssetCodeTxAction = {
    AssetHash : AssetHash
    AssetCode : AssetCode
}

type ConfigureValidatorTxAction = {
    NetworkAddress : NetworkAddress
    SharedRewardPercent : decimal
    IsEnabled : bool
}

type DelegateStakeTxAction = {
    ValidatorAddress : BlockchainAddress
    Amount : ChxAmount
}

type SubmitVoteTxAction = {
    VoteId : VoteId
    VoteHash : VoteHash
}

type SubmitVoteWeightTxAction = {
    VoteId : VoteId
    VoteWeight : VoteWeight
}

type SetAccountEligibilityTxAction = {
    AccountHash : AccountHash
    AssetHash : AssetHash
    Eligibility : Eligibility
}

type SetAssetEligibilityTxAction = {
    AssetHash : AssetHash
    IsEligibilityRequired : bool
}

type ChangeKycControllerAddressTxAction = {
    AccountHash : AccountHash
    AssetHash : AssetHash
    KycControllerAddress : BlockchainAddress
}

type AddKycProviderTxAction = {
    AssetHash : AssetHash
    ProviderAddress : BlockchainAddress
}

type RemoveKycProviderTxAction = {
    AssetHash : AssetHash
    ProviderAddress : BlockchainAddress
}

type ConfigureTradingPairTxAction = {
    BaseAssetHash : AssetHash
    QuoteAssetHash : AssetHash
    IsEnabled : bool
}

type PlaceTradeOrderTxAction = {
    AccountHash : AccountHash
    BaseAssetHash : AssetHash
    QuoteAssetHash : AssetHash
    Side : TradeOrderSide
    Amount : AssetAmount
    OrderType : TradeOrderType
    LimitPrice : AssetAmount
    StopPrice : AssetAmount
    TrailingOffset : AssetAmount
    TrailingOffsetIsPercentage : bool
    TimeInForce : TradeOrderTimeInForce
}

type CancelTradeOrderTxAction = {
    TradeOrderHash : TradeOrderHash
}

type TxAction =
    | TransferChx of TransferChxTxAction
    | TransferAsset of TransferAssetTxAction
    | CreateAssetEmission of CreateAssetEmissionTxAction
    | CreateAccount
    | CreateAsset
    | SetAccountController of SetAccountControllerTxAction
    | SetAssetController of SetAssetControllerTxAction
    | SetAssetCode of SetAssetCodeTxAction
    | ConfigureValidator of ConfigureValidatorTxAction
    | RemoveValidator
    | DelegateStake of DelegateStakeTxAction
    | SubmitVote of SubmitVoteTxAction
    | SubmitVoteWeight of SubmitVoteWeightTxAction
    | SetAccountEligibility of SetAccountEligibilityTxAction
    | SetAssetEligibility of SetAssetEligibilityTxAction
    | ChangeKycControllerAddress of ChangeKycControllerAddressTxAction
    | AddKycProvider of AddKycProviderTxAction
    | RemoveKycProvider of RemoveKycProviderTxAction
    | ConfigureTradingPair of ConfigureTradingPairTxAction
    | PlaceTradeOrder of PlaceTradeOrderTxAction
    | CancelTradeOrder of CancelTradeOrderTxAction

type TxHash = TxHash of string

type Tx = {
    TxHash : TxHash
    Sender : BlockchainAddress
    Nonce : Nonce
    ExpirationTime : Timestamp
    ActionFee : ChxAmount
    Actions : TxAction list
}

type TxEnvelope = {
    RawTx : byte[]
    Signature : Signature
}

////////////////////////////////////////////////////////////////////////////////////////////////////
// Blockchain Configuration
////////////////////////////////////////////////////////////////////////////////////////////////////

type ValidatorSnapshot = {
    ValidatorAddress : BlockchainAddress
    NetworkAddress : NetworkAddress
    SharedRewardPercent : decimal
    TotalStake : ChxAmount
}

type BlockchainConfiguration = {
    ConfigurationBlockDelta : int
    Validators : ValidatorSnapshot list
    ValidatorsBlacklist : BlockchainAddress list
    ValidatorDepositLockTime : int16
    ValidatorBlacklistTime : int16
    MaxTxCountPerBlock : int
}

////////////////////////////////////////////////////////////////////////////////////////////////////
// Block
////////////////////////////////////////////////////////////////////////////////////////////////////

type BlockNumber = BlockNumber of int64
type BlockHash = BlockHash of string
type Timestamp = Timestamp of int64 // Unix timestamp in milliseconds
type MerkleTreeRoot = MerkleTreeRoot of string

type BlockHeader = {
    Number : BlockNumber
    Hash : BlockHash
    PreviousHash : BlockHash
    ConfigurationBlockNumber : BlockNumber
    Timestamp : Timestamp
    ProposerAddress : BlockchainAddress
    TxSetRoot : MerkleTreeRoot
    TxResultSetRoot : MerkleTreeRoot
    EquivocationProofsRoot : MerkleTreeRoot
    EquivocationProofResultsRoot : MerkleTreeRoot
    StateRoot : MerkleTreeRoot
    StakingRewardsRoot : MerkleTreeRoot
    ConfigurationRoot : MerkleTreeRoot
    TradesRoot : MerkleTreeRoot
}

type StakingReward = {
    StakerAddress : BlockchainAddress
    Amount : ChxAmount
}

type Block = {
    Header : BlockHeader
    TxSet : TxHash list
    EquivocationProofs : EquivocationProofHash list
    StakingRewards : StakingReward list
    Configuration : BlockchainConfiguration option
    Trades : Trade list
}

type BlockEnvelope = {
    Block : Block
    ConsensusRound : ConsensusRound
    Signatures : Signature list
}

type BlockchainHeadInfo = {
    BlockNumber : BlockNumber
}

////////////////////////////////////////////////////////////////////////////////////////////////////
// Processing
////////////////////////////////////////////////////////////////////////////////////////////////////

type TxActionNumber = TxActionNumber of int16

type TxErrorCode =
    // CHANGING THESE NUMBERS WILL INVALIDATE TX RESULTS MERKLE ROOT IN EXISTING BLOCKS!!!

    // Common
    | TxExpired = 10s
    | ValueTooBig = 50s

    // Address
    | NonceTooLow = 100s
    | InsufficientChxBalance = 110s

    // Holding
    | HoldingNotFound = 210s
    | InsufficientAssetHoldingBalance = 220s

    // Account
    | AccountNotFound = 310s
    | AccountAlreadyExists = 320s
    | SenderIsNotSourceAccountController = 330s
    | SourceAccountNotFound = 340s
    | DestinationAccountNotFound = 350s

    // Asset
    | AssetNotFound = 410s
    | AssetAlreadyExists = 420s
    | AssetCodeAlreadyExists = 430s
    | SenderIsNotAssetController = 440s

    // Voting
    | VoteNotFound = 510s
    | VoteIsAlreadyWeighted = 520s

    // Eligibility
    | EligibilityNotFound = 610s
    | SenderIsNotCurrentKycController = 620s
    | SenderIsNotApprovedKycProvider = 630s
    | SenderIsNotAssetControllerOrApprovedKycProvider = 640s
    | NotEligibleInPrimary = 650s
    | NotEligibleInSecondary = 660s
    | KycProviderAlreadyExists = 670s

    // Trading
    | BaseAssetNotFound = 710s
    | QuoteAssetNotFound = 720s
    | TradingPairNotFound = 730s
    | SenderIsNotTradingPairController = 740s
    | TradeOrderNotFound = 750s
    | InsufficientBaseAssetBalance = 760s
    | InsufficientQuoteAssetBalance = 770s

    // Validators
    | ValidatorNotFound = 910s
    | InsufficientStake = 920s
    | ValidatorIsBlacklisted = 930s
    | ValidatorDepositLocked = 940s

type TxError =
    | TxError of TxErrorCode
    | TxActionError of TxActionNumber * TxErrorCode

type TxStatus =
    | Success
    | Failure of TxError

type TxResult = {
    Status : TxStatus
    BlockNumber : BlockNumber
}

type DistributedDeposit = {
    ValidatorAddress : BlockchainAddress
    Amount : ChxAmount
}

type EquivocationProofResult = {
    DepositTaken : ChxAmount
    DepositDistribution : DistributedDeposit list
    BlockNumber : BlockNumber
}

type PendingTxInfo = {
    TxHash : TxHash
    Sender : BlockchainAddress
    Nonce : Nonce
    ActionFee : ChxAmount
    ActionCount : int16
    AppearanceOrder : int64
}

type ChxAddressState = {
    Nonce : Nonce
    Balance : ChxAmount
}

type HoldingState = {
    Balance : AssetAmount
    IsEmission : bool
}

type VoteState = {
    VoteHash : VoteHash
    VoteWeight : VoteWeight option
}

type EligibilityState = {
    Eligibility: Eligibility
    KycControllerAddress : BlockchainAddress
}

[<RequireQualifiedAccess>]
type KycProviderChange =
    | Add
    | Remove

type AccountState = {
    ControllerAddress : BlockchainAddress
}

type AssetState = {
    AssetCode : AssetCode option
    ControllerAddress : BlockchainAddress
    IsEligibilityRequired : bool
}

type ValidatorState = {
    NetworkAddress : NetworkAddress
    SharedRewardPercent : decimal
    TimeToLockDeposit : int16
    TimeToBlacklist : int16
    IsEnabled : bool
}

[<RequireQualifiedAccess>]
type ValidatorChange =
    | Add
    | Remove
    | Update

type StakeState = {
    Amount : ChxAmount
}

type StakerInfo = {
    StakerAddress : BlockchainAddress
    Amount : ChxAmount
}

type TradingPairState = {
    IsEnabled : bool
    LastPrice : AssetAmount
    PriceChange : AssetAmount
}

type TradeOrderState = {
    BlockTimestamp : Timestamp
    BlockNumber : BlockNumber
    TxPosition : int
    ActionNumber : TxActionNumber
    AccountHash : AccountHash
    BaseAssetHash : AssetHash
    QuoteAssetHash : AssetHash
    Side : TradeOrderSide
    Amount : AssetAmount
    OrderType : TradeOrderType
    LimitPrice : AssetAmount
    StopPrice : AssetAmount
    TrailingOffset : AssetAmount
    TrailingOffsetIsPercentage : bool
    TimeInForce : TradeOrderTimeInForce
    ExpirationTimestamp : Timestamp

    // Execution tracking fields
    IsExecutable : bool
    AmountFilled : AssetAmount
    Status : TradeOrderStatus
}

type ProcessingOutput = {
    TxResults : Map<TxHash, TxResult>
    EquivocationProofResults : Map<EquivocationProofHash, EquivocationProofResult>
    ChxAddresses : Map<BlockchainAddress, ChxAddressState>
    Holdings : Map<AccountHash * AssetHash, HoldingState>
    Votes : Map<VoteId, VoteState>
    Eligibilities : Map<AccountHash * AssetHash, EligibilityState>
    KycProviders : Map<AssetHash, Map<BlockchainAddress, KycProviderChange>>
    Accounts : Map<AccountHash, AccountState>
    Assets : Map<AssetHash, AssetState>
    Validators : Map<BlockchainAddress, ValidatorState * ValidatorChange>
    Stakes : Map<BlockchainAddress * BlockchainAddress, StakeState>
    StakingRewards : Map<BlockchainAddress, ChxAmount>
    TradingPairs : Map<AssetHash * AssetHash, TradingPairState>
    TradeOrders : Map<TradeOrderHash, TradeOrderState * TradeOrderChange>
    Trades : Trade list
}

////////////////////////////////////////////////////////////////////////////////////////////////////
// Consensus
////////////////////////////////////////////////////////////////////////////////////////////////////

[<RequireQualifiedAccess>]
type ConsensusStep =
    | Propose
    | Vote
    | Commit

type ConsensusRound = ConsensusRound of int

type ConsensusMessage =
    | Propose of Block * ConsensusRound
    | Vote of BlockHash option
    | Commit of BlockHash option

type ConsensusMessageEnvelope = {
    BlockNumber : BlockNumber
    Round : ConsensusRound
    ConsensusMessage : ConsensusMessage
    Signature : Signature
}

type ConsensusStateInfo = {
    BlockNumber : BlockNumber
    ConsensusRound : ConsensusRound
    ConsensusStep : ConsensusStep
    LockedBlock : Block option
    LockedRound : ConsensusRound
    ValidBlock : Block option
    ValidRound : ConsensusRound
    ValidBlockSignatures : Signature list
}

type ConsensusStateRequest = {
    ValidatorAddress : BlockchainAddress
    ConsensusRound : ConsensusRound
    TargetValidatorAddress : BlockchainAddress option
}

type ConsensusStateResponse = {
    Messages : ConsensusMessageEnvelope list
    ValidRound : ConsensusRound
    ValidProposal : ConsensusMessageEnvelope option
    ValidVoteSignatures : Signature list
}

type ConsensusCommand =
    | Synchronize
    | Message of BlockchainAddress * ConsensusMessageEnvelope
    | RetryPropose of BlockNumber * ConsensusRound
    | Timeout of BlockNumber * ConsensusRound * ConsensusStep
    | StateRequested of ConsensusStateRequest * PeerNetworkIdentity
    | StateReceived of ConsensusStateResponse

type ConsensusMessageId = ConsensusMessageId of string // Just for the network layer

type EquivocationProofHash = EquivocationProofHash of string

[<RequireQualifiedAccess>]
type EquivocationValue =
    | BlockHash of BlockHash option
    | BlockHashAndValidRound of BlockHash * ConsensusRound

type EquivocationProof = {
    EquivocationProofHash : EquivocationProofHash
    ValidatorAddress : BlockchainAddress
    BlockNumber : BlockNumber
    ConsensusRound : ConsensusRound
    ConsensusStep : ConsensusStep
    EquivocationValue1 : EquivocationValue
    EquivocationValue2 : EquivocationValue
    Signature1 : Signature
    Signature2 : Signature
}

////////////////////////////////////////////////////////////////////////////////////////////////////
// Network
////////////////////////////////////////////////////////////////////////////////////////////////////

type NetworkId = NetworkId of byte[] // Unencoded hash of the network code.
type NetworkAddress = NetworkAddress of string
type PeerNetworkIdentity = PeerNetworkIdentity of byte[]

type NetworkMessageId =
    | Tx of TxHash
    | EquivocationProof of EquivocationProofHash
    | Block of BlockNumber
    | Consensus of ConsensusMessageId
    | ConsensusState
    | BlockchainHead
    | PeerList

type NetworkNodeConfig = {
    Identity : PeerNetworkIdentity
    ListeningAddress : NetworkAddress
    PublicAddress : NetworkAddress option
    BootstrapNodes : NetworkAddress list
    AllowPrivateNetworkPeers : bool
    MaxConnectedPeers : int
    DnsResolverCacheExpirationTime : int
    NetworkSendoutRetryTimeout : int
    PeerMessageMaxSize : int
    DeadPeerExpirationTime : int
}

type GossipNetworkConfig = {
    SessionTimestamp : int64
    FanoutPercentage : int
    GossipDiscoveryIntervalMillis : int
    GossipIntervalMillis : int
    MissedHeartbeatIntervalMillis : int
    PeerResponseThrottlingTime : int
}

type GossipPeer = {
    NetworkAddress : NetworkAddress
    Heartbeat : int64
    SessionTimestamp : int64
}

type GossipPeerInfo = {
    NetworkAddress : NetworkAddress
    SessionTimestamp : int64
    IsDead : bool
    DeadTimestamp : int64 option
}

type GossipDiscoveryMessage = {
    ActivePeers : GossipPeer list
}

type GossipMessage = {
    MessageId : NetworkMessageId
    SenderAddress : NetworkAddress option
    Data : byte[]
}

type MulticastMessage = {
    MessageId : NetworkMessageId
    SenderIdentity : PeerNetworkIdentity option
    Data : byte[]
}

type RequestDataMessage = {
    Items : NetworkMessageId list
    SenderIdentity : PeerNetworkIdentity
}

type ResponseItemMessage = {
    MessageId : NetworkMessageId
    Data : byte[]
}

type ResponseDataMessage = {
    Items : ResponseItemMessage list
}

type PeerMessage =
    | GossipDiscoveryMessage of GossipDiscoveryMessage
    | GossipMessage of GossipMessage
    | MulticastMessage of MulticastMessage
    | RequestDataMessage of RequestDataMessage
    | ResponseDataMessage of ResponseDataMessage

type PeerMessageEnvelope = {
    NetworkId : NetworkId
    PeerMessage : PeerMessage
}

////////////////////////////////////////////////////////////////////////////////////////////////////
// Storage
////////////////////////////////////////////////////////////////////////////////////////////////////

type DbEngineType =
    | Firebird
    | Postgres

////////////////////////////////////////////////////////////////////////////////////////////////////
// Domain Type Logic
////////////////////////////////////////////////////////////////////////////////////////////////////

type NetworkId with
    member __.Value =
        __ |> fun (NetworkId v) -> v

type NetworkAddress with
    member __.Value =
        __ |> fun (NetworkAddress v) -> v

type PeerNetworkIdentity with
    member __.Value =
        __ |> fun (PeerNetworkIdentity v) -> v

type PrivateKey with
    member __.Value =
        __ |> fun (PrivateKey v) -> v

type BlockchainAddress with
    member __.Value =
        __ |> fun (BlockchainAddress v) -> v

type Signature with
    member __.Value =
        __ |> fun (Signature v) -> v

type AccountHash with
    member __.Value =
        __ |> fun (AccountHash v) -> v

type AssetHash with
    member __.Value =
        __ |> fun (AssetHash v) -> v

type AssetCode with
    member __.Value =
        __ |> fun (AssetCode v) -> v

type TxHash with
    member __.Value =
        __ |> fun (TxHash v) -> v

type TxActionNumber with
    member __.Value =
        __ |> fun (TxActionNumber v) -> v

type EquivocationProofHash with
    member __.Value =
        __ |> fun (EquivocationProofHash v) -> v

type BlockHash with
    member __.Value =
        __ |> fun (BlockHash v) -> v

type MerkleTreeRoot with
    member __.Value =
        __ |> fun (MerkleTreeRoot v) -> v

type VotingResolutionHash with
    member __.Value =
        __ |> fun (VotingResolutionHash v) -> v

type VoteHash with
    member __.Value =
        __ |> fun (VoteHash v) -> v

type VoteWeight with
    member __.Value =
        __ |> fun (VoteWeight v) -> v

type Timestamp with
    member __.Value =
        __ |> fun (Timestamp v) -> v
    static member Zero =
        Timestamp 0L
    static member One =
        Timestamp 1L
    static member (+) (Timestamp n1, Timestamp n2) =
        Timestamp (n1 + n2)
    static member (+) (Timestamp n1, n2) =
        Timestamp (n1 + n2)
    static member (+) (Timestamp n1, n2) =
        Timestamp (n1 + int64 n2)
    static member (-) (Timestamp n1, Timestamp n2) =
        Timestamp (n1 - n2)
    static member (-) (Timestamp n1, n2) =
        Timestamp (n1 - n2)
    static member (-) (Timestamp n1, n2) =
        Timestamp (n1 - int64 n2)

type BlockNumber with
    member __.Value =
        __ |> fun (BlockNumber v) -> v
    static member Zero =
        BlockNumber 0L
    static member One =
        BlockNumber 1L
    static member (+) (BlockNumber n1, BlockNumber n2) =
        BlockNumber (n1 + n2)
    static member (+) (BlockNumber n1, n2) =
        BlockNumber (n1 + n2)
    static member (+) (BlockNumber n1, n2) =
        BlockNumber (n1 + int64 n2)
    static member (-) (BlockNumber n1, BlockNumber n2) =
        BlockNumber (n1 - n2)
    static member (-) (BlockNumber n1, n2) =
        BlockNumber (n1 - n2)
    static member (-) (BlockNumber n1, n2) =
        BlockNumber (n1 - int64 n2)

type ConsensusRound with
    member __.Value =
        __ |> fun (ConsensusRound v) -> v
    static member Zero =
        ConsensusRound 0
    static member One =
        ConsensusRound 1
    static member (+) (ConsensusRound n1, ConsensusRound n2) =
        ConsensusRound (n1 + n2)
    static member (+) (ConsensusRound n1, n2) =
        ConsensusRound (n1 + n2)
    static member (-) (ConsensusRound n1, ConsensusRound n2) =
        ConsensusRound (n1 - n2)
    static member (-) (ConsensusRound n1, n2) =
        ConsensusRound (n1 - n2)

type Nonce with
    member __.Value =
        __ |> fun (Nonce v) -> v
    static member Zero =
        Nonce 0L
    static member One =
        Nonce 1L
    static member (+) (Nonce n1, Nonce n2) =
        Nonce (n1 + n2)
    static member (+) (Nonce n1, n2) =
        Nonce (n1 + n2)
    static member (+) (Nonce n1, n2) =
        Nonce (n1 + int64 n2)
    static member (-) (Nonce n1, Nonce n2) =
        Nonce (n1 - n2)
    static member (-) (Nonce n1, n2) =
        Nonce (n1 - n2)
    static member (-) (Nonce n1, n2) =
        Nonce (n1 - int64 n2)

type ChxAmount with
    member __.Value =
        __ |> fun (ChxAmount v) -> v
    member __.Rounded =
        __ |> fun (ChxAmount v) -> Utils.round v 7 |> ChxAmount
    static member Zero =
        ChxAmount 0m
    static member (+) (ChxAmount a1, ChxAmount a2) =
        ChxAmount (a1 + a2)
    static member (+) (ChxAmount a1, a2) =
        ChxAmount (a1 + a2)
    static member (-) (ChxAmount a1, ChxAmount a2) =
        ChxAmount (a1 - a2)
    static member (-) (ChxAmount a1, a2) =
        ChxAmount (a1 - a2)
    static member (*) (ChxAmount a1, ChxAmount a2) =
        ChxAmount (a1 * a2)
    static member (*) (ChxAmount a1, a2) =
        ChxAmount (a1 * a2)
    static member (/) (ChxAmount a1, ChxAmount a2) =
        ChxAmount (a1 / a2)
    static member (/) (ChxAmount a1, a2) =
        ChxAmount (a1 / a2)

type AssetAmount with
    member __.Value =
        __ |> fun (AssetAmount v) -> v
    member __.Rounded =
        __ |> fun (AssetAmount v) -> Utils.round v 7 |> AssetAmount
    static member Zero =
        AssetAmount 0m
    static member (+) (AssetAmount a1, AssetAmount a2) =
        AssetAmount (a1 + a2)
    static member (+) (AssetAmount a1, a2) =
        AssetAmount (a1 + a2)
    static member (-) (AssetAmount a1, AssetAmount a2) =
        AssetAmount (a1 - a2)
    static member (-) (AssetAmount a1, a2) =
        AssetAmount (a1 - a2)
    static member (*) (AssetAmount a1, AssetAmount a2) =
        AssetAmount (a1 * a2)
    static member (*) (AssetAmount a1, a2) =
        AssetAmount (a1 * a2)
    static member (/) (AssetAmount a1, AssetAmount a2) =
        AssetAmount (a1 / a2)
    static member (/) (AssetAmount a1, a2) =
        AssetAmount (a1 / a2)

type TradeOrderHash with
    member __.Value =
        __ |> fun (TradeOrderHash v) -> v

type TradeOrderSide with
    member __.CaseName =
        match __ with
        | Buy -> "Buy"
        | Sell -> "Sell"

type TradeOrderState with
    member __.Time =
        __.BlockNumber, __.TxPosition, __.ActionNumber
    member __.ExecOrderType =
        match __.OrderType with
        | TradeOrderType.Market
        | TradeOrderType.StopMarket
        | TradeOrderType.TrailingStopMarket -> ExecTradeOrderType.Market
        | TradeOrderType.Limit
        | TradeOrderType.StopLimit
        | TradeOrderType.TrailingStopLimit -> ExecTradeOrderType.Limit
    member __.IsStopOrder =
        match __.OrderType with
        | TradeOrderType.Market
        | TradeOrderType.Limit -> false
        | TradeOrderType.StopMarket
        | TradeOrderType.StopLimit
        | TradeOrderType.TrailingStopMarket
        | TradeOrderType.TrailingStopLimit -> true
    member __.IsTrailingStopOrder =
        match __.OrderType with
        | TradeOrderType.Market
        | TradeOrderType.Limit
        | TradeOrderType.StopMarket
        | TradeOrderType.StopLimit -> false
        | TradeOrderType.TrailingStopMarket
        | TradeOrderType.TrailingStopLimit -> true
    member __.AmountRemaining =
        __.Amount - __.AmountFilled

type Tx with
    member __.TotalFee = __.ActionFee * decimal __.Actions.Length

type PendingTxInfo with
    member __.TotalFee = __.ActionFee * decimal __.ActionCount

type ConsensusStep with
    member __.CaseName =
        match __ with
        | Propose -> "Propose"
        | Vote -> "Vote"
        | Commit -> "Commit"

type ConsensusMessage with
    member __.CaseName =
        match __ with
        | Propose _ -> "Propose"
        | Vote _ -> "Vote"
        | Commit _ -> "Commit"

type PeerMessage with
    member __.CaseName =
        match __ with
        | GossipDiscoveryMessage _ -> "GossipDiscoveryMessage"
        | GossipMessage _ -> "GossipMessage"
        | MulticastMessage _ -> "MulticastMessage"
        | RequestDataMessage _ -> "RequestDataMessage"
        | ResponseDataMessage _ -> "ResponseDataMessage"

type DbEngineType with
    member __.CaseName =
        match __ with
        | Firebird -> "Firebird"
        | Postgres -> "Postgres"
