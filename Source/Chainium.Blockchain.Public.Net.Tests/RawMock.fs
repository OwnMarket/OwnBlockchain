namespace Chainium.Blockchain.Public.Net.Tests

open System.Collections.Concurrent
open Chainium.Blockchain.Public.Core.DomainTypes

module RawMock =

    let private peerData = new ConcurrentDictionary<NetworkAddress, NetworkMessageId list>()

    let savePeerData address messageId =
        match peerData.TryGetValue address with
        | (true, messageList) ->
            peerData.AddOrUpdate(address, messageId :: messageList, fun _ _ -> messageId :: messageList)
        | _ -> peerData.AddOrUpdate (address, [messageId], fun _ _ -> [messageId])
        |> ignore

    let hasData address messageId =
        match peerData.TryGetValue address with
        | true, messageList ->
            messageList |> List.contains messageId
        | _ -> false

    let reset () =
        peerData.Clear()
