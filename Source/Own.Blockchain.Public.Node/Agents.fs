namespace Own.Blockchain.Public.Node

open Own.Common
open Own.Blockchain.Common
open Own.Blockchain.Public.Core
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Core.Events
open Own.Blockchain.Public.Crypto

module Agents =

    let private txPropagator = Agent.start <| fun (message : TxReceivedEventData) ->
        async {
            Composition.propagateTx message.TxHash
        }

    let private blockPropagator = Agent.start <| fun (message : BlockCreatedEventData) ->
        async {
            Composition.propagateBlock message.BlockNumber
        }

    let mutable private validator : MailboxProcessor<ConsensusCommand> option = None

    let logEvent event =
        match event with
        | AppEvent.TxSubmitted e
        | TxReceived e -> e.TxHash.Value
        | BlockCreated e
        | BlockReceived e -> e.BlockNumber.Value |> string
        | ConsensusMessageReceived e
        | ConsensusCommandInvoked e ->
            match e with
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
        |> Log.infof "EVENT: %s: %s" (unionCaseName event)

    let publishEvent event =
        logEvent event

        match event with
        | TxSubmitted e
        | TxReceived e ->
            txPropagator.Post e
        | BlockCreated e
        | BlockReceived e ->
            blockPropagator.Post e
        | ConsensusMessageReceived e
        | ConsensusCommandInvoked e ->
            match validator with
            | Some v -> v.Post e
            | None -> Log.error "Validator agent not started."

    let startValidator () =
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
