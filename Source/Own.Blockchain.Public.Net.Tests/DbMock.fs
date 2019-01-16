namespace Own.Blockchain.Public.Net.Tests

open System.Collections.Concurrent
open Own.Common
open Own.Blockchain.Public.Core.DomainTypes

module DbMock =

    let private peers = new ConcurrentDictionary<NetworkAddress, NetworkAddress list>()

    let getAllPeerNodes localAddress () =
        match peers.TryGetValue localAddress with
        | true, peerNodes -> peerNodes
        | _ -> []

    let savePeerNode localAddress networkAddress =
        result {
            let peerNodes = getAllPeerNodes localAddress ()
            let newPeerNodes = networkAddress :: peerNodes |> List.distinct
            peers.AddOrUpdate (localAddress, newPeerNodes, fun _ _ -> newPeerNodes) |> ignore
        }

    let removePeerNode localAddress networkAddress =
        result {

            let peerNodes = getAllPeerNodes localAddress ()
            let newPeerNodes = peerNodes |> List.filter (fun a -> a <> networkAddress)
            peers.AddOrUpdate (localAddress, newPeerNodes, fun _ _ -> newPeerNodes) |> ignore
        }

    let getValidators localAddress () =
        getAllPeerNodes localAddress ()
        |> List.map (fun (NetworkAddress n) ->
            {
                ValidatorSnapshot.ValidatorAddress = BlockchainAddress "CH"
                NetworkAddress = NetworkAddress n
                SharedRewardPercent = 0m
                TotalStake = ChxAmount 0m
            }
        )

    let reset () =
        peers.Clear()
