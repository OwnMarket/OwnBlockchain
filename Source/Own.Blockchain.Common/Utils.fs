namespace Own.Blockchain.Common

open System

[<RequireQualifiedAccess>]
module Utils =

    let maxBlockchainNumeric = 99_999_999_999.9_999_999m

    /// Machine time as Unix timestamp in milliseconds.
    let getMachineTimestamp () =
        System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()

    /// Number of milliseconds network time is ahead (+) or behind (-) local machine time.
    let mutable networkTimeOffset = 0L

    /// Network time as Unix timestamp in milliseconds (local timestamp adjusted for network time offset).
    let getNetworkTimestamp () =
        getMachineTimestamp () + networkTimeOffset

    /// Truncates a decimal number to a specific number of decimal digits.
    let round (x : decimal) (digits : int) =
        let multiplier = pown 10 digits |> decimal
        Math.Truncate(x * multiplier) / multiplier

    /// Checks if a decimal number is truncated to blockchain default of 7 decimal digits.
    let isRounded7 x =
        x = round x 7

    /// Checks if a decimal number is truncated to 2 decimal digits.
    let isRounded2 x =
        x = round x 2

    /// Starts async task to repeat "f" waiting "sleepBefore" and "sleepAfter" milliseconds.
    let asyncLoop sleepBefore sleepAfter f =
        let rec loop () =
            async {
                if sleepBefore > 0 then
                    do! Async.Sleep sleepBefore

                f ()

                if sleepAfter > 0 then
                    do! Async.Sleep sleepAfter

                return! loop ()
            }
        loop ()
