namespace Own.Blockchain.Public.Net.Tests

open System.Collections.Concurrent

module TransportMock =

    let mutable private transportCoreMock : TransportCoreMock option = None
    let messageQueue = new ConcurrentDictionary<string, ConcurrentQueue<byte[]>>()
    let init networkId identity networkSendoutRetryTimeout peerMessageMaxSize receivePeerMessage =
        let transport =
            TransportCoreMock (
                networkId,
                identity,
                networkSendoutRetryTimeout,
                peerMessageMaxSize,
                messageQueue,
                receivePeerMessage
            )
        transportCoreMock <- transport |> Some

    let receiveMessage listeningAddress =
        match transportCoreMock with
        | Some transport -> transport.ReceiveMessage listeningAddress
        | None -> failwith "Please initialize transport first"

    let sendGossipDiscoveryMessage gossipDiscoveryMessage targetAddress =
        match transportCoreMock with
        | Some transport -> transport.SendGossipDiscoveryMessage gossipDiscoveryMessage targetAddress
        | None -> failwith "Please initialize transport first"

    let sendGossipMessage gossipMessage targetAddress =
        match transportCoreMock with
        | Some transport -> transport.SendGossipMessage gossipMessage targetAddress
        | None -> failwith "Please initialize transport first"

    let sendRequestMessage requestMessage targetAddress =
        match transportCoreMock with
        | Some transport -> transport.SendRequestMessage requestMessage targetAddress
        | None -> failwith "Please initialize transport first"

    let sendResponseMessage responseMessage (targetIdentity : byte[]) =
        match transportCoreMock with
        | Some transport -> transport.SendResponseMessage responseMessage targetIdentity
        | None -> failwith "Please initialize transport first"

    let sendMulticastMessage multicastMessage multicastAddresses =
        match transportCoreMock with
        | Some transport -> transport.SendMulticastMessage multicastMessage multicastAddresses
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
