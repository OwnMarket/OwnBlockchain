namespace Chainium.Blockchain.Public.Net

open System.Collections.Concurrent
open Chainium.Common
open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Core
open Chainium.Blockchain.Public.Core.Dtos
open NetMQ
open NetMQ.Sockets
open Newtonsoft.Json

module Transport =

    let mutable private receiverSocket : PullSocket option = None
    let private poller = new NetMQPoller()
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

    let private sendAgent = Agent.start <| fun (message, targetAddress) ->
        async {
            send message targetAddress
        }

    let sendGossipDiscoveryMessage gossipDiscoveryMessage targetAddress =
        let msg = packMessage gossipDiscoveryMessage
        sendAgent.Post(msg, targetAddress)

    let sendGossipMessage gossipMessage (targetMember: GossipMemberDto) =
        let msg = packMessage gossipMessage
        sendAgent.Post(msg, targetMember.NetworkAddress)

    let sendUnicastMessage unicastMessage targetAddress =
        let msg = packMessage unicastMessage
        sendAgent.Post(msg, targetAddress)

    let sendMulticastMessage senderAddress multicastMessage multicastAddresses =
        let multicastAddresses =
            multicastAddresses
            |> List.filter (fun a -> a <> senderAddress)

        match multicastAddresses with
        | [] -> ()
        | _ ->
            multicastAddresses
            |> Seq.shuffleG
            |> Seq.toList
            |> List.iter (fun networkAddress ->
                let msg = packMessage multicastMessage
                sendAgent.Post(msg, networkAddress)
            )

    let receiveMessage networkAddress receiveCallback =
        match receiverSocket with
        | Some _ -> ()
        | None -> receiverSocket <- new PullSocket("@tcp://" + networkAddress) |> Some

        receiverSocket |> Option.iter(fun socket ->
            poller.Add socket
            socket.ReceiveReady
            |> Observable.subscribe (fun eventArgs ->
                let received, message = eventArgs.Socket.TryReceiveFrameString()
                if received then
                    let peerMessage = unpackMessage message
                    receiveCallback peerMessage
            )
            |> ignore
        )
        poller.RunAsync()

    let closeConnection networkAddress =
        match connectionPool.TryGetValue networkAddress with
        | true, socket ->
            connectionPool.TryRemove networkAddress |> ignore
            socket.Dispose()
        | _ -> ()

    let closeAllConnections () =
        poller.Dispose()
        connectionPool
        |> Map.ofDict
        |> Seq.iter (fun x -> x.Value.Dispose())
