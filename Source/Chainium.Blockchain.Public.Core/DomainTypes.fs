namespace Chainium.Blockchain.Public.Core.DomainTypes

open System


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
// Tx
////////////////////////////////////////////////////////////////////////////////////////////////////

type ChxAmount = ChxAmount of decimal
type AccountHash = AccountHash of string
type EquityID = EquityID of string
type EquityAmount = EquityAmount of decimal

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
    Nonce : int64
    Fee : ChxAmount
    Actions : TxAction list
}

type TxEnvelope = {
    RawTx : byte[]
    Signature : Signature
}

// Processing

type TxProcessedStatus =
    | Success
    | Failure

type TxStatus =
    | Pending
    | Processed of TxProcessedStatus


////////////////////////////////////////////////////////////////////////////////////////////////////
// Block
////////////////////////////////////////////////////////////////////////////////////////////////////

type Timestamp = Timestamp of int64 // UNIX Timestamp
type BlockIndex = BlockIndex of int64
type BlockHash = BlockHash of string
type MerkleTreeRoot = MerkleTreeRoot of string

type BlockHeader = {
    Index : BlockIndex
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
// Errors
////////////////////////////////////////////////////////////////////////////////////////////////////

type AppError = AppError of string
type AppErrors = AppError list
