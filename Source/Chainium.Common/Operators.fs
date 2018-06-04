namespace Chainium.Common

open System

[<AutoOpen>]
module Operators =

    let (|?) = defaultArg

    let (|?>) opt f =
        match opt with
        | Some x -> x
        | None -> f ()
