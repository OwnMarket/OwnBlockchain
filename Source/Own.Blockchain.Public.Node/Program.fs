open System.Globalization
open System.Threading
open MessagePack.Resolvers
open MessagePack.FSharp
open Own.Common
open Own.Blockchain.Common
open Own.Blockchain.Public.Node

[<EntryPoint>]
let main argv =
    printfn "Own Public Blockchain Node"

    try
        Thread.CurrentThread.CurrentCulture <- CultureInfo.InvariantCulture
        Thread.CurrentThread.CurrentUICulture <- CultureInfo.InvariantCulture

        CompositeResolver.RegisterAndSetAsDefault(
            FSharpResolver.Instance,
            StandardResolver.Instance
        )

        argv |> Array.toList |> Cli.handleCommand
    with
    | ex -> Log.error ex.AllMessagesAndStackTraces

    Thread.Sleep 500 // Give some time to logger to write the messages.
    Log.stopLogging ()

    0 // Exit code
