namespace Own.Common.FSharp

open System

[<AutoOpen>]
module Operators =

    let (|?) = defaultArg

    let (|?>) opt f =
        match opt with
        | Some x -> x
        | None -> f ()

    let (|??) (nullable : Nullable<_>) defaultValue =
        if nullable.HasValue then
            nullable.Value
        else
            defaultValue

    let (|???) x defaultValue =
        if isNull (box x) then
            defaultValue
        else
            x
