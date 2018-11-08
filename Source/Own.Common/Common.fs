namespace Own.Common

open System
open System.Collections.Concurrent
open Microsoft.FSharp.Reflection

[<AutoOpen>]
module Common =

    let notNull x = not (isNull x)

    let flip f x y = f y x

    let tee f x = f x; x

    let memoize (f : 'TIn -> 'TOut) =
        let cache = ConcurrentDictionary<'TIn, 'TOut>()
        fun x -> cache.GetOrAdd(x, f)

    let retry timesToRetry f =
        if timesToRetry < 1 then
            raise (new ArgumentOutOfRangeException("timesToRetry", timesToRetry, "Value must be greater than zero."))

        let rec retryIfFails n =
            try
                f ()
            with
            | _ ->
                if n > 0 then
                    retryIfFails (n - 1)
                else
                    reraise ()

        retryIfFails timesToRetry

    let unionCaseName (x : 'T) =
        match FSharpValue.GetUnionFields(x, typeof<'T>) with
        | case, _ -> case.Name
