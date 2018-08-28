namespace Chainium.Blockchain.Public.Core.Dtos

open System

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
    FromAccount : string
    ToAccount : string
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
type TxEnvelopeDto = {
    Tx : string
    V : string
    R : string
    S : string
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
type TxResultDto = {
    Status : byte
    ErrorCode : Nullable<int16>
    FailedActionNumber : Nullable<int16>
    BlockNumber : int64
}

////////////////////////////////////////////////////////////////////////////////////////////////////
// Block
////////////////////////////////////////////////////////////////////////////////////////////////////

[<CLIMutable>]
type BlockHeaderDto = {
    Number : int64
    Hash : string
    PreviousHash : string
    Timestamp : int64
    Validator : string
    TxSetRoot : string
    TxResultSetRoot : string
    StateRoot : string
}

[<CLIMutable>]
type BlockDto = {
    Header : BlockHeaderDto
    TxSet : string list
}

[<CLIMutable>]
type BlockInfoDto = {
    BlockNumber : int64
    BlockHash : string
    BlockTimestamp : int64
}

[<CLIMutable>]
type BlockEnvelopeDto = {
    Block : string
    V : string
    R : string
    S : string
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
type ValidatorSnapshotDto = {
    ValidatorAddress : string
    NetworkAddress : string
    TotalStake : decimal
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
    ValidatorSnapshots : ValidatorSnapshotDto list
    Stakes : Map<string * string, StakeStateDto>
}

[<CLIMutable>]
type ChxBalanceInfoDto = {
    ChainiumAddress : string
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
// API
////////////////////////////////////////////////////////////////////////////////////////////////////

type SubmitTxResponseDto = {
    TxHash : string
}

type ErrorResponseDto = {
    Errors : string list
}

type GetAddressApiResponseDto = {
    ChainiumAddress : string
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

type GetBlockApiResponseDto = {
    Number : int64
    Hash : string
    PreviousHash : string
    Timestamp : int64
    Validator : string
    TxSetRoot : string
    TxResultSetRoot : string
    StateRoot : string
    TxSet : string list
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
