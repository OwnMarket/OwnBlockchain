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

    let validateTx sender hash (txDto : TxDto) : Result<Tx, AppErrors> =
        // TODO: Implement
        Ok {
            TxHash = hash
            Sender = sender
            Nonce = txDto.Nonce
            Actions = []
            Fee = ChxAmount txDto.Fee
        }
