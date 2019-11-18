namespace Own.Blockchain.Public.Net.Tests

open System.Collections.Concurrent
open Own.Common.FSharp
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Core.Dtos

module DbMock =

    let private peers = new ConcurrentDictionary<NetworkAddress, GossipPeerInfo list>()

    let getActivePeersFromDb localAddress () =
        match peers.TryGetValue localAddress with
        | true, peerNodes -> peerNodes
        | _ -> []

    let getDeadPeers () =
        []

    let savePeer (localAddress : NetworkAddress) (peerInfo : GossipPeerInfo) =
        result {
            let peerNodes = getActivePeersFromDb localAddress ()
            let newPeerNodes =
                peerInfo :: peerNodes
                |> List.distinctBy (fun p -> p.NetworkAddress)

            peers.AddOrUpdate (localAddress, newPeerNodes, fun _ _ -> newPeerNodes) |> ignore
        }

    let removePeer localAddress (networkAddress : NetworkAddress) =
        result {

            let peerNodes = getActivePeersFromDb localAddress ()
            let newPeerNodes = peerNodes |> List.filter (fun a -> a.NetworkAddress <> networkAddress)
            peers.AddOrUpdate (localAddress, newPeerNodes, fun _ _ -> newPeerNodes) |> ignore
        }

    let getValidators localAddress () =
        getActivePeersFromDb localAddress ()
        |> List.map (fun peer ->
            {
                ValidatorSnapshot.ValidatorAddress = BlockchainAddress "CH"
                NetworkAddress = peer.NetworkAddress
                SharedRewardPercent = 0m
                TotalStake = ChxAmount 0m
            }
        )

    let reset () =
        peers.Clear()
