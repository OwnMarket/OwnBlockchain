namespace Own.Blockchain.Public.Net

open System
open Own.Common.FSharp
open Own.Blockchain.Common
open Own.Blockchain.Public.Core
open Own.Blockchain.Public.Core.Dtos
open Own.Blockchain.Public.Core.DomainTypes

module internal PeerMessageHandler =

    let mutable private node : NetworkNode option = None

    let startGossip
        listeningAddress
        publicAddress
        bootstrapNodes
        allowPrivateNetworkPeers
        dnsResolverCacheExpirationTime
        maxConnectedPeers
        gossipFanoutPercentage
        gossipDiscoveryIntervalMillis
        gossipIntervalMillis
        gossipMaxMissedHeartbeats
        peerResponseThrottingTime
        networkSendoutRetryTimeout
        peerMessageMaxSize
        getNetworkId
        getActivePeersFromDb
        getDeadPeersFromDb
        (savePeerToDb : GossipPeerInfoDto -> Result<unit, AppErrors>)
        (removePeerFromDb : NetworkAddress -> Result<unit, AppErrors>)
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
                NetworkSendoutRetryTimeout = networkSendoutRetryTimeout
                PeerMessageMaxSize = peerMessageMaxSize
            }

        let gossipConfig = {
            SessionTimestamp = Utils.getMachineTimestamp ()
            FanoutPercentage = gossipFanoutPercentage
            GossipDiscoveryIntervalMillis = gossipDiscoveryIntervalMillis
            GossipIntervalMillis = gossipIntervalMillis
            MissedHeartbeatIntervalMillis = gossipMaxMissedHeartbeats * gossipDiscoveryIntervalMillis
            PeerResponseThrottlingTime = peerResponseThrottingTime
        }

        let savePeer = Mapping.gossipPeerInfoToDto >> savePeerToDb
        let getDeadPeers () = getDeadPeersFromDb () |> List.map Mapping.gossipPeerInfoFromDto
        let getActivePeers () = getActivePeersFromDb () |> List.map Mapping.gossipPeerInfoFromDto

        let n =
            NetworkNode (
                getNetworkId,
                getActivePeers,
                getDeadPeers,
                savePeer,
                removePeerFromDb,
                Utils.resolveHostToIpAddress,
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

    let requestFromPeer requestIds preferredPeer =
        match node with
        | Some n ->
            let validRequestIds = requestIds |> List.filter (fun requestId -> not (n.IsRequestPending requestId))
            n.SendRequestDataMessage validRequestIds preferredPeer
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
        | Some n -> n.GetActivePeers ()
        | None -> failwith "Please start gossip first"

    let updatePeerList activePeers =
        match node with
        | Some n -> n.ReceivePeers {ActivePeers = activePeers}
        | None -> failwith "Please start gossip first"

    let getNetworkStats () =
        match node with
        | Some n -> n.GetNetworkStats ()
        | None -> failwith "Please start gossip first"
