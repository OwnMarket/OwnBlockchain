namespace Own.Blockchain.Public.Node

open System
open System.IO
open Microsoft.Extensions.Configuration
open Own.Common

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
    // Network
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    static member NetworkAddress
        with get () =
            let networkAddress = config.["NetworkAddress"]
            if networkAddress.IsNullOrWhiteSpace() then
                "*:25718"
            else
                networkAddress

    static member NetworkBootstrapNodes
        with get () =
            config.GetSection("NetworkBootstrapNodes").GetChildren()
            |> Seq.map (fun c -> c.Value)
            |> Seq.toList

    static member NetworkDiscoveryTime = 30 // Seconds

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

    static member ConfigurationBlockDelta = 100

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Synchronization
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    static member MaxNumberOfBlocksToFetchInParallel = 10

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Consensus
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    static member MinValidatorCount = 20

    static member MaxValidatorCount = 100

    static member QuorumSupplyPercent = 25m
