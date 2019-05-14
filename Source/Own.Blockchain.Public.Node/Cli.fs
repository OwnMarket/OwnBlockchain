namespace Own.Blockchain.Public.Node

open System.Reflection
open Own.Blockchain.Public.Core.DomainTypes

module Cli =

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Handlers
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let handleShowVersionCommand () =
        let assembly = Assembly.GetExecutingAssembly()
        assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion
        |> printfn "%s"

    let handleStartNodeCommand () =
        Composition.initDb ()
        Composition.initBlockchainState ()
        Composition.rebuildBlockchainState ()
        Composition.startTxCacheMonitor ()
        Workers.startNetworkTimeSynchronizer ()
        Agents.startAgents ()
        Composition.startNetworkAgents ()
        Composition.startGossip Agents.publishEvent
        Composition.discoverNetwork ()
        Agents.startValidator ()
        Workers.startBlockchainHeadPoller ()
        Workers.startFetcher ()
        Workers.startPendingTxMonitor ()
        Api.start () // Blocks and waits for the exit signal.

        // Cleanup
        Composition.stopGossip ()

    let handleSignGenesisBlockCommand privateKey =
        privateKey // TODO: Use key file path, to prevent keys being logged in terminal history.
        |> PrivateKey
        |> Composition.signGenesisBlock
        |> fun (Signature signature) -> printfn "Signature: %s" signature

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Configuration
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let handleCommand args =
        match args with
        | [] -> handleStartNodeCommand () // Default command
        | ["--version"] -> handleShowVersionCommand ()
        | ["--sign-genesis"; privateKey] -> handleSignGenesisBlockCommand privateKey
        | _ -> printfn "Invalid arguments"
