namespace Chainium.Blockchain.Public.Core.Dtos

open System

[<CLIMutable>]
type ChxTransferTxActionDto = {
    RecipientAddress : string
    Amount : decimal
}

[<CLIMutable>]
type EquityTransferTxActionDto = {
    FromAccount : string
    ToAccount : string
    Equity : string
    Amount : decimal
}

[<CLIMutable>]
type TxActionDto = {
    ActionType : string
    ActionData : obj
}

[<CLIMutable>]
type TxDto = {
    Nonce : int64
    Actions : TxActionDto list
    Fee : decimal
}


////////////////////////////////////////////////////////////////////////////////////////////////////
// API
////////////////////////////////////////////////////////////////////////////////////////////////////

type SubmitTxRequestDto = {
    SignedTx : string
}

type SubmitTxResponseDto = {
    TxHash : string
}

type ErrorResponseDto = {
    Errors : string list
}
