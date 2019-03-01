namespace Own.Blockchain.Public.Net.Tests

open System.Collections.Concurrent
open Own.Common
open Own.Blockchain.Common
open Own.Blockchain.Public.Core
open Own.Blockchain.Public.Core.Dtos

module TransportMock =

    let messageQueue = new ConcurrentDictionary<string, ConcurrentQueue<byte[]>>()

    let private packMessage message =
        message |> Serialization.serializeBinary

    let private unpackMessage message =
        message |> Serialization.deserializePeerMessage

    let private send (msg : byte[]) targetAddress =
        let set =
            match messageQueue.TryGetValue targetAddress with
            | true, messages ->
                messages.Enqueue(msg)
                messages
            | _ ->
                let queue = new ConcurrentQueue<byte[]>()
                queue.Enqueue(msg)
                queue

        messageQueue.AddOrUpdate (targetAddress, set, fun _ _ -> set) |> ignore

    let sendGossipDiscoveryMessage gossipDiscoveryMessage targetAddress =
        let msg = packMessage gossipDiscoveryMessage
        send msg targetAddress

    let sendGossipMessage gossipMessage (targetMember: GossipMemberDto) =
        let msg = packMessage gossipMessage
        send msg targetMember.NetworkAddress

    let sendRequestMessage requestMessage targetAddress =
        let msg = packMessage requestMessage
        send msg targetAddress

    let sendResponseMessage requestMessage =
        let msg = packMessage requestMessage
        send msg "" // TODO fix this

    let sendMulticastMessage multicastMessage multicastAddresses =
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
                | true, queue ->
                    let mutable message = Array.empty
                    while queue.TryDequeue &message do
                        match unpackMessage message with
                        | Ok peerMessage -> callback peerMessage
                        | Error error -> Log.error error
                | _ -> ()
                do! Async.Sleep(100)
                return! loop address callback
            }
        Async.Start (loop networkAddress receiveCallback)

    let closeConnection networkAddress =
        messageQueue.TryRemove networkAddress |> ignore

    let closeAllConnections () =
        messageQueue.Clear()
