namespace Own.Common.FSharp

open System
open System.Collections.Concurrent
open System.Threading
open Microsoft.FSharp.Reflection

[<AutoOpen>]
module Common =

    let notNull x = not (isNull x)

    let between l r x =
        l <= x && x <= r

    let betweenExcluding l r x =
        l < x && x < r

    let flip f x y = f y x

    let tap f x = f x; x

    let memoize (f : 'TIn -> 'TOut) =
        let cache = ConcurrentDictionary<'TIn, 'TOut>()
        fun x -> cache.GetOrAdd(x, f)

    let memoizePerThread (f : 'TIn -> 'TOut) =
        let cache = ConcurrentDictionary<int * 'TIn, 'TOut>()
        fun x -> cache.GetOrAdd((Thread.CurrentThread.ManagedThreadId, x), fun _ -> f x)

    /// Memoize by key created from the input value to avoid storing large keys.
    let memoizeBy (keyPredicate : 'TIn -> 'TKey) (f : 'TIn -> 'TOut) =
        let cache = ConcurrentDictionary<'TKey, 'TOut>()
        fun x -> cache.GetOrAdd(keyPredicate x, fun _ -> f x)

    /// Memoize the value once the calculated output satisfies the condition.
    let memoizeWhen (condition : 'TOut -> bool) (f : 'TIn -> 'TOut) =
        let cache = ConcurrentDictionary<'TIn, 'TOut>()
        fun x ->
            match cache.TryGetValue(x) with
            | true, v -> v
            | _ ->
                let v = f x
                if condition v then
                    cache.GetOrAdd(x, v)
                else
                    v

    /// Repeats execution of "f", in the case of exception, as long as "condition" is true.
    let retryWhile condition f =
        let rec retryIfFails iteration =
            try
                f iteration
            with
            | _ ->
                if condition iteration then
                    retryIfFails (iteration + 1)
                else
                    reraise ()

        retryIfFails 1

    /// Repeats execution of "f", in the case of exception, at most "timesToTry" times.
    let retry timesToTry f =
        if timesToTry < 1 then
            raise (new ArgumentOutOfRangeException("timesToTry", timesToTry, "Value must be greater than zero"))

        retryWhile (fun iteration -> iteration < timesToTry) f

    let unionCaseName (x : 'T) =
        match FSharpValue.GetUnionFields(x, typeof<'T>) with
        | case, _ -> case.Name
