namespace Own.Blockchain.Public.Node

open System.Reflection

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
        Composition.initSynchronizationState ()
        Composition.startGossip Agents.publishEvent
        Composition.discoverNetwork ()
        Composition.requestLastBlockFromPeer ()
        Workers.startApplier ()
        Workers.startProposer ()
        Api.start ()
        Composition.stopGossip ()

    let handleHelpCommand args =
        printfn "TODO: Print short command reference"

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Configuration
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let handleCommand args =
        match args with
        | ["-v"] -> handleShowVersionCommand ()
        | ["-n"] | [] -> handleStartNodeCommand () // Default command
        | ["--help"] | _ -> handleHelpCommand args
