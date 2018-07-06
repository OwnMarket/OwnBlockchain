namespace Chainium.Blockchain.Public.Faucet

open System
open Chainium.Common
open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Core.Events

module PaceMaker =

    let rec private loop lastDistributionTime distributionInterval =
        async {
            do! Async.Sleep(1000)

            let lastDistributionTime =
                try
                    let timeSinceLastDistribution = Utils.getUnixTimestamp () - lastDistributionTime
                    if timeSinceLastDistribution >= distributionInterval then
                        match Composition.distributeChx (), Composition.distributeAsset () with
                        | None, None -> lastDistributionTime
                        | chxResult, assetResult ->
                            for result in [chxResult; assetResult] do
                                match result with
                                | Some data ->
                                    Log.infof "Distribution output: %s" data
                                | None -> ()
                            Utils.getUnixTimestamp ()
                    else
                        lastDistributionTime
                with
                | ex ->
                    Log.error ex.AllMessagesAndStackTraces
                    lastDistributionTime

            return! loop lastDistributionTime distributionInterval
        }

    let start () =
        Config.DistributionInterval
        |> int64
        |> loop 0L
        |> Async.Start
