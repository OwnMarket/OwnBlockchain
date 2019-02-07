namespace Own.Blockchain.Public.Net

module Ntp =

    let ntpServers =
        [
            ""
            ""
            ""
        ]

    /// Returns number of milliseconds network time is ahead (+) or behind (-) local machine time.
    let getNetworkTimeOffset () =
        0L // TODO: Implement
