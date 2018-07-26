namespace Chainium.Blockchain.Public.Net

open System.Collections.Concurrent
open Chainium.Blockchain.Public.Core
open Chainium.Blockchain.Public.Core.Dtos
open Chainium.Common
open NetMQ
open NetMQ.Sockets
open Newtonsoft.Json

module Transport =

    let mutable private receiverSocket : PullSocket option = None
    let mutable private poller : NetMQPoller option = None
    let private connectionPool = new ConcurrentDictionary<string, PushSocket>()

    let private packMessage message =
        message |> Serialization.serializePeerMessage

    let private unpackMessage message =
        message |> Serialization.deserializePeerMessage

    let private send (msg : string) targetAddress =
        match connectionPool.TryGetValue targetAddress with
        | true, socket ->
            socket.TrySendFrame msg |> ignore
        | _ ->
            let senderSocket = new PushSocket(">tcp://" + targetAddress)
            connectionPool.AddOrUpdate (targetAddress, senderSocket, fun _ _ -> senderSocket) |> ignore
            senderSocket.TrySendFrame msg |> ignore

    let sendGossipDiscoveryMessage gossipDiscoveryMessage targetAddress =
        let msg = packMessage gossipDiscoveryMessage
        send msg targetAddress

    let sendMulticastMessage senderAddress multicastMessage connections =
        let multicastGroupMembers =
            connections
            |> List.filter (fun m -> m.NetworkAddress <> senderAddress)

        match multicastGroupMembers with
        | [] -> ()
        | _ ->
            multicastGroupMembers
            |> Seq.shuffleG
            |> Seq.toList
            |> List.iter (fun m ->
                let msg = packMessage multicastMessage
                send msg m.NetworkAddress
            )

    let sendGossipMessage gossipMessage (targetMember: GossipMemberDto) =
        let msg = packMessage gossipMessage
        send msg targetMember.NetworkAddress

    let receiveMessage networkAddress receiveCallback =
        match receiverSocket with
        | Some _ -> ()
        | None -> receiverSocket <- new PullSocket("@tcp://" + networkAddress) |> Some

        match poller with
        | Some _ -> ()
        | None ->
            poller <- new NetMQPoller() |> Some

        poller |> Option.iter(fun p ->
            receiverSocket |> Option.iter(fun socket ->
                p.Add socket
                socket.ReceiveReady
                |> Observable.subscribe (fun eventArgs ->
                    let received, message = eventArgs.Socket.TryReceiveFrameString()
                    if received then
                        let peerMessage = unpackMessage message
                        receiveCallback peerMessage
                )
                |> ignore
            )
            p.RunAsync()
        )

    let closeConnection host =
        let found, socket = connectionPool.TryGetValue host
        if found then
            connectionPool.TryRemove host |> ignore
            socket.Dispose()