namespace Own.Blockchain.Public.Net

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
    let dealerMessageQueue = new NetMQQueue<string * NetMQMessage>()
    let routerMessageQueue = new NetMQQueue<NetMQMessage>()
    let mutable peerMessageHandler : (PeerMessageDto -> unit) option = None

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Private
    ////////////////////////////////////////////////////////////////////////////////////////////////////

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
                peerMessageHandler |> Option.iter(fun handler ->
                    handler peerMessage)
            )

    let private createDealerSocket targetHost =
        let dealerSocket = new DealerSocket(">tcp://" + targetHost)
        dealerSocket.Options.Identity <- Guid.NewGuid().ToString() |> Encoding.Unicode.GetBytes
        dealerSocket.ReceiveReady
        |> Observable.subscribe receiveMessageCallback
        |> ignore
        dealerSocket

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Dealer
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let private dealerSendAsync (msg : NetMQMessage) targetHost =
        let found, _ = dealerSockets.TryGetValue targetHost
        if not found then
            let dealerSocket = createDealerSocket targetHost
            poller.Add dealerSocket
            dealerSockets.AddOrUpdate (targetHost, dealerSocket, fun _ _ -> dealerSocket) |> ignore
        dealerMessageQueue.Enqueue (targetHost, msg)

    dealerMessageQueue.ReceiveReady |> Observable.subscribe (fun e ->
        let mutable message = "", new NetMQMessage()
        while e.Queue.TryDequeue(&message, TimeSpan.FromMilliseconds(10.)) do
            let targetAddress, payload = message
            match dealerSockets.TryGetValue targetAddress with
            | true, socket -> socket.TrySendMultipartMessage payload |> ignore
            | _ -> failwithf "Socket not found for target %s" targetAddress
    )
    |> ignore

    poller.Add dealerMessageQueue

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Router
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let private routerSendAsync (msg : NetMQMessage) =
        routerMessageQueue.Enqueue msg

    routerMessageQueue.ReceiveReady |> Observable.subscribe (fun e ->
        let mutable message = new NetMQMessage()
        while e.Queue.TryDequeue(&message, TimeSpan.FromMilliseconds(10.)) do
            routerSocket |> Option.iter(fun socket ->
                socket.TrySendMultipartMessage message |> ignore)
    )
    |> ignore

    poller.Add routerMessageQueue

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
    // Send
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let sendGossipDiscoveryMessage gossipDiscoveryMessage targetAddress =
        let msg = packMessage gossipDiscoveryMessage
        dealerSendAsync msg targetAddress

    let sendGossipMessage gossipMessage (targetMember: GossipMemberDto) =
        let msg = packMessage gossipMessage
        dealerSendAsync msg targetMember.NetworkAddress

    let sendRequestMessage requestMessage targetAddress =
        let msg = packMessage requestMessage
        dealerSendAsync msg targetAddress

    let sendResponseMessage responseMessage =
        let msg = packMessage responseMessage
        routerSendAsync msg

    let sendMulticastMessage multicastMessage multicastAddresses =
        match multicastAddresses with
        | [] -> ()
        | _ ->
            multicastAddresses
            |> Seq.shuffle
            |> Seq.iter (fun networkAddress ->
                let msg = packMessage multicastMessage
                dealerSendAsync msg networkAddress
            )

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
            dealerMessageQueue.Dispose()

        dealerSockets
        |> List.ofDict
        |> List.iter (fun (_, socket) ->
            socket.Dispose()
        )
        dealerSockets.Clear()

        routerSocket |> Option.iter (fun socket -> socket.Dispose())
        routerSocket <- None
