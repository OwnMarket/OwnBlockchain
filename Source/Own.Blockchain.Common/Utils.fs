namespace Own.Blockchain.Common

open System

[<RequireQualifiedAccess>]
module Utils =

    let getUnixTimestamp () =
        System.DateTimeOffset.UtcNow.ToUnixTimeSeconds()

    let round (x : decimal) =
        let multiplier = 10_000_000m // 7 decimal places
        Math.Truncate(x * multiplier) / multiplier

    let isRounded x =
        x = round x
