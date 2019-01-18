namespace Own.Blockchain.Common

module Utils =

    let getUnixTimestamp () =
        System.DateTimeOffset.UtcNow.ToUnixTimeSeconds()
