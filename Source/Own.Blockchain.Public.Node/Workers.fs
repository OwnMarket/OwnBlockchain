namespace Own.Blockchain.Public.Node

open System
open Own.Common
open Own.Blockchain.Common
open Own.Blockchain.Public.Core
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Core.Events
open Own.Blockchain.Public.Net

module Workers =

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Fetcher
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let startFetcher () =
        let rec loop () =
            async {
                try
                    Composition.fetchMissingBlocks Agents.publishEvent
                with
                | ex -> Log.error ex.AllMessagesAndStackTraces

                do! Async.Sleep 1000
                return! loop ()
            }

        loop ()
        |> Async.Start
