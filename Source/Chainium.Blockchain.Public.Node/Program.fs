open System
open System.Globalization
open System.Threading
open Chainium.Common
open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Node

[<EntryPoint>]
let main argv =
    printfn "Chainium Public Blockchain Node"

    try
        Thread.CurrentThread.CurrentCulture <- CultureInfo.InvariantCulture
        Thread.CurrentThread.CurrentUICulture <- CultureInfo.InvariantCulture

        argv |> Array.toList |> Cli.handleCommand
    with
    | ex -> Log.error ex.AllMessagesAndStackTraces

    0 // Exit code
