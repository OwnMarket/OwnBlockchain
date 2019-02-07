namespace Own.Blockchain.Common

open System

[<RequireQualifiedAccess>]
module Utils =

    /// Unix timestamp in milliseconds.
    let getUnixTimestamp () =
        System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()

    /// Truncates a decimal number to blockchain default of 7 decimal digits.
    let round (x : decimal) =
        let multiplier = 10_000_000m // 7 decimal digits
        Math.Truncate(x * multiplier) / multiplier

    /// Checks if a decimal number is truncated to blockchain default of 7 decimal digits.
    let isRounded x =
        x = round x
