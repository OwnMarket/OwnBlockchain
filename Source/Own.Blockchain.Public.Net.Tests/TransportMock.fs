namespace Own.Blockchain.Public.Net.Tests

open Own.Blockchain.Public.Core.Dtos

module TransportMock =

    let mutable private transportStubMock : TransportStubMock option = None
    let init networkId identity receivePeerMessage =
        let stub = TransportStubMock (networkId, identity, receivePeerMessage)
        transportStubMock <- stub |> Some

    let receiveMessage listeningAddress =
        match transportStubMock with
        | Some transport -> transport.ReceiveMessage listeningAddress
        | None -> failwith "Please initialize transport first"

    let sendGossipDiscoveryMessage gossipDiscoveryMessage targetAddress =
        match transportStubMock with
        | Some transport -> transport.SendGossipDiscoveryMessage gossipDiscoveryMessage targetAddress
        | None -> failwith "Please initialize transport first"

    let sendGossipMessage gossipMessage (targetMember: GossipMemberDto) =
        match transportStubMock with
        | Some transport -> transport.SendGossipMessage gossipMessage targetMember
        | None -> failwith "Please initialize transport first"

    let sendRequestMessage requestMessage targetAddress =
        match transportStubMock with
        | Some transport -> transport.SendRequestMessage requestMessage targetAddress
        | None -> failwith "Please initialize transport first"

    let sendResponseMessage responseMessage (targetIdentity : byte[]) =
        match transportStubMock with
        | Some transport -> transport.SendResponseMessage responseMessage targetIdentity
        | None -> failwith "Please initialize transport first"

    let sendMulticastMessage multicastMessage multicastAddresses =
        match transportStubMock with
        | Some transport -> transport.SendMulticastMessage multicastMessage multicastAddresses
        | None -> failwith "Please initialize transport first"

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Cleanup
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let closeConnection remoteAddress =
        match transportStubMock with
        | Some transport -> transport.CloseConnection remoteAddress
        | None -> failwith "Please initialize transport first"

    let closeAllConnections () =
        match transportStubMock with
        | Some transport -> transport.CloseAllConnections ()
        | None -> failwith "Please initialize transport first"
