namespace Own.Blockchain.Public.Core.DomainTypes

open System

type NetworkAddress = NetworkAddress of string

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
    IsEligible : bool
    IsTransferable : bool
}

type EligibilityInfo = {
    AccountHash : AccountHash
    AssetHash : AssetHash
    Eligibility : Eligibility
    KycControllerAddress : BlockchainAddress
}

type KycController = {
    AssetHash : AssetHash
    ControllerAddress : BlockchainAddress
}

////////////////////////////////////////////////////////////////////////////////////////////////////
// Tx
////////////////////////////////////////////////////////////////////////////////////////////////////

type TxHash = TxHash of string

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

type SetEligibilityTxAction = {
    AccountHash : AccountHash
    AssetHash : AssetHash
    Eligibility : Eligibility
}

type ChangeKycControllerAddressTxAction = {
    AccountHash : AccountHash
    AssetHash : AssetHash
    KycControllerAddress : BlockchainAddress
}

type AddKycControllerTxAction = {
    AssetHash : AssetHash
    ControllerAddress : BlockchainAddress
}

type RemoveKycControllerTxAction = {
    AssetHash : AssetHash
    ControllerAddress : BlockchainAddress
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
    | DelegateStake of DelegateStakeTxAction
    | SubmitVote of SubmitVoteTxAction
    | SubmitVoteWeight of SubmitVoteWeightTxAction
    | SetEligibility of SetEligibilityTxAction
    | ChangeKycControllerAddress of ChangeKycControllerAddressTxAction
    | AddKycController of AddKycControllerTxAction
    | RemoveKycController of RemoveKycControllerTxAction

type Tx = {
    TxHash : TxHash
    Sender : BlockchainAddress
    Nonce : Nonce
    Fee : ChxAmount
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
    Validators : ValidatorSnapshot list
}

////////////////////////////////////////////////////////////////////////////////////////////////////
// Block
////////////////////////////////////////////////////////////////////////////////////////////////////

type Timestamp = Timestamp of int64 // UNIX Timestamp
type BlockNumber = BlockNumber of int64
type BlockHash = BlockHash of string
type MerkleTreeRoot = MerkleTreeRoot of string

type BlockHeader = {
    Number : BlockNumber
    Hash : BlockHash
    PreviousHash : BlockHash
    ConfigurationBlockNumber : BlockNumber
    Timestamp : Timestamp
    ProposerAddress : BlockchainAddress // Fee beneficiary
    TxSetRoot : MerkleTreeRoot
    TxResultSetRoot : MerkleTreeRoot
    StateRoot : MerkleTreeRoot
    StakerRewardsRoot : MerkleTreeRoot
    ConfigurationRoot : MerkleTreeRoot
}

type StakerReward = {
    StakerAddress : BlockchainAddress
    Amount : ChxAmount
}

type Block = {
    Header : BlockHeader
    TxSet : TxHash list
    StakerRewards : StakerReward list
    Configuration : BlockchainConfiguration option
}

type ConsensusRound = ConsensusRound of int

type BlockEnvelope = {
    RawBlock : byte[]
    ConsensusRound : ConsensusRound
    Signatures : Signature list
}

////////////////////////////////////////////////////////////////////////////////////////////////////
// Processing
////////////////////////////////////////////////////////////////////////////////////////////////////

type TxActionNumber = TxActionNumber of int16

type TxErrorCode =
    // CHANGING THESE NUMBERS WILL INVALIDATE TX RESULTS MERKLE ROOT IN EXISTING BLOCKS!!!

    // Address
    | NonceTooLow = 100s
    | InsufficientChxBalance = 110s

    // Holding
    | InsufficientAssetHoldingBalance = 210s

    // Account
    | AccountNotFound = 310s
    | AccountAlreadyExists = 320s
    | SenderIsNotSourceAccountController = 330s
    | SourceAccountNotFound = 340s
    | DestinationAccountNotFound = 350s

    // Asset
    | AssetNotFound = 410s
    | AssetAlreadyExists = 420s
    | SenderIsNotAssetController = 430s

    // Voting
    | VoteNotFound = 510s
    | VoteIsAlreadyWeighted = 520s

    // Eligibility
    | EligibilityNotFound = 610s
    | SenderIsNotCurrentKycController = 620s

    // Validators
    | InsufficientStake = 910s

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

type PendingTxInfo = {
    TxHash : TxHash
    Sender : BlockchainAddress
    Nonce : Nonce
    Fee : ChxAmount
    ActionCount : int16
    AppearanceOrder : int64
}

type ChxBalanceState = {
    Amount : ChxAmount
    Nonce : Nonce
}

type HoldingState = {
    Amount : AssetAmount
}

type VoteState = {
    VoteHash : VoteHash
    VoteWeight : VoteWeight option
}

type EligibilityState = {
    Eligibility: Eligibility
    KycControllerAddress : BlockchainAddress
}

type KycControllerState = {
    AssetHash : AssetHash
    ControllerAddress : BlockchainAddress
}

type KycControllerChange =
    | Add
    | Remove

type AccountState = {
    ControllerAddress : BlockchainAddress
}

type AssetState = {
    AssetCode : AssetCode option
    ControllerAddress : BlockchainAddress
}

type ValidatorState = {
    NetworkAddress : NetworkAddress
    SharedRewardPercent : decimal
}

type StakeState = {
    Amount : ChxAmount
}

type StakerInfo = {
    StakerAddress : BlockchainAddress
    Amount : ChxAmount
}

type ProcessingOutput = {
    TxResults : Map<TxHash, TxResult>
    ChxBalances : Map<BlockchainAddress, ChxBalanceState>
    Holdings : Map<AccountHash * AssetHash, HoldingState>
    Votes : Map<VoteId, VoteState>
    Eligibilities : Map<AccountHash * AssetHash, EligibilityState>
    KycControllers : Map<KycControllerState, KycControllerChange>
    Accounts : Map<AccountHash, AccountState>
    Assets : Map<AssetHash, AssetState>
    Validators : Map<BlockchainAddress, ValidatorState>
    Stakes : Map<BlockchainAddress * BlockchainAddress, StakeState>
    StakerRewards : Map<BlockchainAddress, ChxAmount>
}

////////////////////////////////////////////////////////////////////////////////////////////////////
// Consensus
////////////////////////////////////////////////////////////////////////////////////////////////////

type ConsensusStep =
    | Propose
    | Vote
    | Commit

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

type ConsensusCommand =
    | Synchronize
    | Message of BlockchainAddress * ConsensusMessageEnvelope
    | RetryPropose of BlockNumber * ConsensusRound
    | Timeout of BlockNumber * ConsensusRound * ConsensusStep

type ConsensusMessageId = ConsensusMessageId of string // Just for the network layer

////////////////////////////////////////////////////////////////////////////////////////////////////
// Network
////////////////////////////////////////////////////////////////////////////////////////////////////

type NetworkMessageId =
    | Tx of TxHash
    | Block of BlockNumber
    | Consensus of ConsensusMessageId

type NetworkNodeConfig = {
    NetworkAddress : NetworkAddress
    BootstrapNodes : NetworkAddress list
}

type GossipMember = {
    NetworkAddress : NetworkAddress
    Heartbeat : int64
}

type GossipDiscoveryMessage = {
    ActiveMembers : GossipMember list
}

type GossipMessage = {
    MessageId : NetworkMessageId
    SenderAddress : NetworkAddress
    Data : obj
}

type MulticastMessage = {
    MessageId : NetworkMessageId
    Data : obj
}

type RequestDataMessage = {
    MessageId : NetworkMessageId
    SenderAddress : NetworkAddress
}

type ResponseDataMessage = {
    MessageId : NetworkMessageId
    Data : obj
}

type PeerMessage =
    | GossipDiscoveryMessage of GossipDiscoveryMessage
    | GossipMessage of GossipMessage
    | MulticastMessage of MulticastMessage
    | RequestDataMessage of RequestDataMessage
    | ResponseDataMessage of ResponseDataMessage

////////////////////////////////////////////////////////////////////////////////////////////////////
// Storage
////////////////////////////////////////////////////////////////////////////////////////////////////

type DbEngineType =
    | Firebird
    | PostgreSQL

////////////////////////////////////////////////////////////////////////////////////////////////////
// Domain Type Logic
////////////////////////////////////////////////////////////////////////////////////////////////////

type NetworkAddress with
    member __.Value =
        __ |> fun (NetworkAddress v) -> v

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

type Timestamp with
    member __.Value =
        __ |> fun (Timestamp v) -> v

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
    static member Zero =
        ChxAmount 0m
    static member (+) (ChxAmount a1, ChxAmount a2) =
        ChxAmount (Decimal.Round(a1 + a2, 18))
    static member (+) (ChxAmount a1, a2) =
        ChxAmount (Decimal.Round(a1 + a2, 18))
    static member (-) (ChxAmount a1, ChxAmount a2) =
        ChxAmount (Decimal.Round(a1 - a2, 18))
    static member (-) (ChxAmount a1, a2) =
        ChxAmount (Decimal.Round(a1 - a2, 18))
    static member (*) (ChxAmount a1, ChxAmount a2) =
        ChxAmount (Decimal.Round(a1 * a2, 18))
    static member (*) (ChxAmount a1, a2) =
        ChxAmount (Decimal.Round(a1 * a2, 18))
    static member (/) (ChxAmount a1, ChxAmount a2) =
        ChxAmount (Decimal.Round(a1 / a2, 18))
    static member (/) (ChxAmount a1, a2) =
        ChxAmount (Decimal.Round(a1 / a2, 18))

type AssetAmount with
    member __.Value =
        __ |> fun (AssetAmount v) -> v
    static member Zero =
        AssetAmount 0m
    static member (+) (AssetAmount a1, AssetAmount a2) =
        AssetAmount (Decimal.Round(a1 + a2, 18))
    static member (+) (AssetAmount a1, a2) =
        AssetAmount (Decimal.Round(a1 + a2, 18))
    static member (-) (AssetAmount a1, AssetAmount a2) =
        AssetAmount (Decimal.Round(a1 - a2, 18))
    static member (-) (AssetAmount a1, a2) =
        AssetAmount (Decimal.Round(a1 - a2, 18))
    static member (*) (AssetAmount a1, AssetAmount a2) =
        AssetAmount (Decimal.Round(a1 * a2, 18))
    static member (*) (AssetAmount a1, a2) =
        AssetAmount (Decimal.Round(a1 * a2, 18))
    static member (/) (AssetAmount a1, AssetAmount a2) =
        AssetAmount (Decimal.Round(a1 / a2, 18))
    static member (/) (AssetAmount a1, a2) =
        AssetAmount (Decimal.Round(a1 / a2, 18))

type Tx with
    member __.TotalFee = __.Fee * decimal __.Actions.Length

type PendingTxInfo with
    member __.TotalFee = __.Fee * decimal __.ActionCount
