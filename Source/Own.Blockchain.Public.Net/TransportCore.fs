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

    let dealerSockets = new ConcurrentDictionary<string, DealerSocket>()
    let multicastMessages, discoveryMessages, gossipMessages, requestsMessages =
        new NetMQQueue<string * PeerMessageEnvelopeDto>(),
        new NetMQQueue<string * PeerMessageEnvelopeDto>(),
        new NetMQQueue<string * PeerMessageEnvelopeDto>(),
        new NetMQQueue<string * PeerMessageEnvelopeDto>()

    let mutable routerSocket : RouterSocket option = None
    let routerMessages = new NetMQQueue<byte[] * PeerMessageEnvelopeDto>()

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Private
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let composeMultipartMessage (identity : byte[] option) (msg : byte[]) =
        let multipartMessage = new NetMQMessage()
        identity |> Option.iter multipartMessage.Append
        multipartMessage.Append msg
        multipartMessage

    let packMessage (identity : byte[] option) message =
        message
        |> Serialization.serializeBinary
        |> composeMultipartMessage identity

    let unpackMessage message =
        message |> Serialization.deserializePeerMessageEnvelope

    let mutable receivePeerMessageDispatcher : MailboxProcessor<byte[]> option = None
    let invokeReceivePeerMessage m =
        match receivePeerMessageDispatcher with
        | Some h -> h.Post m
        | None -> Log.error "ReceivePeerMessage agent is not started"

    let startReceivePeerMessageDispatcher () =
        if receivePeerMessageDispatcher <> None then
            failwith "ReceivePeerMessage agent is already started"

        receivePeerMessageDispatcher <-
            Agent.start <| fun msg ->
                async {
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
                }
            |> Some

    let groupPeerMessageByTarget (queue : NetMQQueue<'T1 * PeerMessageEnvelopeDto>) =
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

    let extractMessageFromMultipart (multipartMessage : NetMQMessage) =
        if multipartMessage.FrameCount = 2 then
            let originatorIdentity = multipartMessage.[0] // TODO: use this instead of SenderIdentity
            multipartMessage.[1].ToByteArray()
            |> Some
        else
            Log.errorf "Invalid message frame count (Expected 2, received %i)" multipartMessage.FrameCount
            None

    let receiveMessageCallback (eventArgs : NetMQSocketEventArgs) =
        let mutable msg = new NetMQMessage()
        while eventArgs.Socket.TryReceiveMultipartMessage &msg do
            msg
            |> extractMessageFromMultipart
            |> Option.iter invokeReceivePeerMessage

    let createDealerSocket targetHost =
        let dealerSocket = new DealerSocket("tcp://" + targetHost)
        dealerSocket.Options.IPv4Only <- false
        dealerSocket.Options.Identity <- peerIdentity
        dealerSocket.Options.Backlog <- 1000
        // dealerSocket.Options.ReceiveHighWatermark <- 10000
        dealerSocket.ReceiveReady
        |> Observable.subscribe (fun e ->
            let mutable msg = Array.empty<byte>
            while e.Socket.TryReceiveFrameBytes &msg do
                invokeReceivePeerMessage msg
        )
        |> ignore

        dealerSocket

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Dealer
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let dealerEnqueueMessage (peerMessages : NetMQQueue<_>) msg targetAddress =
        let found, _ = dealerSockets.TryGetValue targetAddress
        if not found then
            try
                targetAddress
                |> createDealerSocket
                |> fun socket ->
                    (targetAddress, socket, fun _ _ -> socket)
                    |> dealerSockets.AddOrUpdate
                    |> poller.Add
            with
            | :? ObjectDisposedException ->
                Log.error "Poller was disposed while adding socket"
        peerMessages.Enqueue (targetAddress, msg)

    let dealerSendAsync (peerMessages : NetMQQueue<_>) =
        peerMessages
        |> groupPeerMessageByTarget
        |> Seq.ofDict
        |> Seq.iter (fun (targetAddress, payload) ->
            match dealerSockets.TryGetValue targetAddress with
            | true, socket ->
                let multipartMessage =
                    payload
                    |> List.ofSeq
                    |> packMessage None

                let timeout = TimeSpan.FromMilliseconds(float networkSendoutRetryTimeout)
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
        dealerSendAsync multicastMessages

    let handleDiscoverySendout () =
        handleMulticastSendout ()
        dealerSendAsync discoveryMessages

    let handleRequestsSendout () =
        handleDiscoverySendout ()
        dealerSendAsync requestsMessages

    let handleGossipSendout () =
        handleRequestsSendout ()
        dealerSendAsync gossipMessages

    let wireDealerMessageQueueEvents () =
        multicastMessages.ReceiveReady
        |> Observable.subscribe (fun _ ->
            handleMulticastSendout ()
        )
        |> ignore

        discoveryMessages.ReceiveReady
        |> Observable.subscribe (fun _ ->
            handleDiscoverySendout ()
        )
        |> ignore

        requestsMessages.ReceiveReady
        |> Observable.subscribe (fun _ ->
            handleRequestsSendout ()
        )
        |> ignore

        gossipMessages.ReceiveReady
        |> Observable.subscribe (fun _ ->
            handleGossipSendout ()
        )
        |> ignore

        [
            multicastMessages
            discoveryMessages
            requestsMessages
            gossipMessages
        ]
        |> List.iter poller.Add

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Router
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let routerEnqueueMessage msg =
        routerMessages.Enqueue msg

    let wireRouterMessageQueueEvents () =
        routerMessages.ReceiveReady |> Observable.subscribe (fun e ->
            handleRequestsSendout ()
            e.Queue
            |> groupPeerMessageByTarget
            |> Seq.ofDict
            |> Seq.iter (fun (targetIdentity, payload) ->
                let multipartMessage =
                    payload
                    |> List.ofSeq
                    |> packMessage (Some targetIdentity)

                routerSocket |> Option.iter (fun socket ->
                    let timeout = TimeSpan.FromMilliseconds(float networkSendoutRetryTimeout)
                    if not (socket.TrySendMultipartMessage(timeout, multipartMessage)) then
                        Stats.increment Stats.Counter.FailedMessageSendouts
                        Log.errorf "Could not send response message"
                )
            )
        )
        |> ignore

        poller.Add routerMessages

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
            // socket.Options.ReceiveHighWatermark <- 10000
            routerSocket <- Some socket

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

    member __.SendGossipDiscoveryMessage targetAddress peerMessage =
        dealerEnqueueMessage discoveryMessages peerMessage targetAddress

    member __.SendGossipMessage targetAddress peerMessage =
        dealerEnqueueMessage gossipMessages peerMessage targetAddress

    member __.SendRequestMessage targetAddress peerMessage =
        dealerEnqueueMessage requestsMessages peerMessage targetAddress

    member __.SendResponseMessage (targetIdentity : byte[]) peerMessage =
        routerEnqueueMessage (targetIdentity, peerMessage)

    member __.SendMulticastMessage multicastAddresses peerMessage =
        match multicastAddresses with
        | [] -> ()
        | _ ->
            multicastAddresses
            |> Seq.shuffle
            |> Seq.iter (dealerEnqueueMessage multicastMessages peerMessage)

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

        routerSocket |> Option.iter poller.RemoveAndDispose
        routerSocket <- None

        [
            multicastMessages
            discoveryMessages
            requestsMessages
            gossipMessages
        ]
        |> List.iter (fun queue ->
            if not queue.IsDisposed then
                poller.RemoveAndDispose queue
        )

        if poller.IsRunning then
            poller.Dispose()
