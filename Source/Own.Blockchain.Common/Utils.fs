namespace Own.Blockchain.Common

open System

[<RequireQualifiedAccess>]
module Utils =

    /// Machine time as Unix timestamp in milliseconds.
    let getMachineTimestamp () =
        System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()

    /// Number of milliseconds network time is ahead (+) or behind (-) local machine time.
    let mutable networkTimeOffset = 0L

    /// Network time as Unix timestamp in milliseconds (local timestamp adjusted for network time offset).
    let getNetworkTimestamp () =
        getMachineTimestamp () + networkTimeOffset

    /// Truncates a decimal number to blockchain default of 7 decimal digits.
    let round (x : decimal) =
        let multiplier = 10_000_000m // 7 decimal digits
        Math.Truncate(x * multiplier) / multiplier

    /// Checks if a decimal number is truncated to blockchain default of 7 decimal digits.
    let isRounded x =
        x = round x
