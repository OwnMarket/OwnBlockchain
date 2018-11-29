namespace Own.Blockchain.Public.Net.Tests

open System.Collections.Concurrent
open Own.Common
open Own.Blockchain.Public.Core
open Own.Blockchain.Public.Core.Dtos

module TransportMock =

    let messageQueue = new ConcurrentDictionary<string, Set<string>>()

    let private packMessage message =
        message |> Serialization.serializePeerMessage

    let private unpackMessage message =
        message |> Serialization.deserializePeerMessage

    let private send (msg : string) targetAddress =
        match messageQueue.TryGetValue targetAddress with
        | true, messages ->
            let set = messages.Add(msg)
            messageQueue.AddOrUpdate (targetAddress, messages.Add(msg), fun _ _ -> messages.Add(msg)) |> ignore
        | _ -> messageQueue.AddOrUpdate (targetAddress, Set.empty.Add(msg), fun _ _ -> Set.empty.Add(msg)) |> ignore

    let sendGossipDiscoveryMessage gossipDiscoveryMessage targetAddress =
        let msg = packMessage gossipDiscoveryMessage
        send msg targetAddress

    let sendGossipMessage gossipMessage (targetMember: GossipMemberDto) =
        let msg = packMessage gossipMessage
        send msg targetMember.NetworkAddress

    let sendUnicastMessage unicastMessage targetAddress =
        let msg = packMessage unicastMessage
        send msg targetAddress

    let sendMulticastMessage senderAddress multicastMessage multicastAddresses =
        let multicastAddresses =
            multicastAddresses
            |> List.filter (fun a -> a <> senderAddress)

        match multicastAddresses with
        | [] -> ()
        | _ ->
            multicastAddresses
            |> Seq.shuffle
            |> Seq.toList
            |> List.iter (fun networkAddress ->
                let msg = packMessage multicastMessage
                send msg networkAddress
            )

    let receiveMessage networkAddress receiveCallback =
        let rec loop address callback =
            async {
                match messageQueue.TryGetValue address with
                | true, messages ->
                    messages
                    |> Set.iter(fun message ->
                        let peerMessage = unpackMessage message
                        callback peerMessage
                    )
                    messageQueue.TryRemove address |> ignore
                | _ -> ()
                do! Async.Sleep(100)
                return! loop address callback
            }
        Async.Start (loop networkAddress receiveCallback)

    let closeConnection networkAddress =
        messageQueue.TryRemove networkAddress |> ignore

    let closeAllConnections () =
        messageQueue.Clear()
