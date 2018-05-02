namespace Chainium.Blockchain.Common

open System
open Chainium.Common

[<AutoOpen>]
module Framework =

    module Result =

        let plus addOk addError f1 f2 x =
            match (f1 x), (f2 x) with
            | Ok v1, Ok v2 -> Ok (addOk v1 v2)
            | Error e1, Ok _  -> Error e1
            | Ok _ , Error e2 -> Error e2
            | Error e1, Error e2 -> Error (addError e1 e2)

    let (>>=) r f = Result.bind f r

    let (&&&) f1 f2 x = Result.plus (fun _ v2 -> v2) (@) f1 f2 x

    let (&&&!) f1 f2 x = Result.plus (@) (@) f1 f2 x
