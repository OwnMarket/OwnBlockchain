﻿namespace Own.Blockchain.Public.Net

open System
open System.Text
open System.Collections.Concurrent
open Own.Common
open Own.Blockchain.Common
open Own.Blockchain.Public.Core
open Own.Blockchain.Public.Core.Dtos
open NetMQ
open NetMQ.Sockets
open Newtonsoft.Json

module Transport =

    let private poller = new NetMQPoller()
    let mutable routerSocket : RouterSocket option = None
    let private dealerSockets = new ConcurrentDictionary<string, DealerSocket>()
    let messageQueue = new NetMQQueue<string * NetMQMessage>()
    let mutable peerMessageHandler : (PeerMessageDto -> unit) option = None

    let private composeMultipartMessage (msg : byte[]) =
        let netMqMessage = new NetMQMessage();
        netMqMessage.AppendEmptyFrame();
        netMqMessage.Append(msg);
        netMqMessage

    let private extractMessageFromMultipart (multipartMessage : NetMQMessage) =
        if multipartMessage.FrameCount = 3 then
            let originatorIdentity = multipartMessage.[0];
            let msg = multipartMessage.[2].ToByteArray();
            msg |> Some
        else
            Log.errorf "Invalid message frame count. Expected 3, received %i" multipartMessage.FrameCount
            None

    let private packMessage message =
        message |> Serialization.serializeBinary |> composeMultipartMessage

    let private unpackMessage message =
        match extractMessageFromMultipart message with
        | Some bytes ->
            let unpackedMessage = bytes |> Serialization.deserializePeerMessage
            match unpackedMessage with
            | Ok peerMessage ->
                Some peerMessage
            | Error error ->
                Log.error error
                None
        | None -> None

    let private receiveMessageCallback (eventArgs : NetMQSocketEventArgs) =
        let mutable message = new NetMQMessage()
        if eventArgs.Socket.TryReceiveMultipartMessage(&message) then
            message
            |> unpackMessage
            |> Option.iter (fun peerMessage ->
                peerMessageHandler
                |> Option.iter(fun handler -> handler peerMessage)
            )

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Send
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    messageQueue.ReceiveReady |> Observable.subscribe (fun _ ->
        let mutable message = "", new NetMQMessage()
        while messageQueue.TryDequeue(&message, TimeSpan.FromMilliseconds(10.)) do
            let targetAddress, payload = message
            match dealerSockets.TryGetValue targetAddress with
            | true, socket -> socket.TrySendMultipartMessage payload |> ignore
            | _ -> failwithf "Socket not found for target %s" targetAddress
    )
    |> ignore

    poller.Add messageQueue

    let createDealerSocket targetHost =
        let dealerSocket = new DealerSocket(">tcp://" + targetHost)
        dealerSocket.Options.Identity <- Guid.NewGuid().ToString() |> Encoding.Unicode.GetBytes
        dealerSocket.ReceiveReady
        |> Observable.subscribe receiveMessageCallback
        |> ignore
        dealerSocket

    let private sendAsync (msg : NetMQMessage) targetHost =
        let found, _ = dealerSockets.TryGetValue targetHost
        if not found then
            let dealerSocket = createDealerSocket targetHost
            poller.Add dealerSocket
            dealerSockets.AddOrUpdate (targetHost, dealerSocket, fun _ _ -> dealerSocket) |> ignore
        messageQueue.Enqueue (targetHost, msg)

    let sendGossipDiscoveryMessage gossipDiscoveryMessage targetAddress =
        let msg = packMessage gossipDiscoveryMessage
        sendAsync msg targetAddress

    let sendGossipMessage gossipMessage (targetMember: GossipMemberDto) =
        let msg = packMessage gossipMessage
        sendAsync msg targetMember.NetworkAddress

    let sendUnicastMessage unicastMessage targetAddress =
        let msg = packMessage unicastMessage
        sendAsync msg targetAddress

    let sendMulticastMessage multicastMessage multicastAddresses =
        match multicastAddresses with
        | [] -> ()
        | _ ->
            multicastAddresses
            |> Seq.shuffle
            |> Seq.iter (fun networkAddress ->
                let msg = packMessage multicastMessage
                sendAsync msg networkAddress
            )

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Receive
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let receiveMessage listeningAddress receivePeerMessage =
        match routerSocket with
        | Some _ -> ()
        | None -> routerSocket <- new RouterSocket("@tcp://" + listeningAddress) |> Some
        match peerMessageHandler with
        | Some _ -> ()
        | None -> peerMessageHandler <- receivePeerMessage |> Some
        routerSocket |> Option.iter(fun socket ->
            poller.Add socket
            socket.ReceiveReady
            |> Observable.subscribe receiveMessageCallback
            |> ignore
        )
        if not poller.IsRunning then
            poller.RunAsync()

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Cleanup
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let closeConnection networkAddress =
        match dealerSockets.TryGetValue networkAddress with
        | true, socket ->
            dealerSockets.TryRemove networkAddress |> ignore
            socket.Dispose()
        | _ -> ()

    let closeAllConnections () =
        if poller.IsRunning then
            poller.Dispose()
            messageQueue.Dispose()

        dealerSockets
        |> List.ofDict
        |> List.iter (fun (_, socket) ->
            socket.Dispose()
        )
        dealerSockets.Clear()

        routerSocket |> Option.iter (fun socket -> socket.Dispose())
        routerSocket <- None
