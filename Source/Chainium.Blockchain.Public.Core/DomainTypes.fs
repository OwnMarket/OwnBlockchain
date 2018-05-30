namespace Chainium.Blockchain.Public.Core.DomainTypes

open System


////////////////////////////////////////////////////////////////////////////////////////////////////
// Errors
////////////////////////////////////////////////////////////////////////////////////////////////////

type AppError = AppError of string
type AppErrors = AppError list


////////////////////////////////////////////////////////////////////////////////////////////////////
// Wallet
////////////////////////////////////////////////////////////////////////////////////////////////////

type PrivateKey = PrivateKey of string
type ChainiumAddress = ChainiumAddress of string

type WalletInfo = {
    PrivateKey : PrivateKey
    Address : ChainiumAddress
}

type Signature = {
    V : string
    R : string
    S : string
}


////////////////////////////////////////////////////////////////////////////////////////////////////
// Accounts
////////////////////////////////////////////////////////////////////////////////////////////////////

type AccountHash = AccountHash of string
type EquityID = EquityID of string

type Nonce = Nonce of int64
type ChxAmount = ChxAmount of decimal
type EquityAmount = EquityAmount of decimal

// Arithmetic

type Nonce with
    static member (+) (Nonce n1, Nonce n2) =
        Nonce (n1 + n2)
    static member (+) (Nonce n1, n2) =
        Nonce (n1 + n2)
    static member (-) (Nonce n1, Nonce n2) =
        Nonce (n1 - n2)
    static member (-) (Nonce n1, n2) =
        Nonce (n1 - n2)

type ChxAmount with
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

type EquityAmount with
    static member (+) (EquityAmount a1, EquityAmount a2) =
        EquityAmount (Decimal.Round(a1 + a2, 18))
    static member (+) (EquityAmount a1, a2) =
        EquityAmount (Decimal.Round(a1 + a2, 18))
    static member (-) (EquityAmount a1, EquityAmount a2) =
        EquityAmount (Decimal.Round(a1 - a2, 18))
    static member (-) (EquityAmount a1, a2) =
        EquityAmount (Decimal.Round(a1 - a2, 18))
    static member (*) (EquityAmount a1, EquityAmount a2) =
        EquityAmount (Decimal.Round(a1 * a2, 18))
    static member (*) (EquityAmount a1, a2) =
        EquityAmount (Decimal.Round(a1 * a2, 18))
    static member (/) (EquityAmount a1, EquityAmount a2) =
        EquityAmount (Decimal.Round(a1 / a2, 18))
    static member (/) (EquityAmount a1, a2) =
        EquityAmount (Decimal.Round(a1 / a2, 18))


////////////////////////////////////////////////////////////////////////////////////////////////////
// Tx
////////////////////////////////////////////////////////////////////////////////////////////////////

type TxHash = TxHash of string

type ChxTransferTxAction = {
    RecipientAddress : ChainiumAddress
    Amount : ChxAmount
}

type EquityTransferTxAction = {
    FromAccountHash : AccountHash
    ToAccountHash : AccountHash
    EquityID : EquityID
    Amount : EquityAmount
}

type TxAction =
    | ChxTransfer of ChxTransferTxAction
    | EquityTransfer of EquityTransferTxAction

type Tx = {
    TxHash : TxHash
    Sender : ChainiumAddress
    Nonce : Nonce
    Fee : ChxAmount
    Actions : TxAction list
}

type TxEnvelope = {
    RawTx : byte[]
    Signature : Signature
}


////////////////////////////////////////////////////////////////////////////////////////////////////
// Processing
////////////////////////////////////////////////////////////////////////////////////////////////////

type TxProcessedStatus =
    | Success
    | Failure of AppErrors

type TxStatus =
    | Pending
    | Processed of TxProcessedStatus

type PendingTxInfo = {
    TxHash : TxHash
    Sender : ChainiumAddress
    Nonce : Nonce
    Fee : ChxAmount
    AppearanceOrder : int64
}

type ChxBalanceState = {
    Amount : ChxAmount
    Nonce : Nonce
}

type HoldingState = {
    Amount : EquityAmount
    Nonce : Nonce
}

type ProcessingOutput = {
    TxResults : Map<TxHash, TxProcessedStatus>
    ChxBalances : Map<ChainiumAddress, ChxBalanceState>
    Holdings : Map<AccountHash * EquityID, HoldingState>
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
    Timestamp : Timestamp
    Validator : ChainiumAddress // Fee beneficiary
    TxSetRoot : MerkleTreeRoot
    TxResultSetRoot : MerkleTreeRoot
    StateRoot : MerkleTreeRoot
}

type Block = {
    Header : BlockHeader
    TxSet : TxHash list
}
