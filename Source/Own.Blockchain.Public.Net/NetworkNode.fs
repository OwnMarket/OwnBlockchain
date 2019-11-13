namespace Own.Blockchain.Public.Net

open System
open System.Collections.Concurrent
open System.Collections.Generic
open System.Threading
open Own.Common.FSharp
open Own.Blockchain.Common
open Own.Blockchain.Public.Core
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Core.Events

type NetworkStats = {
    ReceivesGossip : bool
}

type NetworkNode
    (
    getNetworkId : unit -> NetworkId,
    getAllPeerNodes,
    savePeerNode : NetworkAddress -> Result<unit, AppErrors>,
    removePeerNode : NetworkAddress -> Result<unit, AppErrors>,
    resolveHostToIpAddress,
    initTransport,
    sendGossipDiscoveryMessage,
    sendGossipMessage,
    sendMulticastMessage,
    sendRequestMessage,
    sendResponseMessage,
    receiveMessage,
    closeConnection,
    closeAllConnections : _ -> unit,
    getCurrentValidators : unit -> ValidatorSnapshot list,
    nodeConfig,
    gossipConfig
    ) =

    let activePeers = new ConcurrentDictionary<NetworkAddress, GossipPeer>()
    let deadPeers = new ConcurrentDictionary<NetworkAddress, GossipPeer>()
    let excludedPeers = new ConcurrentDictionary<NetworkAddress, GossipPeer>()
    let peersStateMonitor = new ConcurrentDictionary<NetworkAddress, CancellationTokenSource>()

    let validatorsCache = new ConcurrentDictionary<NetworkAddress, _>()

    let gossipMessages = new ConcurrentDictionary<NetworkMessageId, NetworkAddress list>()
    let peerSelectionSentRequests = new ConcurrentDictionary<NetworkMessageId, NetworkAddress list>()
    let mutable lastMessageReceivedTimestamp = DateTime.UtcNow
    let sentRequests = new ConcurrentDictionary<NetworkMessageId, DateTime>()
    let receivedRequests = new ConcurrentDictionary<RequestDataMessage, DateTime>()
    let sentGossipMessages = new ConcurrentDictionary<NetworkMessageId, DateTime>()
    let receivedGossipMessages = new ConcurrentDictionary<NetworkMessageId, DateTime>()

    let dnsResolverCache = new ConcurrentDictionary<string, NetworkAddress * DateTime>()

    let mutable cts = new CancellationTokenSource()

    let printActivePeers () =
        #if DEBUG
            Log.verbose "==================== ACTIVE CONNECTIONS ===================="
            activePeers
            |> Seq.ofDict
            |> Seq.sort
            |> Seq.iter (fun (a, m) -> Log.verbosef "%s Heartbeat:%i" a.Value m.Heartbeat)
            Log.verbose "============================================================"
        #else
            ()
        #endif

    let memoizedConvertToIpAddress (networkAddress : string) =
        match dnsResolverCache.TryGetValue networkAddress with
        | true, (ip, _) -> Some ip
        | _ ->
            resolveHostToIpAddress networkAddress nodeConfig.AllowPrivateNetworkPeers
            |> Option.bind (fun ip ->
                let timestamp = DateTime.UtcNow
                dnsResolverCache.AddOrUpdate (networkAddress, (ip, timestamp), fun _ _ -> (ip, timestamp))
                |> fst
                |> Some
            )

    let nodeConfigPublicIPAddress =
        match nodeConfig.PublicAddress with
        | Some a -> memoizedConvertToIpAddress a.Value
        | None -> None

    let gossipPeerWithIpAddress m =
        m.NetworkAddress.Value
        |> memoizedConvertToIpAddress
        |> Option.map (fun ip -> {m with NetworkAddress = ip})

    let isSelf networkAddress =
        nodeConfigPublicIPAddress = Some networkAddress

    let isWhitelisted networkAddress =
        isSelf networkAddress
        || nodeConfig.BootstrapNodes |> List.contains networkAddress
        || validatorsCache.ContainsKey networkAddress

    let optionToList = function | Some x -> [x] | None -> []

    (*
        A peer is dead if it's in the list of dead-peers and
        the heartbeat the local node is bigger than the one passed by argument.
    *)
    let isDead peer =
        match deadPeers.TryGetValue peer.NetworkAddress with
        | true, deadPeer ->
            Log.verbosef "Received a node with heartbeat %i - in dead-peers it has heartbeat %i"
                peer.Heartbeat
                deadPeer.Heartbeat
            deadPeer.Heartbeat >= peer.Heartbeat
        | _ -> false

    let cancelPeerStateMonitoring peerAddress =
        match peersStateMonitor.TryGetValue peerAddress with
        | true, cts ->
            cts.Cancel()
            peersStateMonitor.TryRemove peerAddress |> ignore
        | _ -> ()

    (*
        Once a peer has been declared dead and it hasn't recovered in
        2xTFail time is removed from the dead-peers list.
        So if node has been down for a while and come back it can be added again.
        Here this will be scheduled right after a node is declared, so total time
        elapsed is 2xTFail
    *)
    let setFinalDeadPeer peerAddress =
        async {
            do! Async.Sleep gossipConfig.MissedHeartbeatIntervalMillis
            let found, _ = activePeers.TryGetValue peerAddress
            if not (found || isWhitelisted peerAddress) then
                Log.warningf "Peer marked as DEAD %s" peerAddress.Value
                removePeerNode peerAddress
                |> Result.handle
                    (fun _ ->
                        deadPeers.TryRemove peerAddress |> ignore
                        peersStateMonitor.TryRemove peerAddress |> ignore
                        peerAddress.Value |> closeConnection
                    )
                    (fun _ -> Log.errorf "Error removing peer %A" peerAddress)
            else if isWhitelisted peerAddress then
                cancelPeerStateMonitoring peerAddress
        }

    let monitorPendingDeadPeer peerAddress =
        let cts =
            match peersStateMonitor.TryGetValue peerAddress with
            | true, cts -> cts
            | _ -> new CancellationTokenSource()

        peersStateMonitor.AddOrUpdate (peerAddress, cts, fun _ _ -> cts) |> ignore
        Async.Start ((setFinalDeadPeer peerAddress), cts.Token)

    let setPeerForExclusion networkAddress =
        async {
            do! Async.Sleep gossipConfig.MissedHeartbeatIntervalMillis
            let found, _ = activePeers.TryGetValue networkAddress
            if not found then
                Log.debugf "Peer %s has been excluded" networkAddress.Value
            else
                activePeers.TryRemove networkAddress |> ignore
                Log.debugf "Excluding %s peer" networkAddress.Value

            excludedPeers.TryRemove networkAddress |> ignore
            networkAddress.Value |> closeConnection
            removePeerNode networkAddress
            |> Result.iterError (fun _ -> Log.errorf "Error removing peer %A" networkAddress)
        }

    (*
        It declares a peer as dead.
        - remove it from active nodes
        - add it to dead nodes
        - remove its timers
        - set to be removed from the dead-nodes. so that if it recovers can be added
    *)
    let setPendingDeadPeer (peerAddress : NetworkAddress) =
        async {
            do! Async.Sleep gossipConfig.MissedHeartbeatIntervalMillis
            if (isWhitelisted peerAddress) then
                cancelPeerStateMonitoring peerAddress
            else
                Log.verbosef "Peer potentially DEAD: %s" peerAddress.Value
                match activePeers.TryGetValue peerAddress with
                | true, activePeer ->
                    activePeers.TryRemove peerAddress |> ignore
                    deadPeers.AddOrUpdate (peerAddress, activePeer, fun _ _ -> activePeer) |> ignore
                    monitorPendingDeadPeer peerAddress
                | _ -> ()
        }

    let monitorActivePeer peerAddress =
        cancelPeerStateMonitoring peerAddress

        if not (isWhitelisted peerAddress) then
            let cts = new CancellationTokenSource()
            peersStateMonitor.AddOrUpdate (peerAddress, cts, fun _ _ -> cts) |> ignore
            Async.Start ((setPendingDeadPeer peerAddress), cts.Token)

    let updateActivePeer peer =
        if not (excludedPeers.ContainsKey peer.NetworkAddress) then
            activePeers.AddOrUpdate (peer.NetworkAddress, peer, fun _ _ -> peer) |> ignore

    let saveActivePeer peer =
        if not (excludedPeers.ContainsKey peer.NetworkAddress)
            && activePeers.Count < nodeConfig.MaxConnectedPeers then
            activePeers.AddOrUpdate (peer.NetworkAddress, peer, fun _ _ -> peer) |> ignore
            savePeerNode peer.NetworkAddress
        else
            Ok()

    let startCachedDataMonitor (entries : ConcurrentDictionary<_, DateTime>) =
        let rec loop () =
            async {
                let lastValidTime = DateTime.UtcNow.AddMilliseconds(-gossipConfig.PeerResponseThrottlingTime |> float)
                entries
                |> List.ofDict
                |> List.filter (fun (_, fetchedAt) -> fetchedAt < lastValidTime)
                |> List.iter (fun (entry, _) ->
                    entries.TryRemove entry |> ignore
                )
                do! Async.Sleep(gossipConfig.GossipIntervalMillis)
                return! loop ()
            }

        if gossipConfig.PeerResponseThrottlingTime > 0 then
            Async.Start (loop (), cts.Token)

    let getGossipFanout () =
        Math.Max (4, gossipConfig.FanoutPercentage * validatorsCache.Count / 100 + 1)

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Public
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    member __.GetListenAddress () =
        nodeConfig.ListeningAddress

    member __.GetPublicAddress () =
        nodeConfigPublicIPAddress

    member __.Identity =
        nodeConfig.Identity

    member __.GetNetworkStats () =
        let receivesGossip = lastMessageReceivedTimestamp.AddSeconds(30.) >= DateTime.UtcNow
        {
            ReceivesGossip = receivesGossip
        }

    member private __.Throttle (entries : ConcurrentDictionary<_, DateTime>) (entryBatch : _ list) func =
        let validEntries = new HashSet<_>()
        entryBatch
        |> List.iter (fun entry ->
            match entries.TryGetValue entry with
            | true, timestamp
                when timestamp > DateTime.UtcNow.AddMilliseconds(-gossipConfig.PeerResponseThrottlingTime |> float) ->
                () // Throttle the request
            | _ ->
                if gossipConfig.PeerResponseThrottlingTime > 0 then
                    let timestamp = DateTime.UtcNow
                    entries.AddOrUpdate(entry, timestamp, fun _ _ -> timestamp)
                    |> ignore
                    validEntries.Add entry |> ignore
        )

        validEntries
        |> Seq.toList
        |> (fun entries -> if not entries.IsEmpty then func entries)

    member private __.StartSentRequestsMonitor () =
        startCachedDataMonitor sentRequests

    member private __.StartReceivedRequestsMonitor () =
        startCachedDataMonitor receivedRequests

    member private __.StartSentGossipMessagesMonitor () =
        startCachedDataMonitor sentGossipMessages

    member private __.StartReceivedGossipMessagesMonitor () =
        startCachedDataMonitor receivedGossipMessages

    member __.StartGossip publishEvent =
        cts <- new CancellationTokenSource()
        Log.debugf "Node identity is %s" (nodeConfig.Identity.Value |> Conversion.bytesToString)
        let networkId = getNetworkId ()
        initTransport
            networkId.Value
            nodeConfig.Identity.Value
            nodeConfig.NetworkSendoutRetryTimeout
            nodeConfig.PeerMessageMaxSize
            (__.ReceivePeerMessage publishEvent)
        __.StartNode ()
        __.StartGossipDiscovery ()
        Log.info "Network layer initialized"

    member __.StopGossip () =
        cts.Cancel()
        closeAllConnections ()

    member __.GetActivePeers () =
        let result =
            activePeers
            |> List.ofDict
            |> List.map (fun (_, m) -> m)

        // Fallback to boostrapnodes when no peers available.
        match result with
        | [] | [_] ->
            __.BootstrapNode ()
        | _ -> ()

        result |> List.choose gossipPeerWithIpAddress

    member __.ReceivePeers msg =
        // Keep max allowed peers.
        let receivedPeers =
            msg.ActivePeers
            |> List.shuffle
            |> List.truncate nodeConfig.MaxConnectedPeers
            |> List.choose gossipPeerWithIpAddress

        // Filter the existing peers, if any, (used to refresh connection, i.e increase heartbeat).
        let existingPeers =
            receivedPeers
            |> Set.ofList
            |> Set.intersect (__.GetActivePeers() |> Set.ofList)
            |> Set.toList

        let activePeersCount = __.GetActivePeers().Length

        // Max peers count reached.
        if activePeersCount >= nodeConfig.MaxConnectedPeers then
            // Exclude non-whitelisted peer.
            __.GetActivePeers()
            |> List.shuffle
            |> List.filter (fun peer -> not (isWhitelisted peer.NetworkAddress))
            |> List.tryHead
            |> Option.iter (fun peer ->
                // Shallow exclusion.
                activePeers.TryRemove peer.NetworkAddress |> ignore
                excludedPeers.AddOrUpdate(peer.NetworkAddress, peer, fun _ _ -> peer) |> ignore
                // Deep exclusion.
                let cts = new CancellationTokenSource()
                Async.Start ((setPeerForExclusion peer.NetworkAddress), cts.Token)
            )

        // If active peers count reached max, onboard a new peer, otherwise onboard up to max.
        let take = Math.Max(1, nodeConfig.MaxConnectedPeers - activePeersCount)
        receivedPeers
        |> List.except existingPeers
        |> List.except excludedPeers.Values
        |> List.shuffle
        |> List.truncate take
        |> List.append existingPeers
        |> __.MergePeerList

    member __.SendMessage message =
        let sendMessageTask =
            async {
                match message.PeerMessage with
                | GossipDiscoveryMessage _ ->
                    __.SelectRandomPeers()
                    |> List.iter (fun m ->
                        Log.verbosef "Sending peerlist to: %s" m.NetworkAddress.Value
                        let peerMessageEnvelopeDto =
                            Mapping.peerMessageEnvelopeToDto Serialization.serializeBinary message
                        sendGossipDiscoveryMessage
                            peerMessageEnvelopeDto
                            m.NetworkAddress.Value
                    )
                | MulticastMessage _ ->
                    let peerMessageEnvelopeDto =
                        Mapping.peerMessageEnvelopeToDto Serialization.serializeBinary message
                    let multicastAddresses =
                        getCurrentValidators ()
                        |> List.choose (fun v -> v.NetworkAddress.Value |> memoizedConvertToIpAddress)
                        |> List.filter (isSelf >> not)
                        |> List.map (fun a -> a.Value)

                    sendMulticastMessage
                        peerMessageEnvelopeDto
                        multicastAddresses
                | GossipMessage m -> __.Throttle sentGossipMessages [ m.MessageId ] (fun _ -> __.SendGossipMessage m)
                | _ -> ()
            }
        Async.Start (sendMessageTask, cts.Token)

    member __.IsRequestPending requestId =
        peerSelectionSentRequests.ContainsKey requestId

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Gossip Discovery
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    member private __.StartValidatorsCache () =
        let rec loop () =
            async {
                let newValidators =
                    getCurrentValidators ()
                    |> List.choose (fun v -> v.NetworkAddress.Value |> memoizedConvertToIpAddress)

                // Remove old validators.
                validatorsCache
                |> List.ofDict
                |> List.iter (fun (v, _) ->
                    if not (newValidators |> List.contains v) then
                        validatorsCache.TryRemove v |> ignore
                )

                // Add new validators.
                newValidators
                |> List.filter (validatorsCache.ContainsKey >> not)
                |> List.iter (fun v -> validatorsCache.AddOrUpdate (v, None, fun _ _ -> None) |> ignore)

                do! Async.Sleep(30_000)
                return! loop ()
            }
        Async.Start (loop (), cts.Token)

    member private __.StartDnsResolver () =
        let rec loop () =
            async {
                let lastValidTime = DateTime.UtcNow.AddSeconds(-nodeConfig.DnsResolverCacheExpirationTime |> float)
                dnsResolverCache
                |> List.ofDict
                |> List.filter (fun (_, (_, fetchedAt)) -> fetchedAt < lastValidTime)
                |> List.iter (fun (dns, _) ->
                    dnsResolverCache.TryRemove dns |> ignore
                )
                do! Async.Sleep(gossipConfig.GossipDiscoveryIntervalMillis)
                return! loop ()
            }
        Async.Start (loop (), cts.Token)

    member private __.StartNode () =
        Log.debug "Starting node..."
        __.InitializePeerList ()
        __.StartDnsResolver ()
        __.StartValidatorsCache ()
        __.StartSentRequestsMonitor ()
        __.StartReceivedRequestsMonitor ()
        __.StartSentGossipMessagesMonitor ()
        __.StartReceivedGossipMessagesMonitor ()
        __.StartServer ()

    member private __.StartServer () =
        Log.infof "Listening to peers on: %s" nodeConfig.ListeningAddress.Value
        nodeConfigPublicIPAddress |> Option.iter (fun a -> Log.infof "Public address: %s" a.Value)
        receiveMessage nodeConfig.ListeningAddress.Value

    member private __.StartGossipDiscovery () =
        let rec loop () =
            async {
                __.Discover ()
                do! Async.Sleep(gossipConfig.GossipDiscoveryIntervalMillis)
                return! loop ()
            }
        Async.Start (loop (), cts.Token)

    member private __.Discover () =
        __.IncreaseHeartbeat()
        match nodeConfig.PublicAddress with
        | Some address -> // Propagate discovery message.
            // Propagate public address along (used to handle peer ip change).
            let self = { GossipPeer.NetworkAddress = address; Heartbeat = 0L }
            {
                PeerMessageEnvelope.NetworkId = getNetworkId ()
                PeerMessage = { ActivePeers = self :: __.GetActivePeers() } |> GossipDiscoveryMessage
            }
            |> __.SendMessage
        | None -> // Request peer list.
            __.SendRequestDataMessage [ NetworkMessageId.PeerList ] None

        printActivePeers ()

    member private __.InitializePeerList () =
        let publicAddress = nodeConfigPublicIPAddress |> optionToList
        getAllPeerNodes () @ nodeConfig.BootstrapNodes @ publicAddress
        |> List.choose (fun a -> memoizedConvertToIpAddress a.Value)
        |> Set.ofList
        |> Set.iter (fun ip ->
            let heartbeat = if isSelf ip then -2L else 0L
            __.AddPeer { NetworkAddress = ip; Heartbeat = heartbeat }
        )

    member private __.BootstrapNode () =
        let publicAddress = nodeConfigPublicIPAddress |> optionToList
        nodeConfig.BootstrapNodes @ publicAddress
        |> List.choose (fun a -> memoizedConvertToIpAddress a.Value)
        |> Set.ofList
        |> Set.iter (fun ip ->
            let found, _ = activePeers.TryGetValue ip
            if not found then
                __.AddPeer { NetworkAddress = ip; Heartbeat = 0L }
        )

    member private __.AddPeer peer =
        Log.verbosef "Adding new peer: %s" peer.NetworkAddress.Value
        saveActivePeer peer |> Result.iterError Log.appErrors
        if not (isSelf peer.NetworkAddress) then
            monitorActivePeer peer.NetworkAddress

    member private __.UpdatePeer peer =
        __.GetActivePeer peer.NetworkAddress
        |> Option.iter (fun _ ->
            updateActivePeer peer
            monitorActivePeer peer.NetworkAddress
        )

    member private __.ResetLocalPeer localPeer =
        __.UpdatePeer {localPeer with Heartbeat = 0L}

    member private __.MergePeerList peers =
        peers |> List.iter __.MergePeer

    member private __.MergePeer peer =
        if not (isSelf peer.NetworkAddress) then
            Log.verbosef "Received peer %s with heartbeat %i"
                peer.NetworkAddress.Value
                peer.Heartbeat
            if peer.Heartbeat = -1L then
                Log.verbosef "Peer %s has been restarted, reset its heartbeat to 0"
                    peer.NetworkAddress.Value
            match __.GetActivePeer peer.NetworkAddress with
            | Some localPeer ->
                if peer.Heartbeat = -1L then
                    __.ResetLocalPeer localPeer
                else if localPeer.Heartbeat < peer.Heartbeat then
                    __.UpdatePeer peer
            | None ->
                if peer.Heartbeat = -1L || not (isDead peer) then
                    let heartbeat = max peer.Heartbeat 0L
                    __.AddPeer { peer with Heartbeat = heartbeat }
                    deadPeers.TryRemove peer.NetworkAddress |> ignore

    member private __.GetActivePeer networkAddress =
        __.GetActivePeers() |> List.tryFind (fun m -> m.NetworkAddress = networkAddress)

    member private __.IncreaseHeartbeat () =
        nodeConfigPublicIPAddress
        |> Option.iter (fun ipAddress ->
            __.GetActivePeer ipAddress
            |> Option.iter (fun m ->
                let localPeer = {
                    NetworkAddress = m.NetworkAddress
                    Heartbeat = m.Heartbeat + 1L
                }
                updateActivePeer localPeer
            )
        )

    member private __.SelectRandomPeers () =
        __.GetActivePeers()
        |> List.filter (fun m -> not (isSelf m.NetworkAddress))
        |> List.shuffle
        |> List.truncate (getGossipFanout ())

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Gossip Message Passing
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    member private __.UpdateGossipMessagesProcessingQueue networkAddresses gossipMessageId =
        let found, processedAddresses = gossipMessages.TryGetValue gossipMessageId
        let newProcessedAddresses = if found then networkAddresses @ processedAddresses else networkAddresses

        gossipMessages.AddOrUpdate(
            gossipMessageId,
            newProcessedAddresses,
            fun _ _ -> newProcessedAddresses) |> ignore

    member private __.SendGossipMessageToRecipient recipientAddress (gossipMessage : GossipMessage) =
        match activePeers.TryGetValue recipientAddress with
        | true, targetPeer ->
            Log.verbosef "Sending gossip message %A to %s"
                gossipMessage.MessageId
                recipientAddress.Value

            let peerMessageEnvelopeDto =
                {
                    PeerMessageEnvelope.NetworkId = getNetworkId ()
                    PeerMessage = gossipMessage |> GossipMessage
                }
                |> Mapping.peerMessageEnvelopeToDto Serialization.serializeBinary

            sendGossipMessage peerMessageEnvelopeDto targetPeer.NetworkAddress.Value
        | _ -> ()

    member private __.ProcessGossipMessage (gossipMessage : GossipMessage) recipientAddresses =
        match recipientAddresses with
        // No recipients left to send message to, remove gossip message from the processing queue.
        | [] -> gossipMessages.TryRemove gossipMessage.MessageId |> ignore
        // If two or more recipients left, select randomly a subset (fanout) of recipients to send the
        // gossip message to.
        // If gossip message was processed before, append the selected recipients to the processed recipients list.
        // If not, add the gossip message (and the corresponding recipient) to the processing queue.
        | _ ->
            let fanoutRecipientAddresses =
                recipientAddresses
                |> List.shuffle
                |> List.truncate (getGossipFanout ())

            fanoutRecipientAddresses
            |> List.iter (fun recipientAddress ->
                __.SendGossipMessageToRecipient recipientAddress gossipMessage)

            let senderAddress = gossipMessage.SenderAddress |> optionToList
            __.UpdateGossipMessagesProcessingQueue
                (fanoutRecipientAddresses @ senderAddress)
                gossipMessage.MessageId

    member private __.SendGossipMessage message =
        let gossipFanout = getGossipFanout ()

        let rec loop (msg : GossipMessage) =
            async {
                let senderAddress = msg.SenderAddress |> optionToList
                let processedAddresses =
                    match gossipMessages.TryGetValue msg.MessageId with
                    | true, processedAddresses -> processedAddresses @ senderAddress
                    | _ -> senderAddress

                let remainingRecipientAddresses =
                    __.GetActivePeers()
                    |> List.map (fun m -> m.NetworkAddress)
                    |> List.except processedAddresses
                    |> List.filter (isSelf >> not)

                __.ProcessGossipMessage msg remainingRecipientAddresses

                if remainingRecipientAddresses.Length >= gossipFanout then
                    do! Async.Sleep(gossipConfig.GossipIntervalMillis)
                    return! loop msg
            }

        Async.Start (loop message, cts.Token)

    member private __.ReceiveGossipMessage publishEvent (gossipMessage : GossipMessage) =
        match gossipMessages.TryGetValue gossipMessage.MessageId with
        | true, processedAddresses ->
            gossipMessage.SenderAddress |> Option.iter (fun senderAddress ->
                if not (processedAddresses |> List.contains senderAddress) then
                    let addresses = senderAddress :: processedAddresses
                    gossipMessages.AddOrUpdate(
                        gossipMessage.MessageId,
                        addresses,
                        fun _ _ -> addresses) |> ignore
            )

        | false, _ ->
            let fromMsg =
                match gossipMessage.SenderAddress with
                | Some a -> sprintf "from %s" a.Value
                | None -> ""

            Log.verbosef "Received gossip message %A %s"
                gossipMessage.MessageId
                fromMsg

            // Make sure the message is not processed twice.
            gossipMessages.AddOrUpdate(
                gossipMessage.MessageId,
                [],
                fun _ _ -> []) |> ignore

            let peerMessageReceivedEvent =
                {
                    PeerMessageEnvelope.NetworkId = getNetworkId ()
                    PeerMessage = gossipMessage |> GossipMessage
                }
                |> PeerMessageReceived

            // Throttle received gossip messages.
            __.Throttle receivedGossipMessages [ gossipMessage.MessageId ] (fun _ ->
                publishEvent peerMessageReceivedEvent
            )

    member private __.ReceivePeerMessage publishEvent dto =
        try
            let peerMessageEnvelope = Mapping.peerMessageEnvelopeFromDto Serialization.deserializePeerMessage dto
            match peerMessageEnvelope.PeerMessage with
            | GossipDiscoveryMessage m ->
                lastMessageReceivedTimestamp <- DateTime.UtcNow
                __.ReceivePeers m
            | GossipMessage m ->
                lastMessageReceivedTimestamp <- DateTime.UtcNow
                __.ReceiveGossipMessage publishEvent m
            | MulticastMessage m ->
                lastMessageReceivedTimestamp <- DateTime.UtcNow
                __.ReceiveMulticastMessage publishEvent m
            | RequestDataMessage m -> __.ReceiveRequestMessage publishEvent m
            | ResponseDataMessage m -> __.ReceiveResponseMessage publishEvent m
        with
        | ex when ex.Message.Contains("code is invalid") ->
            Log.warningf "Cannot deserialize peer message"
            Log.debug ex.AllMessagesAndStackTraces
        | ex ->
            Log.warning ex.AllMessages
            Log.debug ex.AllMessagesAndStackTraces

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Multicast Message Passing
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    member private __.ReceiveMulticastMessage publishEvent multicastMessage =
        {
            PeerMessageEnvelope.NetworkId = getNetworkId ()
            PeerMessage = multicastMessage |> MulticastMessage
        }
        |> PeerMessageReceived
        |> publishEvent

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Request/Response
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    member __.SendRequestDataMessage requestIds (preferredPeer : NetworkAddress option) =
        __.Throttle sentRequests requestIds (fun validRequestIds ->
            Stats.increment Stats.Counter.PeerRequests
            Log.debugf "Sending request for %A" validRequestIds
            let rec loop messageIds preferredPeer =
                async {

                    let usedAddresses = new HashSet<NetworkAddress>()
                    messageIds
                    |> List.iter (fun messageId ->
                        match messageId with
                        | Tx _ -> Stats.increment Stats.Counter.RequestedTxs
                        | Block _ -> Stats.increment Stats.Counter.RequestedBlocks
                        | _ -> ()

                        match peerSelectionSentRequests.TryGetValue messageId with
                        | true, addresses -> usedAddresses.UnionWith(addresses)
                        | _ -> ()
                    )

                    let targetedAddresses = usedAddresses |> Seq.toList
                    let selectedPeer =
                        __.GetActivePeers()
                        |> List.map (fun m -> m.NetworkAddress)
                        |> List.except targetedAddresses
                        |> List.filter (isSelf >> not)
                        |> __.SelectPeer preferredPeer
                        |> tee (fun address ->
                            messageIds
                            |> List.iter (fun messageId ->
                                match address with
                                | Some networkAddress ->
                                    peerSelectionSentRequests.AddOrUpdate(
                                        messageId,
                                        networkAddress :: targetedAddresses,
                                        fun _ _ -> networkAddress :: targetedAddresses)
                                    |> ignore
                                | None ->
                                    Stats.increment Stats.Counter.PeerRequestFailures
                                    Log.errorf "Cannot retrieve data from peers for %A" messageId
                                    peerSelectionSentRequests.TryRemove messageId |> ignore
                                )
                        )
                    match selectedPeer with
                    | Some address ->
                        let peerMessageEnvelope =
                            {
                                PeerMessageEnvelope.NetworkId = getNetworkId ()
                                PeerMessage =
                                    {
                                        Items = validRequestIds
                                        SenderIdentity = nodeConfig.Identity
                                    }
                                    |> RequestDataMessage
                            }
                        let peerMessageEnvelopeDto =
                            Mapping.peerMessageEnvelopeToDto Serialization.serializeBinary peerMessageEnvelope
                        sendRequestMessage peerMessageEnvelopeDto address.Value

                        do! Async.Sleep(4 * gossipConfig.GossipIntervalMillis)

                        (*
                            If no answer is received within 2 cycles (request - response i.e 4xtCycle),
                            repeat (i.e choose another peer).
                        *)
                        let requestsWithNoReplies =
                            messageIds
                            |> List.choose (fun messageId ->
                                match (peerSelectionSentRequests.TryGetValue messageId) with
                                | true, addresses when not (addresses.IsEmpty) -> Some messageId
                                | _ -> None
                            )

                        if not requestsWithNoReplies.IsEmpty then
                            Stats.increment Stats.Counter.PeerRequestTimeouts
                            return! loop requestsWithNoReplies None
                    | None -> ()
                }

            Async.Start (loop validRequestIds preferredPeer, cts.Token)
        )

    member __.SendResponseDataMessage (targetIdentity : PeerNetworkIdentity) peerMessageEnvelope =
        Stats.increment Stats.Counter.PeerResponses
        match peerMessageEnvelope.PeerMessage with
        | ResponseDataMessage responseMessage ->
            Log.debugf "Sending response (to %A request) to %s"
                responseMessage.Items.Head.MessageId
                (targetIdentity.Value |> Conversion.bytesToString)

            let unicastMessageTask =
                async {
                    let peerMessageEnvelopeDto =
                        Mapping.peerMessageEnvelopeToDto Serialization.serializeBinary peerMessageEnvelope
                    sendResponseMessage peerMessageEnvelopeDto targetIdentity.Value
                }
            Async.Start (unicastMessageTask, cts.Token)
        | _ -> ()

    member private __.ReceiveRequestMessage publishEvent (requestDataMessage : RequestDataMessage) =
        __.Throttle receivedRequests [ requestDataMessage ] (fun _ ->
            Log.debugf "Received request for %A from %s"
                requestDataMessage.Items.Head
                (requestDataMessage.SenderIdentity.Value |> Conversion.bytesToString)
            {
                PeerMessageEnvelope.NetworkId = getNetworkId ()
                PeerMessage = requestDataMessage |> RequestDataMessage
            }
            |> PeerMessageReceived
            |> publishEvent
        )

    member private __.ReceiveResponseMessage publishEvent (responseDataMessage : ResponseDataMessage) =
        Log.debugf "Received response to %A request" responseDataMessage.Items.Head.MessageId
        {
            PeerMessageEnvelope.NetworkId = getNetworkId ()
            PeerMessage = responseDataMessage |> ResponseDataMessage
        }
        |> PeerMessageReceived
        |> publishEvent

        responseDataMessage.Items
        |> List.iter (fun response -> peerSelectionSentRequests.TryRemove response.MessageId |> ignore)

    member private __.SelectPeer preferredPeer peerList =
        match preferredPeer with
        | Some _ -> preferredPeer
        | None ->
            peerList
            |> List.shuffle
            |> List.tryHead
