namespace Own.Blockchain.Public.Net

open System
open System.Net
open System.Collections.Concurrent
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

    let activeMembers = new ConcurrentDictionary<NetworkAddress, GossipMember>()
    let deadMembers = new ConcurrentDictionary<NetworkAddress, GossipMember>()
    let memberStateMonitor = new ConcurrentDictionary<NetworkAddress, CancellationTokenSource>()

    let cacheValidators = new ConcurrentBag<NetworkAddress>()

    let gossipMessages = new ConcurrentDictionary<NetworkMessageId, NetworkAddress list>()
    let peerSelectionSentRequests = new ConcurrentDictionary<NetworkMessageId, NetworkAddress list>()
    let mutable lastMessageReceivedTimestamp = DateTime.UtcNow
    let sentRequests = new ConcurrentDictionary<NetworkMessageId, DateTime>()
    let receivedRequests = new ConcurrentDictionary<RequestDataMessage, DateTime>()

    let dnsResolverCache = new ConcurrentDictionary<string, NetworkAddress * DateTime>()

    let cts = new CancellationTokenSource()

    let printActiveMembers () =
        #if DEBUG
            Log.verbose "==================== ACTIVE CONNECTIONS ===================="
            activeMembers
            |> Seq.ofDict
            |> Seq.iter (fun (a, m) -> Log.verbosef "%s Heartbeat:%i" a.Value m.Heartbeat)
            Log.verbose "============================================================"
        #else
            ()
        #endif

    let convertToIpAddress (networkAddress : string) =
        match networkAddress.LastIndexOf ":" with
        | index when index > 0 ->
            let port = networkAddress.Substring(index + 1)
            match UInt16.TryParse port with
            | true, 0us ->
                Log.verbose "Received peer with port 0 discarded"
                None
            | true, _ ->
                try
                    let host = networkAddress.Substring(0, index)
                    let ipAddress =
                        Dns.GetHostAddresses(host)
                        |> Array.sortBy (fun ip -> ip.AddressFamily)
                        |> Array.head
                    let isPrivateIp = ipAddress.IsPrivate()
                    if not nodeConfig.AllowPrivateNetworkPeers && isPrivateIp then
                        Log.verbose "Private IPs are not allowed as peers"
                        None
                    else
                        sprintf "%s:%s" (ipAddress.ToString()) port
                        |> NetworkAddress
                        |> Some
                with
                | ex ->
                    Log.warningf "[%s] %s" networkAddress ex.AllMessages
                    None
            | _ ->
                Log.verbosef "Invalid port value: %s" networkAddress
                None
        | _ ->
            Log.verbosef "Invalid peer format: %s" networkAddress
            None

    let memoizedConvertToIpAddress (networkAddress : string) =
        match dnsResolverCache.TryGetValue networkAddress with
        | true, (ip, _) -> Some ip
        | _ ->
            convertToIpAddress networkAddress
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

    let gossipMemberWithIpAddress m =
        m.NetworkAddress.Value
        |> memoizedConvertToIpAddress
        |> Option.map (fun ip -> {m with NetworkAddress = ip})

    let isSelf networkAddress =
        nodeConfigPublicIPAddress = Some networkAddress

    let isWhitelisted networkAddress =
        isSelf networkAddress
        || nodeConfig.BootstrapNodes |> List.contains networkAddress
        || cacheValidators |> Seq.contains networkAddress

    let optionToList = function | Some x -> [x] | None -> []

    (*
        A member is dead if it's in the list of dead-members and
        the heartbeat the local node is bigger than the one passed by argument.
    *)
    let isDead inputMember =
        match deadMembers.TryGetValue inputMember.NetworkAddress with
        | true, deadMember ->
            Log.verbosef "Received a node with heartbeat %i - in dead-members it has heartbeat %i"
                inputMember.Heartbeat
                deadMember.Heartbeat
            deadMember.Heartbeat >= inputMember.Heartbeat
        | _ -> false

    (*
        Once a member has been declared dead and it hasn't recovered in
        2xTFail time is removed from the dead-members list.
        So if node has been down for a while and come back it can be added again.
        Here this will be scheduled right after a node is declared, so total time
        elapsed is 2xTFail
    *)
    let setFinalDeadMember networkAddress =
        async {
            do! Async.Sleep gossipConfig.MissedHeartbeatIntervalMillis
            let found, _ = activeMembers.TryGetValue networkAddress
            if not found then
                Log.warningf "Member marked as DEAD %s" networkAddress.Value

                deadMembers.TryRemove networkAddress |> ignore
                memberStateMonitor.TryRemove networkAddress |> ignore
                networkAddress.Value |> closeConnection

                removePeerNode networkAddress
                |> Result.iterError (fun _ -> Log.errorf "Error removing member %A" networkAddress)
        }

    let monitorPendingDeadMember networkAddress =
        let cts = new CancellationTokenSource()
        Async.Start ((setFinalDeadMember networkAddress), cts.Token)
        memberStateMonitor.AddOrUpdate (networkAddress, cts, fun _ _ -> cts) |> ignore

    (*
        It declares a member as dead.
        - remove it from active nodes
        - add it to dead nodes
        - remove its timers
        - set to be removed from the dead-nodes. so that if it recovers can be added
    *)
    let setPendingDeadMember (networkAddress : NetworkAddress) =
        async {
            do! Async.Sleep gossipConfig.MissedHeartbeatIntervalMillis
            Log.verbosef "Member potentially DEAD: %s" networkAddress.Value
            match activeMembers.TryGetValue networkAddress with
            | true, activeMember ->
                activeMembers.TryRemove networkAddress |> ignore
                deadMembers.AddOrUpdate (networkAddress, activeMember, fun _ _ -> activeMember) |> ignore
                monitorPendingDeadMember networkAddress
            | _ -> ()
        }

    let monitorActiveMember address =
        match memberStateMonitor.TryGetValue address with
        | true, cts -> cts.Cancel()
        | _ -> ()

        if not (isWhitelisted address) then
            let cts = new CancellationTokenSource()
            Async.Start ((setPendingDeadMember address), cts.Token)
            memberStateMonitor.AddOrUpdate (address, cts, fun _ _ -> cts) |> ignore

    let updateActiveMember m =
        activeMembers.AddOrUpdate (m.NetworkAddress, m, fun _ _ -> m) |> ignore

    let saveActiveMember m =
        activeMembers.AddOrUpdate (m.NetworkAddress, m, fun _ _ -> m) |> ignore
        savePeerNode m.NetworkAddress

    let startRequestsMonitor (requestsMap : ConcurrentDictionary<_, DateTime>) =
        let rec loop () =
            async {
                let lastValidTime = DateTime.UtcNow.AddMilliseconds(-gossipConfig.PeerResponseThrottlingTime |> float)
                requestsMap
                |> List.ofDict
                |> List.filter (fun (_, fetchedAt) -> fetchedAt < lastValidTime)
                |> List.iter (fun (requestItem, _) ->
                    requestsMap.TryRemove requestItem |> ignore
                )
                do! Async.Sleep(gossipConfig.GossipIntervalMillis)
                return! loop ()
            }

        if gossipConfig.PeerResponseThrottlingTime > 0 then
            Async.Start (loop (), cts.Token)

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Public
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    member __.GetListenAddress () =
        nodeConfig.ListeningAddress

    member __.GetPublicAddress () =
        nodeConfigPublicIPAddress

    member __.Identity =
        nodeConfig.Identity

    member __.StartDnsResolver () =
        let rec loop () =
            async {
                let lastValidTime = DateTime.UtcNow.AddMinutes(-nodeConfig.DnsResolverCacheExpirationTime |> float)
                dnsResolverCache
                |> List.ofDict
                |> List.filter (fun (_, (_, fetchedAt)) -> fetchedAt < lastValidTime)
                |> List.iter (fun (dns, (ip, _)) ->
                    convertToIpAddress dns
                    |> Option.iter (fun newIp ->
                        if newIp <> ip then
                            let cacheValue = newIp, DateTime.UtcNow
                            dnsResolverCache.AddOrUpdate(dns, cacheValue, fun _ _ -> cacheValue) |> ignore
                    )
                )
                do! Async.Sleep(gossipConfig.GossipDiscoveryIntervalMillis)
                return! loop ()
            }
        Async.Start (loop (), cts.Token)

    member __.GetNetworkStats () =
        let receivesGossip = lastMessageReceivedTimestamp.AddSeconds(30.) >= DateTime.UtcNow
        {
            ReceivesGossip = receivesGossip
        }

    member __.StartSentRequestsMonitor () =
        startRequestsMonitor sentRequests

    member __.StartReceivedRequestsMonitor () =
        startRequestsMonitor receivedRequests

    member private __.StartValidatorsCache () =
        let rec loop () =
            async {
                cacheValidators.Clear()
                getCurrentValidators ()
                |> List.choose (fun v -> v.NetworkAddress.Value |> memoizedConvertToIpAddress)
                |> List.iter (fun a -> cacheValidators.Add a |> ignore)

                do! Async.Sleep(30_000)
                return! loop ()
            }
        Async.Start (loop (), cts.Token)

    member __.StartGossip publishEvent =
        Log.debugf "Node identity is %s" (nodeConfig.Identity.Value |> Conversion.bytesToString)
        let networkId = getNetworkId ()
        initTransport
            networkId.Value
            nodeConfig.Identity.Value
            nodeConfig.NetworkSendoutRetryTimeout
            (__.ReceivePeerMessage publishEvent)
        __.StartNode ()
        __.StartGossipDiscovery ()
        Log.info "Network layer initialized"

    member __.StopGossip () =
        cts.Cancel()
        closeAllConnections ()

    member __.GetActiveMembers () =
        let result =
            activeMembers
            |> List.ofDict
            |> List.map (fun (_, m) -> m)

        // Fallback to boostrapnodes when no peers available.
        match result with
        | [] | [_] ->
            __.BootstrapNode ()
        | _ -> ()

        result |> List.choose gossipMemberWithIpAddress

    member __.ReceiveMembers msg =
        // Keep max allowed peers.
        let receivedMembers =
            msg.ActiveMembers
            |> List.shuffle
            |> List.truncate nodeConfig.MaxConnectedPeers
            |> List.choose gossipMemberWithIpAddress

        // Filter the existing peers, if any, (used to refresh connection, i.e increase heartbeat).
        let existingMembers =
            receivedMembers
            |> List.filter (fun m -> m.NetworkAddress |> __.GetActiveMember |> Option.isSome)

        let activeMembersCount = __.GetActiveMembers() |> List.length
        let take = nodeConfig.MaxConnectedPeers - activeMembersCount

        receivedMembers
        |> List.except existingMembers
        |> List.shuffle
        |> List.truncate take
        |> List.append existingMembers
        |> __.MergeMemberList

    member __.SendMessage message =
        let sendMessageTask =
            async {
                match message.PeerMessage with
                | GossipDiscoveryMessage _ ->
                    __.SelectRandomMembers()
                    |> List.iter (fun m ->
                        Log.verbosef "Sending memberlist to: %s" m.NetworkAddress.Value
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

                | GossipMessage m -> __.SendGossipMessage m
                | _ -> ()
            }
        Async.Start (sendMessageTask, cts.Token)

    member __.IsRequestPending requestId =
        peerSelectionSentRequests.ContainsKey requestId

    member private __.Throttle (requestsMap : ConcurrentDictionary<_, DateTime>) requestItem func =
        match requestsMap.TryGetValue requestItem with
            | true, timestamp
                when timestamp > DateTime.UtcNow.AddMilliseconds(-gossipConfig.PeerResponseThrottlingTime |> float) ->
                () // Throttle the request
            | _ ->
                if gossipConfig.PeerResponseThrottlingTime > 0 then
                    let timestamp = DateTime.UtcNow
                    requestsMap.AddOrUpdate(requestItem, timestamp, fun _ _ -> timestamp)
                    |> ignore

                func requestItem

    member __.SendRequestDataMessage requestId (preferredPeer : NetworkAddress option) =
        __.Throttle sentRequests requestId (fun _ ->
            Stats.increment Stats.Counter.PeerRequests
            Log.debugf "Sending request for %A" requestId
            let rec loop messageId peer =
                async {
                    let usedAddresses =
                        match peerSelectionSentRequests.TryGetValue messageId with
                        | true, addresses -> addresses
                        | _ -> []

                    __.GetActiveMembers()
                    |> List.map (fun m -> m.NetworkAddress)
                    |> List.except usedAddresses
                    |> List.filter (isSelf >> not)
                    |> __.SelectPeer peer
                    |> tee (function
                        | Some networkAddress ->
                            peerSelectionSentRequests.AddOrUpdate(
                                messageId,
                                networkAddress :: usedAddresses,
                                fun _ _ -> networkAddress :: usedAddresses)
                            |> ignore
                        | None ->
                            Log.errorf "Cannot retrieve data from peers for %A" messageId
                            peerSelectionSentRequests.TryRemove messageId |> ignore
                    )
                    |> Option.iter (fun address ->
                        let peerMessageEnvelope =
                            {
                                PeerMessageEnvelope.NetworkId = getNetworkId ()
                                PeerMessage =
                                    {
                                        MessageId = messageId
                                        SenderIdentity = nodeConfig.Identity
                                    }
                                    |> RequestDataMessage
                            }
                        let peerMessageEnvelopeDto =
                            Mapping.peerMessageEnvelopeToDto Serialization.serializeBinary peerMessageEnvelope
                        sendRequestMessage peerMessageEnvelopeDto address.Value
                    )

                    do! Async.Sleep(4 * gossipConfig.GossipIntervalMillis)

                    (*
                        If no answer is received within 2 cycles (request - response i.e 4xtCycle),
                        repeat (i.e choose another peer).
                    *)
                    match (peerSelectionSentRequests.TryGetValue messageId) with
                    | true, addresses ->
                        if not (addresses.IsEmpty) then
                            return! loop messageId None
                    | _ -> ()
                }

            Async.Start (loop requestId preferredPeer, cts.Token)
        )

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Gossip Discovery
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    member private __.StartNode () =
        Log.debug "Start node..."
        __.InitializeMemberList ()
        __.StartDnsResolver ()
        __.StartValidatorsCache ()
        __.StartSentRequestsMonitor ()
        __.StartReceivedRequestsMonitor ()
        __.StartServer ()

    member private __.StartServer () =
        Log.infof "Listen on: %s" nodeConfig.ListeningAddress.Value
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
            let self = { GossipMember.NetworkAddress = address; Heartbeat = 0L }
            {
                PeerMessageEnvelope.NetworkId = getNetworkId ()
                PeerMessage = { ActiveMembers = self :: __.GetActiveMembers() } |> GossipDiscoveryMessage
            }
            |> __.SendMessage
        | None -> // Request peer list.
            __.SendRequestDataMessage NetworkMessageId.PeerList None

        printActiveMembers ()

    member private __.InitializeMemberList () =
        let publicAddress = nodeConfigPublicIPAddress |> optionToList
        getAllPeerNodes () @ nodeConfig.BootstrapNodes @ publicAddress
        |> Set.ofList
        |> Set.iter (fun a ->
            a.Value
            |> memoizedConvertToIpAddress
            |> Option.iter (fun ip ->
                let heartbeat = if isSelf ip then -2L else 0L
                __.AddMember { NetworkAddress = ip; Heartbeat = heartbeat }
            )
        )

    member private __.BootstrapNode () =
        let publicAddress = nodeConfigPublicIPAddress |> optionToList
        nodeConfig.BootstrapNodes @ publicAddress
        |> Set.ofList
        |> Set.iter (fun a ->
            a.Value
            |> memoizedConvertToIpAddress
            |> Option.iter (fun ip ->
                let found, _ = activeMembers.TryGetValue ip
                if not found then
                    __.AddMember { NetworkAddress = ip; Heartbeat = 0L }
            )
        )

    member private __.AddMember inputMember =
        Log.verbosef "Adding new member: %s" inputMember.NetworkAddress.Value
        saveActiveMember inputMember |> Result.iterError Log.appErrors
        if not (isSelf inputMember.NetworkAddress) then
            monitorActiveMember inputMember.NetworkAddress

    member private __.UpdateMember inputMember =
        __.GetActiveMember inputMember.NetworkAddress
        |> Option.iter (fun _ ->
            saveActiveMember inputMember |> Result.iterError Log.appErrors
            monitorActiveMember inputMember.NetworkAddress
        )

    member private __.ResetLocalMember localMember =
        __.UpdateMember {localMember with Heartbeat = 0L}

    member private __.MergeMemberList members =
        members |> List.iter __.MergeMember

    member private __.MergeMember inputMember =
        if not (isSelf inputMember.NetworkAddress) then
            Log.verbosef "Received member %s with heartbeat %i"
                inputMember.NetworkAddress.Value
                inputMember.Heartbeat
            if inputMember.Heartbeat = -1L then
                Log.verbosef "Peer %s has been restarted, reset its heartbeat to 0"
                    inputMember.NetworkAddress.Value
            match __.GetActiveMember inputMember.NetworkAddress with
            | Some localMember ->
                if inputMember.Heartbeat = -1L then
                    __.ResetLocalMember localMember
                else if localMember.Heartbeat < inputMember.Heartbeat then
                    __.UpdateMember inputMember
            | None ->
                if inputMember.Heartbeat = -1L || not (isDead inputMember) then
                    let heartbeat = max inputMember.Heartbeat 0L
                    __.AddMember { inputMember with Heartbeat = heartbeat }
                    deadMembers.TryRemove inputMember.NetworkAddress |> ignore

    member private __.GetActiveMember networkAddress =
        __.GetActiveMembers() |> List.tryFind (fun m -> m.NetworkAddress = networkAddress)

    member private __.IncreaseHeartbeat () =
        nodeConfigPublicIPAddress
        |> Option.iter (fun ipAddress ->
            __.GetActiveMember ipAddress
            |> Option.iter (fun m ->
                let localMember = {
                    NetworkAddress = m.NetworkAddress
                    Heartbeat = m.Heartbeat + 1L
                }
                updateActiveMember localMember
            )
        )

    member private __.SelectRandomMembers () =
        __.GetActiveMembers()
        |> List.filter (fun m -> not (isSelf m.NetworkAddress))
        |> List.shuffle
        |> List.truncate gossipConfig.Fanout

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
        match activeMembers.TryGetValue recipientAddress with
        | true, recipientMember ->
            Log.verbosef "Sending gossip message %A to %s"
                gossipMessage.MessageId
                recipientAddress.Value

            let peerMessageEnvelope = {
                PeerMessageEnvelope.NetworkId = getNetworkId ()
                PeerMessage = gossipMessage |> GossipMessage
            }
            let peerMessageEnvelopeDto =
                Mapping.peerMessageEnvelopeToDto Serialization.serializeBinary peerMessageEnvelope
            let recipientMemberDto = recipientMember |> Mapping.gossipMemberToDto
            sendGossipMessage peerMessageEnvelopeDto recipientMemberDto.NetworkAddress
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
                |> List.truncate gossipConfig.Fanout

            fanoutRecipientAddresses |> List.iter (fun recipientAddress ->
                __.SendGossipMessageToRecipient recipientAddress gossipMessage)

            let senderAddress = gossipMessage.SenderAddress |> optionToList
            __.UpdateGossipMessagesProcessingQueue
                (fanoutRecipientAddresses @ senderAddress)
                gossipMessage.MessageId

    member private __.SendGossipMessage message =
        let rec loop (msg : GossipMessage) =
            async {
                let recipientAddresses =
                    __.GetActiveMembers()
                    |> List.map (fun m -> m.NetworkAddress)

                let senderAddress = msg.SenderAddress |> optionToList
                let remainingRecipientAddresses =
                    match gossipMessages.TryGetValue msg.MessageId with
                    | true, processedAddresses ->
                        List.except (processedAddresses @ senderAddress) recipientAddresses
                    | _ ->
                        List.except senderAddress recipientAddresses

                __.ProcessGossipMessage msg remainingRecipientAddresses

                if remainingRecipientAddresses.Length >= gossipConfig.Fanout then
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

            {
                PeerMessageEnvelope.NetworkId = getNetworkId ()
                PeerMessage = gossipMessage |> GossipMessage
            }
            |> PeerMessageReceived
            |> publishEvent

    member private __.ReceivePeerMessage publishEvent dto =
        let peerMessageEnvelope = Mapping.peerMessageEnvelopeFromDto dto
        match peerMessageEnvelope.PeerMessage with
        | GossipDiscoveryMessage m ->
            lastMessageReceivedTimestamp <- DateTime.UtcNow
            __.ReceiveMembers m
        | GossipMessage m ->
            lastMessageReceivedTimestamp <- DateTime.UtcNow
            __.ReceiveGossipMessage publishEvent m
        | MulticastMessage m ->
            lastMessageReceivedTimestamp <- DateTime.UtcNow
            __.ReceiveMulticastMessage publishEvent m
        | RequestDataMessage m -> __.ReceiveRequestMessage publishEvent m
        | ResponseDataMessage m -> __.ReceiveResponseMessage publishEvent m

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

    member __.SendResponseDataMessage (targetIdentity : PeerNetworkIdentity) peerMessageEnvelope =
        Stats.increment Stats.Counter.PeerResponses
        match peerMessageEnvelope.PeerMessage with
        | ResponseDataMessage responseMessage ->
            Log.debugf "Sending response (to %A request) to %s"
                responseMessage.MessageId
                (targetIdentity.Value |> Conversion.bytesToString)
        | _ -> ()

        let unicastMessageTask =
            async {
                let peerMessageEnvelopeDto =
                    Mapping.peerMessageEnvelopeToDto Serialization.serializeBinary peerMessageEnvelope
                sendResponseMessage peerMessageEnvelopeDto targetIdentity.Value
            }
        Async.Start (unicastMessageTask, cts.Token)

    member private __.ReceiveRequestMessage publishEvent (requestDataMessage : RequestDataMessage) =
        __.Throttle receivedRequests requestDataMessage (fun _ ->
            Log.debugf "Received request for %A from %s"
                requestDataMessage.MessageId
                (requestDataMessage.SenderIdentity.Value |> Conversion.bytesToString)
            {
                PeerMessageEnvelope.NetworkId = getNetworkId ()
                PeerMessage = requestDataMessage |> RequestDataMessage
            }
            |> PeerMessageReceived
            |> publishEvent
        )

    member private __.ReceiveResponseMessage publishEvent (responseDataMessage : ResponseDataMessage) =
        Log.debugf "Received response to %A request" responseDataMessage.MessageId
        {
            PeerMessageEnvelope.NetworkId = getNetworkId ()
            PeerMessage = responseDataMessage |> ResponseDataMessage
        }
        |> PeerMessageReceived
        |> publishEvent

        peerSelectionSentRequests.TryRemove responseDataMessage.MessageId |> ignore

    member private __.SelectPeer preferredPeer peerList =
        match preferredPeer with
        | Some _ -> preferredPeer
        | None ->
            peerList
            |> List.shuffle
            |> List.tryHead
