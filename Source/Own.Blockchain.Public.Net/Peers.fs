namespace Chainium.Blockchain.Public.Net

open System
open System.Collections.Concurrent
open System.Collections.Generic
open System.Threading
open Chainium.Common
open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Core
open Chainium.Blockchain.Public.Core.DomainTypes

module Peers =

    type NetworkNode
        (
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
        getCurrentValidators : unit -> ValidatorSnapshot list,
        config : NetworkNodeConfig,
        fanout,
        tCycle,
        tFail
        ) =

        let activeMembers = new ConcurrentDictionary<NetworkAddress, GossipMember>()
        let deadMembers = new ConcurrentDictionary<NetworkAddress, GossipMember>()
        let memberStateTimers = new ConcurrentDictionary<NetworkAddress, System.Timers.Timer>()
        let gossipMessages = new ConcurrentDictionary<NetworkMessageId, NetworkAddress list>()
        let pendingDataRequests = new ConcurrentDictionary<NetworkMessageId, NetworkAddress list>()
        let cts = new CancellationTokenSource()

        let networkAddressToString (NetworkAddress a) = a

        let extractValuesFromKeyValuePairs (pairs : KeyValuePair<_, _> seq) =
            pairs
            |> Seq.map (fun p -> p.Value)
            |> Seq.toList

        let printActiveMembers () =
            #if DEBUG
                printfn "\n ========= ACTIVE CONNECTIONS [%s] ========="
                    (DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                for m in activeMembers do
                    printfn " %s Heartbeat:%i" (m.Key |> networkAddressToString) m.Value.Heartbeat
                printfn " ================================================================\n"
            #else
                ()
            #endif

        (*
            A member is dead if it's in the list of dead-members and
            the heartbeat the local node is bigger than the one passed by argument.
        *)
        let isDead inputMember =
            match deadMembers.TryGetValue inputMember.NetworkAddress with
            | true, deadMember ->
                Log.debugf "Received a node with heartbeat %i, in dead-members it has heartbeat %i"
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
            match activeMembers.TryGetValue networkAddress with
            | false, _ ->
                Log.debugf "*** Member marked as DEAD %s" (networkAddress |> networkAddressToString)
                deadMembers.TryRemove networkAddress |> ignore
                memberStateTimers.TryRemove networkAddress |> ignore
                networkAddress |> networkAddressToString |> closeConnection

                match removePeerNode networkAddress with
                | Ok () -> ()
                | _ -> Log.errorf "Error removing member %A" networkAddress
            | _ -> ()

        (*
            It declares a member as dead.
            - remove it from active nodes
            - add it to dead nodes
            - remove its timers
            - set to be removed from the dead-nodes. so that if it recovers can be added
        *)
        let setPendingDeadMember networkAddress =
            Log.debugf "*** Member potentially DEAD: %s" (networkAddress |> networkAddressToString)
            match activeMembers.TryGetValue networkAddress with
            | true, activeMember ->
                activeMembers.TryRemove networkAddress |> ignore
                deadMembers.AddOrUpdate (networkAddress, activeMember, fun _ _ -> activeMember) |> ignore
                memberStateTimers.TryRemove networkAddress |> ignore
                let timer = Timers.createTimer tFail (fun _ -> (setFinalDeadMember networkAddress))
                timer.Start()
                memberStateTimers.AddOrUpdate (networkAddress, timer, fun _ _ -> timer) |> ignore
            | false, _ -> ()

        let restartTimer networkAddress =
            Timers.restartTimer<NetworkAddress>
                memberStateTimers
                networkAddress
                tFail
                (fun _ -> (setPendingDeadMember networkAddress))

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Public
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        member __.StartGossip processPeerMessage publishEvent =
            __.StartNode processPeerMessage publishEvent
            __.StartGossipDiscovery()

        member __.StopGossip () =
            closeAllConnections()
            cts.Cancel()

        member __.GetActiveMembers () =
            activeMembers |> extractValuesFromKeyValuePairs

        member __.GetNetworkAddress () =
            config.NetworkAddress

        member __.SendMessage message =
            match message with
            | GossipDiscoveryMessage _ ->
                __.SelectRandomMembers()
                |> Option.iter (fun members ->
                    for m in members do
                        Log.debugf "Sending memberlist to: %s" (m.NetworkAddress |> networkAddressToString)
                        let peerMessageDto = Mapping.peerMessageToDto Serialization.serializePeerMessage message
                        sendGossipDiscoveryMessage
                            peerMessageDto
                            (m.NetworkAddress |> networkAddressToString)
                )

            | MulticastMessage _ ->
                let peerMessageDto = Mapping.peerMessageToDto Serialization.serializePeerMessage message
                sendMulticastMessage
                    (config.NetworkAddress |> networkAddressToString)
                    peerMessageDto
                    (getCurrentValidators() |> List.map (fun v -> v.NetworkAddress))

            | GossipMessage m -> __.SendGossipMessage m
            | _ -> ()

        member __.SendRequestDataMessage requestId =
            let rec loop id =
                async {
                    let expiredAddresses =
                        match pendingDataRequests.TryGetValue id with
                        | true, expiredAddresses -> expiredAddresses
                        | false, _ -> []

                    let targetAddress =
                        let networkAddressPool =
                            __.GetActiveMembers()
                            |> List.map (fun m -> m.NetworkAddress)
                            |> List.filter (fun a -> a <> config.NetworkAddress)
                            |> List.except expiredAddresses

                        let selectedUnicastPeer = __.SelectNewUnicastPeer networkAddressPool
                        match selectedUnicastPeer with
                        | None ->
                            Log.errorf "Cannot retrieve data from peers for %A" id
                            pendingDataRequests.TryRemove id |> ignore
                        | Some networkAddress ->
                            pendingDataRequests.AddOrUpdate(
                                id,
                                networkAddress :: expiredAddresses,
                                fun _ _ -> networkAddress :: expiredAddresses)
                            |> ignore
                        selectedUnicastPeer

                    targetAddress |> Option.iter(fun address ->
                        let unicastMessage = RequestDataMessage {
                            MessageId = id
                            SenderAddress = config.NetworkAddress
                        }
                        let peerMessageDto = Mapping.peerMessageToDto Serialization.serializePeerMessage unicastMessage
                        sendUnicastMessage peerMessageDto (address |> networkAddressToString)
                    )

                    do! Async.Sleep(4 * tCycle)

                    (*
                        If no answer is received within 2 cycles (request - response i.e 4xtCycle),
                        repeat (i.e choose another peer).
                    *)
                    match (pendingDataRequests.TryGetValue id) with
                    | true, addresses ->
                        if not (addresses.IsEmpty) then
                            return! loop id
                    | false, _ -> ()
                }
            Async.Start(loop requestId, cts.Token)

        member __.SendResponseDataMessage targetAddress responseMessage =
            let peerMessageDto = Mapping.peerMessageToDto Serialization.serializePeerMessage responseMessage
            sendUnicastMessage peerMessageDto (targetAddress |> networkAddressToString)

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Gossip Discovery
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        member private __.StartNode processPeerMessage publishEvent =
            Log.debug "Start node..."
            __.InitializeMemberList()
            __.StartServer processPeerMessage publishEvent

        member private __.StartServer processPeerMessage publishEvent =
            Log.infof "Open communication channel for %s" (config.NetworkAddress |> networkAddressToString)
            receiveMessage
                (config.NetworkAddress |> networkAddressToString)
                (__.ReceivePeerMessage processPeerMessage publishEvent)

        member private __.StartGossipDiscovery () =
            Log.info "Network layer initialized"
            let rec loop () =
                async {
                    __.SendMembership ()
                    do! Async.Sleep(tCycle)
                    return! loop ()
                }
            Async.Start (loop (), cts.Token)

        member private __.SendMembership () =
            __.IncreaseHeartbeat()
            let gossipDiscoveryMessage = GossipDiscoveryMessage {
                ActiveMembers = __.GetActiveMembers()
            }
            __.SendMessage gossipDiscoveryMessage
            printActiveMembers ()

        member private __.InitializeMemberList () =
            getAllPeerNodes () @ (config.NetworkAddress :: config.BootstrapNodes)
            |> Set.ofList
            |> Set.iter (fun n -> __.AddMember { NetworkAddress = n; Heartbeat = 0L })

        member private __.AddMember inputMember =
            let rec loop (mem : GossipMember) =
                Log.debugf "Adding new member: %s" (mem.NetworkAddress |> networkAddressToString)
                activeMembers.AddOrUpdate (mem.NetworkAddress, mem, fun _ _ -> mem) |> ignore
                match savePeerNode mem.NetworkAddress with
                | Ok () -> ()
                | _ -> Log.errorf "Error saving member %A" mem.NetworkAddress

                if mem.NetworkAddress <> config.NetworkAddress then
                    restartTimer mem.NetworkAddress |> ignore

            loop inputMember

        member private __.ReceiveActiveMember inputMember =
            match __.GetActiveMember inputMember.NetworkAddress with
            | Some m ->
                let localMember = {
                    NetworkAddress = m.NetworkAddress
                    Heartbeat = inputMember.Heartbeat
                }
                activeMembers.AddOrUpdate (inputMember.NetworkAddress, localMember, fun _ _ -> localMember) |> ignore
                match savePeerNode inputMember.NetworkAddress with
                | Ok () -> ()
                | _ -> Log.errorf "Error saving member %A" inputMember.NetworkAddress

                restartTimer inputMember.NetworkAddress |> ignore
            | None -> ()

        member private __.ReceiveMembers msg =
            __.MergeMemberList msg.ActiveMembers

        member private __.MergeMember inputMember =
            if inputMember.NetworkAddress <> config.NetworkAddress then
                Log.debugf "Receive member: %s" (inputMember.NetworkAddress |> networkAddressToString)
                match __.GetActiveMember inputMember.NetworkAddress with
                | Some localMember ->
                    if localMember.Heartbeat < inputMember.Heartbeat then
                        __.ReceiveActiveMember inputMember
                | None ->
                    if not (isDead inputMember) then
                        __.AddMember inputMember
                        deadMembers.TryRemove inputMember.NetworkAddress |> ignore

        member private __.MergeMemberList members =
            members |> List.iter (fun m -> __.MergeMember m)

        member private __.GetActiveMember networkAddress =
            match activeMembers.TryGetValue networkAddress with
            | true, localMember -> Some localMember
            | false, _ -> None

        member private __.IncreaseHeartbeat () =
            match __.GetActiveMember config.NetworkAddress with
            | Some m ->
                let localMember = {
                    NetworkAddress = m.NetworkAddress
                    Heartbeat = m.Heartbeat + 1L
                }
                activeMembers.AddOrUpdate (config.NetworkAddress, localMember, fun _ _ -> localMember) |> ignore
            | None -> ()

        member private __.SelectRandomMembers () =
            let connectedMembers =
                activeMembers
                |> Map.ofDict
                |> Map.filter (fun a _ -> a <> config.NetworkAddress)
                |> Seq.toList

            match connectedMembers with
            | [] -> None
            | _ ->
                connectedMembers
                |> Seq.shuffleG
                |> Seq.chunkBySize fanout
                |> Seq.head
                |> extractValuesFromKeyValuePairs
                |> Some

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
                Log.debugf "Sending gossip message %A to %s"
                    gossipMessage.MessageId
                    (recipientAddress |> networkAddressToString)

                let peerMessage = GossipMessage gossipMessage
                let peerMessageDto = Mapping.peerMessageToDto Serialization.serializePeerMessage peerMessage
                let recipientMemberDto = Mapping.gossipMemberToDto recipientMember
                sendGossipMessage peerMessageDto recipientMemberDto
            | false, _ -> ()

        member private __.ProcessGossipMessage (gossipMessage : GossipMessage) recipientAddresses =
            match recipientAddresses with
            (*
                No recipients left to send message to, remove gossip message from the processing queue
            *)
            | [] -> gossipMessages.TryRemove gossipMessage.MessageId |> ignore
            (*
                If two or more recipients left, select randomly a subset (fanout) of recipients to send the
                gossip message to.
                If gossip message was processed before, append the selected recipients to the processed recipients list
                If not, add the gossip message (and the corresponding recipient) to the processing queue
            *)
            | _ ->
                let fanoutRecipientAddresses =
                    recipientAddresses
                    |> Seq.shuffleG
                    |> Seq.chunkBySize fanout
                    |> Seq.head
                    |> Seq.toList

                fanoutRecipientAddresses |> List.iter (fun recipientAddress ->
                    __.SendGossipMessageToRecipient recipientAddress gossipMessage)

                __.UpdateGossipMessagesProcessingQueue
                    (gossipMessage.SenderAddress :: fanoutRecipientAddresses)
                    gossipMessage.MessageId

        member private __.SendGossipMessage message =
            let rec loop (msg : GossipMessage) =
                async {
                    let recipientAddresses =
                        __.GetActiveMembers()
                        |> List.map (fun m -> m.NetworkAddress)
                        |> List.filter (fun a -> a <> config.NetworkAddress)

                    let remainingrecipientAddresses =
                        match gossipMessages.TryGetValue msg.MessageId with
                        | true, processedAddresses ->
                            List.except (msg.SenderAddress :: processedAddresses) recipientAddresses
                        | false, _ ->
                            List.except [msg.SenderAddress] recipientAddresses

                    __.ProcessGossipMessage msg remainingrecipientAddresses

                    if remainingrecipientAddresses.Length >= fanout then
                        do! Async.Sleep(tCycle)
                        return! loop msg
                }

            Async.Start (loop message, cts.Token)

        member private __.ReceiveGossipMessage processPeerMessage publishEvent (gossipMessage : GossipMessage) =
            match gossipMessages.TryGetValue gossipMessage.MessageId with
            | true, processedAddresses ->
                if not (processedAddresses |> List.contains gossipMessage.SenderAddress) then
                    gossipMessages.AddOrUpdate(
                        gossipMessage.MessageId,
                        gossipMessage.SenderAddress :: processedAddresses,
                        fun _ _ -> gossipMessage.SenderAddress :: processedAddresses) |> ignore
            | false, _ ->
                Log.debugf "Received gossip message %A from %s "
                    gossipMessage.MessageId
                    (gossipMessage.SenderAddress |> networkAddressToString)

                // Make sure the message is not processed twice.
                gossipMessages.AddOrUpdate(
                    gossipMessage.MessageId,
                    [],
                    fun _ _ -> []) |> ignore

                processPeerMessage (GossipMessage gossipMessage)
                |> Option.iter(fun result ->
                    match result with
                    | Ok appEvent -> appEvent |> Option.iter(fun e -> e |> publishEvent)
                    | Error errors -> Log.appErrors errors
                )

                let msg = GossipMessage {
                    MessageId = gossipMessage.MessageId
                    SenderAddress = config.NetworkAddress
                    Data = gossipMessage.Data
                }

                // Once a node is infected, propagate the message further.
                __.SendMessage msg

        member private __.ReceivePeerMessage processPeerMessage publishEvent dto =
            let peerMessage = Mapping.peerMessageFromDto dto
            match peerMessage with
            | GossipDiscoveryMessage m -> __.ReceiveMembers m
            | GossipMessage m -> __.ReceiveGossipMessage processPeerMessage publishEvent m
            | MulticastMessage m -> __.ReceiveMulticastMessage processPeerMessage m
            | RequestDataMessage m -> __.ReceiveRequestMessage processPeerMessage m
            | ResponseDataMessage m -> __.ReceiveResponseMessage processPeerMessage m

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Multicast Message Passing
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        member private __.ReceiveMulticastMessage processPeerMessage multicastMessage =
            processPeerMessage (MulticastMessage multicastMessage)
            |> Option.iter(fun result ->
                match result with
                | Ok appEvent ->
                    // Do not propagate on multicast.
                    appEvent |> Option.iter(fun e -> Log.infof "EVENT: %A" e)
                | Error errors -> Log.appErrors errors
            )

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Request/Response
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        member private __.ReceiveRequestMessage processPeerMessage (requestDataMessage : RequestDataMessage) =
            processPeerMessage (RequestDataMessage requestDataMessage)
            |> Option.iter(fun result ->
                match result with
                | Ok appEvent ->
                    appEvent |> Option.iter(fun e -> Log.infof "EVENT: %A" e)
                | Error errors -> Log.appErrors errors
            )

        member private __.ReceiveResponseMessage processPeerMessage (requestDataMessage : ResponseDataMessage) =
            processPeerMessage (ResponseDataMessage requestDataMessage)
            |> Option.iter(fun result ->
                match result with
                | Ok appEvent ->
                    pendingDataRequests.TryRemove requestDataMessage.MessageId |> ignore
                    appEvent |> Option.iter(fun e -> Log.infof "EVENT: %A" e)
                | Error errors -> Log.appErrors errors
            )

        member private __.SelectNewUnicastPeer networkAddressPool =
            networkAddressPool
            |> List.toSeq
            |> Seq.shuffleG
            |> Seq.tryHead

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
        processPeerMessage
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
        n.StartGossip processPeerMessage publishEvent
        node <- Some n

    let stopGossip () =
        node |> Option.iter(fun n -> n.StopGossip())

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
        | Some n -> n.SendRequestDataMessage requestId
        | None -> failwith "Please start the node first"

    let requestBlockFromPeer blockNumber =
        requestFromPeer (NetworkMessageId.Block blockNumber)

    let requestLastBlockFromPeer () =
        requestBlockFromPeer (BlockNumber -1L)

    let requestTxFromPeer txHash =
        requestFromPeer (NetworkMessageId.Tx txHash)

    let respondToPeer targetAddress peerMessage =
        match node with
        | Some n -> n.SendResponseDataMessage targetAddress peerMessage
        | None -> failwith "Please start the node first"
