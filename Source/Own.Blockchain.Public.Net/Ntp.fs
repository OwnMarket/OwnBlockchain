namespace Own.Blockchain.Public.Net

open System
open System.Net
open GuerrillaNtp
open Own.Common.FSharp
open Own.Blockchain.Common

module Ntp =

    let ntpServers =
        [
            "pool.ntp.org"

            "europe.pool.ntp.org"
            "north-america.pool.ntp.org"
            "asia.pool.ntp.org"
            "oceania.pool.ntp.org"
            "south-america.pool.ntp.org"
            "africa.pool.ntp.org"

            "time.windows.com"
            "time.apple.com"
            "time.nist.gov"
            "time.google.com" // Uses leap second smearing
        ]

    let private getNetworkTimeOffsetFromNtpServer ntpServerName =
        try
            Log.infof "Fetching time from NTP server: %s" ntpServerName
            use ntp = new NtpClient(Dns.GetHostAddresses(ntpServerName).[0])
            ntp.GetCorrectionOffset().TotalMilliseconds |> Convert.ToInt64 |> Some
        with
        | ex ->
            Log.error ex.AllMessages
            None

    /// Returns number of milliseconds network time is ahead (+) or behind (-) local machine time.
    let getNetworkTimeOffset () =
        ntpServers
        |> Seq.choose getNetworkTimeOffsetFromNtpServer
        |> Seq.tryHead
        |? 0L
