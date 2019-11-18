namespace Own.Blockchain.Public.Net

module Transport =

    let mutable private transportCore : TransportCore option = None

    let init networkId identity networkSendoutRetryTimeout peerMessageMaxSize receivePeerMessage =
        let transport =
            TransportCore (networkId, identity, networkSendoutRetryTimeout, peerMessageMaxSize, receivePeerMessage)
        transport.Init ()
        transportCore <- transport |> Some

    let receiveMessage listeningAddress =
        match transportCore with
        | Some transport -> transport.ReceiveMessage listeningAddress
        | None -> failwith "Please initialize transport first"

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Send
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let sendGossipDiscoveryMessage targetAddress gossipDiscoveryMessage =
        match transportCore with
        | Some transport -> transport.SendGossipDiscoveryMessage targetAddress gossipDiscoveryMessage
        | None -> failwith "Please initialize transport first"

    let sendGossipMessage targetAddress gossipMessage =
        match transportCore with
        | Some transport -> transport.SendGossipMessage targetAddress gossipMessage
        | None -> failwith "Please initialize transport first"

    let sendRequestMessage targetAddress requestMessage =
        match transportCore with
        | Some transport -> transport.SendRequestMessage targetAddress requestMessage
        | None -> failwith "Please initialize transport first"

    let sendResponseMessage (targetIdentity : byte[]) responseMessage =
        match transportCore with
        | Some transport -> transport.SendResponseMessage targetIdentity responseMessage
        | None -> failwith "Please initialize transport first"

    let sendMulticastMessage multicastAddresses multicastMessage =
        match transportCore with
        | Some transport -> transport.SendMulticastMessage multicastAddresses multicastMessage
        | None -> failwith "Please initialize transport first"

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Cleanup
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let closeConnection remoteAddress =
        match transportCore with
        | Some transport -> transport.CloseConnection remoteAddress
        | None -> failwith "Please initialize transport first"

    let closeAllConnections () =
        match transportCore with
        | Some transport -> transport.CloseAllConnections ()
        | None -> failwith "Please initialize transport first"
