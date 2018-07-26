namespace Chainium.Blockchain.Public.Net

open System
open System.Collections.Concurrent
open Chainium.Common
open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Core
open Chainium.Blockchain.Public.Core.Events
open Chainium.Blockchain.Public.Core.DomainTypes

module Peers =

    type NetworkNode
        (
        sendGossipDiscoveryMessage,
        sendGossipMessage,
        sendMulticastMessage,
        receiveMessage,
        closeConnection,
        config : NetworkNodeConfig
        ) =

        let fanout = 2
        let tCycle = 10000
        let tFail = 50000

        let activeMembers = new ConcurrentDictionary<NetworkAddress, GossipMember>()
        let deadMembers = new ConcurrentDictionary<NetworkAddress, GossipMember>()
        let memberStateTimers = new ConcurrentDictionary<NetworkAddress, System.Timers.Timer>()
        let gossipMessages = new ConcurrentDictionary<NetworkMessageId, NetworkAddress list>()

        let networkAddressToString (NetworkAddress a) = a

        let seqOfKeyValuePairToList seq =
            seq
            |> Map.ofDict
            |> Seq.toList
            |> List.map (fun x -> x.Value)

        let printActiveMembers () =
            #if DEBUG
                printfn "\n ========= ACTIVE CONNECTIONS [%s] ========="
                    (DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                activeMembers
                |> Map.ofDict
                |> Seq.toList
                |> List.iter (fun x ->
                    printfn " %s Heartbeat:%i" (x.Key |> networkAddressToString) x.Value.Heartbeat
                )
                printfn " ================================================================\n"
            #else
                ()
            #endif

        (*
            A member is dead if it's in the list of dead-members and
            the heartbeat the local node is bigger than the one passed by argument.
        *)
        let isDead inputMember =
            let foundDead, deadMember = deadMembers.TryGetValue inputMember.NetworkAddress
            if foundDead then
                Log.debugf "Received a node with heartbeat %i, in dead-members it has heartbeat %i"
                    inputMember.Heartbeat
                    deadMember.Heartbeat
                deadMember.Heartbeat >= inputMember.Heartbeat
            else
                false
        (*
            Once a member has been declared dead and it hasn't recovered in
            2xTFail time is removed from the dead-members list.
            So if node has been down for a while and come back it can be added again.
            Here this will be scheduled right after a node is declared, so total time
            elapsed is 2xTFail
        *)
        let setFinalDeadMember networkAddress =
            let found, _ = activeMembers.TryGetValue networkAddress
            if not found then
                Log.debugf "*** Member marked as DEAD %s" (networkAddress |> networkAddressToString)
                deadMembers.TryRemove networkAddress |> ignore
                memberStateTimers.TryRemove networkAddress |> ignore
                networkAddress |> networkAddressToString |> closeConnection

        (*
            It declares a member as dead.
            - remove it from active nodes
            - add it to dead nodes
            - remove its timers
            - set to be removed from the dead-nodes. so that if it recovers can be added
        *)
        let setPendingDeadMember networkAddress =
            Log.debugf "*** Member potentially DEAD: %s" (networkAddress |> networkAddressToString)
            let found, activeMember = activeMembers.TryGetValue networkAddress
            if found then
                activeMembers.TryRemove networkAddress |> ignore
                deadMembers.AddOrUpdate (networkAddress, activeMember, fun _ _ -> activeMember) |> ignore
                memberStateTimers.TryRemove networkAddress |> ignore
                let timer = Timers.createTimer tFail (fun _ -> (setFinalDeadMember networkAddress))
                timer.Start()
                memberStateTimers.AddOrUpdate (networkAddress, timer, fun _ _ -> timer) |> ignore

        let restartTimer networkAddress =
            Timers.restartTimer<NetworkAddress>
                memberStateTimers
                networkAddress
                tFail
                (fun _ -> (setPendingDeadMember networkAddress))

        let updateGossipMessagesProcessingQueue networkAddresses gossipMessageId =
            let found, processedAddresses = gossipMessages.TryGetValue gossipMessageId
            let newProcessedAddresses = if found then networkAddresses @ processedAddresses else networkAddresses

            gossipMessages.AddOrUpdate(
                gossipMessageId,
                newProcessedAddresses,
                fun _ _ -> newProcessedAddresses) |> ignore

        member __.Start processPeerMessage publishEvent =
            __.StartNode processPeerMessage publishEvent
            __.StartGossipDiscovery()

        member __.StartNode processPeerMessage publishEvent =
            Log.debug "Start node .."
            __.InitializeMemberList()
            __.StartServer processPeerMessage publishEvent

        member __.StartGossipDiscovery () =
            Log.info "Network layer initialized"
            let rec loop () =
                async {
                    __.SendMembership ()
                    do! Async.Sleep(tCycle)
                    return! loop ()
                }
            Async.Start (loop ())

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
                    (__.GetActiveMembers() |> List.map Mapping.gossipMemberToDto)

            | GossipMessage m -> __.SendGossipMessage m

        member private __.SendGossipMessageToRecipient recipientAddress (gossipMessage : GossipMessage) =
            let found, recipientMember = activeMembers.TryGetValue recipientAddress
            if found then
                Log.debugf "Sending gossip message %A to %s"
                    gossipMessage.MessageId
                    (recipientAddress |> networkAddressToString)

                let peerMessage = GossipMessage gossipMessage
                let peerMessageDto = Mapping.peerMessageToDto Serialization.serializePeerMessage peerMessage
                let recipientMemberDto = Mapping.gossipMemberToDto recipientMember
                sendGossipMessage peerMessageDto recipientMemberDto

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
                updateGossipMessagesProcessingQueue
                    (gossipMessage.SenderAddress :: fanoutRecipientAddresses)
                    gossipMessage.MessageId

        member private __.SendGossipMessage message =
            let rec loop (msg : GossipMessage) =
                async {
                    let recipientAddresses =
                        __.GetActiveMembers()
                        |> List.map (fun m -> m.NetworkAddress)
                        |> List.filter (fun a -> a <> config.NetworkAddress)

                    let found, processedAddresses = gossipMessages.TryGetValue msg.MessageId

                    let remainingrecipientAddresses =
                        if found then
                            List.except (msg.SenderAddress :: processedAddresses) recipientAddresses
                        else
                            List.except [msg.SenderAddress] recipientAddresses

                    __.ProcessGossipMessage msg remainingrecipientAddresses

                    if remainingrecipientAddresses.Length >= fanout then
                        do! Async.Sleep(tCycle)
                        return! loop msg
                }

            Async.Start (loop message)

        member private __.ReceiveGossipMessage processPeerMessage publishEvent (gossipMessage : GossipMessage) =
            let processed, processedAddresses = gossipMessages.TryGetValue gossipMessage.MessageId
            if not processed then
                Log.debugf "*** RECEIVED GOSSIP MESSAGE %A from %s "
                    gossipMessage.MessageId
                    (gossipMessage.SenderAddress |> networkAddressToString)

                // Make sure the message is not processed twice.
                gossipMessages.AddOrUpdate(
                    gossipMessage.MessageId,
                    [],
                    fun _ _ -> []) |> ignore

                match processPeerMessage (GossipMessage gossipMessage) with
                | Some result ->
                    match result with
                    | Ok data -> data |> TxReceived |> publishEvent
                    | Error errors -> Log.appErrors errors
                | None -> ()

                let msg = GossipMessage {
                    MessageId = gossipMessage.MessageId
                    SenderAddress = config.NetworkAddress
                    Data = gossipMessage.Data
                }

                // Once a node is infected, propagate the message further.
                __.SendMessage msg
            else
                // Message was already processed.
                if not (processedAddresses |> List.contains gossipMessage.SenderAddress) then
                    gossipMessages.AddOrUpdate(
                        gossipMessage.MessageId,
                        gossipMessage.SenderAddress :: processedAddresses,
                        fun _ _ -> gossipMessage.SenderAddress :: processedAddresses) |> ignore

        member private __.ReceiveMulticastMessage
            processPeerMessage
            publishEvent
            (multicastMessage : MulticastMessage)
            =

            printfn "Received multicast message from somebody"
            match processPeerMessage (MulticastMessage multicastMessage) with
            | Some result ->
                match result with
                | Ok data -> data |> TxReceived |> publishEvent
                | Error errors -> Log.appErrors errors
            | None -> ()

        member private __.AddMember inputMember =
            let rec loop (mem : GossipMember) =
                Log.debugf "Adding new member : %s" (mem.NetworkAddress |> networkAddressToString)
                activeMembers.AddOrUpdate (mem.NetworkAddress, mem, fun _ _ -> mem) |> ignore

                let isCurrentNode = mem.NetworkAddress = config.NetworkAddress
                if not isCurrentNode then
                    restartTimer mem.NetworkAddress |> ignore

            loop inputMember

        member private __.InitializeMemberList () =
            let self = {
                NetworkAddress = config.NetworkAddress
                Heartbeat = 0L
            }
            __.AddMember self

            config.BootstrapNodes
            |> List.map (fun n ->
                {
                    NetworkAddress = n
                    Heartbeat = 0L
                })
            |> List.iter (fun m -> __.AddMember m)

        member private __.GetActiveMember networkAddress =
            let found, localMember = activeMembers.TryGetValue networkAddress
            if found then
                Some localMember
            else
                None

        member private __.MergeMember inputMember =
            if inputMember.NetworkAddress <> config.NetworkAddress then
                Log.debugf "Receive member: %s ..." (inputMember.NetworkAddress |> networkAddressToString)
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

        member private __.ReceiveMembers msg =
            __.MergeMemberList msg.ActiveMembers

        member private __.StartServer processPeerMessage publishEvent =
            Log.infof "Open communication channel for %s" (config.NetworkAddress |> networkAddressToString)
            receiveMessage
                (config.NetworkAddress |> networkAddressToString)
                (__.ReceivePeerMessage processPeerMessage publishEvent)

        member private __.ReceivePeerMessage processPeerMessage publishEvent dto =
            let peerMessage = Mapping.peerMessageFromDto dto
            match peerMessage with
            | GossipDiscoveryMessage m -> __.ReceiveMembers m
            | GossipMessage m -> __.ReceiveGossipMessage processPeerMessage publishEvent m
            | MulticastMessage m -> __.ReceiveMulticastMessage processPeerMessage publishEvent m

        member private __.SendMembership () =
            __.IncreaseHeartbeat()
            let gossipDiscoveryMessage = GossipDiscoveryMessage {
                ActiveMembers = __.GetActiveMembers()
            }
            __.SendMessage gossipDiscoveryMessage
            printActiveMembers ()

        // Returns N (fanout) members from the memberlist without local address
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
                |> seqOfKeyValuePairToList
                |> Some

        member private __.IncreaseHeartbeat () =
            match __.GetActiveMember config.NetworkAddress with
            | Some m ->
                let localMember = {
                    NetworkAddress = m.NetworkAddress
                    Heartbeat = m.Heartbeat + 1L
                }
                activeMembers.AddOrUpdate (config.NetworkAddress, localMember, fun _ _ -> localMember) |> ignore
            | None -> ()

        member private __.ReceiveActiveMember inputMember =
            match __.GetActiveMember inputMember.NetworkAddress with
            | Some m ->
                let localMember = {
                    NetworkAddress = m.NetworkAddress
                    Heartbeat = inputMember.Heartbeat
                }
                activeMembers.AddOrUpdate (inputMember.NetworkAddress, localMember, fun _ _ -> localMember) |> ignore
                restartTimer inputMember.NetworkAddress |> ignore
            | None -> ()

        member private __.GetActiveMembers () =
            activeMembers |> seqOfKeyValuePairToList

        member private __.GetDeadMembers () =
            deadMembers |> seqOfKeyValuePairToList

    let mutable private node : NetworkNode option = None

    let startGossip
        sendGossipDiscoveryMessage
        sendGossipMessage
        sendMulticastMessage
        receiveMessage
        closeConnection
        networkAddress
        (bootstrapNodes : string list)
        (publishEvent : AppEvent -> unit)
        processPeerMessage
        =

        let nodeConfig : NetworkNodeConfig = {
            BootstrapNodes = bootstrapNodes
            |> List.map (fun n -> NetworkAddress n)

            NetworkAddress = NetworkAddress networkAddress
        }
        let n =
            NetworkNode (
                sendGossipDiscoveryMessage,
                sendGossipMessage,
                sendMulticastMessage,
                receiveMessage,
                closeConnection,
                nodeConfig
            )
        n.Start processPeerMessage publishEvent
        node <- Some n

    let sendMessage message =
        match node with
        | Some n -> n.SendMessage message
        | None -> failwith "Please start gossip first"
