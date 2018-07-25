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

        let activeMembers = new ConcurrentDictionary<GossipMemberId, GossipMember>()
        let deadMembers = new ConcurrentDictionary<GossipMemberId, GossipMember>()
        let memberStateTimers = new ConcurrentDictionary<GossipMemberId, System.Timers.Timer>()
        let gossipMessages = new ConcurrentDictionary<NetworkMessageId, GossipMemberId list>()

        let networkAddress = Helpers.createNetworkAddress config.NetworkHost config.NetworkPort

        let printActiveMembers () =
            #if DEBUG
                printfn "\n ========= ACTIVE CONNECTIONS [%s] ========="
                    (DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                activeMembers
                |> Map.ofDict
                |> Seq.toList
                |> List.iter (fun x ->
                    printfn " %s Heartbeat:%i" (Helpers.gossipMemberIdToString x.Key)x.Value.Heartbeat
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
            let foundDead, deadMember = deadMembers.TryGetValue inputMember.Id
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
        let setFinalDeadMember id =
            let found, _ = activeMembers.TryGetValue id
            if not found then
                Log.debugf "*** Member marked as DEAD %s" (Helpers.gossipMemberIdToString id)
                deadMembers.TryRemove id |> ignore
                memberStateTimers.TryRemove id |> ignore
                closeConnection (Helpers.gossipMemberIdToString id)

        (*
            It declares a member as dead.
            - remove it from active nodes
            - add it to dead nodes
            - remove its timers
            - set to be removed from the dead-nodes. so that if it recovers can be added
        *)
        let setPendingDeadMember id =
            Log.debugf "*** Member potentially DEAD: %s" (Helpers.gossipMemberIdToString id)
            let found, activeMember = activeMembers.TryGetValue id
            if found then
                activeMembers.TryRemove id |> ignore
                deadMembers.AddOrUpdate (id, activeMember, fun _ _ -> activeMember) |> ignore
                memberStateTimers.TryRemove id |> ignore
                let timer = Helpers.Timer.createTimer tFail (fun _ -> (setFinalDeadMember id))
                timer.Start()
                memberStateTimers.AddOrUpdate (id, timer, fun _ _ -> timer) |> ignore

        let restartTimer id =
            Helpers.Timer.restartTimer<GossipMemberId> memberStateTimers id tFail (fun _ -> (setPendingDeadMember id))

        let updateGossipMessagesProcessingQueue ids gossipMessageId =
            let found, recipientIds = gossipMessages.TryGetValue gossipMessageId
            let processedIds = if found then ids @ recipientIds else ids

            gossipMessages.AddOrUpdate(
                gossipMessageId,
                processedIds,
                fun _ _ -> processedIds) |> ignore

        member __.Id
            with get () = networkAddress

        member __.Start publishEvent processPeerMessage =
            __.StartNode publishEvent processPeerMessage
            __.StartGossipDiscovery()

        member __.StartNode publishEvent processPeerMessage =
            Log.debug "Start node .."
            __.InitializeMemberList()
            __.StartServer publishEvent processPeerMessage

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
            | GossipDiscoveryMessage m ->
                let peerMessageDto = Mapping.peerMessageToDto Serialization.serializePeerMessage message
                sendGossipDiscoveryMessage
                    peerMessageDto
                    (Helpers.createNetworkAddress m.NetworkHost m.NetworkPort |> fun (GossipMemberId s) -> s)

            | MulticastMessage _ ->
                let peerMessageDto = Mapping.peerMessageToDto Serialization.serializePeerMessage message
                sendMulticastMessage
                    (Helpers.gossipMemberIdToString __.Id)
                    peerMessageDto
                    (__.GetActiveMembers() |> List.map Mapping.gossipMemberToDto)

            | GossipMessage m -> __.SendGossipMessage m

        member private __.SendGossipMessageToRecipient recipientId (gossipMessage : GossipMessage) =
            let found, recipientMember = activeMembers.TryGetValue recipientId
            if found then
                Log.debugf "Sending gossip message %A to %s"
                    (Helpers.gossipMessageIdToString gossipMessage.MessageId)
                    (Helpers.gossipMemberIdToString recipientId)

                let peerMessage = GossipMessage gossipMessage
                let peerMessageDto = Mapping.peerMessageToDto Serialization.serializePeerMessage peerMessage
                let recipientMemberDto = Mapping.gossipMemberToDto recipientMember
                sendGossipMessage peerMessageDto recipientMemberDto

        member private __.ProcessGossipMessage (gossipMessage : GossipMessage) recipientIds =
            match recipientIds with
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
                let selectedRecipientIds =
                    recipientIds
                    |> Seq.shuffleG
                    |> Seq.chunkBySize fanout
                    |> Seq.head
                    |> Seq.toList

                selectedRecipientIds |> List.iter (fun recipientId ->
                    __.SendGossipMessageToRecipient recipientId gossipMessage)
                updateGossipMessagesProcessingQueue
                    (gossipMessage.SenderId :: selectedRecipientIds)
                    gossipMessage.MessageId

        member private __.SendGossipMessage message =
            let rec loop (msg : GossipMessage) =
                async {
                    let recipientIds =
                        __.GetActiveMembers()
                        |> List.map (fun m -> m.Id)
                        |> List.filter (fun i -> i <> __.Id)

                    let found, processedIds = gossipMessages.TryGetValue msg.MessageId

                    let remainingRecipientIds =
                        if found then
                            List.except (msg.SenderId :: processedIds) recipientIds
                        else
                            List.except [msg.SenderId] recipientIds

                    __.ProcessGossipMessage msg remainingRecipientIds

                    if remainingRecipientIds.Length >= fanout then
                        do! Async.Sleep(tCycle)
                        return! loop msg
                }

            Async.Start (loop message)

        member private __.ReceiveGossipMessage publishEvent processPeerMessage (gossipMessage : GossipMessage) =
            let processed, processedIds = gossipMessages.TryGetValue gossipMessage.MessageId
            if not processed then
                Log.debugf "*** RECEIVED GOSSIP MESSAGE %A from %s "
                    gossipMessage.MessageId
                    (Helpers.gossipMemberIdToString gossipMessage.SenderId)

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
                    SenderId = __.Id
                    Data = gossipMessage.Data
                }

                // Once a node is infected, propagate the message further.
                __.SendMessage msg
            else
                // Message was already processed.
                if not (processedIds |> List.contains gossipMessage.SenderId) then
                    gossipMessages.AddOrUpdate(
                        gossipMessage.MessageId,
                        gossipMessage.SenderId :: processedIds,
                        fun _ _ -> gossipMessage.SenderId :: processedIds) |> ignore

        member private __.ReceiveMulticastMessage
            publishEvent
            processPeerMessage
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
                Log.debugf "Adding new member : %s port %i"
                    (Helpers.hostToString mem.NetworkHost)
                    (Helpers.portToInt mem.NetworkPort)
                let id = Helpers.createNetworkAddress mem.NetworkHost mem.NetworkPort
                activeMembers.AddOrUpdate (id, mem, fun _ _ -> mem) |> ignore

                let isCurrentNode = mem.Id = __.Id
                if not isCurrentNode then
                    restartTimer mem.Id |> ignore

            loop inputMember

        member private __.InitializeMemberList () =
            let self = {
                Id = Helpers.createNetworkAddress config.NetworkHost config.NetworkPort
                NetworkHost = config.NetworkHost
                NetworkPort = config.NetworkPort
                Heartbeat = 0L
            }
            __.AddMember self

            config.BootstrapNodes
            |> List.map (fun n ->
                {
                    Id = Helpers.createNetworkAddress n.NetworkHost n.NetworkPort
                    NetworkHost = n.NetworkHost
                    NetworkPort = n.NetworkPort
                    Heartbeat = 0L
                })
            |> List.iter (fun m -> __.AddMember m)

        member private __.IsCurrentNode key =
            key = __.Id

        member private __.GetActiveMember id =
            let found, localMember = activeMembers.TryGetValue id
            if found then
                Some localMember
            else
                None

        member private __.MergeMember inputMember =
            if not (__.IsCurrentNode inputMember.Id) then
                Log.debugf "Receive member: %s ..." (Helpers.gossipMemberIdToString inputMember.Id)
                match __.GetActiveMember inputMember.Id with
                | Some localMember ->
                    if localMember.Heartbeat < inputMember.Heartbeat then
                        __.ReceiveActiveMember inputMember
                | None ->
                    if not (isDead inputMember) then
                        __.AddMember inputMember
                        deadMembers.TryRemove inputMember.Id |> ignore

        member private __.MergeMemberList members =
            members |> List.iter (fun m -> __.MergeMember m)

        member private __.ReceiveMembers msg =
            __.MergeMemberList msg.ActiveMembers

        member private __.StartServer publishEvent processPeerMessage =
            Log.infof "Open communication channel for %s" (Helpers.gossipMemberIdToString __.Id)
            receiveMessage
                (Helpers.gossipMemberIdToString __.Id)
                (__.ReceivePeerMessage publishEvent processPeerMessage)

        member private __.ReceivePeerMessage publishEvent processPeerMessage dto =
            let peerMessage = Mapping.peerMessageFromDto dto
            match peerMessage with
            | GossipDiscoveryMessage m -> __.ReceiveMembers m
            | GossipMessage m -> __.ReceiveGossipMessage publishEvent processPeerMessage m
            | MulticastMessage m -> __.ReceiveMulticastMessage publishEvent processPeerMessage m

        member private __.SendMembership () =
            __.IncreaseHeartbeat()
            __.SelectRandomMembers()
            |> Option.iter (fun members ->
                for m in members do
                    Log.debugf "Sending memberlist to: %s" (Helpers.gossipMemberIdToString m.Id)
                    let gossipDiscoveryMessage = GossipDiscoveryMessage {
                        NetworkHost = m.NetworkHost
                        NetworkPort = m.NetworkPort
                        ActiveMembers = __.GetActiveMembers()
                    }
                    __.SendMessage gossipDiscoveryMessage
            )
            printActiveMembers ()

        // Returns N (fanout) members from the memberlist without current Id
        member private __.SelectRandomMembers () =
            let connectedMembers =
                activeMembers
                |> Map.ofDict
                |> Map.filter (fun key _ -> not (__.IsCurrentNode key) )
                |> Seq.toList

            match connectedMembers with
            | [] -> None
            | _ ->
                connectedMembers
                |> Seq.shuffleG
                |> Seq.chunkBySize fanout
                |> Seq.head
                |> Helpers.seqOfKeyValuePairToList
                |> Some

        member private __.IncreaseHeartbeat () =
            match __.GetActiveMember __.Id with
            | Some m ->
                let localMember = {
                    Id = m.Id
                    NetworkHost = m.NetworkHost
                    NetworkPort = m.NetworkPort
                    Heartbeat = m.Heartbeat + 1L
                }
                activeMembers.AddOrUpdate (__.Id, localMember, fun _ _ -> localMember) |> ignore
            | None -> ()

        member private __.ReceiveActiveMember inputMember =
            match __.GetActiveMember inputMember.Id with
            | Some m ->
                let localMember = {
                    Id = m.Id
                    NetworkHost = m.NetworkHost
                    NetworkPort = m.NetworkPort
                    Heartbeat = inputMember.Heartbeat
                }
                activeMembers.AddOrUpdate (inputMember.Id, localMember, fun _ _ -> localMember) |> ignore
                restartTimer inputMember.Id |> ignore
            | None -> ()

        member private __.GetActiveMembers () =
            activeMembers |> Helpers.seqOfKeyValuePairToList

        member private __.GetDeadMembers () =
            deadMembers |> Helpers.seqOfKeyValuePairToList

    let mutable private node : NetworkNode option = None

    let startGossip
        sendGossipDiscoveryMessage
        sendGossipMessage
        sendMulticastMessage
        receiveMessage
        closeConnection
        host
        networkPort
        (bootstrapNodes : string list)
        (publishEvent : AppEvent -> unit)
        processPeerMessage
        =

        let nodeConfig : NetworkNodeConfig = {
            BootstrapNodes = bootstrapNodes
            |> List.map Mapping.endPointFromString

            NetworkHost = NetworkHost host
            NetworkPort = NetworkPort networkPort
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
        n.Start publishEvent processPeerMessage
        node <- Some n

    let sendMessage message =
        match node with
        | Some n -> n.SendMessage message
        | None -> failwith "Please start gossip first"
