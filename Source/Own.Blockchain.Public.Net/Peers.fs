namespace Own.Blockchain.Public.Net

open Own.Blockchain.Common
open Own.Blockchain.Public.Core.DomainTypes

module Peers =

    let mutable peerMessageDispatcher : MailboxProcessor<PeerMessage> option = None
    let invokeSendPeerMessage m =
        match peerMessageDispatcher with
        | Some h -> h.Post m
        | None -> Log.error "SendPeerMessage agent not started."

    let mutable requestFromPeerDispatcher : MailboxProcessor<NetworkMessageId> option = None
    let invokeRequestFromPeer m =
        match requestFromPeerDispatcher with
        | Some h -> h.Post m
        | None -> Log.error "RequestFromPeer agent not started."

    let mutable respondToPeerDispatcher : MailboxProcessor<PeerNetworkIdentity * PeerMessage> option = None
    let invokeRespondToPeer m =
        match respondToPeerDispatcher with
        | Some h -> h.Post m
        | None -> Log.error "RespondToPeer agent not started."

    let private startSendPeerMessageDispatcher () =
        if peerMessageDispatcher <> None then
            failwith "SendPeerMessage agent is already started."

        peerMessageDispatcher <-
            Agent.start <| fun peerMessage ->
                async {
                    PeerMessageHandler.sendMessage peerMessage
                }
            |> Some

    let private startRequestFromPeerDispatcher () =
        if requestFromPeerDispatcher <> None then
            failwith "RequestFromPeer agent is already started."

        requestFromPeerDispatcher <-
            Agent.start <| fun messageId ->
                async {
                    PeerMessageHandler.requestFromPeer messageId
                }
            |> Some

    let private startRespondToPeerDispatcher () =
        if respondToPeerDispatcher <> None then
            failwith "RespondToPeer agent is already started."

        respondToPeerDispatcher <-
            Agent.start <| fun (targetIdentity, peerMessage) ->
                async {
                    PeerMessageHandler.respondToPeer targetIdentity peerMessage
                }
            |> Some

    let sendMessage peerMessage =
        invokeSendPeerMessage peerMessage

    let requestFromPeer messageId =
        invokeRequestFromPeer messageId

    let requestBlockFromPeer blockNumber =
        requestFromPeer (NetworkMessageId.Block blockNumber)

    let requestLastBlockFromPeer () =
        requestBlockFromPeer (BlockNumber -1L)

    let requestTxFromPeer txHash =
        requestFromPeer (NetworkMessageId.Tx txHash)

    let requestEquivocationProofFromPeer equivocationProofHash =
        requestFromPeer (NetworkMessageId.EquivocationProof equivocationProofHash)

    let respondToPeer targetIdentity peerMessage =
        invokeRespondToPeer (targetIdentity, peerMessage)

    let startGossip = PeerMessageHandler.startGossip

    let stopGossip = PeerMessageHandler.stopGossip

    let discoverNetwork = PeerMessageHandler.discoverNetwork

    let startNetworkAgents () =
        startSendPeerMessageDispatcher ()
        startRequestFromPeerDispatcher ()
        startRespondToPeerDispatcher ()
