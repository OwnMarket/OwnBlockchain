namespace Chainium.Blockchain.Public.Core

open System
open Chainium.Common
open Chainium.Blockchain.Public.Core
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Core.Dtos
open Chainium.Blockchain.Public.Core.Events

module Mapping =

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Tx
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let txEnvelopeFromDto (dto : TxEnvelopeDto) : TxEnvelope =
        {
            RawTx = dto.Tx |> Convert.FromBase64String
            Signature =
                {
                    V = dto.V
                    R = dto.R
                    S = dto.S
                }
        }

    let txActionFromDto (action : TxActionDto) =
        match action.ActionData with
        | :? ChxTransferTxActionDto as chx ->
            {
                ChxTransferTxAction.RecipientAddress = ChainiumAddress chx.RecipientAddress
                Amount = ChxAmount chx.Amount
            }
            |> ChxTransfer
        | :? EquityTransferTxActionDto as eq ->
            {
                FromAccountHash = AccountHash eq.FromAccount
                ToAccountHash = AccountHash eq.ToAccount
                EquityID = EquityID eq.Equity
                Amount = EquityAmount eq.Amount
            }
            |> EquityTransfer
        | _ ->
            failwith "Invalid action type to map."

    let txFromDto sender hash (dto : TxDto) : Tx =
        {
            TxHash = hash
            Sender = sender
            Nonce = Nonce dto.Nonce
            Fee = ChxAmount dto.Fee
            Actions = dto.Actions |> List.map txActionFromDto
        }

    let pendingTxInfoFromDto (dto : PendingTxInfoDto) : PendingTxInfo =
        {
            TxHash = TxHash dto.TxHash
            Sender = ChainiumAddress dto.SenderAddress
            Nonce = Nonce dto.Nonce
            Fee = ChxAmount dto.Fee
            AppearanceOrder = dto.AppearanceOrder
        }

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Block
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let blockHeaderFromDto (dto : BlockHeaderDto) : BlockHeader =
        {
            BlockHeader.Number = BlockNumber dto.Number
            Hash = BlockHash dto.Hash
            PreviousHash = BlockHash dto.PreviousHash
            Timestamp = Timestamp dto.Timestamp
            Validator = ChainiumAddress dto.Validator
            TxSetRoot = MerkleTreeRoot dto.TxSetRoot
            TxResultSetRoot = MerkleTreeRoot dto.TxResultSetRoot
            StateRoot = MerkleTreeRoot dto.StateRoot
        }

    let blockFromDto (dto : BlockDto) : Block =
        {
            Header = blockHeaderFromDto dto.Header
            TxSet = dto.TxSet |> List.map TxHash
        }

    let blockHeaderToDto (block : BlockHeader) : BlockHeaderDto =
        {
            BlockHeaderDto.Number = block.Number |> fun (BlockNumber n) -> n
            Hash = block.Hash |> fun (BlockHash h) -> h
            PreviousHash = block.PreviousHash |> fun (BlockHash h) -> h
            Timestamp = block.Timestamp |> fun (Timestamp t) -> t
            Validator = block.Validator |> fun (ChainiumAddress a) -> a
            TxSetRoot = block.TxSetRoot |> fun (MerkleTreeRoot r) -> r
            TxResultSetRoot = block.TxResultSetRoot |> fun (MerkleTreeRoot r) -> r
            StateRoot = block.StateRoot |> fun (MerkleTreeRoot r) -> r
        }

    let blockToDto (block : Block) : BlockDto =
        {
            Header = blockHeaderToDto block.Header
            TxSet = block.TxSet |> List.map (fun (TxHash h) -> h)
        }

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // State
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let chxBalanceStateFromDto (dto : ChxBalanceStateDto) : ChxBalanceState =
        {
            Amount = ChxAmount dto.Amount
            Nonce = Nonce dto.Nonce
        }

    let holdingStateFromDto (dto : HoldingStateDto) : HoldingState =
        {
            Amount = EquityAmount dto.Amount
            Nonce = Nonce dto.Nonce
        }

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Events
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let txSubmittedEventToSubmitTxResponseDto (event : TxSubmittedEvent) =
        let (TxHash hash) = event.TxHash
        { SubmitTxResponseDto.TxHash = hash }
