namespace Own.Blockchain.Common

open System

module Utils =

    let getUnixTimestamp () =
        System.DateTimeOffset.UtcNow.ToUnixTimeSeconds()

    let isRounded dec =
        dec = Decimal.Round(dec, 7)
