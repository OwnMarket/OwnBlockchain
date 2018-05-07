namespace Chainium.Blockchain.Public.Data

open System
open System.IO
open Chainium.Common
open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Core.DomainTypes

module Raw =

    let saveTx (dataDir : string) (TxHash txHash) (signedTx : string) : Result<unit, AppErrors> =
        // TODO: Implement proper storage for canonical representation of data.
        try
            if not (Directory.Exists(dataDir)) then
                Directory.CreateDirectory(dataDir) |> ignore

            let path = Path.Combine(dataDir, txHash + ".tx")

            if File.Exists(path) then
                Error [AppError (sprintf "Tx with hash %s already exists." txHash)]
            else
                File.WriteAllText(path, signedTx)
                Ok ()
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Error [AppError "Save failed"]
