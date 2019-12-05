namespace Own.Blockchain.Public.Net.Tests

open System.Collections.Concurrent

module TransportMock =

    let mutable private transportCoreMock : TransportCoreMock option = None
    let messageQueue = new ConcurrentDictionary<string, ConcurrentQueue<byte[]>>()
    let init
        networkId
        networkSendoutRetryTimeout
        socketConnectionTimeout
        peerMessageMaxSize
        receivePeerMessage
        =

        let transport =
            TransportCoreMock (
                networkId,
                networkSendoutRetryTimeout,
                socketConnectionTimeout,
                peerMessageMaxSize,
                messageQueue,
                receivePeerMessage
            )
        transportCoreMock <- transport |> Some

    let receiveMessage listeningAddress =
        match transportCoreMock with
        | Some transport -> transport.ReceiveMessage listeningAddress
        | None -> failwith "Please initialize transport first"

    let getConnectionsInfo () =
        0, 0

    let sendGossipDiscoveryMessage targetAddress gossipDiscoveryMessage =
        match transportCoreMock with
        | Some transport -> transport.SendGossipDiscoveryMessage targetAddress gossipDiscoveryMessage
        | None -> failwith "Please initialize transport first"

    let sendGossipMessage targetAddress gossipMessage =
        match transportCoreMock with
        | Some transport -> transport.SendGossipMessage targetAddress gossipMessage
        | None -> failwith "Please initialize transport first"

    let sendRequestMessage senderAddress targetAddress requestMessage =
        match transportCoreMock with
        | Some transport -> transport.SendRequestMessage targetAddress requestMessage senderAddress
        | None -> failwith "Please initialize transport first"

    let sendResponseMessage responseMessage =
        match transportCoreMock with
        | Some transport -> transport.SendResponseMessage responseMessage
        | None -> failwith "Please initialize transport first"

    let sendMulticastMessage multicastAddresses multicastMessage =
        match transportCoreMock with
        | Some transport -> transport.SendMulticastMessage multicastAddresses multicastMessage
        | None -> failwith "Please initialize transport first"

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Cleanup
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let closeConnection remoteAddress =
        match transportCoreMock with
        | Some transport -> transport.CloseConnection remoteAddress
        | None -> failwith "Please initialize transport first"

    let closeAllConnections () =
        match transportCoreMock with
        | Some transport -> transport.CloseAllConnections ()
        | None -> failwith "Please initialize transport first"
