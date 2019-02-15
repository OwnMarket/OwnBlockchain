namespace Own.Blockchain.Public.Net

open Own.Common
open Own.Blockchain.Common
open Own.Blockchain.Public.Core.DomainTypes

module internal PeerMessageHandler =

    let mutable private node : NetworkNode option = None

    let startGossip
        getAllPeerNodes
        (savePeerNode : NetworkAddress -> Result<unit, AppErrors>)
        (removePeerNode : NetworkAddress -> Result<unit, AppErrors>)
        sendGossipDiscoveryMessage
        sendGossipMessage
        sendMulticastMessage
        sendUnicastMessage
        receiveMessage
        closeConnection
        closeAllConnections
        networkAddress
        bootstrapNodes
        getCurrentValidators
        publishEvent
        =

        let nodeConfig : NetworkNodeConfig = {
            BootstrapNodes = bootstrapNodes
            |> List.map (fun a -> NetworkAddress a)

            NetworkAddress = NetworkAddress networkAddress
        }

        let fanout, tCycle, tFail = 2, 10000, 50000

        let n =
            NetworkNode (
                getAllPeerNodes,
                savePeerNode,
                removePeerNode,
                sendGossipDiscoveryMessage,
                sendGossipMessage,
                sendMulticastMessage,
                sendUnicastMessage,
                receiveMessage,
                closeConnection,
                closeAllConnections,
                getCurrentValidators,
                nodeConfig,
                fanout,
                tCycle,
                tFail
            )
        n.StartGossip publishEvent
        node <- Some n

    let stopGossip () =
        node |> Option.iter (fun n -> n.StopGossip())

    let discoverNetwork networkDiscoveryTime =
        Log.info "Discovering peers..."
        async {
            do! Async.Sleep (networkDiscoveryTime * 1000) // Give gossip time to discover some of peers.
            // TODO: If number of nodes reaches some threshold, consider network discovery done and proceed.
        }
        |> Async.RunSynchronously
        Log.info "Peer discovery finished"

    let sendMessage message =
        match node with
        | Some n -> n.SendMessage message
        | None -> failwith "Please start gossip first"

    let requestFromPeer requestId =
        match node with
        | Some n ->
            if not (n.IsRequestPending requestId) then
                n.SendRequestDataMessage requestId
        | None -> failwith "Please start gossip first"

    let respondToPeer targetAddress peerMessage =
        match node with
        | Some n -> n.SendResponseDataMessage targetAddress peerMessage
        | None -> failwith "Please start gossip first"
