namespace Own.Blockchain.Public.Node

open System
open System.Globalization
open System.IO
open System.Reflection
open System.Text.RegularExpressions
open Microsoft.Extensions.Configuration
open Own.Common.FSharp
open Own.Blockchain.Common
open Own.Blockchain.Public.Core.DomainTypes

type Config () =

    static let appDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)

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
    static member VersionNumber
        with get () =
            let assembly = Assembly.GetExecutingAssembly()
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion

    static member VersionHash
        with get () =
            Path.Combine(appDir, "Version")
            |> Some
            |> Option.filter File.Exists
            |> Option.bind (File.ReadLines >> Seq.tryHead)
            |> Option.map (fun s -> s.Trim())
            |> Option.filter (String.IsNullOrWhiteSpace >> not)
            |> Option.filter (fun s -> Regex.IsMatch(s, "^[a-z0-9]+$"))
            |? "UNKNOWN"

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
    // Content
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    static member WalletFrontendFile
        with get () =
            let value = config.["WalletFrontendFile"]
            if value.IsNullOrWhiteSpace() then
                Path.Combine(appDir, "Wallet/index.html")
            else
                value

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

    static member DnsResolverCacheExpirationTime // Seconds
        with get () =
            match Int32.TryParse config.["DnsResolverCacheExpirationTime"] with
            | true, timeout when timeout > 0 -> timeout
            | _ -> 600

    static member MaxConnectedPeers
        with get () =
            match Int32.TryParse config.["MaxConnectedPeers"] with
            | true, value when value > 0 -> value
            | _ -> 200 // Default value if not explicitly configured.

    static member GossipFanoutPercentage // Percentage of validator count
        with get () =
            match Int32.TryParse config.["GossipFanoutPercentage"] with
            | true, value when value >= 4 && value <= 100 -> value
            | _ -> 15

    static member GossipDiscoveryInterval // Milliseconds
        with get () =
            match Int32.TryParse config.["GossipDiscoveryInterval"] with
            | true, interval when interval > 0 -> interval
            | _ -> 10000

    static member GossipInterval // Milliseconds
        with get () =
            match Int32.TryParse config.["GossipInterval"] with
            | true, interval when interval > 0 -> interval
            | _ -> 2000

    static member GossipMaxMissedHeartbeats
        with get () =
            match Int32.TryParse config.["GossipMaxMissedHeartbeats"] with
            | true, cycles when cycles > 0 -> cycles
            | _ -> 100

    static member PeerResponseThrottlingTime // Milliseconds
        with get () =
            match Int32.TryParse config.["PeerResponseThrottlingTime"] with
            | true, value when value >= 0 -> value
            | _ -> 3000

    static member NetworkSendoutRetryTimeout // Milliseconds
        with get () =
            match Int32.TryParse config.["NetworkSendoutRetryTimeout"] with
            | true, timeout when timeout >= 0 -> timeout
            | _ -> 20

    static member PeerMessageMaxSize // Bytes
        with get () =
            match Int32.TryParse config.["PeerMessageMaxSize"] with
            | true, size when size >= 0 -> size
            | _ -> 1_000_000

    static member DeadPeerExpirationTime // Hours
        with get () =
            match Int32.TryParse config.["DeadPeerExpirationTime"] with
            | true, time when time >= 0 -> time
            | _ -> 24

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Synchronization
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    static member NetworkTimePollInterval = 60 // Seconds

    static member BlockchainHeadPollInterval = 60 // Seconds

    static member MaxBlockFetchQueue
        with get () =
            match Int32.TryParse config.["MaxBlockFetchQueue"] with
            | true, value when value > 0 -> value
            | _ -> 50

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Consensus
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    static member MinValidatorCount = 4
    static member MaxValidatorCount = 100

    static member MaxRewardedStakesCount = 100

    static member ValidatorThreshold = 500_000m
    static member ValidatorDeposit = 10_000m

    static member ConsensusMessageRetryingInterval // Milliseconds
        with get () =
            match Int32.TryParse config.["ConsensusMessageRetryingInterval"] with
            | true, value when value > 0 -> value
            | _ -> 1000
    static member ConsensusProposeRetryingInterval // Milliseconds
        with get () =
            match Int32.TryParse config.["ConsensusProposeRetryingInterval"] with
            | true, value when value > 0 -> value
            | _ -> 1000

    static member ConsensusTimeoutPropose = 5000 // Milliseconds
    static member ConsensusTimeoutVote = 5000 // Milliseconds
    static member ConsensusTimeoutCommit = 5000 // Milliseconds
    static member ConsensusTimeoutDelta = 1000 // Milliseconds

    static member StaleConsensusDetectionInterval = 10000 // Milliseconds

    static member ConsensusCacheCleanupInterval = 60 // Seconds

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

    static member MaxValidatorDormantTime = 168 // Hours

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // TXs and Blocks
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    static member CreateEmptyBlocks = true
    static member MinEmptyBlockTime = 30 // Seconds

    static member MaxActionCountPerTx = 1000

    static member MaxTxSetFetchIterations
        with get () =
            match Int32.TryParse config.["MaxTxSetFetchIterations"] with
            | true, value when value > 0 -> value
            | _ -> 10

    static member TxCacheExpirationTime // Seconds
        with get () =
            match Int32.TryParse config.["TxCacheExpirationTime"] with
            | true, timeout when timeout > 0 -> timeout
            | _ -> 30

    static member MaxTxCacheSize
        with get () =
            match Int32.TryParse config.["MaxTxCacheSize"] with
            | true, cacheSize when cacheSize > 0 -> cacheSize
            | _ -> 10000

    static member BlockCacheExpirationTime // Seconds
        with get () =
            match Int32.TryParse config.["BlockCacheExpirationTime"] with
            | true, timeout when timeout > 0 -> timeout
            | _ -> 10

    static member MaxBlockCacheSize
        with get () =
            match Int32.TryParse config.["MaxBlockCacheSize"] with
            | true, cacheSize when cacheSize > 0 -> cacheSize
            | _ -> 20

    static member TxRepropagationCount
        with get () =
            match Int32.TryParse config.["TxRepropagationCount"] with
            | true, value when value > 0 -> value
            | _ -> 10

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
