namespace Own.Blockchain.Public.Node

open Own.Common.FSharp
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

                do! Async.Sleep (Config.NetworkTimePollInterval * 1000)
                return! loop ()
            }

        loop ()
        |> Async.Start

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Blockchain Head Poller
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let startBlockchainHeadPoller () =
        let rec loop () =
            async {
                try
                    Composition.synchronizeBlockchainHead ()
                with
                | ex -> Log.error ex.AllMessagesAndStackTraces

                do! Async.Sleep (Config.BlockchainHeadPollInterval * 1000)
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

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // TX propagation
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let startPendingTxMonitor () =
        let fetchPendingTxInterval = 60 * 1000 // 60 seconds
        let rec loop () =
            async {
                do! Async.Sleep fetchPendingTxInterval
                try
                    Composition.repropagatePendingTx Agents.publishEvent
                with
                | ex -> Log.error ex.AllMessagesAndStackTraces
                return! loop ()
            }

        loop ()
        |> Async.Start
