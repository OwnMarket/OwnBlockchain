namespace Own.Blockchain.Public.Node

open System
open System.Globalization
open System.IO
open Microsoft.Extensions.Configuration
open Own.Common
open Own.Blockchain.Public.Core.DomainTypes

type Config () =

    static let appDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)

    static let workingDir = Directory.GetCurrentDirectory()

    static let config =
        ConfigurationBuilder()
            .SetBasePath(workingDir)
            .AddJsonFile("Config.json")
            .Build()

    static let genesis =
        ConfigurationBuilder()
            .SetBasePath(workingDir)
            .AddJsonFile("Genesis.json")
            .Build()

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Storage
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    static member DataDir
        with get () =
            Path.Combine(workingDir, "Data")

    static member DbConnectionString
        with get () =
            config.["DbConnectionString"]

    static member DbEngineType
        with get () =
            match config.["DbEngineType"] with
            | "Firebird" -> Firebird
            | "PostgreSQL" -> PostgreSQL
            | t -> failwithf "Unknown DB engine type: %s" t

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
    static member GenesisChxSupply = 168_956_522.0930844m

    static member GenesisAddress
        with get () =
            genesis.["GenesisAddress"]

    static member GenesisValidators
        with get () =
            genesis.GetSection("GenesisValidators").GetChildren()
            |> Seq.map (fun e ->
                match e.Value.Split(",") with
                | [| validatorAddress; networkAddress |] -> validatorAddress, networkAddress
                | _ -> failwith "Invalid GenesisValidators configuration."
            )
            |> Seq.toList

    static member GenesisSignatures
        with get () =
            genesis.GetSection("GenesisSignatures").GetChildren()
            |> Seq.map (fun e -> e.Value)
            |> Seq.toList

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Processing
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    static member MinTxActionFee // In CHX
        with get () =
            match Decimal.TryParse(config.["MinTxActionFee"], NumberStyles.Number, CultureInfo.InvariantCulture) with
            | true, value -> if value > 0m then value else failwith "MinTxActionFee must be a positive number."
            | _ -> 0.001m

    static member MaxTxCountPerBlock = 100

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
    static member MinValidatorCount = 4
    static member MaxValidatorCount = 100

    static member ValidatorThreshold = 500_000m

    static member MaxRewardedStakersCount = 100

    static member ConsensusMessageRetryingInterval = 1000
    static member ConsensusProposeRetryingInterval = 1000

    static member ConsensusTimeoutPropose = 5000
    static member ConsensusTimeoutVote = 5000
    static member ConsensusTimeoutCommit = 5000
