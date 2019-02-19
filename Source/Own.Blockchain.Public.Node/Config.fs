namespace Own.Blockchain.Public.Node

open System
open System.Globalization
open System.IO
open System.Text.RegularExpressions
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
            | "Postgres" -> Postgres
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

    static member NetworkCode
        with get () =
            let networkCode = config.["NetworkCode"]
            if networkCode.IsNullOrWhiteSpace() then
                "OWN_PUBLIC_BLOCKCHAIN_DEVNET"
            elif Regex.IsMatch(networkCode, "^[A-Z_0-9]+$") then
                networkCode
            else
                failwithf "Invalid NetworkCode: %s" networkCode

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Synchronization
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    static member NetworkTimeUpdateInterval = 1 // Minutes

    static member MaxNumberOfBlocksToFetchInParallel = 10

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Processing
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    static member MaxTxCountPerBlock = 1000

    static member MinTxActionFee // In CHX
        with get () =
            match Decimal.TryParse(config.["MinTxActionFee"], NumberStyles.Number, CultureInfo.InvariantCulture) with
            | true, value ->
                if value >= 0.0000001m then // Smallest possible CHX value (7 decimal places).
                    value
                else
                    failwith "MinTxActionFee must be at least 0.0000001 CHX."
            | _ -> 0.001m // Default value if not explicitly configured.

    static member ValidatorPrivateKey
        with get () =
            config.["ValidatorPrivateKey"]

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Consensus
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    static member MinValidatorCount = 4
    static member MaxValidatorCount = 100

    static member ValidatorThreshold = 500_000m
    static member ValidatorDeposit = 5_000m

    static member MaxRewardedStakesCount = 100

    static member ConsensusMessageRetryingInterval = 1000 // Milliseconds
    static member ConsensusProposeRetryingInterval = 1000 // Milliseconds

    static member ConsensusTimeoutPropose = 5000 // Milliseconds
    static member ConsensusTimeoutVote = 5000 // Milliseconds
    static member ConsensusTimeoutCommit = 5000 // Milliseconds

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Blockchain Configuration (initial values)
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    static member ConfigurationBlockDelta = 3 // Number of blocks between two config blocks.

    static member ValidatorDepositLockTime = 2 // Number of config blocks to keep the deposit locked after leaving.
    static member ValidatorBlacklistTime = 5 // Number of config blocks to keep the validator blacklisted.

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
                match e.Value.Split("@") with
                | [| validatorAddress; networkAddress |] -> validatorAddress, networkAddress
                | _ -> failwith "Invalid GenesisValidators configuration."
            )
            |> Seq.toList

    static member GenesisSignatures
        with get () =
            genesis.GetSection("GenesisSignatures").GetChildren()
            |> Seq.map (fun e -> e.Value)
            |> Seq.toList
