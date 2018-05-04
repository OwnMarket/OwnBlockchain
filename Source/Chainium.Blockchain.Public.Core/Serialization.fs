namespace Chainium.Blockchain.Public.Core

open System
open System.Text
open Chainium.Common
open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Core.Dtos

module Serialization =

    let stringToBytes (str : string) =
        Encoding.UTF8.GetBytes(str)

    let bytesToString (bytes : byte[]) =
        Encoding.UTF8.GetString(bytes)

    let deserializeSignedTx (signedTx : string) : Result<SignedTx, AppErrors> =
        match signedTx.Split(";") with
        | [| rawTx; signaturePartR; signaturePartS |] ->
            Ok {
                RawTx = Convert.FromBase64String(rawTx)
                Signature = { R = signaturePartR; S = signaturePartS }
            }
        | _ -> Error [AppError "SignedTx is expected to have three parts separated by a semicolon ';'."]

    let deserializeTx (rawTx : byte[]) : Result<TxDto, AppErrors> =
        // TODO: Implement
        Ok {
            Nonce = 1L
            Actions = []
            Fee = 1M
        }
