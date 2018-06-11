namespace Chainium.Blockchain.Public.Core.Dtos

open System

////////////////////////////////////////////////////////////////////////////////////////////////////
// Tx
////////////////////////////////////////////////////////////////////////////////////////////////////

[<CLIMutable>]
type ChxTransferTxActionDto = {
    RecipientAddress : string
    Amount : decimal
}

[<CLIMutable>]
type AssetTransferTxActionDto = {
    FromAccount : string
    ToAccount : string
    AssetCode : string
    Amount : decimal
}

[<CLIMutable>]
type AccountControllerChangeTxActionDto = {
    AccountHash : string
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
    Status : byte
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

type ProcessingOutputDto = {
    TxResults : Map<string, TxResultDto>
    ChxBalances : Map<string, ChxBalanceStateDto>
    Holdings : Map<string * string, HoldingStateDto>
    AccountControllers : Map<string, AccountControllerStateDto>
}

[<CLIMutable>]
type ChxBalanceInfoDto = {
    ChainiumAddress : string
    ChxBalanceState : ChxBalanceStateDto
}

[<CLIMutable>]
type HoldingInfoDto = {
    AccountHash : string
    AssetCode : string
    HoldingState : HoldingStateDto
}

[<CLIMutable>]
type AccountControllerDto = {
    AccountHash : string
    ControllerAddress : string
}

[<CLIMutable>]
type AssetControllerDto = {
    AssetCode : string
    ControllerAddress : string
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
