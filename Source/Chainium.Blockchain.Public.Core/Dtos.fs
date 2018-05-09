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

[<CLIMutable>]
type TxEnvelopeDto = {
    Tx : string
    V : string
    R : string
    S : string
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
