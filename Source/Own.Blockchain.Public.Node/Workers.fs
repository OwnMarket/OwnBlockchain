namespace Own.Blockchain.Public.Node

open Own.Common
open Own.Blockchain.Common

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
