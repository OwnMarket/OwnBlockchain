namespace Chainium.Blockchain.Public.Net.Tests

open System.Collections.Concurrent
open Chainium.Common
open Chainium.Blockchain.Public.Core
open Chainium.Blockchain.Public.Core.Dtos

module TransportMock =

    let messageQueue = new ConcurrentDictionary<string, List<string>>()

    let private packMessage message =
        message |> Serialization.serializePeerMessage

    let private unpackMessage message =
        message |> Serialization.deserializePeerMessage

    let private send (msg : string) targetAddress =
        match messageQueue.TryGetValue targetAddress with
        | true, messages ->
            messageQueue.AddOrUpdate (targetAddress, msg :: messages, fun _ _ -> msg :: messages) |> ignore
        | _ -> messageQueue.AddOrUpdate (targetAddress, [msg], fun _ _ -> [msg]) |> ignore

    let sendGossipDiscoveryMessage gossipDiscoveryMessage targetAddress =
        let msg = packMessage gossipDiscoveryMessage
        send msg targetAddress

    let sendGossipMessage gossipMessage (targetMember: GossipMemberDto) =
        let msg = packMessage gossipMessage
        send msg targetMember.NetworkAddress

    let sendUnicastMessage unicastMessage targetAddress =
        let msg = packMessage unicastMessage
        send msg targetAddress

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
                send msg networkAddress
            )

    let receiveMessage networkAddress receiveCallback =
        let rec loop address callback =
            async {
                match messageQueue.TryGetValue address with
                | true, messages ->
                    messages
                    |> List.iter(fun message ->
                        let peerMessage = unpackMessage message
                        callback peerMessage
                    )
                    messageQueue.TryRemove address |> ignore

                | _ -> ()
                do! Async.Sleep(100)
                return! loop address callback
            }
        Async.Start (loop networkAddress receiveCallback)

    let closeConnection networkAddress =
        messageQueue.TryRemove networkAddress |> ignore

    let closeAllConnections () =
        messageQueue.Clear()
