namespace Chainium.Blockchain.Public.Node

open System
open System.IO
open System.Reflection

type Config () =

    // TODO: Implement with https://www.demystifyfp.com/FsConfig

    static member DataDir
        with get () =
            let baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            Path.Combine(baseDir, "Data")

    static member DbConnectionString
        with get () =
            ""
            // ConfigurationManager.ConnectionStrings.["DB"].ConnectionString

    static member MaxTxCountPerBlock = 100 // TODO: Shall this be part of the consensus protocol?

    static member ValidatorAddress
        with get () =
            ""
            // ConfigurationManager.AppSettings.["ValidatorAddress"]
