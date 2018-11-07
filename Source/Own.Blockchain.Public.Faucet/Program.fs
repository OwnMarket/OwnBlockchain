open System
open System.Globalization
open System.Threading
open Chainium.Common
open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Faucet

[<EntryPoint>]
let main argv =
    printfn "Chainium Public Blockchain Faucet"

    try
        Thread.CurrentThread.CurrentCulture <- CultureInfo.InvariantCulture
        Thread.CurrentThread.CurrentUICulture <- CultureInfo.InvariantCulture

        PaceMaker.start ()
        Api.start ()
    with
    | ex -> Log.error ex.AllMessagesAndStackTraces

    0 // Exit code
