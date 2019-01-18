open System.Globalization
open System.Threading
open Own.Common
open Own.Blockchain.Common
open Own.Blockchain.Public.Faucet

[<EntryPoint>]
let main argv =
    printfn "Own Public Blockchain Faucet"

    try
        Thread.CurrentThread.CurrentCulture <- CultureInfo.InvariantCulture
        Thread.CurrentThread.CurrentUICulture <- CultureInfo.InvariantCulture

        PaceMaker.start ()
        Api.start ()
    with
    | ex -> Log.error ex.AllMessagesAndStackTraces

    Log.stopLogging ()

    0 // Exit code
