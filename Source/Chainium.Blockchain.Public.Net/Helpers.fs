namespace Chainium.Blockchain.Public.Net

open System
open System.Collections.Concurrent
open Chainium.Common
open Chainium.Blockchain.Public.Core.DomainTypes

module Helpers =

    let hostToString (NetworkHost a) = a
    let portToInt (NetworkPort p) = p
    let gossipMemberIdToString (GossipMemberId id) = id
    let gossipMessageIdToString id =
        match id with
        | Tx txHash -> txHash |> fun (TxHash t) -> t
        | Block blockNumber -> blockNumber |> fun (BlockNumber b) -> b |> Convert.ToString

    let createNetworkAddress (NetworkHost host) (NetworkPort port) =
        sprintf "%s:%i" host port |> GossipMemberId

    let seqOfKeyValuePairToList seq =
        seq
        |> Map.ofDict
        |> Seq.toList
        |> List.map (fun x -> x.Value)

    module Timer =

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
