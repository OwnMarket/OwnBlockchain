namespace Own.Blockchain.Common

open System
open System.Collections.Concurrent
open Own.Common

module Stats =

    type Counter =
        | PeerRequests

    type StatsSummaryEntry = {
        Counter : string
        Value : int64
    }

    type StatsSummary = {
        NodeStartTime : string
        NodeUpTime : string
        NodeCurrentTime : string
        Counters : StatsSummaryEntry list
    }

    let private nodeStartTime = DateTime.UtcNow
    let private counters = new ConcurrentDictionary<Counter, int64>()

    let increment counter =
        counters.AddOrUpdate(counter, 1L, fun _ c -> c + 1L) |> ignore

    let getCurrent () =
        let currentTime = DateTime.UtcNow

        {
            NodeStartTime = nodeStartTime.ToString("u")
            NodeUpTime = currentTime.Subtract(nodeStartTime).ToString("d\.hh\:mm\:ss")
            NodeCurrentTime = currentTime.ToString("u")
            Counters =
                counters
                |> List.ofDict
                |> List.map (fun (k, v) -> {Counter = unionCaseName k; Value = v})
        }
