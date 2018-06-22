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
type TxActionDto = {
    ActionType : string
    ActionData : obj
}

[<CLIMutable>]
type TxDto = {
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
    Status : int16
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
type AccountControllerStateDto = {
    ControllerAddress : string
}

[<CLIMutable>]
type AssetControllerStateDto = {
    ControllerAddress : string
}

type ProcessingOutputDto = {
    TxResults : Map<string, TxResultDto>
    ChxBalances : Map<string, ChxBalanceStateDto>
    Holdings : Map<string * string, HoldingStateDto>
    AccountControllers : Map<string, AccountControllerStateDto>
    AssetControllers : Map<string, AssetControllerStateDto>
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
type AccountControllerDto = {
    AccountHash : string
    ControllerAddress : string
}

[<CLIMutable>]
type AssetControllerDto = {
    AssetHash : string
    ControllerAddress : string
}

[<CLIMutable>]
type AccountHoldingsDto = {
    AssetHash: string
    Amount: decimal
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

type GetAddressApiRequestDto = {
    ChainiumAddress : string
}

type GetAddressApiResponseDto = {
    ChainiumAddress : string
    Balance : decimal
    Nonce : int64
}

type GetAccountApiRequestDto = {
    AccountHash : string option
}

type GetAccountApiHoldingDto = {
    AssetHash: string
    Balance: decimal
}

type GetAccountApiResponseDto = {
    AccountHash: string
    ControllerAddress: string
    Holdings: GetAccountApiHoldingDto list
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
    Status : byte
    ErrorCode : Nullable<int16>
    FailedActionNumber : Nullable<int16>
    BlockNumber : Nullable<int64>
}
