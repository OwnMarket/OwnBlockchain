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
    receivePeerMessage : PeerMessageEnvelopeDto -> unit
    ) =

    let poller = new NetMQPoller()
    let mutable routerSocket : RouterSocket option = None
    let dealerSockets = new ConcurrentDictionary<string, DealerSocket>()
    let dealerMessageQueue = new NetMQQueue<string * NetMQMessage>()
    let routerMessageQueue = new NetMQQueue<NetMQMessage>()

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Private
    ////////////////////////////////////////////////////////////////////////////////////////////////////

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
        if eventArgs.Socket.TryReceiveMultipartMessage &message then
            extractMessageFromMultipart message
            |> Option.iter (fun msg ->
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
        else
            Log.warning "An error has occurred while trying to read the message"

    let createDealerSocket targetHost =
        let dealerSocket = new DealerSocket("tcp://" + targetHost)
        dealerSocket.Options.IPv4Only <- false
        dealerSocket.Options.Identity <- peerIdentity
        dealerSocket.ReceiveReady
        |> Observable.subscribe (fun e ->
            let hasMore, _ = e.Socket.TryReceiveFrameString()
            if hasMore then
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
            else
                Log.warning "Possible invalid multipart message format"
        )
        |> ignore

        dealerSocket

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Dealer
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let dealerSendAsync (msg : NetMQMessage) targetAddress =
        let found, _ = dealerSockets.TryGetValue targetAddress
        if not found then
            let dealerSocket = createDealerSocket targetAddress
            poller.Add dealerSocket
            dealerSockets.AddOrUpdate (targetAddress, dealerSocket, fun _ _ -> dealerSocket) |> ignore
        dealerMessageQueue.Enqueue (targetAddress, msg)

    let wireDealerMessageQueueEvents () =
        dealerMessageQueue.ReceiveReady |> Observable.subscribe (fun e ->
            let mutable message = "", new NetMQMessage()

            // Deduplicate messages.
            let messagesSet = new HashSet<string * NetMQMessage>()
            while e.Queue.TryDequeue(&message, TimeSpan.FromMilliseconds(10.)) do
                messagesSet.Add message |> ignore

            messagesSet |> Seq.iter (fun message ->
                let targetAddress, payload = message
                match dealerSockets.TryGetValue targetAddress with
                | true, socket ->
                    if not (socket.TrySendMultipartMessage payload) then
                        Log.errorf "Could not send message to %s" targetAddress
                | _ ->
                    failwithf "Socket not found for target %s" targetAddress
            )
        )
        |> ignore

        poller.Add dealerMessageQueue

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
            while e.Queue.TryDequeue(&message, TimeSpan.FromMilliseconds(10.)) do
                messagesSet.Add message |> ignore

            messagesSet |> Seq.iter (fun message ->
                routerSocket |> Option.iter (fun socket -> socket.TrySendMultipartMessage message |> ignore)
            )
        )
        |> ignore

        poller.Add routerMessageQueue

    member __.Init() =
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
        dealerSendAsync msg targetAddress

    member __.SendGossipMessage gossipMessage (targetMember: GossipMemberDto) =
        let msg = packMessage gossipMessage None
        dealerSendAsync msg targetMember.NetworkAddress

    member __.SendRequestMessage requestMessage targetAddress =
        let msg = packMessage requestMessage None
        dealerSendAsync msg targetAddress

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
                dealerSendAsync msg networkAddress
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

        if not dealerMessageQueue.IsDisposed then
            poller.Remove dealerMessageQueue

        if poller.IsRunning then
            poller.Dispose()
