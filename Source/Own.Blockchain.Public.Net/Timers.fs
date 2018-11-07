namespace Own.Blockchain.Public.Net

open System.Collections.Concurrent
open Own.Common

module Timers =

    let createTimer tInterval callback =
        let timer = new System.Timers.Timer(float tInterval)
        timer.AutoReset <- false
        timer.Enabled <- true
        timer.Elapsed |> Observable.subscribe callback |> ignore
        timer

    let getTimer timers id =
        let timer =
            timers
            |> Map.ofDict
            |> Map.filter (fun key _ -> key = id)
            |> Seq.toList

        match timer with
            | [t] -> Some t.Value
            | _ -> None

    let restartTimer<'T when 'T : comparison>
        (timers : ConcurrentDictionary<'T, System.Timers.Timer>)
        id
        tInterval
        callback
        =

        match getTimer timers id with
        | Some t ->
            t.Stop()
            t.Dispose()
        | None -> ()

        let timer = createTimer tInterval callback
        timer.Start()
        timers.AddOrUpdate (id, timer, fun _ _ -> timer) |> ignore
