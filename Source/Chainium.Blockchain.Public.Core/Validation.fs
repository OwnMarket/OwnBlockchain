namespace Chainium.Blockchain.Public.Core

open System
open Chainium.Common
open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Core.Dtos

module Validation =

    let validateTxEnvelope (txEnvelopeDto : TxEnvelopeDto) : Result<TxEnvelope, AppErrors> =
        [
            if txEnvelopeDto.Tx.IsNullOrWhiteSpace() then
                yield AppError "Tx is missing from the envelope."
            if txEnvelopeDto.V.IsNullOrWhiteSpace() then
                yield AppError "Signature component V is missing from the envelope."
            if txEnvelopeDto.R.IsNullOrWhiteSpace() then
                yield AppError "Signature component R is missing from the envelope."
            if txEnvelopeDto.S.IsNullOrWhiteSpace() then
                yield AppError "Signature component S is missing from the envelope."
        ]
        |> Errors.orElseWith (fun () ->
            {
                RawTx = txEnvelopeDto.Tx |> Convert.FromBase64String
                Signature =
                    {
                        V = txEnvelopeDto.V
                        R = txEnvelopeDto.R
                        S = txEnvelopeDto.S
                    }
            }
        )

    let verifyTxSignature verifySignature createHash (txEnvelope : TxEnvelope)
        : Result<ChainiumAddress * TxHash, AppErrors> =

        let txHash =
            txEnvelope.RawTx
            |> createHash
            |> TxHash

        match verifySignature txEnvelope.Signature txEnvelope.RawTx with
        | Some chainiumAddress ->
            Ok (chainiumAddress, txHash)
        | None ->
            Error [AppError "Cannot verify signature"]


    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Initial transaction validation
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    let private basicValidation (t : TxDto) =
        let checkForUnknownTransactions actions =
            let knownTransactions =
                [
                    typeof<ChxTransferTxActionDto>
                    typeof<EquityTransferTxActionDto>
                ]

            actions
            |> List.map(fun a -> a.ActionData.GetType())
            |> List.except knownTransactions
            |> List.isEmpty
            |> not

        [
            if t.Nonce < 0L then
                yield AppError "Nonce cannot be negative number."
            if t.Fee <= 0M then
                yield AppError "Fee must be positive."
            if t.Actions |> List.isEmpty then
                yield AppError "There are no actions provided for this transaction."
            if t.Actions |> checkForUnknownTransactions then
                yield AppError "Actions list contains at least one transaction that was not serialized."
        ]

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Validation rules based on transaction type
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    let private validateChxTransfer chx=
        [
            if chx.RecipientAddress.IsNullOrWhiteSpace() then
                yield AppError "Recipient address is not valid."

            if chx.Amount <= 0M then
                yield AppError "Chx transfer amount must be larger than zero."
        ]

    let private validateEquityTransfer eq=
        [
            if eq.FromAccount.IsNullOrWhiteSpace() then
                yield AppError "FromAccount value is not valid."

            if eq.ToAccount.IsNullOrWhiteSpace() then
                yield AppError "ToAccount value is not valid."

            if eq.Equity.IsNullOrWhiteSpace() then
                yield AppError "Equity value is not valid."

            if eq.Amount <= 0M then
                yield AppError "Equity amount must be larger than zero"
        ]

    let rec private validateTransactions (actions : TxActionDto list) (errors : AppError list) =
        let addAndNext newErrors =
            newErrors
            |> List.append errors
            |> validateTransactions actions.Tail

        match actions with
        | [] -> errors
        | head :: tail ->
            match head.ActionData with
            | :? ChxTransferTxActionDto as chx ->
                chx
                |> validateChxTransfer
                |> addAndNext
            | :? EquityTransferTxActionDto as eq ->
                eq
                |> validateEquityTransfer
                |> addAndNext
            | _ ->
                validateTransactions tail errors

    let private mapTransactions actions =
        let map (action : TxActionDto) =
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
                failwith "Invalid transaction type to map."

        actions
        |> List.map(fun a -> map(a))

    let validateTx sender hash (txDto : TxDto) : Result<Tx, AppErrors> =
        txDto
        |> basicValidation
        |> validateTransactions txDto.Actions
        |> Errors.orElseWith(fun () ->
            {
                TxHash = hash
                Sender = sender
                Nonce = txDto.Nonce
                Actions = mapTransactions txDto.Actions
                Fee = ChxAmount txDto.Fee
            }
        )
