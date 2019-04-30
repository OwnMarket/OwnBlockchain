namespace Own.Blockchain.Public.Node

open Own.Common.FSharp
open Own.Blockchain.Common
open Own.Blockchain.Public.Core
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Core.Dtos
open Own.Blockchain.Public.Core.Events
open Own.Blockchain.Public.Crypto

module Agents =

    let private txPropagator = Agent.start <| fun (txHash : TxHash, txEnvelopeDto : TxEnvelopeDto option) ->
        async {
            Log.debugf "Propagating Tx %s" txHash.Value
            Composition.propagateTx txHash txEnvelopeDto
        }

    let private equivocationProofPropagator = Agent.start <| fun (equivocationProofHash : EquivocationProofHash) ->
        async {
            Log.debugf "Propagating EquivocationProof %s" equivocationProofHash.Value
            Composition.propagateEquivocationProof equivocationProofHash
        }

    let private blockPropagator = Agent.start <| fun (blockNumber : BlockNumber) ->
        async {
            Log.debugf "Propagating block %i" blockNumber.Value
            Composition.propagateBlock blockNumber
        }

    let mutable private peerMessageHandler : MailboxProcessor<PeerMessageEnvelope> option = None
    let private invokePeerMessageHandler m =
        match peerMessageHandler with
        | Some h -> h.Post m
        | None -> Log.error "PeerMessageHandler agent not started"

    let mutable private updatePeerListHandler : MailboxProcessor<GossipPeer list> option = None
    let private invokeUpdatePeerListHandler peerList =
        match updatePeerListHandler with
        | Some h -> h.Post peerList
        | None -> Log.error "UpdatePeerListHandler agent not started"

    let mutable private txVerifier : MailboxProcessor<TxEnvelopeDto * bool> option = None
    let private invokeTxVerifier e =
        match txVerifier with
        | Some v -> v.Post e
        | None -> Log.error "TxVerifier agent not started"

    let mutable private equivocationProofVerifier : MailboxProcessor<EquivocationProofDto * bool> option = None
    let private invokeEquivocationProofVerifier e =
        match equivocationProofVerifier with
        | Some v -> v.Post e
        | None -> Log.error "EquivocationProofVerifier agent not started"

    let mutable private blockchainHeadHandler : MailboxProcessor<BlockNumber> option = None
    let private invokeBlockchainHeadHandler e =
        match blockchainHeadHandler with
        | Some v -> v.Post e
        | None -> Log.error "BlockchainHeadHandler agent not started"

    let mutable private blockVerifier : MailboxProcessor<BlockEnvelopeDto * bool> option = None
    let private invokeBlockVerifier e =
        match blockVerifier with
        | Some v -> v.Post e
        | None -> Log.error "BlockVerifier agent not started"

    let mutable private applier : MailboxProcessor<_> option = None
    let private invokeApplier () =
        match applier with
        | Some a -> a.Post ()
        | None -> Log.error "Applier agent not started"

    let mutable private validator : MailboxProcessor<ConsensusCommand> option = None
    let private invokeValidator c =
        match validator with
        | Some v -> v.Post c
        | None -> Log.error "Validator agent not started"

    let private logEvent (event : AppEvent) =
        let formatMessage =
            sprintf "EVENT: %s: %s" event.CaseName

        match event with
        | PeerMessageReceived m ->
            m.PeerMessage.CaseName
            |> formatMessage
            |> Log.debug
        | TxSubmitted h ->
            h.Value
            |> formatMessage
            |> Log.info
        | TxReceived (h, _)
        | TxFetched (h, _) ->
            h.Value
            |> formatMessage
            |> Log.info
        | TxVerified (h, _) ->
            h.Value
            |> formatMessage
            |> Log.debug
        | TxStored (h, _) ->
            h.Value
            |> formatMessage
            |> Log.info
        | EquivocationProofDetected (proof, validatorAddress) ->
            sprintf "Validator %s: %A" validatorAddress.Value proof
            |> formatMessage
            |> Log.warning
        | EquivocationProofReceived proof ->
            sprintf "%A" proof
            |> formatMessage
            |> Log.warning
        | EquivocationProofFetched proof ->
            sprintf "%A" proof
            |> formatMessage
            |> Log.info
        | EquivocationProofStored (equivocationProofHash, isFetched) ->
            equivocationProofHash.Value
            |> formatMessage
            |> Log.info
        | BlockCommitted (bn, _) ->
            bn.Value
            |> string
            |> formatMessage
            |> Log.notice
        | BlockReceived (bn, _)
        | BlockFetched (bn, _) ->
            bn.Value
            |> string
            |> formatMessage
            |> Log.info
        | BlockStored (bn, _) ->
            bn.Value
            |> string
            |> formatMessage
            |> Log.info
        | BlockReady bn ->
            bn.Value
            |> string
            |> formatMessage
            |> Log.info
        | BlockApplied bn ->
            bn.Value
            |> string
            |> formatMessage
            |> Log.success
        | ConsensusMessageReceived c
        | ConsensusCommandInvoked c ->
            match c with
            | Synchronize -> "Synchronize"
            | Message (sender, consensusMessageEnvelope) ->
                sprintf "Message from %s: %i / %i / %A"
                    sender.Value
                    consensusMessageEnvelope.BlockNumber.Value
                    consensusMessageEnvelope.Round.Value
                    (consensusMessageEnvelope.ConsensusMessage |> Consensus.consensusMessageDisplayFormat)
            | RetryPropose (blockNumber, consensusRound) ->
                sprintf "RetryPropose %i / %i"
                    blockNumber.Value
                    consensusRound.Value
            | Timeout (blockNumber, consensusRound, consensusStep) ->
                sprintf "Timeout %i / %i / %s"
                    blockNumber.Value
                    consensusRound.Value
                    consensusStep.CaseName
            | StateRequested (request, _) ->
                sprintf "StateRequested %s" request.ValidatorAddress.Value
            | StateReceived response ->
                sprintf "StateReceived %A" response
            |> formatMessage
            |> Log.info
        | ConsensusStateRequestReceived (request, _) ->
            sprintf "%s / %i / %A"
                request.ValidatorAddress.Value
                request.ConsensusRound.Value
                (request.TargetValidatorAddress |> Option.map (fun a -> a.Value))
            |> formatMessage
            |> Log.debug
        | ConsensusStateResponseReceived response ->
            sprintf "%i messages / valid round: %i / valid value: %s / %i signatures"
                response.Messages.Length
                response.ValidRound.Value
                (if response.ValidProposal.IsSome then "Some" else "None")
                response.ValidVoteSignatures.Length
            |> formatMessage
            |> Log.debug
        | BlockchainHeadReceived blockNr ->
            sprintf "BlockNumber %i" blockNr.Value
            |> formatMessage
            |> Log.debug
        | PeerListReceived peerList ->
            Log.verbose "PeerListReceived"
            Log.verbose "============================================================"
            peerList
            |> List.map Mapping.gossipPeerToDto
            |> List.iter (fun m -> Log.verbosef "%s Heartbeat: %i" m.NetworkAddress m.Heartbeat)
            Log.verbose "============================================================"

    let publishEvent event =
        logEvent event

        match event with
        | PeerMessageReceived message ->
            invokePeerMessageHandler message
        | TxSubmitted txHash ->
            invokeApplier ()
            txPropagator.Post (txHash, None)
        | TxReceived (txHash, txEnvelopeDto) ->
            invokeTxVerifier (txEnvelopeDto, false)
        | TxFetched (txHash, txEnvelopeDto) ->
            invokeTxVerifier (txEnvelopeDto, true)
        | TxVerified (txHash, txEnvelopeDto) ->
            txPropagator.Post (txHash, txEnvelopeDto)
        | TxStored (txHash, isFetched) ->
            invokeApplier ()
        | EquivocationProofDetected (proof, validatorAddress) ->
            invokeEquivocationProofVerifier (proof, false)
        | EquivocationProofReceived proof ->
            invokeEquivocationProofVerifier (proof, false)
        | EquivocationProofFetched proof ->
            invokeEquivocationProofVerifier (proof, true)
        | EquivocationProofStored (equivocationProofHash, isFetched) ->
            invokeApplier ()
            if not isFetched then
                equivocationProofPropagator.Post equivocationProofHash
        | BlockCommitted (blockNumber, blockEnvelopeDto)
        | BlockReceived (blockNumber, blockEnvelopeDto) ->
            invokeBlockVerifier (blockEnvelopeDto, false)
        | BlockFetched (blockNumber, blockEnvelopeDto) ->
            invokeBlockVerifier (blockEnvelopeDto, true)
        | BlockStored (blockNumber, isFetched) ->
            invokeApplier ()
            if not isFetched then
                blockPropagator.Post blockNumber
        | BlockReady blockNumber ->
            invokeApplier ()
        | BlockApplied blockNumber ->
            invokeValidator Synchronize // TODO: Don't invoke if not validator (and remove WORKAROUND below).
            invokeApplier () // Avoid waiting for a kick from the Fetcher.
        | ConsensusMessageReceived c
        | ConsensusCommandInvoked c ->
            invokeValidator c
        | ConsensusStateRequestReceived (request, peerIdentity) ->
            ConsensusCommand.StateRequested (request, peerIdentity)
            |> invokeValidator
        | ConsensusStateResponseReceived state ->
            ConsensusCommand.StateReceived state
            |> invokeValidator
        | BlockchainHeadReceived blockNumber ->
            invokeBlockchainHeadHandler blockNumber
        | PeerListReceived peerList ->
            invokeUpdatePeerListHandler peerList

    let private startPeerMessageHandler () =
        if peerMessageHandler <> None then
            failwith "PeerMessageHandler agent is already started"

        peerMessageHandler <-
            Agent.start <| fun peerMessageEnvelope ->
                async {
                    Composition.processPeerMessage peerMessageEnvelope
                    |> Option.iter (
                        Result.handle
                            (Option.iter publishEvent)
                            Log.appErrors
                    )
                }
            |> Some

    let private startUpdatePeerListHandler () =
        if updatePeerListHandler <> None then
            failwith "UpdatePeerListHandler agent is already started"

        updatePeerListHandler <-
            Agent.start <| fun peerList ->
                async {
                    Composition.updatePeerList peerList
                }
            |> Some

    let private startTxVerifier () =
        if txVerifier <> None then
            failwith "TxVerifier agent is already started"

        txVerifier <-
            Agent.start <| fun (txEnvelopeDto, isFetched) ->
                async {
                    Composition.submitTx publishEvent isFetched txEnvelopeDto
                    |> Result.handle
                        (fun txHash -> (txHash, isFetched) |> TxStored |> publishEvent)
                        Log.appErrors
                }
            |> Some

    let private startEquivocationProofVerifier () =
        if equivocationProofVerifier <> None then
            failwith "EquivocationProofVerifier agent is already started"

        equivocationProofVerifier <-
            Agent.start <| fun (equivocationProofDto, isFetched) ->
                async {
                    Composition.storeEquivocationProof equivocationProofDto
                    |> Result.handle
                        (fun equivocationProofHash ->
                            (equivocationProofHash, isFetched) |> EquivocationProofStored |> publishEvent
                        )
                        Log.appErrors
                }
            |> Some

    let private startBlockchainHeadHandler () =
        if blockchainHeadHandler <> None then
            failwith "BlockchainHeadHandler agent is already started"

        blockchainHeadHandler <-
            Agent.start <| fun blockNumber ->
                async {
                    Composition.handleReceivedBlockchainHead blockNumber
                }
            |> Some

    let private startBlockVerifier () =
        if blockVerifier <> None then
            failwith "BlockVerifier agent is already started"

        blockVerifier <-
            Agent.start <| fun (blockEnvelopeDto, isFetched) ->
                async {
                    Composition.storeReceivedBlock blockEnvelopeDto
                    |> Result.handle
                        (fun blockNumber -> (blockNumber, isFetched) |> BlockStored |> publishEvent)
                        Log.appErrors
                }
            |> Some

    let private startApplier () =
        if applier <> None then
            failwith "Applier agent is already started"

        applier <-
            Agent.start <| fun message ->
                async {
                    Composition.tryApplyNextBlock publishEvent
                }
            |> Some

    let startValidator () =
        if validator <> None then
            failwith "Validator agent is already started"

        let state =
            Config.ValidatorPrivateKey
            |> Option.ofObj
            |> Option.map (PrivateKey >> Composition.addressFromPrivateKey)
            |> Option.bind (fun validatorAddress ->
                if not (Hashing.isValidBlockchainAddress validatorAddress) then
                    Log.error "Configured validator address is not a valid blockchain address"
                    None
                else
                    Log.infof "Configured as validator with address %s" validatorAddress.Value
                    Composition.createConsensusStateInstance publishEvent
                    |> Some
            )

        validator <-
            Agent.start <| fun (command : ConsensusCommand) ->
                async {
                    match state with
                    | Some s -> s.HandleConsensusCommand command
                    | None ->
                        // WORKAROUND: Avoid log polution due to Synchronize being invoked upon applying the block.
                        if command <> Synchronize then
                            Log.warning "Consensus command ignored (node is not configured as validator)"
                }
            |> Some

        state
        |> Option.iter (fun s -> s.StartConsensus())

    let startAgents () =
        startPeerMessageHandler ()
        startUpdatePeerListHandler ()
        startTxVerifier ()
        startEquivocationProofVerifier ()
        startBlockchainHeadHandler ()
        startBlockVerifier ()
        startApplier ()
