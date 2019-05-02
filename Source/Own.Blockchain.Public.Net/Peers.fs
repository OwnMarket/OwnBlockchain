namespace Own.Blockchain.Public.Net

open Own.Blockchain.Common
open Own.Blockchain.Public.Core.DomainTypes

module Peers =

    let mutable peerMessageDispatcher : MailboxProcessor<PeerMessageEnvelope> option = None
    let invokeSendPeerMessage m =
        match peerMessageDispatcher with
        | Some h -> h.Post m
        | None -> Log.error "SendPeerMessage agent is not started"

    let mutable requestFromPeerDispatcher : MailboxProcessor<NetworkMessageId * NetworkAddress option> option = None
    let invokeRequestFromPeer m =
        match requestFromPeerDispatcher with
        | Some h -> h.Post m
        | None -> Log.error "RequestFromPeer agent is not started"

    let mutable respondToPeerDispatcher : MailboxProcessor<PeerNetworkIdentity * PeerMessageEnvelope> option = None
    let invokeRespondToPeer m =
        match respondToPeerDispatcher with
        | Some h -> h.Post m
        | None -> Log.error "RespondToPeer agent is not started"

    let private startSendPeerMessageDispatcher () =
        if peerMessageDispatcher <> None then
            failwith "SendPeerMessage agent is already started"

        peerMessageDispatcher <-
            Agent.start <| fun peerMessageEnvelope ->
                async {
                    PeerMessageHandler.sendMessage peerMessageEnvelope
                }
            |> Some

    let private startRequestFromPeerDispatcher () =
        if requestFromPeerDispatcher <> None then
            failwith "RequestFromPeer agent is already started"

        requestFromPeerDispatcher <-
            Agent.start <| fun (messageId, preferredPeer) ->
                async {
                    PeerMessageHandler.requestFromPeer messageId preferredPeer
                }
            |> Some

    let private startRespondToPeerDispatcher () =
        if respondToPeerDispatcher <> None then
            failwith "RespondToPeer agent is already started"

        respondToPeerDispatcher <-
            Agent.start <| fun (targetIdentity, peerMessageEnvelope) ->
                async {
                    PeerMessageHandler.respondToPeer targetIdentity peerMessageEnvelope
                }
            |> Some

    let startGossip = PeerMessageHandler.startGossip

    let stopGossip = PeerMessageHandler.stopGossip

    let discoverNetwork = PeerMessageHandler.discoverNetwork

    let sendMessage peerMessageEnvelope =
        invokeSendPeerMessage peerMessageEnvelope

    let private requestFromRandomPeer messageId =
        invokeRequestFromPeer (messageId, None)

    let private requestFromPreferredPeer preferredPeer messageId =
        invokeRequestFromPeer (messageId, Some preferredPeer)

    let requestBlockFromPeer blockNumber =
        requestFromRandomPeer (NetworkMessageId.Block blockNumber)

    let requestLastBlockFromPeer () =
        requestBlockFromPeer (BlockNumber -1L)

    let requestBlockchainHeadFromPeer () =
        requestFromRandomPeer NetworkMessageId.BlockchainHead

    let requestTxFromPeer txHash =
        requestFromRandomPeer (NetworkMessageId.Tx txHash)

    let requestTxFromPreferredPeer preferredPeer txHash =
        requestFromPreferredPeer preferredPeer (NetworkMessageId.Tx txHash)

    let requestEquivocationProofFromPeer equivocationProofHash =
        requestFromRandomPeer (NetworkMessageId.EquivocationProof equivocationProofHash)

    let requestEquivocationProofFromPreferredPeer preferredPeer equivocationProofHash =
        requestFromPreferredPeer preferredPeer (NetworkMessageId.EquivocationProof equivocationProofHash)

    let respondToPeer targetIdentity peerMessage =
        invokeRespondToPeer (targetIdentity, peerMessage)

    let getIdentity () = PeerMessageHandler.getIdentity ()

    let getPeerList () = PeerMessageHandler.getPeerList ()

    let updatePeerList = PeerMessageHandler.updatePeerList

    let getNetworkStats () = PeerMessageHandler.getNetworkStats ()

    let startNetworkAgents () =
        startSendPeerMessageDispatcher ()
        startRequestFromPeerDispatcher ()
        startRespondToPeerDispatcher ()
