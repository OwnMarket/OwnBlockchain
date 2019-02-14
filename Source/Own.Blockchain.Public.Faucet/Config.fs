namespace Own.Blockchain.Public.Faucet

open System
open System.IO
open System.Reflection
open System.Text.RegularExpressions
open Microsoft.Extensions.Configuration
open Own.Common

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

    static member NetworkCode
        with get () =
            let networkCode = config.["NetworkCode"]
            if networkCode.IsNullOrWhiteSpace() then
                "OWN_PUBLIC_BLOCKCHAIN_DEVNET"
            elif Regex.IsMatch(networkCode, "^[A-Z_0-9]+$") then
                networkCode
            else
                failwithf "Invalid NetworkCode: %s" networkCode

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
