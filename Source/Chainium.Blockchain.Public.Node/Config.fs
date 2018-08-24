namespace Chainium.Blockchain.Public.Node

open System
open System.IO
open Microsoft.Extensions.Configuration
open Chainium.Common

type Config () =

    static let appDir = Directory.GetCurrentDirectory()

    static let config =
        ConfigurationBuilder()
            .SetBasePath(appDir)
            .AddJsonFile("AppSettings.json")
            .Build()

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

    static member NetworkAddress
        with get () =
            let networkAddress = config.["NetworkAddress"]
            if networkAddress.IsNullOrWhiteSpace() then
                "127.0.0.1:25718"
            else
                networkAddress

    static member NetworkBootstrapNodes
        with get () =
            config.GetSection("NetworkBootstrapNodes").GetChildren()
            |> Seq.map (fun c -> c.Value)
            |> Seq.toList

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // API
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    static member ApiListeningAddresses
        with get () =
            let configAddress = config.["ApiListeningAddresses"]
            if configAddress.IsNullOrWhiteSpace() then
                "http://*:10717"
            else
                configAddress

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Genesis
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    static member GenesisChxSupply = 168956522.093084393899693958m

    static member GenesisAddress
        with get () =
            config.["GenesisAddress"]

    static member GenesisValidators
        with get () =
            config.GetSection("GenesisValidators").GetChildren()
            |> Seq.map (fun e ->
                match e.Value.Split(",") with
                | [| validatorAddress; networkAddress |] -> validatorAddress, networkAddress
                | _ -> failwith "Invalid GenesisValidators configuration."
            )
            |> Seq.toList

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Processing
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    static member MinTxActionFee = 0.001m // CHX

    static member BlockCreationInterval = 5 // Seconds

    static member MaxTxCountPerBlock = 100 // TODO: Shall this be managed by consensus protocol?

    static member ValidatorPrivateKey
        with get () =
            config.["ValidatorPrivateKey"]
