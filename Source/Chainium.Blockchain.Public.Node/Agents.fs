namespace Chainium.Blockchain.Public.Node

open System
open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Core
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Core.Events
open Chainium.Blockchain.Public.Net

module Agents =

    let private txPropagator = Agent.start <| fun (message : TxSubmittedEvent) ->
        async {
            // TODO: Implement in Workflow module, compose in Composition module, call here.
            sprintf "%A" message.TxHash
            |> Peers.sendMessage
        }

    let private blockPropagator = Agent.start <| fun (message : BlockCreatedEvent) ->
        async {
            // TODO: Implement in Workflow module, compose in Composition module, call here.
            sprintf "%A" message.BlockNumber
            |> Peers.sendMessage
        }

    let publishEvent event =
        Log.infof "EVENT: %A" event

        match event with
        | TxSubmitted e -> txPropagator.Post e
        | BlockCreated e -> blockPropagator.Post e
