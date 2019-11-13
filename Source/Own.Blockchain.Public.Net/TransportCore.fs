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
    peerMessageMaxSize,
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
            Agent.start <| fun envelope ->
                async {
                    receivePeerMessage envelope
                }
            |> Some

    let netMQQueueToDict (queue : NetMQQueue<'T1 * PeerMessageEnvelopeDto>) =
        let mutable queueItem = Unchecked.defaultof<'T1>, Unchecked.defaultof<PeerMessageEnvelopeDto>
        let dict = new ConcurrentDictionary<'T1, HashSet<PeerMessageEnvelopeDto>>()
        let mutable peerMessageSize = 0
        while (peerMessageMaxSize = 0 || peerMessageSize <= peerMessageMaxSize)
            && queue.TryDequeue(&queueItem, TimeSpan.Zero) do
            let target, peerMessageEnvelope = queueItem
            peerMessageSize <- peerMessageSize + peerMessageEnvelope.PeerMessage.MessageData.Length
            match dict.TryGetValue target with
            | true, peerMessages ->
                peerMessages.Add peerMessageEnvelope |> ignore
            | _ ->
                let peerMessages = new HashSet<PeerMessageEnvelopeDto>()
                peerMessages.Add peerMessageEnvelope |> ignore
                dict.AddOrUpdate (target, peerMessages, fun _ _ -> peerMessages) |> ignore
        dict

    let composeMultipartMessage (msg : byte[]) (identity : byte[] option) =
        let multipartMessage = new NetMQMessage()
        identity |> Option.iter (fun id -> multipartMessage.Append(id))
        multipartMessage.Append(msg)
        multipartMessage

    let extractMessageFromMultipart (multipartMessage : NetMQMessage) =
        if multipartMessage.FrameCount = 2 then
            let originatorIdentity = multipartMessage.[0] // TODO: use this instead of SenderIdentity
            let msg = multipartMessage.[1].ToByteArray()
            msg |> Some
        else
            Log.errorf "Invalid message frame count (Expected 2, received %i)" multipartMessage.FrameCount
            None

    let packMessage (identity : byte[] option) message =
        let bytes = message |> Serialization.serializeBinary
        composeMultipartMessage bytes identity

    let unpackMessage message =
        message |> Serialization.deserializePeerMessageEnvelope

    let receiveMessageCallback (eventArgs : NetMQSocketEventArgs) =
        let mutable message = new NetMQMessage()
        while eventArgs.Socket.TryReceiveMultipartMessage &message do
            extractMessageFromMultipart message
            |> Option.iter (fun msg ->
                msg
                |> unpackMessage
                |> Result.handle
                    (
                        List.iter (fun envelope ->
                            if envelope.NetworkId <> networkId then
                                Log.error "Peer message with invalid networkId ignored"
                            else
                                invokeReceivePeerMessage envelope
                        )
                    )
                    Log.error
            )

    let createDealerSocket targetHost =
        let dealerSocket = new DealerSocket("tcp://" + targetHost)
        dealerSocket.Options.IPv4Only <- false
        dealerSocket.Options.Identity <- peerIdentity
        dealerSocket.Options.Backlog <- 1000
        dealerSocket.ReceiveReady
        |> Observable.subscribe (fun e ->
            let mutable msg = Array.empty<byte>
            while e.Socket.TryReceiveFrameBytes &msg do
                msg
                |> unpackMessage
                |> Result.handle
                    (
                        List.iter (fun envelope ->
                            if envelope.NetworkId <> networkId then
                                Log.error "Peer message with invalid networkId ignored"
                            else
                                receivePeerMessage envelope
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

    let dealerSendAsync (queue : NetMQQueue<_>) =
        queue
        |> netMQQueueToDict
        |> Seq.ofDict
        |> Seq.iter (fun (targetAddress, payload) ->
            match dealerSockets.TryGetValue targetAddress with
            | true, socket ->
                let msg = payload |> List.ofSeq
                let multipartMessage = packMessage None msg
                let timeout = TimeSpan.FromMilliseconds(networkSendoutRetryTimeout |> float)
                if not (socket.TrySendMultipartMessage(timeout, multipartMessage)) then
                    Stats.increment Stats.Counter.FailedMessageSendouts
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

    let handleMulticastSendout () =
        dealerSendAsync multicastMessageQueue

    let handleDiscoverySendout () =
        handleMulticastSendout ()
        dealerSendAsync discoveryMessageQueue

    let handleRequestsSendout () =
        handleDiscoverySendout ()
        dealerSendAsync requestsMessageQueue

    let handleGossipSendout () =
        handleRequestsSendout ()
        dealerSendAsync gossipMessageQueue

    let wireDealerMessageQueueEvents () =
        multicastMessageQueue.ReceiveReady
        |> Observable.subscribe (fun _ ->
            handleMulticastSendout ()
        )
        |> ignore

        discoveryMessageQueue.ReceiveReady
        |> Observable.subscribe (fun _ ->
            handleDiscoverySendout ()
        )
        |> ignore

        requestsMessageQueue.ReceiveReady
        |> Observable.subscribe (fun _ ->
            handleRequestsSendout ()
        )
        |> ignore

        gossipMessageQueue.ReceiveReady
        |> Observable.subscribe (fun _ ->
            handleGossipSendout ()
        )
        |> ignore

        [
            multicastMessageQueue
            discoveryMessageQueue
            requestsMessageQueue
            gossipMessageQueue
        ]
        |> List.iter poller.Add

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Router
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let routerEnqueueMessage msg =
        routerMessageQueue.Enqueue msg

    let wireRouterMessageQueueEvents () =
        routerMessageQueue.ReceiveReady |> Observable.subscribe (fun e ->
            handleRequestsSendout ()
            e.Queue
            |> netMQQueueToDict
            |> Seq.ofDict
            |> Seq.iter (fun (targetIdentity, payload) ->
                let msg = payload |> List.ofSeq
                let multipartMessage = packMessage (Some targetIdentity) msg
                routerSocket |> Option.iter (fun socket ->
                    let timeout = TimeSpan.FromMilliseconds(networkSendoutRetryTimeout |> float)
                    if not (socket.TrySendMultipartMessage(timeout, multipartMessage)) then
                        Stats.increment Stats.Counter.FailedMessageSendouts
                        Log.errorf "Could not send response message"
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
            socket.Options.Backlog <- 1000
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

        [
            multicastMessageQueue
            discoveryMessageQueue
            requestsMessageQueue
            gossipMessageQueue
        ]
        |> List.iter (fun queue ->
            if not queue.IsDisposed then
                poller.RemoveAndDispose queue
        )

        if poller.IsRunning then
            poller.Dispose()
