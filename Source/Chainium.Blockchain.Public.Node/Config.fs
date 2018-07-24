namespace Chainium.Blockchain.Public.Node

open System
open System.IO
open System.Reflection
open Microsoft.Extensions.Configuration
open Chainium.Common

type Config () =

    static let appDir = Directory.GetCurrentDirectory()

    static let config =
        (
            ConfigurationBuilder()
                .SetBasePath(appDir)
                .AddJsonFile("AppSettings.json")
        ).Build()

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Storage
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    static member DataDir
        with get () =
            Path.Combine(appDir, "Data")

    static member DbConnectionString
        with get () =
            config.["DbConnectionString"]

    static member DbEngineType
        with get () =
            config.["DbEngineType"]

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Network
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    static member ListeningAddresses
        with get () =
            let configAddress = config.["ListeningAddresses"]
            if configAddress.IsNullOrWhiteSpace() then
                "http://*:10717"
            else
                configAddress

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Genesis
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    static member GenesisChxSupply = 168956522.093084393899693958M

    static member GenesisAddress
        with get () =
            config.["GenesisAddress"]

    static member GenesisValidators
        with get () =
            config.GetSection("GenesisValidators").GetChildren()
            |> Seq.map (fun e ->
                match e.Value.Split(",") with
                | [| chainiumAddress; networkAddress |] -> chainiumAddress, networkAddress
                | _ -> failwith "Invalid GenesisValidators configuration."
            )
            |> Seq.toList

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Processing
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    static member MinTxActionFee = 0.001M // CHX

    static member BlockCreationInterval = 5 // Seconds

    static member MaxTxCountPerBlock = 100 // TODO: Shall this be managed by consensus protocol?

    static member ValidatorAddress
        with get () =
            config.["ValidatorAddress"]
