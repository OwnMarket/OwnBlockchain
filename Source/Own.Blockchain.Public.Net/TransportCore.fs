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
    let multicastMessageQueue = new NetMQQueue<string * NetMQMessage>()
    let discoveryMessageQueue = new NetMQQueue<string * NetMQMessage>()
    let requestMessageQueue = new NetMQQueue<string * NetMQMessage>()
    let gossipMessageQueue = new NetMQQueue<string * NetMQMessage>()
    let routerMessageQueue = new NetMQQueue<NetMQMessage>()

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

    let packMessage message (identity : byte[] option) =
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
                    (fun peerMessageEnvelope ->
                        if peerMessageEnvelope.NetworkId <> networkId then
                            Log.error "Peer message with invalid networkId ignored"
                        else
                            invokeReceivePeerMessage peerMessageEnvelope
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
                        (fun peerMessageEnvelope ->
                            if peerMessageEnvelope.NetworkId <> networkId then
                                Log.error "Peer message with invalid networkId ignored"
                            else
                                receivePeerMessage peerMessageEnvelope
                        )
                        Log.error
        )
        |> ignore

        dealerSocket

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Dealer
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let dealerSendAsync (queue : NetMQQueue<string * NetMQMessage>) (msg : NetMQMessage) targetAddress =
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

    let sendMessageCallback (e : NetMQQueueEventArgs<string * NetMQMessage>) =
        let mutable message = "", new NetMQMessage()

        // Deduplicate messages.
        let messagesSet = new HashSet<string * NetMQMessage>()
        while e.Queue.TryDequeue(&message, TimeSpan.FromMilliseconds(100.)) do
            messagesSet.Add message |> ignore

        messagesSet |> Seq.iter (fun message ->
            let targetAddress, payload = message
            match dealerSockets.TryGetValue targetAddress with
            | true, socket ->
                let timeout = TimeSpan.FromMilliseconds(networkSendoutRetryTimeout |> float)
                if not (socket.TrySendMultipartMessage(timeout, payload)) then
                    Log.errorf "Could not send message to %s" targetAddress
                    if not socket.IsDisposed then
                        try
                            socket.Disconnect("tcp://" + targetAddress)
                            socket.Connect("tcp://" + targetAddress)
                        with
                        | _ -> Log.error "Could not reset socket state"
            | _ ->
                Log.errorf "Socket not found for target %s" targetAddress
        )

    let wireDealerMessageQueueEvents () =
        multicastMessageQueue.ReceiveReady |> Observable.subscribe sendMessageCallback
        |> ignore
        discoveryMessageQueue.ReceiveReady |> Observable.subscribe sendMessageCallback
        |> ignore
        requestMessageQueue.ReceiveReady |> Observable.subscribe sendMessageCallback
        |> ignore
        gossipMessageQueue.ReceiveReady |> Observable.subscribe sendMessageCallback
        |> ignore

        poller.Add multicastMessageQueue
        poller.Add discoveryMessageQueue
        poller.Add requestMessageQueue
        poller.Add gossipMessageQueue

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Router
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let routerSendAsync (msg : NetMQMessage) =
        routerMessageQueue.Enqueue msg

    let wireRouterMessageQueueEvents () =
        routerMessageQueue.ReceiveReady |> Observable.subscribe (fun e ->
            let mutable message = new NetMQMessage()

            // Deduplicate messages.
            let messagesSet = new HashSet<NetMQMessage>()
            while e.Queue.TryDequeue(&message, TimeSpan.FromMilliseconds(100.)) do
                messagesSet.Add message |> ignore

            messagesSet |> Seq.iter (fun message ->
                routerSocket |> Option.iter (fun socket ->
                    let timeout = TimeSpan.FromMilliseconds(networkSendoutRetryTimeout |> float)
                    socket.TrySendMultipartMessage(timeout, message) |> ignore
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
        let msg = packMessage gossipDiscoveryMessage None
        dealerSendAsync discoveryMessageQueue msg targetAddress

    member __.SendGossipMessage gossipMessage (targetMember: GossipMemberDto) =
        let msg = packMessage gossipMessage None
        dealerSendAsync gossipMessageQueue msg targetMember.NetworkAddress

    member __.SendRequestMessage requestMessage targetAddress =
        let msg = packMessage requestMessage None
        dealerSendAsync requestMessageQueue msg targetAddress

    member __.SendResponseMessage responseMessage (targetIdentity : byte[]) =
        let msg = packMessage responseMessage (targetIdentity |> Some)
        routerSendAsync msg

    member __.SendMulticastMessage multicastMessage multicastAddresses =
        match multicastAddresses with
        | [] -> ()
        | _ ->
            multicastAddresses
            |> Seq.shuffle
            |> Seq.iter (fun networkAddress ->
                let msg = packMessage multicastMessage None
                dealerSendAsync multicastMessageQueue msg networkAddress
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

        if not requestMessageQueue.IsDisposed then
            poller.RemoveAndDispose requestMessageQueue

        if not gossipMessageQueue.IsDisposed then
            poller.RemoveAndDispose gossipMessageQueue

        if poller.IsRunning then
            poller.Dispose()
