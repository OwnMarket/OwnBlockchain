namespace Chainium.Blockchain.Public.Node

open System
open System.Text
open System.Reflection
open Chainium.Common

module Cli =

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Handlers
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let handleShowVersionCommand () =
        let assembly = System.Reflection.Assembly.GetExecutingAssembly()
        assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion
        |> printfn "%s"

    let handleStartNodeCommand () =
        Composition.initDb ()
        Composition.initBlockchainState ()
        PaceMaker.start ()
        Api.start ()

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
