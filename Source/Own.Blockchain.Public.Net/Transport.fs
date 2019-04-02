namespace Own.Blockchain.Public.Net

open Own.Blockchain.Public.Core.Dtos

module Transport =

    let mutable private transportCore : TransportCore option = None

    let init networkId identity networkSendoutRetryTimeout receivePeerMessage =
        let transport = TransportCore (networkId, identity, networkSendoutRetryTimeout, receivePeerMessage)
        transport.Init ()
        transportCore <- transport |> Some

    let receiveMessage listeningAddress =
        match transportCore with
        | Some transport -> transport.ReceiveMessage listeningAddress
        | None -> failwith "Please initialize transport first"

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Send
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let sendGossipDiscoveryMessage gossipDiscoveryMessage targetAddress =
        match transportCore with
        | Some transport -> transport.SendGossipDiscoveryMessage gossipDiscoveryMessage targetAddress
        | None -> failwith "Please initialize transport first"

    let sendGossipMessage gossipMessage (targetMember: GossipMemberDto) =
        match transportCore with
        | Some transport -> transport.SendGossipMessage gossipMessage targetMember
        | None -> failwith "Please initialize transport first"

    let sendRequestMessage requestMessage targetAddress =
        match transportCore with
        | Some transport -> transport.SendRequestMessage requestMessage targetAddress
        | None -> failwith "Please initialize transport first"

    let sendResponseMessage responseMessage (targetIdentity : byte[]) =
        match transportCore with
        | Some transport -> transport.SendResponseMessage responseMessage targetIdentity
        | None -> failwith "Please initialize transport first"

    let sendMulticastMessage multicastMessage multicastAddresses =
        match transportCore with
        | Some transport -> transport.SendMulticastMessage multicastMessage multicastAddresses
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
