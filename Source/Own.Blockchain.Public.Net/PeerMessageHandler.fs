namespace Own.Blockchain.Public.Net

open System
open Own.Common.FSharp
open Own.Blockchain.Common
open Own.Blockchain.Public.Core.DomainTypes

module internal PeerMessageHandler =

    let mutable private node : NetworkNode option = None

    let startGossip
        listeningAddress
        publicAddress
        bootstrapNodes
        allowPrivateNetworkPeers
        maxConnectedPeers
        dnsResolverCacheExpirationTime
        gossipFanout
        gossipIntervalMillis
        gossipMaxMissedHeartbeats
        peerResponseThrottingTime
        getNetworkId
        getAllPeerNodes
        (savePeerNode : NetworkAddress -> Result<unit, AppErrors>)
        (removePeerNode : NetworkAddress -> Result<unit, AppErrors>)
        initTransport
        sendGossipDiscoveryMessage
        sendGossipMessage
        sendMulticastMessage
        sendRequestMessage
        sendResponseMessage
        receiveMessage
        closeConnection
        closeAllConnections
        getCurrentValidators
        publishEvent
        =

        let nodeConfig =
            {
                Identity = Guid.NewGuid().ToString("N") |> Conversion.stringToBytes |> PeerNetworkIdentity
                ListeningAddress = NetworkAddress listeningAddress
                PublicAddress = publicAddress |> Option.map NetworkAddress
                BootstrapNodes = bootstrapNodes |> List.map NetworkAddress
                AllowPrivateNetworkPeers = allowPrivateNetworkPeers
                MaxConnectedPeers = maxConnectedPeers
                DnsResolverCacheExpirationTime = dnsResolverCacheExpirationTime
            }

        let gossipConfig = {
            Fanout = gossipFanout
            IntervalMillis = gossipIntervalMillis
            MissedHeartbeatIntervalMillis = gossipMaxMissedHeartbeats * gossipIntervalMillis
            PeerResponseThrottlingTime = peerResponseThrottingTime
        }

        let n =
            NetworkNode (
                getNetworkId,
                getAllPeerNodes,
                savePeerNode,
                removePeerNode,
                initTransport,
                sendGossipDiscoveryMessage,
                sendGossipMessage,
                sendMulticastMessage,
                sendRequestMessage,
                sendResponseMessage,
                receiveMessage,
                closeConnection,
                closeAllConnections,
                getCurrentValidators,
                nodeConfig,
                gossipConfig
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

    let respondToPeer targetIdentity peerMessageEnvelope =
        match node with
        | Some n -> n.SendResponseDataMessage targetIdentity peerMessageEnvelope
        | None -> failwith "Please start gossip first"

    let getIdentity () =
        match node with
        | Some n -> n.Identity
        | None -> failwith "Please start gossip first"

    let getPeerList () =
        match node with
        | Some n -> n.GetActiveMembers ()
        | None -> failwith "Please start gossip first"

    let updatePeerList activeMembers =
        match node with
        | Some n -> n.ReceiveMembers {ActiveMembers = activeMembers}
        | None -> failwith "Please start gossip first"
