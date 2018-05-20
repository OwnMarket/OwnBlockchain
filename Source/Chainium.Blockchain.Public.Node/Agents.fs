namespace Chainium.Blockchain.Public.Node

open System
open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Core
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Core.Events

module Agents =

    let private txPropagator = Agent.start <| fun (message : TxSubmittedEvent) ->
        async {
            Composition.propagateTx message.TxHash
        }

    let private blockPropagator = Agent.start <| fun (message : BlockCreatedEvent) ->
        async {
            Composition.propagateBlock message.BlockNumber
        }

    let publishEvent event =
        Log.infof "EVENT: %A" event

        match event with
        | TxSubmitted e -> txPropagator.Post e
        | BlockCreated e -> blockPropagator.Post e
