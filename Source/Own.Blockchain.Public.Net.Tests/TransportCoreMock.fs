namespace Own.Blockchain.Public.Net.Tests

open System.Collections.Concurrent
open System.Threading
open Own.Common.FSharp
open Own.Blockchain.Common
open Own.Blockchain.Public.Core
open Own.Blockchain.Public.Core.Dtos
open System

type internal TransportCoreMock
    (
    networkId,
    networkSendoutRetryTimeout,
    socketConnectionTimeout,
    peerMessageMaxSize,
    messageQueue : ConcurrentDictionary<string, ConcurrentQueue<byte[]>>,
    receiveCallback : PeerMessageEnvelopeDto -> unit
    ) =

    let cts = new CancellationTokenSource()

    let packMessage message =
        message |> Serialization.serializeBinary

    let unpackMessage message =
        message |> Serialization.deserializePeerMessageEnvelope

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

    member __.SendGossipDiscoveryMessage targetAddress gossipDiscoveryMessage =
        let msg = packMessage [gossipDiscoveryMessage]
        send msg targetAddress

    member __.SendGossipMessage targetAddress gossipMessage =
        let msg = packMessage [gossipMessage]
        send msg targetAddress

    member __.SendRequestMessage targetAddress requestMessage senderAddress =
        let msg = packMessage [{ requestMessage with PeerMessageId = senderAddress }]
        send msg targetAddress

    member __.SendResponseMessage targetIdentity responseMessage =
        let msg = packMessage [responseMessage]
        send msg targetIdentity

    member __.SendMulticastMessage multicastAddresses multicastMessage =
        match multicastAddresses with
        | [] -> ()
        | _ ->
            multicastAddresses
            |> Seq.shuffle
            |> Seq.toList
            |> List.iter (fun networkAddress ->
                let msg = packMessage [multicastMessage]
                send msg networkAddress
            )

    member __.ReceiveMessage networkAddress =
        let rec loop address callback =
            async {
                match messageQueue.TryGetValue address with
                | true, queue ->
                    let mutable message = Array.empty<byte>
                    while queue.TryDequeue &message do
                        message
                        |> unpackMessage
                        |> Result.handle
                            (List.iter callback)
                            Log.error
                | _ -> ()
                do! Async.Sleep(25)
                return! loop address callback
            }
        Async.Start (loop networkAddress receiveCallback, cts.Token)

    member __.CloseConnection networkAddress =
        messageQueue.TryRemove networkAddress |> ignore

    member __.CloseAllConnections () =
        messageQueue.Clear()
        cts.Cancel()
