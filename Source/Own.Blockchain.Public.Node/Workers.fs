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
    // Applier
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let startApplier () =
        let rec loop () =
            async {
                try
                    Composition.acquireAndApplyMissingBlocks ()
                with
                | ex -> Log.error ex.AllMessagesAndStackTraces

                do! Async.Sleep 1000
                return! loop ()
            }

        loop ()
        |> Async.Start

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Proposer
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let startProposer () =
        let rec loop () =
            async {
                try
                    Config.ValidatorPrivateKey
                    |> Option.ofObj
                    |> Option.map (PrivateKey >> Composition.addressFromPrivateKey)
                    |> Option.iter (fun validatorAddress ->
                        if Composition.isValidator validatorAddress then
                            match
                                Composition.getLastAppliedBlockNumber (), Composition.getLastAppliedBlockTimestamp ()
                                with
                            | Some lastAppliedBlockNumber, Some lastBlockTimestamp ->
                                let shouldProposeBlock =
                                    Composition.shouldProposeBlock
                                        validatorAddress
                                        lastAppliedBlockNumber
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
                return! loop ()
            }

        loop ()
        |> Async.Start
