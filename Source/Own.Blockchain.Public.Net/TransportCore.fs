namespace Own.Blockchain.Public.Net

open System
open System.Collections.Concurrent
open System.Collections.Generic
open System.Threading
open System.Net
open System.Net.Sockets
open System.IO
open Own.Common.FSharp
open Own.Blockchain.Common
open Own.Blockchain.Public.Core
open Own.Blockchain.Public.Core.Dtos

type internal PeerMessagePriority =
    | Multicast
    | Discovery
    | Gossip
    | Request
    | Response

type internal TransportCore
    (
    networkId,
    networkSendoutRetryTimeout,
    socketConnectionTimeout,
    peerMessageMaxSize,
    receivePeerMessage
    ) =

    let mutable cts = new CancellationTokenSource()
    let bufferSize = peerMessageMaxSize

    // Thread signal.
    let tcpClientConnectedEvent = new ManualResetEvent(false)

    // Opened connections:
    // by client on sending requests until a response is received
    // by server on receiving requests until a response is sent
    let connectionPool = new ConcurrentDictionary<string, TcpClient * CancellationTokenSource * DateTime>()

    // Group client messages by priority
    let clientMessages =
        new ConcurrentDictionary<PeerMessagePriority, ConcurrentQueue<string * PeerMessageEnvelopeDto>>()

    let mutable listenerSocket : TcpListener option = None

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Receive peer message Agent
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

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Private
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let closeConnection connectionId =
        match connectionPool.TryRemove connectionId with
        | true, (client, cts, _) ->
            Log.verbosef "Removing connection %s" connectionId
            cts.Cancel ()
            client.Close()
        | _ -> ()

    let writeToClientAsync connectionId (client : TcpClient) bufferSize (data: byte[]) =
        async {
            try
                let stream = client.GetStream()
                let mutable sentBytes = 0
                let rec sendAllBytes (sm : NetworkStream) =
                    async {
                        let count = Math.Min(data.Length - sentBytes, bufferSize)
                        if count > 0 then
                            do! sm.AsyncWrite(data, sentBytes, count)
                            sentBytes <- sentBytes + count
                            if sentBytes < data.Length then
                                return! sendAllBytes sm
                    }

                do! sendAllBytes stream
            with
            | _ ->
                match connectionId with
                | Some requestId -> closeConnection requestId
                | _ -> client.Close()
                Log.warningf "Cannot send data, connection was probably closed by the remote host"
        }

    let processBytes client bytes =
        bytes
        |> Serialization.deserializePeerMessageEnvelope
        |> Result.handle
            (
                List.iter (fun envelope ->
                    if envelope.NetworkId <> networkId then
                        Log.error "Peer message with invalid networkId ignored"
                    else
                        // TODO: can it be done better?
                        match envelope.PeerMessage.MessageType with
                        | "ResponseDataMessage" ->
                            // Close connection with remote client once response is received.
                            envelope.PeerMessageId
                            |> Option.iter closeConnection
                        | "RequestDataMessage" ->
                            // Persist connection for response handling.
                            envelope.PeerMessageId
                            |> Option.iter (fun requestId ->
                                let connectionInfo = (client, new CancellationTokenSource(), DateTime.UtcNow)
                                connectionPool.AddOrUpdate (
                                    requestId,
                                    connectionInfo,
                                    fun _ _ -> connectionInfo) |> ignore
                            )
                        | _ ->
                            // Close connection.
                            match envelope.PeerMessageId with
                            | Some requestId -> closeConnection requestId
                            | _ -> client.Close()

                        invokeReceivePeerMessage envelope
                )
            )
            Log.error

    let rec readAllBytes (sm: NetworkStream) (memStream : MemoryStream) bufferSize =
        let mutable data = Array.zeroCreate<byte> bufferSize
        async {
            let! bytesRead = sm.AsyncRead (data, 0, data.Length)
            if bytesRead > 0 then
                memStream.Write(data, 0, bytesRead)
                if (sm.DataAvailable) then
                    return! readAllBytes sm memStream bufferSize
                else
                    return memStream
            else
                return memStream
        }

    let readFromClientAsync connectionId (client : TcpClient) bufferSize =
        async {
            try
                // Get a stream object for reading and writing.
                let stream = client.GetStream()

                // Loop to receive all the data sent by the client.
                let! ms = readAllBytes stream (new MemoryStream()) bufferSize

                let bytes = ms.ToArray()

                // Proceed if any data is received.
                if bytes.Length > 0 then
                    processBytes client bytes
            with
            | _ ->
                match connectionId with
                | Some requestId -> closeConnection requestId
                | _ -> client.Close()
        }

    let acceptTcpClientCallback (ar : IAsyncResult) =
        // Get the listener that handles the client request.
        let listener = ar.AsyncState :?> TcpListener

        // End the operation and create the tcp client
        // to handle communication with the remote host.
        let client = listener.EndAcceptTcpClient ar

        // Process the connection here.
        Log.verbose "Client connected completed"

        // Signal the calling thread to continue.
        tcpClientConnectedEvent.Set() |> ignore

        // Read all data from the client.
        readFromClientAsync None client bufferSize
        |> Async.Start

    let createTcpClient targetHost =
        try
            targetHost
            |> Utils.resolveToIpPortPair
            |> Option.map (fun (host, port) ->
                let client = new TcpClient(host, port)
                client.SendBufferSize <- bufferSize
                client.ReceiveBufferSize <- bufferSize
                client
            )
        with
        | _ -> None

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Client
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let clientEnqueueMessage (priority : PeerMessagePriority) msg targetAddress =
        let found, _ = clientMessages.TryGetValue priority
        if not found then
            let items = new ConcurrentQueue<string * PeerMessageEnvelopeDto>()
            clientMessages.TryAdd (priority, items) |> ignore

        clientMessages.[priority].Enqueue (targetAddress, msg)

    let peerMessageIter
        (action : string * PeerMessageEnvelopeDto -> unit)
        (peerMessages : ConcurrentQueue<string * PeerMessageEnvelopeDto>)
        =

        let mutable queueItem = String.Empty, Unchecked.defaultof<PeerMessageEnvelopeDto>
        let mutable peerMessageSize = 0
        while (peerMessageMaxSize = 0 || peerMessageSize <= peerMessageMaxSize)
            && peerMessages.TryDequeue &queueItem do
            let targetAddress, peerMessageEnvelope = queueItem
            peerMessageSize <- peerMessageSize + peerMessageEnvelope.PeerMessage.MessageData.Length
            action (targetAddress, peerMessageEnvelope)

    let groupPeerMessageByTarget (peerMessages : ConcurrentQueue<string * PeerMessageEnvelopeDto>) =
        let dict = new ConcurrentDictionary<string, HashSet<PeerMessageEnvelopeDto>>()
        peerMessages
        |> peerMessageIter (fun (targetAddress, peerMessageEnvelope) ->
            match dict.TryGetValue targetAddress with
            | true, peerMessages ->
                peerMessages.Add peerMessageEnvelope |> ignore
            | _ ->
                let peerMessages = new HashSet<PeerMessageEnvelopeDto>()
                peerMessages.Add peerMessageEnvelope |> ignore
                dict.AddOrUpdate (targetAddress, peerMessages, fun _ _ -> peerMessages) |> ignore
        )
        dict

    let clientSendAsync (peerMessages : ConcurrentQueue<_>) =
        peerMessages
        |> groupPeerMessageByTarget
        |> Seq.ofDict
        |> Seq.map (fun (targetAddress, peerMessageEnvelope) ->
            targetAddress
            |> createTcpClient
            |> Option.map (fun client ->
                async {
                    try
                        try
                            let bytes =
                                peerMessageEnvelope
                                |> List.ofSeq
                                |> Serialization.serializeBinary

                            do! writeToClientAsync None client bufferSize bytes

                        with
                        | :? ArgumentException
                        | :? SocketException as ex ->
                            Stats.increment Stats.Counter.FailedMessageSendouts
                            Log.errorf "Could not send message to %s" targetAddress
                            Log.verbosef "Reason: %s" ex.AllMessages
                    finally
                        client.Close()
                }
            )
        )
        |> Seq.choose id
        |> Async.Parallel
        |> Async.Ignore

    let clientSendRequestAsync (requestMessages : ConcurrentQueue<string * PeerMessageEnvelopeDto>) =
        requestMessages
        |> groupPeerMessageByTarget
        |> Seq.ofDict
        |> Seq.collect (fun (targetAddress, peerMessageEnvelopes) ->
            peerMessageEnvelopes
            |> Seq.map (fun envelope ->
                targetAddress
                |> createTcpClient
                |> Option.map (fun client ->
                    async {
                        let requestId = Guid.NewGuid().ToString()
                        try
                            let cts = new CancellationTokenSource()
                            let connectionInfo = (client, cts, DateTime.UtcNow)
                            connectionPool.AddOrUpdate (
                                requestId,
                                connectionInfo,
                                fun _ _ -> connectionInfo) |> ignore

                            let bytes =
                                [ { envelope with PeerMessageId = Some requestId }]
                                |> Serialization.serializeBinary

                            do! writeToClientAsync (Some requestId) client bufferSize bytes
                            Log.verbosef "Sent request to %A" requestId

                            do! readFromClientAsync (Some requestId) client bufferSize

                        with
                        | :? ArgumentException
                        | :? SocketException as ex ->
                            // Ensure connection is closed.
                            closeConnection requestId
                            Stats.increment Stats.Counter.FailedMessageSendouts
                            Log.errorf "Could not send message to %s" targetAddress
                            Log.verbosef "Reason: %s" ex.AllMessages
                    }
                )
            )
            |> Seq.choose id
        )
        |> Async.Parallel
        |> Async.Ignore

    let clientSendResponseAsync (responseMessages : ConcurrentQueue<string * PeerMessageEnvelopeDto>) =
        responseMessages
        |> groupPeerMessageByTarget
        |> Seq.ofDict
        |> Seq.collect (fun (_, peerMessageEnvelopes) ->
            peerMessageEnvelopes
            |> Seq.map (fun peerMessageEnvelope ->
                async {
                    try
                        match peerMessageEnvelope.PeerMessageId with
                        | Some connectionId ->
                            match connectionPool.TryGetValue connectionId with
                            | true, (client, _, _) ->
                                try
                                    [ peerMessageEnvelope ]
                                    |> Serialization.serializeBinary
                                    |> writeToClientAsync (Some connectionId) client bufferSize
                                    |> Async.Start
                                    Log.verbosef "Sent response to %A" peerMessageEnvelope.PeerMessageId

                                with
                                | :? ArgumentException
                                | :? SocketException as ex ->
                                    Stats.increment Stats.Counter.FailedMessageSendouts
                                    Log.errorf "Could not send response message"
                                    Log.verbosef "Reason: %s" ex.AllMessages
                            | _ ->
                                Log.error "Cound not send response message";
                                Log.verbosef "Reason: Connection expired"
                        | _ ->
                            Log.error "Could not send response message"
                            Log.verbosef "Reason: Response message is missing the corresponding requestId"
                    finally
                        // Ensure connection is closed
                        peerMessageEnvelope.PeerMessageId
                        |> Option.iter closeConnection
                }
            )
        )
        |> Async.Parallel
        |> Async.Ignore

    let wireMessageSendoutEvents () =
        let rec dequeue () =
            async {
                let mutable messages = new ConcurrentQueue<string * PeerMessageEnvelopeDto>()
                if clientMessages.TryGetValue(PeerMessagePriority.Multicast, &messages)
                    && not messages.IsEmpty then
                    clientSendAsync messages |> Async.Start
                    return! dequeue ()

                if clientMessages.TryGetValue(PeerMessagePriority.Discovery, &messages)
                    && not messages.IsEmpty then
                    clientSendAsync messages |> Async.Start
                    return! dequeue ()

                if clientMessages.TryGetValue(PeerMessagePriority.Gossip, &messages)
                    && not messages.IsEmpty then
                    clientSendAsync messages |> Async.Start
                    return! dequeue ()

                if clientMessages.TryGetValue(PeerMessagePriority.Request, &messages)
                    && not messages.IsEmpty then
                    clientSendRequestAsync messages |> Async.Start
                    return! dequeue ()

                if clientMessages.TryGetValue(PeerMessagePriority.Response, &messages)
                    && not messages.IsEmpty then
                    clientSendResponseAsync messages |> Async.Start
                    return! dequeue ()

                do! Async.Sleep 200
                return! dequeue ()
            }

        Async.Start (dequeue (), cts.Token)

    let monitorOpenedConnections () =
        let rec loop () =
            async {
                let lastValidTimestamp = DateTime.UtcNow.AddSeconds (-float socketConnectionTimeout)
                connectionPool
                |> List.ofDict
                |> List.filter (fun (_, (_, _, timestamp)) -> timestamp < lastValidTimestamp)
                |> List.iter (fun (connectionId, _) -> closeConnection connectionId)

                do! Async.Sleep 100
                return! loop ()
            }
        Async.Start (loop (), cts.Token)

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Server
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    member __.Init() =
        startReceivePeerMessageDispatcher ()
        wireMessageSendoutEvents ()
        monitorOpenedConnections ()

    member __.ReceiveMessage listeningAddress =
        match listenerSocket with
        | Some _ -> ()
        | None ->
            listeningAddress
            |> Utils.resolveToIpPortPair
            |> Option.iter (fun (host, port) ->
                let ipAddress =
                    match IPAddress.TryParse host with
                    | true, ipAddress -> ipAddress
                    | _ -> IPAddress.Any

                let listener = new TcpListener(ipAddress, port)
                listenerSocket <- Some listener
            )

        listenerSocket |> Option.iter (fun server ->
            let listen () =
                async {
                    try
                        // Start listening for client requests.
                        server.Start()

                        while true do
                            tcpClientConnectedEvent.Reset() |> ignore
                            Log.verbose "Waiting for connection..."

                            // Accept the connection.
                            server.BeginAcceptTcpClient(new AsyncCallback(acceptTcpClientCallback), server) |> ignore

                            // Wait until a connection is made and processed before
                            // continuing.
                            tcpClientConnectedEvent.WaitOne() |> ignore
                    finally
                        server.Stop()
                }
            Async.Start (listen (), cts.Token)
        )

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Send
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    member __.SendGossipDiscoveryMessage targetAddress peerMessage =
        clientEnqueueMessage PeerMessagePriority.Discovery peerMessage targetAddress

    member __.SendGossipMessage targetAddress peerMessage =
        clientEnqueueMessage PeerMessagePriority.Gossip peerMessage targetAddress

    member __.SendRequestMessage targetAddress peerMessage =
        clientEnqueueMessage PeerMessagePriority.Request peerMessage targetAddress

    member __.SendResponseMessage peerMessage =
        clientEnqueueMessage PeerMessagePriority.Response peerMessage String.Empty

    member __.SendMulticastMessage multicastAddresses peerMessage =
        match multicastAddresses with
        | [] -> ()
        | _ ->
            multicastAddresses
            |> Seq.shuffle
            |> Seq.iter (clientEnqueueMessage PeerMessagePriority.Multicast peerMessage)

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Cleanup
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    member __.CloseConnection requestId =
        closeConnection requestId

    member __.CloseAllConnections () =
        // Cancel server async operations.
        cts.Cancel()

        connectionPool
        |> List.ofDict
        |> List.iter (fun (_, (client, cts, _)) ->
            // Cancel client async operations.
            cts.Cancel ()
            // Cancel the socket.
            client.Close()
        )
        connectionPool.Clear()

        listenerSocket <- None
