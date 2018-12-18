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
type SetValidatorNetworkAddressTxActionDto = {
    NetworkAddress : string
}

[<CLIMutable>]
type DelegateStakeTxActionDto = {
    ValidatorAddress : string
    Amount : decimal
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
    [<Key(2)>] TotalStake : decimal
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
    [<Key(8)>] StateRoot : string
    [<Key(9)>] ConfigurationRoot : string
}

[<CLIMutable>]
[<MessagePackObject>]
type BlockDto = {
    [<Key(0)>] Header : BlockHeaderDto
    [<Key(1)>] TxSet : string list
    [<Key(2)>] Configuration : BlockchainConfigurationDto
}

[<CLIMutable>]
[<MessagePackObject>]
type BlockEnvelopeDto = {
    [<Key(0)>] Block : string
    [<Key(1)>] ConsensusRound : int
    [<Key(2)>] Signatures : string[]
}

[<CLIMutable>]
type BlockInfoDto = {
    BlockNumber : int64
    BlockHash : string
    BlockTimestamp : int64
    IsConfigBlock : bool
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
}

[<CLIMutable>]
type AccountStateDto = {
    ControllerAddress : string
}

[<CLIMutable>]
type AssetStateDto = {
    AssetCode : string
    ControllerAddress : string
}

[<CLIMutable>]
type ValidatorStateDto = {
    NetworkAddress : string
}

[<CLIMutable>]
type StakeStateDto = {
    Amount : decimal
}

type ProcessingOutputDto = {
    TxResults : Map<string, TxResultDto>
    ChxBalances : Map<string, ChxBalanceStateDto>
    Holdings : Map<string * string, HoldingStateDto>
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
type AccountInfoDto = {
    AccountHash : string
    ControllerAddress : string
}

[<CLIMutable>]
type AssetInfoDto = {
    AssetHash : string
    AssetCode : string
    ControllerAddress : string
}

[<CLIMutable>]
type ValidatorInfoDto = {
    ValidatorAddress : string
    NetworkAddress : string
}

[<CLIMutable>]
type StakeInfoDto = {
    StakeholderAddress : string
    ValidatorAddress : string
    StakeState : StakeStateDto
}

[<CLIMutable>]
type AccountHoldingDto = {
    AssetHash : string
    Amount : decimal
}

////////////////////////////////////////////////////////////////////////////////////////////////////
// Consensus
////////////////////////////////////////////////////////////////////////////////////////////////////

// TODO: Find a cleaner way to do this.
type ConsensusMessageDto = {
    ConsensusMessageType : string
    BlockHash : string
    Block : BlockDto
    ValidRound : Nullable<int>
}

type ConsensusMessageEnvelopeDto = {
    BlockNumber : int64
    Round : int
    ConsensusMessage : ConsensusMessageDto
    Signature : string
}

////////////////////////////////////////////////////////////////////////////////////////////////////
// Network
////////////////////////////////////////////////////////////////////////////////////////////////////

[<CLIMutable>]
type GossipMemberDto = {
    NetworkAddress : string
    Heartbeat : int64
}

[<CLIMutable>]
type GossipDiscoveryMessageDto = {
    ActiveMembers : GossipMemberDto list
}

[<CLIMutable>]
type GossipMessageDto = {
    MessageType : string
    MessageId : string
    SenderAddress : string
    Data : obj
}

[<CLIMutable>]
type MulticastMessageDto = {
    MessageType : string
    MessageId : string
    Data : obj
}

[<CLIMutable>]
type RequestDataMessageDto = {
    MessageType : string
    MessageId : string
    SenderAddress : string
}

[<CLIMutable>]
type ResponseDataMessageDto = {
    MessageType : string
    MessageId : string
    Data : obj
}

[<CLIMutable>]
type PeerMessageDto = {
    MessageType : string
    MessageData : obj
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
    ErrorCode : string
    FailedActionNumber : Nullable<int16>
    BlockNumber : Nullable<int64>
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
    StateRoot : string
    ConfigurationRoot : string
    TxSet : string list
    Signatures : string list
    Configuration : BlockchainConfigurationDto
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
