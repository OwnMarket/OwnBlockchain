namespace Own.Blockchain.Common

open System

module Log =

    // TODO: Implement logging using an instance of a MailboxProcessor, to avoid corrupted output when multi-threading.

    let private log logType o =
        sprintf "%s %s | %s" (DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz")) logType (o.ToString())

    let private defaultColor = Console.ForegroundColor
    let private printInColor color text =
        Console.ForegroundColor <- color
        printfn "%s" text
        Console.ForegroundColor <- defaultColor

    /// Errors which prevented successful execution.
    let error o =
        log "ERR" o |> printInColor ConsoleColor.Red

    /// Events that are potentially problematic but didn't prevent the successful execution.
    /// (e.g. not being able to propose block due to not having latest block applied to the state yet)
    let warning o =
        log "WRN" o |> printInColor ConsoleColor.Yellow

    /// Important successful events.
    /// (e.g. block applied to the state)
    let success o =
        log "SUC" o |> printInColor ConsoleColor.Green

    /// Important unordinary events.
    /// (e.g. applying DB change; saving TxResult to the disk during processing)
    let notice o =
        log "NOT" o |> printInColor ConsoleColor.Cyan

    /// Ordinary events.
    /// (e.g. Tx submitted; block received)
    let info o =
        log "INF" o |> printInColor ConsoleColor.White

    /// Detailed info for debugging purpose.
    let debug o =
        #if DEBUG
        log "DBG" o |> printInColor ConsoleColor.DarkGray
        #else
        ()
        #endif

    let errorf format = Printf.ksprintf error format
    let warningf format = Printf.ksprintf warning format
    let successf format = Printf.ksprintf success format
    let noticef format = Printf.ksprintf notice format
    let infof format = Printf.ksprintf info format
    let debugf format = Printf.ksprintf debug format

    let appError (AppError message) = error message
    let appErrors errors =
        for e in errors do
            appError e
