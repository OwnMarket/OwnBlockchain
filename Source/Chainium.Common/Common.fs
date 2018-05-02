namespace Chainium.Common

open System
open System.Collections.Concurrent

[<AutoOpen>]
module Common =

    let notNull x = not (isNull x)

    let flip f x y = f y x

    let tee f x = f x; x

    let memoize (f : 'TIn -> 'TOut) =
        let cache = ConcurrentDictionary<'TIn, 'TOut>()
        fun x -> cache.GetOrAdd(x, f)
