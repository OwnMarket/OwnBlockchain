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

type ConnectionType =
    | Inbound
    | Outbound

type ConnectionInfo = {
    Socket : TcpClient
    Timestamp : DateTime
}

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

    // Client Sockets
    let clientSockets = new ConcurrentDictionary<string, ConnectionInfo>()

    // Server Sockets
    let serverSockets = new ConcurrentDictionary<string, ConnectionInfo>()

    // Thread signal.
    let tcpClientConnectedEvent = new ManualResetEvent(false)

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

    let closeConnection (sockets: ConcurrentDictionary<_, _>) target =
        match sockets.TryRemove target with
        | true, conn ->
            Log.verbosef "Removing connection %s" target
            conn.Socket.Close()
        | _ -> ()

    let closeClientConnection target =
        closeConnection clientSockets target

    let closeServerConnection target =
        closeConnection serverSockets target

    // Writes data to the remote host in batches of bufferSize long.
    let writeToClientAsync target (client : TcpClient) bufferSize (data : byte[]) =
        async {
            try
                let stream = client.GetStream()
                let rec sendAllBytes (bytes: byte[]) (sm : NetworkStream) =
                    let mutable sentBytes = 0
                    async {
                        let count = Math.Min(bytes.Length - sentBytes, bufferSize)
                        if count > 0 then
                            do! sm.AsyncWrite(bytes, sentBytes, count)
                            sentBytes <- sentBytes + count
                            if sentBytes < bytes.Length then
                                return! sendAllBytes bytes sm
                    }

                let sizeInfo = BitConverter.GetBytes(data.Length)
                let prefixedData = Array.concat [ sizeInfo; data ]
                do! sendAllBytes prefixedData stream
            with
            | _ ->
                closeClientConnection target
                Log.warningf
                    "Cannot send data to %s, connection was probably closed by the remote host" target
        }

    // Deserializes data, performs necessary connection management and forwards task to application layer.
    let processDataFromRemoteHost (isNewConnection : bool) client bytes =
        bytes
        |> Serialization.deserializePeerMessageEnvelope
        |> Result.handle
            (
                List.iter (fun envelope ->
                    if envelope.NetworkId <> networkId then
                        Log.error "Peer message with invalid networkId ignored"
                    else
                        envelope.PeerMessageId
                        |> Option.iter (fun peerIdentity ->
                            let connectionInfo =
                                match serverSockets.TryGetValue peerIdentity with
                                | true, conn -> { conn with Timestamp = DateTime.UtcNow }
                                | false, _ ->
                                    {
                                        Socket = client
                                        Timestamp = DateTime.UtcNow
                                    }

                            if isNewConnection then
                                serverSockets.AddOrUpdate (
                                    peerIdentity,
                                    connectionInfo,
                                    fun _ _ -> connectionInfo)
                                |> ignore
                        )
                        invokeReceivePeerMessage envelope
                )
            )
            Log.error

    // Recursively read data from a network stream, returns a memory stream containing all data.
    let rec readAllBytes (sm : NetworkStream) (memStream : MemoryStream) bufferSize dataSize =
        let mutable data = Array.zeroCreate<byte> bufferSize
        async {
            if dataSize <= 0 then
                return memStream
            else
                let! bytesRead = sm.AsyncRead (data, 0, Math.Min (data.Length, dataSize))
                if bytesRead <= 0 then
                    return memStream
                else
                    memStream.Write(data, 0, bytesRead)
                    if not (sm.DataAvailable) then
                        return memStream
                    else
                        return! readAllBytes sm memStream bufferSize (dataSize - bytesRead)
        }

    // Reads data from client, deserializes it and forwards it to application layer.
    let readFromClientAsync remoteHost (client : TcpClient) cts bufferSize =
        async {
            try
                // Get a stream object for reading and writing.
                let stream = client.GetStream()

                // Read the count of bytes for the data
                let sizeInfo = Array.zeroCreate<byte> 4
                let! _ = stream.AsyncRead (sizeInfo, 0, 4)
                let dataSize = BitConverter.ToInt32(sizeInfo, 0);

                if dataSize > 0 then
                    // Loop to receive all the data sent by the client.
                    let! ms = readAllBytes stream (new MemoryStream()) bufferSize dataSize
                    let bytes = ms.ToArray()

                    // Proceed if any data is received.
                    if bytes.Length > 0 then
                        let isNewConnection = remoteHost |> Option.isNone
                        processDataFromRemoteHost isNewConnection client bytes

                return true
            with
            | _ ->
                match remoteHost with
                | Some target -> closeServerConnection target
                | None -> client.Close()

                return false
        }

    let acceptTcpClientCallback (ar : IAsyncResult) =
        // Get the listener that handles the client request.
        let listener = ar.AsyncState :?> TcpListener

        // End the operation and create the tcp client
        // to handle communication with the remote host.
        let client = listener.EndAcceptTcpClient ar

        // Signal the calling thread to continue.
        tcpClientConnectedEvent.Set() |> ignore

        let cts = new CancellationTokenSource()
        let rec listen () =
            async {
                let! ok = readFromClientAsync None client (Some cts) bufferSize
                if ok then
                    return! listen ()
            }
        Async.Start (listen (), cts.Token)

    let getOrCreateTcpConnection (target : string) =
        let endpoint = Utils.resolveToIpPortPair target
        match clientSockets.TryGetValue target with
        | true, conn ->
            let connectionInfo = { conn with Timestamp = DateTime.UtcNow }
            clientSockets.AddOrUpdate (target, connectionInfo, fun _ _ -> connectionInfo) |> ignore
            Some conn.Socket
        | _ ->
            try
                endpoint |> Option.map (fun (host, port) ->
                    let client = new TcpClient(host, port)
                    client.SendBufferSize <- bufferSize
                    client.ReceiveBufferSize <- bufferSize

                    let connectionInfo = {
                        Socket = client
                        Timestamp = DateTime.UtcNow
                    }

                    match clientSockets.TryGetValue target with
                    | true, conn ->
                        client.Close ()
                        conn.Socket
                    | _ ->
                        clientSockets.AddOrUpdate (target, connectionInfo, fun _ _ -> connectionInfo) |> ignore
                        let rec listen () =
                            async {
                                let! ok = readFromClientAsync (Some target) client None bufferSize
                                if ok then
                                    return! listen ()
                            }
                        Async.Start (listen (), cts.Token)
                        client
                )
            with
            | _ -> None

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Client
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    // Enqueue sendout messages based on priority.
    let clientEnqueueMessage (priority : PeerMessagePriority) msg target =
        let found, _ = clientMessages.TryGetValue priority
        if not found then
            let items = new ConcurrentQueue<string * PeerMessageEnvelopeDto>()
            clientMessages.TryAdd (priority, items) |> ignore

        clientMessages.[priority].Enqueue (target, msg)

    // Iterate over peer messages with an action.
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

    // Groups peer messages per target host.
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
            getOrCreateTcpConnection targetAddress
            |> Option.map (fun client ->
                async {
                    try
                        let bytes =
                            peerMessageEnvelope
                            |> List.ofSeq
                            |> Serialization.serializeBinary

                        // Send data to the remote host.
                        do! writeToClientAsync targetAddress client bufferSize bytes
                    with
                    | :? ArgumentException
                    | :? SocketException as ex ->
                        closeClientConnection targetAddress
                        Stats.increment Stats.Counter.FailedMessageSendouts
                        Log.errorf "Could not send message to %s" targetAddress
                        Log.verbosef "Reason: %s" ex.AllMessages
                }
            )
        )
        |> Seq.choose id
        |> Async.Parallel
        |> Async.Ignore

    let clientSendResponseAsync (responseMessages : ConcurrentQueue<string * PeerMessageEnvelopeDto>) =
        responseMessages
        |> groupPeerMessageByTarget
        |> Seq.ofDict
        |> Seq.map (fun (targetIdentity, peerMessageEnvelope) ->
            async {
                match serverSockets.TryGetValue targetIdentity with
                | true, conn ->
                    try
                        // Send the response to the remote host.
                        let bytes =
                            peerMessageEnvelope
                            |> List.ofSeq
                            |> Serialization.serializeBinary

                        do! writeToClientAsync targetIdentity conn.Socket bufferSize bytes
                    with
                    | :? ArgumentException
                    | :? SocketException as ex ->
                        // Ensure connection is closed.
                        Log.error ex
                        closeServerConnection targetIdentity
                        Stats.increment Stats.Counter.FailedMessageSendouts
                        Log.errorf "Could not send response message"
                        Log.verbosef "Reason: %s" ex.AllMessages
                | _ ->
                    Log.error "Cound not send response message";
                    Log.verbosef "Reason: Connection expired"
            }
        )
        |> Async.Parallel
        |> Async.Ignore

    let wireMessageSendoutEvents () =
        let rec dequeueMessages () =
            async {
                let mutable messages = new ConcurrentQueue<string * PeerMessageEnvelopeDto>()
                if clientMessages.TryGetValue(PeerMessagePriority.Multicast, &messages)
                    && not messages.IsEmpty then
                    clientSendAsync messages |> Async.Start
                    return! dequeueMessages ()

                if clientMessages.TryGetValue(PeerMessagePriority.Discovery, &messages)
                    && not messages.IsEmpty then
                    clientSendAsync messages |> Async.Start
                    return! dequeueMessages ()

                if clientMessages.TryGetValue(PeerMessagePriority.Gossip, &messages)
                    && not messages.IsEmpty then
                    clientSendAsync messages |> Async.Start
                    return! dequeueMessages ()

                if clientMessages.TryGetValue(PeerMessagePriority.Request, &messages)
                    && not messages.IsEmpty then
                    clientSendAsync messages |> Async.Start
                    return! dequeueMessages ()

                if clientMessages.TryGetValue(PeerMessagePriority.Response, &messages)
                    && not messages.IsEmpty then
                    clientSendResponseAsync messages |> Async.Start
                    return! dequeueMessages ()

                do! Async.Sleep 200
                return! dequeueMessages ()
            }

        Async.Start (dequeueMessages (), cts.Token)

    // Garbage collection of 'expired' socket connections.
    let monitorOpenedConnections () =
        let closeExpiredConnections (sockets: ConcurrentDictionary<string, ConnectionInfo>) =
            let lastValidTimestamp = DateTime.UtcNow.AddSeconds (-float socketConnectionTimeout)
            sockets
            |> List.ofDict
            |> List.filter (fun (_, conn) -> conn.Timestamp < lastValidTimestamp)
            |> List.iter (fun (connectionId, _) -> closeClientConnection connectionId)

        let rec loop () =
            async {
                closeExpiredConnections clientSockets
                closeExpiredConnections serverSockets
                do! Async.Sleep 1000
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
    // Public API
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    member __.GetConnectionsInfo() =
        serverSockets.Count, clientSockets.Count

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Send
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    member __.SendGossipDiscoveryMessage targetAddress peerMessage =
        clientEnqueueMessage PeerMessagePriority.Discovery peerMessage targetAddress

    member __.SendGossipMessage targetAddress peerMessage =
        clientEnqueueMessage PeerMessagePriority.Gossip peerMessage targetAddress

    member __.SendRequestMessage targetAddress peerMessage =
        clientEnqueueMessage PeerMessagePriority.Request peerMessage targetAddress

    member __.SendResponseMessage targetIdentity peerMessage =
        clientEnqueueMessage PeerMessagePriority.Response peerMessage targetIdentity

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

    member __.CloseConnection target =
        closeConnection target

    member __.CloseAllConnections () =
        // Cancel server async operations.
        cts.Cancel()

        clientSockets
        |> List.ofDict
        |> List.iter (fun (_, conn) ->
            // Cancel the socket.
            conn.Socket.Close()
        )
        clientSockets.Clear()

        listenerSocket <- None
