open System.Globalization
open System.Threading
open Own.Common.FSharp
open Own.Blockchain.Common
open Own.Blockchain.Public.Faucet
open MessagePack.Resolvers
open MessagePack.FSharp

[<EntryPoint>]
let main argv =
    printfn "Own Public Blockchain Faucet"

    try
        Thread.CurrentThread.CurrentCulture <- CultureInfo.InvariantCulture
        Thread.CurrentThread.CurrentUICulture <- CultureInfo.InvariantCulture

        CompositeResolver.RegisterAndSetAsDefault(
            FSharpResolver.Instance,
            StandardResolver.Instance
        )

        PaceMaker.start ()
        Api.start ()
    with
    | ex -> Log.error ex.AllMessagesAndStackTraces

    Log.stopLogging ()

    0 // Exit code
