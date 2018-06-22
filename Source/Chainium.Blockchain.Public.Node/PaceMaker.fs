namespace Chainium.Blockchain.Public.Node

open System
open Chainium.Common
open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Core.Events

module PaceMaker =

    let rec private loop blockCreationInterval =
        async {
            do! Async.Sleep(1000)

            try
                match Composition.advanceToLastKnownBlock () with
                | Ok lastAppliedBlockNumber ->
                    () // TODO: Handle output properly - i.e. publish events
                | Error errors ->
                    Log.appErrors errors

                let (Timestamp lastBlockTimestamp) =
                    match Composition.getLastBlockTimestamp () with
                    | Some timestamp -> timestamp
                    | None -> failwith "Blockchain state is not initialized."

                let timeSinceLastBlock = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - lastBlockTimestamp
                if timeSinceLastBlock >= blockCreationInterval then
                    Composition.createNewBlock ()
                    |> Option.iter (fun result ->
                        match result with
                        | Ok event ->
                            event |> BlockCreated |> Agents.publishEvent
                        | Error errors ->
                            Log.appErrors errors
                    )
            with
            | ex -> Log.error ex.AllMessagesAndStackTraces

            return! loop blockCreationInterval
        }

    let start () =
        Async.Start (Config.BlockCreationInterval |> int64 |> loop)
