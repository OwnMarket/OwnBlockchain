namespace Chainium.Blockchain.Public.Core

open System
open Chainium.Common
open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Core
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Core.Dtos

module Workflows =

    let submitTx verifySignature saveTx (requestDto : SubmitTxRequestDto) : Result<TxHash, AppErrors> =
        Serialization.deserializeSignedTx requestDto.SignedTx
        >>= (fun signedTx ->
            Validation.verifyTxSignature verifySignature signedTx
            >>= (fun (senderAddress, txHash) ->
                Serialization.deserializeTx signedTx.RawTx
                >>= Validation.validateTx senderAddress txHash
                >>= (fun _ -> saveTx txHash requestDto.SignedTx)
            )
        )
