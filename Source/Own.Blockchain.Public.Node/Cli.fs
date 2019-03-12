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
        Workers.startNetworkTimeSynchronizer ()
        Agents.startAgents ()
        Composition.startNetworkAgents ()
        Composition.startGossip Agents.publishEvent
        Composition.discoverNetwork ()
        Workers.startBlockchainHeadPoller ()
        Workers.startFetcher ()
        Api.start ()
        Composition.stopGossip ()

    let handleSignGenesisBlockCommand privateKey =
        privateKey // TODO: Use key file path, to prevent keys being logged in terminal history.
        |> PrivateKey
        |> Composition.signGenesisBlock
        |> fun (Signature signature) -> printfn "Signature: %s" signature

    let handleHelpCommand args =
        printfn "TODO: Print short command reference"

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Configuration
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let handleCommand args =
        match args with
        | ["--version"] -> handleShowVersionCommand ()
        | ["--node"] | [] -> handleStartNodeCommand () // Default command
        | ["--sign-genesis"; privateKey] -> handleSignGenesisBlockCommand privateKey
        | ["--help"] | _ -> handleHelpCommand args
