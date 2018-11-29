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
        | TxReceived e -> e.TxHash |> fun (TxHash h) -> h
        | BlockCreated e
        | BlockReceived e -> e.BlockNumber |> fun (BlockNumber n) -> string n
        | ConsensusMessageReceived e
        | ConsensusCommandInvoked e ->
            match e with
            | Synchronize -> "Synchronize"
            | Message (sender, consensusMessageEnvelope) ->
                sprintf "Message from %s: %i / %i / %A"
                    (sender |> fun (BlockchainAddress a) -> a)
                    (consensusMessageEnvelope.BlockNumber |> fun (BlockNumber n) -> n)
                    (consensusMessageEnvelope.Round |> fun (ConsensusRound r) -> r)
                    (consensusMessageEnvelope.ConsensusMessage |> Consensus.consensusMessageDisplayFormat)
            | Timeout (blockNumber, consensusRound, consensusStep) ->
                sprintf "Timeout %i / %i / %s"
                    (blockNumber |> fun (BlockNumber n) -> n)
                    (consensusRound |> fun (ConsensusRound r) -> r)
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
                    validatorAddress
                    |> fun (BlockchainAddress a) -> a
                    |> Log.infof "Configured as validator with address %s."

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
