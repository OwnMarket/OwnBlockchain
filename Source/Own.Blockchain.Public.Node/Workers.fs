namespace Own.Blockchain.Public.Node

open Own.Common
open Own.Blockchain.Common

module Workers =

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Network Time Synchronizer
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let startNetworkTimeSynchronizer () =
        let rec loop () =
            async {
                try
                    Composition.updateNetworkTimeOffset ()
                with
                | ex -> Log.error ex.AllMessagesAndStackTraces

                do! Async.Sleep (Config.NetworkTimeUpdateInterval * 60 * 1000) // Minutes to milliseconds
                return! loop ()
            }

        loop ()
        |> Async.Start

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
