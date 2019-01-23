namespace Own.Blockchain.Common

module Utils =

    let getUnixTimestamp () =
        System.DateTimeOffset.UtcNow.ToUnixTimeSeconds()

    let isRounded dec =
        dec = System.Decimal.Round(dec, 7)
