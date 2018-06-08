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
type AssetCode = AssetCode of string

type Nonce = Nonce of int64
type ChxAmount = ChxAmount of decimal
type AssetAmount = AssetAmount of decimal

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
    static member Zero =
        ChxAmount 0M
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
    static member Zero =
        AssetAmount 0M
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

////////////////////////////////////////////////////////////////////////////////////////////////////
// Tx
////////////////////////////////////////////////////////////////////////////////////////////////////

type TxHash = TxHash of string

type ChxTransferTxAction = {
    RecipientAddress : ChainiumAddress
    Amount : ChxAmount
}

type AssetTransferTxAction = {
    FromAccountHash : AccountHash
    ToAccountHash : AccountHash
    AssetCode : AssetCode
    Amount : AssetAmount
}

type AccountControllerChangeTxAction = {
    AccountHash : AccountHash
    ControllerAddress : ChainiumAddress
}

type TxAction =
    | ChxTransfer of ChxTransferTxAction
    | AssetTransfer of AssetTransferTxAction
    | AccountControllerChange of AccountControllerChangeTxAction

type Tx = {
    TxHash : TxHash
    Sender : ChainiumAddress
    Nonce : Nonce
    Fee : ChxAmount
    Actions : TxAction list
}
with
    member __.TotalFee = __.Fee * decimal __.Actions.Length

type TxEnvelope = {
    RawTx : byte[]
    Signature : Signature
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

////////////////////////////////////////////////////////////////////////////////////////////////////
// Processing
////////////////////////////////////////////////////////////////////////////////////////////////////

type TxActionNumber = TxActionNumber of int16
type TxErrorCode =
    // DO NOT CHANGE THESE NUMBERS - IT WILL BREAK THE BLOCKS!!!
    | NonceTooLow = 10s
    | InsufficientChxBalance = 20s
    | InsufficientAssetHoldingBalance = 30s
    | SenderIsNotSourceAccountController = 110s
    | SenderIsNotAssetController = 120s

type TxError =
    | TxError of TxErrorCode
    | TxActionError of TxActionNumber * TxErrorCode

type TxProcessedStatus =
    | Success
    | Failure of TxError

type TxStatus =
    | Pending
    | Processed of TxProcessedStatus

type TxResult = {
    Status : TxProcessedStatus
    BlockNumber : BlockNumber
}

type PendingTxInfo = {
    TxHash : TxHash
    Sender : ChainiumAddress
    Nonce : Nonce
    Fee : ChxAmount
    ActionCount : int16
    AppearanceOrder : int64
}
with
    member __.TotalFee = __.Fee * decimal __.ActionCount

type ChxBalanceState = {
    Amount : ChxAmount
    Nonce : Nonce
}

type HoldingState = {
    Amount : AssetAmount
}

type ProcessingOutput = {
    TxResults : Map<TxHash, TxResult>
    ChxBalances : Map<ChainiumAddress, ChxBalanceState>
    Holdings : Map<AccountHash * AssetCode, HoldingState>
    AccountControllerChanges : Map<AccountHash, ChainiumAddress>
}
