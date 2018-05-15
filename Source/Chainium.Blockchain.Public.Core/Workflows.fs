namespace Chainium.Blockchain.Public.Core

open System
open Chainium.Common
open Chainium.Blockchain.Public.Core
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Core.Dtos

module Workflows =

    let submitTx verifySignature createHash saveTx (txEnvelopeDto : TxEnvelopeDto) : Result<TxHash, AppErrors> =
        result {
            let! txEnvelope = Validation.validateTxEnvelope txEnvelopeDto
            let! senderAddress, txHash = Validation.verifyTxSignature verifySignature createHash txEnvelope

            let! txDto = Serialization.deserializeTx txEnvelope.RawTx
            let! _ = Validation.validateTx senderAddress txHash txDto

            do! saveTx txHash txEnvelopeDto

            return txHash
        }
