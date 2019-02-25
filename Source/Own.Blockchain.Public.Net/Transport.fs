namespace Own.Blockchain.Public.Net

open System
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
    let mutable receiverSocket : PullSocket option = None
    let private senderSockets = new ConcurrentDictionary<string, DealerSocket>()
    let messageQueue = new NetMQQueue<string * byte[]>()

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Send
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    messageQueue.ReceiveReady |> Observable.subscribe (fun _ ->
        let mutable message = "", Array.zeroCreate<byte> 0
        while messageQueue.TryDequeue(&message, TimeSpan.FromMilliseconds(10.)) do
            let targetAddress, payload = message
            match senderSockets.TryGetValue targetAddress with
            | true, socket -> socket.TrySendFrame payload |> ignore
            | _ -> failwithf "Socket not found for target %s" targetAddress
    )
    |> ignore

    poller.Add messageQueue

    let private send (msg : byte[]) targetAddress =
        let found, _ = senderSockets.TryGetValue targetAddress
        if not found then
            let senderSocket = new DealerSocket(">tcp://" + targetAddress)
            senderSockets.AddOrUpdate (targetAddress, senderSocket, fun _ _ -> senderSocket) |> ignore
        messageQueue.Enqueue (targetAddress, msg)

    let private packMessage message =
        message |> Serialization.serializeBinary

    let private unpackMessage message =
        message |> Serialization.deserializePeerMessage

    let sendGossipDiscoveryMessage gossipDiscoveryMessage targetAddress =
        let msg = packMessage gossipDiscoveryMessage
        send msg targetAddress

    let sendGossipMessage gossipMessage (targetMember: GossipMemberDto) =
        let msg = packMessage gossipMessage
        send msg targetMember.NetworkAddress

    let sendUnicastMessage unicastMessage targetAddress =
        let msg = packMessage unicastMessage
        send msg targetAddress

    let sendMulticastMessage senderAddress multicastMessage multicastAddresses =
        let multicastAddresses =
            multicastAddresses
            |> List.filter (fun a -> a <> senderAddress)

        match multicastAddresses with
        | [] -> ()
        | _ ->
            multicastAddresses
            |> Seq.shuffle
            |> Seq.iter (fun networkAddress ->
                let msg = packMessage multicastMessage
                send msg networkAddress
            )

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Receive
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let receiveMessage networkAddress receiveHandler =
        match receiverSocket with
        | Some _ -> ()
        | None -> receiverSocket <- new PullSocket("@tcp://" + networkAddress) |> Some

        receiverSocket |> Option.iter(fun socket ->
            poller.Add socket
            socket.ReceiveReady
            |> Observable.subscribe (fun eventArgs ->
                let received, message = eventArgs.Socket.TryReceiveFrameBytes()
                if received then
                    match unpackMessage message with
                    | Ok peerMessage -> receiveHandler peerMessage
                    | Error error -> Log.error error
            )
            |> ignore
        )
        if not poller.IsRunning then
            poller.RunAsync()

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Cleanup
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let closeConnection networkAddress =
        match senderSockets.TryGetValue networkAddress with
        | true, socket ->
            senderSockets.TryRemove networkAddress |> ignore
            socket.Dispose()
        | _ -> ()

    let closeAllConnections () =
        if poller.IsRunning then
            poller.Dispose()
            messageQueue.Dispose()

        senderSockets
        |> List.ofDict
        |> List.iter (fun (_, socket) ->
            socket.Dispose()
        )
        senderSockets.Clear()

        receiverSocket |> Option.iter (fun socket -> socket.Dispose())
        receiverSocket <- None
