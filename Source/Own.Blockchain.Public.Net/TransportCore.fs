namespace Own.Blockchain.Public.Net

open System
open System.Collections.Concurrent
open System.Collections.Generic
open NetMQ
open NetMQ.Sockets
open Newtonsoft.Json
open Own.Common.FSharp
open Own.Blockchain.Common
open Own.Blockchain.Public.Core
open Own.Blockchain.Public.Core.Dtos

type internal TransportCore
    (
    networkId,
    peerIdentity,
    networkSendoutRetryTimeout,
    receivePeerMessage
    ) =

    let poller = new NetMQPoller()
    let mutable routerSocket : RouterSocket option = None
    let dealerSockets = new ConcurrentDictionary<string, DealerSocket>()
    let multicastMessageQueue = new NetMQQueue<string * PeerMessageEnvelopeDto>()
    let discoveryMessageQueue = new NetMQQueue<string * PeerMessageEnvelopeDto>()
    let gossipMessageQueue = new NetMQQueue<string * PeerMessageEnvelopeDto>()
    let requestsMessageQueue = new NetMQQueue<string * PeerMessageEnvelopeDto>()
    let routerMessageQueue = new NetMQQueue<byte[] * PeerMessageEnvelopeDto>()

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Private
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let mutable receivePeerMessageDispatcher : MailboxProcessor<PeerMessageEnvelopeDto> option = None
    let invokeReceivePeerMessage m =
        match receivePeerMessageDispatcher with
        | Some h -> h.Post m
        | None -> Log.error "ReceivePeerMessage agent is not started"

    let startReceivePeerMessageDispatcher () =
        if receivePeerMessageDispatcher <> None then
            failwith "ReceivePeerMessage agent is already started"

        receivePeerMessageDispatcher <-
            Agent.start <| fun peerMessageEnvelopeDto ->
                async {
                    receivePeerMessage peerMessageEnvelopeDto
                }
            |> Some

    let netMQQueueToDict (queue : NetMQQueue<'T1 *'T2>) =
        let mutable queueItem = Unchecked.defaultof<'T1>, Unchecked.defaultof<'T2>
        let dict = new ConcurrentDictionary<'T1, HashSet<'T2>>()
        while queue.TryDequeue(&queueItem, TimeSpan.FromMilliseconds(100.)) do
            let key, value = queueItem
            match dict.TryGetValue key with
            | true, itemSet ->
                itemSet.Add value |> ignore
            | _ ->
                let itemSet = new HashSet<'T2>()
                itemSet.Add value |> ignore
                dict.AddOrUpdate (key, itemSet, fun _ _ -> itemSet) |> ignore
        dict

    let composeMultipartMessage (msg : byte[]) (identity : byte[] option) =
        let multipartMessage = new NetMQMessage()
        identity |> Option.iter (fun id -> multipartMessage.Append(id))
        multipartMessage.AppendEmptyFrame()
        multipartMessage.Append(msg)
        multipartMessage

    let extractMessageFromMultipart (multipartMessage : NetMQMessage) =
        if multipartMessage.FrameCount = 3 then
            let originatorIdentity = multipartMessage.[0] // TODO: use this instead of SenderIdentity
            let msg = multipartMessage.[2].ToByteArray()
            msg |> Some
        else
            Log.errorf "Invalid message frame count (Expected 3, received %i)" multipartMessage.FrameCount
            None

    let packMessage (identity : byte[] option) message =
        let bytes = message |> Serialization.serializeBinary
        composeMultipartMessage bytes identity

    let unpackMessage message =
        message |> Serialization.deserializePeerMessage

    let receiveMessageCallback (eventArgs : NetMQSocketEventArgs) =
        let mutable message = new NetMQMessage()
        while eventArgs.Socket.TryReceiveMultipartMessage &message do
            extractMessageFromMultipart message
            |> Option.iter (fun msg ->
                msg
                |> unpackMessage
                |> Result.handle
                    (fun peerMessageEnvelopeList ->
                        peerMessageEnvelopeList |> List.iter (fun peerMessageEnvelope ->
                            if peerMessageEnvelope.NetworkId <> networkId then
                                Log.error "Peer message with invalid networkId ignored"
                            else
                                invokeReceivePeerMessage peerMessageEnvelope
                        )
                    )
                    Log.error
            )

    let createDealerSocket targetHost =
        let dealerSocket = new DealerSocket("tcp://" + targetHost)
        dealerSocket.Options.IPv4Only <- false
        dealerSocket.Options.Identity <- peerIdentity
        dealerSocket.ReceiveReady
        |> Observable.subscribe (fun e ->
            let mutable emptyFrame = ""
            while e.Socket.TryReceiveFrameString &emptyFrame do
                let mutable msg = Array.empty<byte>
                if e.Socket.TryReceiveFrameBytes &msg then
                    msg
                    |> unpackMessage
                    |> Result.handle
                        (fun peerMessageEnvelopeList ->
                            peerMessageEnvelopeList |> List.iter (fun peerMessageEnvelope ->
                                if peerMessageEnvelope.NetworkId <> networkId then
                                    Log.error "Peer message with invalid networkId ignored"
                                else
                                    receivePeerMessage peerMessageEnvelope
                            )
                        )
                        Log.error
        )
        |> ignore

        dealerSocket

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Dealer
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let dealerEnqueueMessage (queue: NetMQQueue<_>) msg targetAddress =
        let found, _ = dealerSockets.TryGetValue targetAddress
        if not found then
            try
                let dealerSocket = createDealerSocket targetAddress
                poller.Add dealerSocket
                dealerSockets.AddOrUpdate (targetAddress, dealerSocket, fun _ _ -> dealerSocket) |> ignore
            with
            | :? ObjectDisposedException ->
                Log.error "Poller was disposed while adding socket"
        queue.Enqueue (targetAddress, msg)

    let dealerSendAsync (e : NetMQQueueEventArgs<_ * PeerMessageEnvelopeDto>) =
        e.Queue
        |> netMQQueueToDict
        |> Seq.ofDict
        |> Seq.iter (fun (targetAddress, payload) ->
            match dealerSockets.TryGetValue targetAddress with
            | true, socket ->
                let msg = payload |> List.ofSeq
                let multipartMessage = packMessage None msg
                let timeout = TimeSpan.FromMilliseconds(msg.Length * networkSendoutRetryTimeout |> float)
                if not (socket.TrySendMultipartMessage(timeout, multipartMessage)) then
                    Log.errorf "Could not send message to %s" targetAddress
                    if not socket.IsDisposed then
                        try
                            socket.Disconnect("tcp://" + targetAddress)
                            socket.Connect("tcp://" + targetAddress)
                        with
                        | _ -> Log.error "Could not reset socket state"
            | _ ->
                if not poller.IsRunning then
                    Log.errorf "Socket not found for target %s" targetAddress
        )

    let wireDealerMessageQueueEvents () =
        multicastMessageQueue.ReceiveReady |> Observable.subscribe dealerSendAsync
        |> ignore
        discoveryMessageQueue.ReceiveReady |> Observable.subscribe dealerSendAsync
        |> ignore
        requestsMessageQueue.ReceiveReady |> Observable.subscribe dealerSendAsync
        |> ignore
        gossipMessageQueue.ReceiveReady |> Observable.subscribe dealerSendAsync
        |> ignore

        poller.Add multicastMessageQueue
        poller.Add discoveryMessageQueue
        poller.Add requestsMessageQueue
        poller.Add gossipMessageQueue

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Router
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let routerEnqueueMessage msg =
        routerMessageQueue.Enqueue msg

    let wireRouterMessageQueueEvents () =
        routerMessageQueue.ReceiveReady |> Observable.subscribe (fun e ->
            e.Queue
            |> netMQQueueToDict
            |> Seq.ofDict
            |> Seq.iter (fun (targetIdentity, payload) ->
                let msg = payload |> List.ofSeq
                let multipartMessage = packMessage (Some targetIdentity) msg
                routerSocket |> Option.iter (fun socket ->
                    let timeout = TimeSpan.FromMilliseconds(msg.Length * networkSendoutRetryTimeout |> float)
                    socket.TrySendMultipartMessage(timeout, multipartMessage) |> ignore
                )
            )
        )
        |> ignore

        poller.Add routerMessageQueue

    member __.Init() =
        startReceivePeerMessageDispatcher ()
        wireDealerMessageQueueEvents ()
        wireRouterMessageQueueEvents ()

    member __.ReceiveMessage listeningAddress =
        match routerSocket with
        | Some _ -> ()
        | None ->
            let socket = new RouterSocket("@tcp://" + listeningAddress)
            socket.Options.IPv4Only <- false
            routerSocket <- socket |> Some

        routerSocket |> Option.iter (fun socket ->
            poller.Add socket
            socket.ReceiveReady
            |> Observable.subscribe receiveMessageCallback
            |> ignore
        )
        if not poller.IsRunning then
            poller.RunAsync()

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Send
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    member __.SendGossipDiscoveryMessage gossipDiscoveryMessage targetAddress =
        dealerEnqueueMessage discoveryMessageQueue gossipDiscoveryMessage targetAddress

    member __.SendGossipMessage gossipMessage targetAddress =
        dealerEnqueueMessage gossipMessageQueue gossipMessage targetAddress

    member __.SendRequestMessage requestMessage targetAddress =
        dealerEnqueueMessage requestsMessageQueue requestMessage targetAddress

    member __.SendResponseMessage responseMessage (targetIdentity : byte[]) =
        let msg = targetIdentity, responseMessage
        routerEnqueueMessage msg

    member __.SendMulticastMessage multicastMessage multicastAddresses =
        match multicastAddresses with
        | [] -> ()
        | _ ->
            multicastAddresses
            |> Seq.shuffle
            |> Seq.iter (fun networkAddress ->
                dealerEnqueueMessage multicastMessageQueue multicastMessage networkAddress
            )

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Cleanup
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    member __.CloseConnection remoteAddress =
        match dealerSockets.TryRemove remoteAddress with
        | true, socket ->
            if not socket.IsDisposed then
                poller.RemoveAndDispose socket
        | _ -> ()

    member __.CloseAllConnections () =
        dealerSockets
        |> List.ofDict
        |> List.iter (fun (_, socket) ->
            if not socket.IsDisposed then
                poller.RemoveAndDispose socket
        )
        dealerSockets.Clear()

        routerSocket |> Option.iter (fun socket -> poller.RemoveAndDispose socket)
        routerSocket <- None

        if not multicastMessageQueue.IsDisposed then
            poller.RemoveAndDispose multicastMessageQueue

        if not discoveryMessageQueue.IsDisposed then
            poller.RemoveAndDispose discoveryMessageQueue

        if not requestsMessageQueue.IsDisposed then
            poller.RemoveAndDispose requestsMessageQueue

        if not gossipMessageQueue.IsDisposed then
            poller.RemoveAndDispose gossipMessageQueue

        if poller.IsRunning then
            poller.Dispose()
