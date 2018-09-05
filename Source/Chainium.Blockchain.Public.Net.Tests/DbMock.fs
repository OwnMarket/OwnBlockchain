namespace Chainium.Blockchain.Public.Net.Tests

open System.Collections.Concurrent
open Chainium.Common
open Chainium.Blockchain.Public.Core.Dtos
open Chainium.Blockchain.Public.Core.DomainTypes

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

    let getAllValidators localAddress () =
        getAllPeerNodes localAddress ()
        |> List.map (fun (NetworkAddress n) ->
            {
                ValidatorInfoDto.ValidatorAddress = "CH"
                NetworkAddress = n
            }
        )

    let reset () =
        peers.Clear()
