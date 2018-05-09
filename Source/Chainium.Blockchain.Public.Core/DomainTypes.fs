namespace Chainium.Blockchain.Public.Core.DomainTypes

open System

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

type TxHash = TxHash of string
type BlockHash = BlockHash of string
type MerkleTree = MerkleTree of string

type AccountHash = AccountHash of string
type EquityID = EquityID of string
type EquityAmount = EquityAmount of decimal
type ChxAmount = ChxAmount of decimal


////////////////////////////////////////////////////////////////////////////////////////////////////
// Tx
////////////////////////////////////////////////////////////////////////////////////////////////////

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
    Actions : TxAction list
    Fee : ChxAmount
}

type TxEnvelope = {
    RawTx : byte[]
    Signature : Signature
}


////////////////////////////////////////////////////////////////////////////////////////////////////
// Errors
////////////////////////////////////////////////////////////////////////////////////////////////////

type AppError = AppError of string
type AppErrors = AppError list
