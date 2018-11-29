namespace Own.Blockchain.Common

open System

module Log =

    // TODO: Implement logging using an instance of a MailboxProcessor, to avoid corrupted output when multi-threading.

    let private log logType o =
        sprintf "%s |%s| %s" (DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz")) logType (o.ToString())

    let info o = log "I" o |> printfn "%s"
    let warning o = log "W" o |> printfn "%s"
    let error o = log "E" o |> printfn "%s"
    let debug o =
        #if DEBUG
            log "D" o |> printfn "%s"
        #else
            ()
        #endif

    let infof format = Printf.ksprintf info format
    let warningf format = Printf.ksprintf warning format
    let errorf format = Printf.ksprintf error format
    let debugf format = Printf.ksprintf debug format

    let appError (AppError message) = error message
    let appErrors errors =
        for e in errors do
            appError e
