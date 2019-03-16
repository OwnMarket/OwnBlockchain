namespace Own.Blockchain.Public.Node

open System
open System.Globalization
open System.IO
open System.Text.RegularExpressions
open Microsoft.Extensions.Configuration
open Own.Common.FSharp
open Own.Blockchain.Common
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
    // General
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    static member MinLogLevel
        with get () =
            let minLogLevel = config.["MinLogLevel"]
            if minLogLevel.IsNullOrWhiteSpace() then
                Log.LogLevel.Info
            else
                match Enum.TryParse<Log.LogLevel>(minLogLevel) with
                | true, value -> value
                | _ -> failwithf "Invalid MinLogLevel value in config file: %s" minLogLevel

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Storage
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    static member DataDir
        with get () =
            Path.Combine(workingDir, "Data")

    static member DbConnectionString
        with get () =
            let connString = config.["DbConnectionString"]
            if connString.IsNullOrWhiteSpace() && Config.DbEngineType = Firebird then
                "Database=State.fdb"
            else
                connString

    static member DbEngineType
        with get () =
            match config.["DbEngineType"] with
            | "Firebird" -> Firebird
            | "Postgres" -> Postgres
            | dbEngineType ->
                if dbEngineType.IsNullOrWhiteSpace() then
                    Firebird
                else
                    failwithf "Unknown DB engine type: %s" dbEngineType

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
    static member ListeningAddress
        with get () =
            let listeningAddress = config.["ListeningAddress"]
            if listeningAddress.IsNullOrWhiteSpace() then
                "*:25718"
            else
                listeningAddress

    static member PublicAddress
        with get () =
            let publicAddress = config.["PublicAddress"]
            if publicAddress.IsNullOrWhiteSpace() then
                None
            else
                Some publicAddress

    static member NetworkBootstrapNodes
        with get () =
            config.GetSection("NetworkBootstrapNodes").GetChildren()
            |> Seq.map (fun c -> c.Value)
            |> Seq.toList

    static member NetworkDiscoveryTime // Seconds
        with get () =
            match Int32.TryParse config.["NetworkDiscoveryTime"] with
            | true, t -> if t > 0 then t else failwith "NetworkDiscoveryTime must be greater than zero"
            | _ -> 10

    static member AllowPrivateNetworkPeers
        with get () =
            let allowPrivateNetworkPeers = config.["AllowPrivateNetworkPeers"]
            if allowPrivateNetworkPeers.IsNullOrWhiteSpace() then
                false
            else
                match bool.TryParse(allowPrivateNetworkPeers) with
                | true, allow -> allow
                | _ -> false

    static member MaxConnectedPeers
        with get () =
            match Int32.TryParse config.["MaxConnectedPeers"] with
            | true, value when value > 0 -> value
            | _ -> 200 // Default value if not explicitly configured.

    static member GossipFanout
        with get () =
            match Int32.TryParse config.["GossipFanout"] with
            | true, fanout when fanout > 0 -> fanout
            | _ -> 4

    static member GossipInterval // Milliseconds
        with get () =
            match Int32.TryParse config.["GossipInterval"] with
            | true, interval when interval > 0 -> interval
            | _ -> 10000

    static member GossipMaxMissedHeartbeats
        with get () =
            match Int32.TryParse config.["GossipMaxMissedHeartbeats"] with
            | true, cycles when cycles > 0 -> cycles
            | _ -> 10

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Synchronization
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    static member NetworkTimePollInterval = 60 // Seconds

    static member BlockchainHeadPollInterval = 60 // Seconds

    static member MaxNumberOfBlocksToFetchInParallel = 10

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Consensus
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    static member MinValidatorCount = 4
    static member MaxValidatorCount = 100

    static member MaxRewardedStakesCount = 100

    static member ValidatorThreshold = 500_000m
    static member ValidatorDeposit = 5_000m

    static member ConsensusMessageRetryingInterval = 1000 // Milliseconds
    static member ConsensusProposeRetryingInterval = 1000 // Milliseconds

    static member ConsensusTimeoutPropose = 3000 // Milliseconds
    static member ConsensusTimeoutVote = 3000 // Milliseconds
    static member ConsensusTimeoutCommit = 3000 // Milliseconds

    static member ConsensusTimeoutDelta = 1000 // Milliseconds
    static member ConsensusTimeoutIncrements = 10

    static member StaleRoundDetectionInterval = 10000 // Milliseconds

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Validator
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    static member ValidatorPrivateKey
        with get () =
            config.["ValidatorPrivateKey"]

    static member MinTxActionFee // In CHX
        with get () =
            match Decimal.TryParse(config.["MinTxActionFee"], NumberStyles.Number, CultureInfo.InvariantCulture) with
            | true, value ->
                if value < 0.0000001m then // Smallest possible CHX value (7 decimal places).
                    failwith "MinTxActionFee must be at least 0.0000001 CHX"
                else
                    value
            | _ -> 0.001m // Default value if not explicitly configured.

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Tx
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    static member MaxActionCountPerTx = 1000

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Blockchain Configuration (initial genesis values)
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    static member ConfigurationBlockDelta // Number of blocks between two config blocks.
        with get () =
            match Int32.TryParse(genesis.["ConfigurationBlockDelta"]) with
            | true, value when value > 0 -> value
            | _ -> failwith "ConfigurationBlockDelta must have a valid positive 32-bit integer value in genesis file"

    static member ValidatorDepositLockTime // Number of config blocks to keep the deposit locked after leaving.
        with get () =
            match Int16.TryParse(genesis.["ValidatorDepositLockTime"]) with
            | true, value when value > 0s -> value
            | _ -> failwith "ValidatorDepositLockTime must have a valid positive 16-bit integer value in genesis file"

    static member ValidatorBlacklistTime // Number of config blocks to keep the validator blacklisted.
        with get () =
            match Int16.TryParse(genesis.["ValidatorBlacklistTime"]) with
            | true, value when value > 0s -> value
            | _ -> failwith "ValidatorBlacklistTime must have a valid positive 16-bit integer value in genesis file"

    static member MaxTxCountPerBlock
        with get () =
            match Int32.TryParse(genesis.["MaxTxCountPerBlock"]) with
            | true, value when value > 0 -> value
            | _ -> failwith "MaxTxCountPerBlock must have a valid positive 32-bit integer value in genesis file"

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Genesis
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    static member NetworkCode
        with get () =
            let networkCode = genesis.["NetworkCode"]
            if networkCode.IsNullOrWhiteSpace() then
                failwith "NetworkCode not found in genesis file"
            elif Regex.IsMatch(networkCode, "^[A-Z_0-9]+$") then
                networkCode
            else
                failwithf "Invalid NetworkCode: %s" networkCode

    static member GenesisChxSupply = 168_956_522.0930844m

    static member GenesisAddress
        with get () =
            let genesisAddress = genesis.["GenesisAddress"]
            if genesisAddress.IsNullOrWhiteSpace() then
                failwith "GenesisAddress not found in genesis file"
            else
                genesisAddress

    static member GenesisValidators
        with get () =
            genesis.GetSection("GenesisValidators").GetChildren()
            |> Seq.map (fun e ->
                match e.Value.Split("@") with
                | [| validatorAddress; networkAddress |] -> validatorAddress, networkAddress
                | _ -> failwith "Invalid GenesisValidators configuration"
            )
            |> Seq.toList

    static member GenesisSignatures
        with get () =
            genesis.GetSection("GenesisSignatures").GetChildren()
            |> Seq.map (fun e -> e.Value)
            |> Seq.toList
