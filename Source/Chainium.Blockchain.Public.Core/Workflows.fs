namespace Chainium.Blockchain.Public.Core

open System
open Chainium.Common
open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Core
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Core.Dtos

module Workflows =

    let submitTx verifySignature createHash saveTx (txEnvelopeDto : TxEnvelopeDto) : Result<TxHash, AppErrors> =
        Validation.validateTxEnvelope txEnvelopeDto
        >>= (fun txEnvelope ->
            Validation.verifyTxSignature verifySignature createHash txEnvelope
            >>= (fun (senderAddress, txHash) ->
                Serialization.deserializeTx txEnvelope.RawTx
                >>= Validation.validateTx senderAddress txHash
                >>= (fun _ -> saveTx txHash txEnvelopeDto)
                |> Result.map (fun _ -> txHash)
            )
        )
