open System.Globalization
open System.Threading
open MessagePack.Resolvers
open MessagePack.FSharp
open Own.Common.FSharp
open Own.Blockchain.Common
open Own.Blockchain.Public.Node

[<EntryPoint>]
let main argv =
    printfn "\nOwn Public Blockchain Node %s\n" Config.VersionNumber

    try
        Thread.CurrentThread.CurrentCulture <- CultureInfo.InvariantCulture
        Thread.CurrentThread.CurrentUICulture <- CultureInfo.InvariantCulture

        Log.minLogLevel <- Config.MinLogLevel

        CompositeResolver.RegisterAndSetAsDefault(
            FSharpResolver.Instance,
            StandardResolver.Instance
        )

        argv |> Array.toList |> Cli.handleCommand
    with
    | ex -> Log.error ex.AllMessagesAndStackTraces

    Log.stopLogging ()

    0 // Exit code
