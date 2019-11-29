namespace Own.Blockchain.Public.Net

open System
open System.Net
open Own.Blockchain.Common
open Own.Blockchain.Public.Core.DomainTypes
open Own.Common.FSharp

module Utils =

    let resolveHostToIpAddress (networkAddress : string) allowPrivatePeers =
        match networkAddress.LastIndexOf ":" with
        | index when index > 0 ->
            let port = networkAddress.Substring(index + 1)
            match UInt16.TryParse port with
            | true, 0us ->
                Log.verbose "Received peer with port 0 discarded"
                None
            | true, _ ->
                try
                    let host = networkAddress.Substring(0, index)
                    let ipAddress =
                        Dns.GetHostAddresses(host)
                        |> Array.sortBy (fun ip -> ip.AddressFamily)
                        |> Array.head
                    let isPrivateIp = ipAddress.IsPrivate()
                    if not allowPrivatePeers && isPrivateIp then
                        Log.verbose "Private IPs are not allowed as peers"
                        None
                    else
                        sprintf "%s:%s" (ipAddress.ToString()) port
                        |> NetworkAddress
                        |> Some
                with
                | ex ->
                    Log.warningf "[%s] %s" networkAddress ex.AllMessages
                    None
            | _ ->
                Log.verbosef "Invalid port value: %s" networkAddress
                None
        | _ ->
            Log.verbosef "Invalid peer format: %s" networkAddress
            None

    let resolveToIpPortPair (networkAddress : string) =
        match networkAddress.LastIndexOf ":" with
        | index when index > 0 ->
            let port = networkAddress.Substring(index + 1)
            match UInt16.TryParse port with
            | true, 0us ->
                Log.verbose "Received peer with port 0 discarded"
                None
            | true, portNumber ->
                let host = networkAddress.Substring(0, index)
                Some (host, int portNumber)
            | _ ->
                Log.verbosef "Invalid port value: %s" networkAddress
                None
        | _ ->
            Log.verbosef "Invalid peer format: %s" networkAddress
            None
