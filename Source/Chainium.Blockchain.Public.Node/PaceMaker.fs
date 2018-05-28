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
                let (Timestamp lastBlockTimestamp) =
                    Composition.getLastBlockTimestamp ()
                    |? Timestamp 0L // TODO: Once genesis block init is added, this should throw.
                let timeSinceLastBlock = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - lastBlockTimestamp
                if timeSinceLastBlock >= blockCreationInterval then
                    Composition.createNewBlock ()
                    |> Option.iter (fun result ->
                        match result with
                        | Ok event ->
                            event |> BlockCreated |> Agents.publishEvent
                        | Error errors ->
                            for (AppError err) in errors do
                                Log.error err
                    )
            with
            | ex -> Log.error ex.AllMessagesAndStackTraces

            return! loop blockCreationInterval
        }

    let start () =
        Async.Start (Config.BlockCreationInterval |> int64 |> loop)
