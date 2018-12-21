namespace Own.Blockchain.Public.Faucet

open System
open System.IO
open System.Reflection
open Microsoft.Extensions.Configuration

type Config () =

    static let appDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)

    static let config =
        ConfigurationBuilder()
            .SetBasePath(appDir)
            .AddJsonFile("Config.json")
            .Build()

    static member NodeApiUrl
        with get () =
            config.["NodeApiUrl"]

    static member FaucetSupplyHolderPrivateKey
        with get () =
            config.["FaucetSupplyHolderPrivateKey"]

    static member FaucetSupplyHolderAddress
        with get () =
            config.["FaucetSupplyHolderAddress"]

    static member FaucetSupplyHolderAccountHash
        with get () =
            config.["FaucetSupplyHolderAccountHash"]

    static member FaucetSupplyAssetHash
        with get () =
            config.["FaucetSupplyAssetHash"]

    static member TxFee
        with get () =
            config.["TxFee"] |> Decimal.Parse

    static member MaxClaimableChxAmount
        with get () =
            config.["MaxClaimableChxAmount"] |> Decimal.Parse

    static member MaxClaimableAssetAmount
        with get () =
            config.["MaxClaimableAssetAmount"] |> Decimal.Parse

    static member DistributionBatchSize
        with get () =
            config.["DistributionBatchSize"] |> Int16.Parse

    static member DistributionInterval // Seconds
        with get () =
            config.["DistributionInterval"] |> Int16.Parse
