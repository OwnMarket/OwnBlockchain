namespace Own.Blockchain.Common

open System
open System.Threading
open Own.Blockchain.Common

module Log =

    type LogLevel =
        | Verbose = 0
        | Debug = 1
        | Info = 2
        | Success = 3
        | Notice = 4
        | Warning = 5
        | Error = 6

    let mutable minLogLevel = LogLevel.Info // Default value

    let private defaultColor = Console.ForegroundColor

    let private cts = new CancellationTokenSource()

    let private logger =
        MailboxProcessor.Start(
            (fun inbox ->
                let rec messageLoop () =
                    async {
                        let! color, message = inbox.Receive()
                        Console.ForegroundColor <- color
                        printfn "%s" message
                        Console.ForegroundColor <- defaultColor
                        return! messageLoop ()
                    }
                messageLoop ()
            ),
            cts.Token
        )

    let stopLogging () =
        Thread.Sleep 500 // Give some time to logger to write the messages.
        cts.Cancel()

    let private printInColor color text =
        logger.Post (color, text)

    let private log logType o =
        sprintf "%s %s | %s" (DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff")) logType (o.ToString())

    /// Verbose info for debugging purpose.
    let verbose o =
        if minLogLevel <= LogLevel.Verbose then
            log "VRB" o |> printInColor ConsoleColor.DarkGray

    /// Detailed info for debugging purpose.
    let debug o =
        if minLogLevel <= LogLevel.Debug then
            log "DBG" o |> printInColor ConsoleColor.DarkGray

    /// Ordinary events.
    /// (e.g. Tx submitted; block received)
    let info o =
        if minLogLevel <= LogLevel.Info then
            log "INF" o |> printInColor ConsoleColor.White

    /// Important successful events.
    /// (e.g. block applied to the state)
    let success o =
        if minLogLevel <= LogLevel.Success then
            log "SUC" o |> printInColor ConsoleColor.Green

    /// Important unordinary events.
    /// (e.g. applying DB change; saving TxResult to the disk during processing)
    let notice o =
        if minLogLevel <= LogLevel.Notice then
            log "NOT" o |> printInColor ConsoleColor.Cyan

    /// Events that are potentially problematic but didn't prevent the successful execution.
    /// (e.g. not being able to propose block due to not having latest block applied to the state yet)
    let warning o =
        if minLogLevel <= LogLevel.Warning then
            log "WRN" o |> printInColor ConsoleColor.Yellow

    /// Errors which prevented successful execution.
    let error o =
        if minLogLevel <= LogLevel.Error then
            log "ERR" o |> printInColor ConsoleColor.Red

    let verbosef format = Printf.ksprintf verbose format
    let debugf format = Printf.ksprintf debug format
    let infof format = Printf.ksprintf info format
    let successf format = Printf.ksprintf success format
    let noticef format = Printf.ksprintf notice format
    let warningf format = Printf.ksprintf warning format
    let errorf format = Printf.ksprintf error format

    let appError (AppError message) = error message
    let appErrors errors =
        for e in errors do
            appError e
