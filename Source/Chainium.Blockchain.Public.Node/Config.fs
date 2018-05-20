namespace Chainium.Blockchain.Public.Node

open System
open System.IO
open System.Reflection
open Microsoft.Extensions.Configuration

type Config () =

    static let appDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)

    static let config =
        (
            ConfigurationBuilder()
                .SetBasePath(appDir)
                .AddJsonFile("AppSettings.json")
        ).Build()

    static member DataDir
        with get () =
            Path.Combine(appDir, "Data")

    static member DbConnectionString
        with get () =
            config.["DbConnectionString"]

    static member BlockCreationInterval = 5 // Seconds

    static member MaxTxCountPerBlock = 100 // TODO: Shall this be part of the consensus protocol?

    static member ValidatorAddress
        with get () =
            config.["ValidatorAddress"]
