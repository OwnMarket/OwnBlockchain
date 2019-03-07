namespace Own.Blockchain.Public.Net.Tests

open System.Collections.Concurrent
open Own.Common.FSharp
open Own.Blockchain.Common
open Own.Blockchain.Public.Core
open Own.Blockchain.Public.Core.Dtos

type internal TransportCoreMock
    (
    networkId,
    peerIdentity,
    receiveCallback : PeerMessageEnvelopeDto -> unit
    ) =

    let messageQueue = new ConcurrentDictionary<string, ConcurrentQueue<byte[]>>()

    let packMessage message =
        message |> Serialization.serializeBinary

    let unpackMessage message =
        message |> Serialization.deserializePeerMessage

    let send (msg : byte[]) targetAddress =
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

    member __.SendGossipDiscoveryMessage gossipDiscoveryMessage targetAddress =
        let msg = packMessage gossipDiscoveryMessage
        send msg targetAddress

    member __.SendGossipMessage gossipMessage (targetMember: GossipMemberDto) =
        let msg = packMessage gossipMessage
        send msg targetMember.NetworkAddress

    member __.SendRequestMessage requestMessage targetAddress =
        let msg = packMessage requestMessage
        send msg targetAddress

    member __.SendResponseMessage requestMessage (targetIdentity : byte[]) =
        let msg = packMessage requestMessage
        send msg ""

    member __.SendMulticastMessage multicastMessage multicastAddresses =
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

    member __.ReceiveMessage networkAddress =
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

    member __.CloseConnection networkAddress =
        messageQueue.TryRemove networkAddress |> ignore

    member __.CloseAllConnections () =
        messageQueue.Clear()
