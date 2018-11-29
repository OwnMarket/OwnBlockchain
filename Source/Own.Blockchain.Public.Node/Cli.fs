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
        Composition.initSynchronizationState ()
        Composition.startGossip Agents.publishEvent
        Composition.discoverNetwork ()
        Composition.requestLastBlockFromPeer ()
        Workers.startApplier ()
        Agents.startValidator ()
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
        | ["-v"] -> handleShowVersionCommand ()
        | ["-n"] | [] -> handleStartNodeCommand () // Default command
        | ["-g"; privateKey] -> handleSignGenesisBlockCommand privateKey
        | ["--help"] | _ -> handleHelpCommand args
