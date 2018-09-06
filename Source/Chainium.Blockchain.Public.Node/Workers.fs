namespace Chainium.Blockchain.Public.Node

open System
open Chainium.Common
open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Core
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Core.Events
open Chainium.Blockchain.Public.Net

module Workers =

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Applier
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let rec private applierLoop () =
        async {
            try
                match Composition.getLastBlockNumber () with
                | Some lastAppliedBlockNumber -> Composition.acquireAndApplyMissingBlocks ()
                | None -> failwith "Cannot load last applied block info."
            with
            | ex -> Log.error ex.AllMessagesAndStackTraces

            do! Async.Sleep 1000
            return! applierLoop ()
        }

    let startApplier () =
        applierLoop ()
        |> Async.Start

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Proposer
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let rec private proposerLoop () =
        async {
            try
                Config.ValidatorPrivateKey
                |> Option.ofObj
                |> Option.map (PrivateKey >> Composition.addressFromPrivateKey)
                |> Option.iter (fun validatorAddress ->
                    if Composition.isValidator validatorAddress then
                        match Composition.getLastBlockNumber (), Composition.getLastBlockTimestamp () with
                        | Some lastAppliedBlockNumber, Some lastBlockTimestamp ->
                            let shouldProposeBlock =
                                Composition.shouldProposeBlock
                                    validatorAddress
                                    (lastAppliedBlockNumber + 1L)
                                    lastBlockTimestamp
                                    (Utils.getUnixTimestamp () |> Timestamp)
                            if shouldProposeBlock then
                                Composition.proposeBlock lastAppliedBlockNumber
                                |> Option.iter (fun result ->
                                    match result with
                                    | Ok event ->
                                        event |> BlockCreated |> Agents.publishEvent
                                    | Error errors ->
                                        Log.appErrors errors
                                )
                        | _ -> failwith "Cannot load last applied block info."
                )
            with
            | ex -> Log.error ex.AllMessagesAndStackTraces

            do! Async.Sleep 1000
            return! proposerLoop ()
        }

    let startProposer () =
        proposerLoop ()
        |> Async.Start
