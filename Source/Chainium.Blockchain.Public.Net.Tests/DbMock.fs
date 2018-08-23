namespace Chainium.Blockchain.Public.Net.Tests

open System.Collections.Concurrent
open Chainium.Common
open Chainium.Blockchain.Public.Core.Dtos
open Chainium.Blockchain.Public.Core.DomainTypes

module DbMock =

    let peerNodes = new ConcurrentBag<NetworkAddress>()

    let getAllPeerNodes () =
        peerNodes.ToArray ()
        |> List.ofArray

    let savePeerNode networkAddress =
        result {
            match peerNodes.TryPeek (ref networkAddress) with
            | true -> ()
            | false -> peerNodes.Add networkAddress
        }

    let removePeerNode networkAddress =
        result {
            peerNodes.TryTake (ref networkAddress) |> ignore
        }

    let getAllValidators () =
        getAllPeerNodes()
        |> List.map (fun (NetworkAddress n) ->
            {
                ValidatorInfoDto.ValidatorAddress = "CH"
                NetworkAddress = n
            }
        )
