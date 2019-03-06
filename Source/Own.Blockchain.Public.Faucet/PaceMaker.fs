namespace Own.Blockchain.Public.Faucet

open Own.Common.FSharp
open Own.Blockchain.Common

module PaceMaker =

    let rec private loop lastDistributionTime distributionInterval =
        async {
            do! Async.Sleep(1000)

            let lastDistributionTime =
                try
                    let timeSinceLastDistribution = Utils.getMachineTimestamp () - lastDistributionTime
                    if timeSinceLastDistribution >= distributionInterval then
                        match Composition.distributeChx (), Composition.distributeAsset () with
                        | None, None -> lastDistributionTime
                        | chxResult, assetResult ->
                            for result in [chxResult; assetResult] do
                                result |> Option.iter (Log.infof "Distribution output: %s")
                            Utils.getMachineTimestamp ()
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
