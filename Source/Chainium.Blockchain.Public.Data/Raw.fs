namespace Chainium.Blockchain.Public.Data

open System
open System.IO
open Newtonsoft.Json
open Chainium.Common
open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Core.Dtos

module Raw =

    let saveTx (dataDir : string) (TxHash txHash) (txEnvelopeDto : TxEnvelopeDto) : Result<unit, AppErrors> =
        // TODO: Implement proper storage for canonical representation of data.
        try
            if not (Directory.Exists(dataDir)) then
                Directory.CreateDirectory(dataDir) |> ignore

            let path = Path.Combine(dataDir, txHash + ".tx")

            if File.Exists(path) then
                Error [AppError (sprintf "Tx with hash %s already exists." txHash)]
            else
                let json = txEnvelopeDto |> JsonConvert.SerializeObject
                File.WriteAllText(path, json)
                Ok ()
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Error [AppError "Save failed"]
