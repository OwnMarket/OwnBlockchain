namespace Own.Blockchain.Public.Node

open Own.Common
open Own.Blockchain.Common
open Own.Blockchain.Public.Core
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Core.Dtos
open Own.Blockchain.Public.Core.Events
open Own.Blockchain.Public.Crypto

module Agents =

    let private txPropagator = Agent.start <| fun txHash ->
        async {
            Composition.propagateTx txHash
        }

    let private blockPropagator = Agent.start <| fun blockNumber ->
        async {
            Composition.propagateBlock blockNumber
        }

    let mutable private peerMessageHandler : MailboxProcessor<PeerMessage> option = None
    let private invokePeerMessageHandler m =
        match peerMessageHandler with
        | Some h -> h.Post m
        | None -> Log.error "PeerMessageHandler agent not started."

    let mutable private txVerifier : MailboxProcessor<TxEnvelopeDto * bool> option = None
    let private invokeTxVerifier e =
        match txVerifier with
        | Some v -> v.Post e
        | None -> Log.error "TxVerifier agent not started."

    let mutable private blockVerifier : MailboxProcessor<BlockEnvelopeDto> option = None
    let private invokeBlockVerifier e =
        match blockVerifier with
        | Some v -> v.Post e
        | None -> Log.error "BlockVerifier agent not started."

    let mutable private applier : MailboxProcessor<_> option = None
    let private invokeApplier () =
        match applier with
        | Some a -> a.Post ()
        | None -> Log.error "Applier agent not started."

    let mutable private validator : MailboxProcessor<ConsensusCommand> option = None
    let private invokeValidator c =
        match validator with
        | Some v -> v.Post c
        | None -> Log.error "Validator agent not started."

    let private logEvent event =
        let formatMessage =
            sprintf "EVENT: %s: %s" (unionCaseName event)

        match event with
        | PeerMessageReceived m ->
            unionCaseName m
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
        | TxStored h ->
            h.Value
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
        | BlockStored bn
        | BlockCompleted bn ->
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
                    (unionCaseName consensusStep)
            |> formatMessage
            |> Log.info

    let publishEvent event =
        logEvent event

        match event with
        | PeerMessageReceived message ->
            invokePeerMessageHandler message
        | TxSubmitted txHash ->
            invokeApplier ()
            txPropagator.Post txHash
        | TxReceived (txHash, txEnvelopeDto) ->
            invokeTxVerifier (txEnvelopeDto, false)
        | TxFetched (txHash, txEnvelopeDto) ->
            invokeTxVerifier (txEnvelopeDto, true)
        | TxStored txHash ->
            invokeApplier ()
            txPropagator.Post txHash // TODO: Don't propagate fetched Txs
        | BlockCommitted (blockNumber, blockEnvelopeDto)
        | BlockReceived (blockNumber, blockEnvelopeDto)
        | BlockFetched (blockNumber, blockEnvelopeDto) ->
            invokeBlockVerifier blockEnvelopeDto
        | BlockStored blockNumber ->
            invokeApplier ()
            blockPropagator.Post blockNumber
        | BlockCompleted blockNumber ->
            invokeApplier ()
        | BlockApplied blockNumber ->
            invokeValidator Synchronize
        | ConsensusMessageReceived c
        | ConsensusCommandInvoked c ->
            invokeValidator c

    let private startPeerMessageHandler () =
        if peerMessageHandler <> None then
            failwith "PeerMessageHandler agent is already started."

        peerMessageHandler <-
            Agent.start <| fun (peerMessage) ->
                async {
                    Composition.processPeerMessage peerMessage
                    |> Option.iter (
                        Result.handle
                            (Option.iter publishEvent)
                            Log.appErrors
                    )
                }
            |> Some

    let private startTxVerifier () =
        if txVerifier <> None then
            failwith "TxVerifier agent is already started."

        txVerifier <-
            Agent.start <| fun (txEnvelopeDto, isIncludedInBlock) ->
                async {
                    Composition.submitTx isIncludedInBlock txEnvelopeDto
                    |> Result.handle
                        (TxStored >> publishEvent)
                        Log.appErrors
                }
            |> Some

    let private startBlockVerifier () =
        if blockVerifier <> None then
            failwith "BlockVerifier agent is already started."

        blockVerifier <-
            Agent.start <| fun (blockEnvelopeDto) ->
                async {
                    Composition.storeReceivedBlock blockEnvelopeDto
                    |> Result.handle
                        (BlockStored >> publishEvent)
                        Log.appErrors
                }
            |> Some

    let private startApplier () =
        if applier <> None then
            failwith "Applier agent is already started."

        applier <-
            Agent.start <| fun (message) ->
                async {
                    Composition.tryApplyNextBlock publishEvent
                }
            |> Some

    let private startValidator () =
        if validator <> None then
            failwith "Validator agent is already started."

        let state =
            Config.ValidatorPrivateKey
            |> Option.ofObj
            |> Option.map (PrivateKey >> Composition.addressFromPrivateKey)
            |> Option.bind (fun validatorAddress ->
                if not (Hashing.isValidBlockchainAddress validatorAddress) then
                    Log.error "Configured validator address is not a valid blockchain address."
                    None
                else
                    Log.infof "Configured as validator with address %s." validatorAddress.Value
                    Composition.createConsensusStateInstance publishEvent
                    |> Some
            )

        validator <-
            Agent.start <| fun (command : ConsensusCommand) ->
                async {
                    match state with
                    | Some s -> s.HandleConsensusCommand command
                    | None -> Log.warning "Consensus message ignored (node is not configured as validator)."
                }
            |> Some

        if state <> None then
            ConsensusCommand.Synchronize |> ConsensusCommandInvoked |> publishEvent

    let startAgents () =
        startPeerMessageHandler ()
        startTxVerifier ()
        startBlockVerifier ()
        startApplier ()
        startValidator ()
