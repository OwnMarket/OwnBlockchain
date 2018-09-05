namespace Chainium.Common

open System

module Result =

    let iter f = function
        | Ok v -> f v
        | Error _ -> ()

    let iterError f = function
        | Ok _ -> ()
        | Error e -> f e

[<AutoOpen>]
module ResultOperators =

    let (>>=) r f = Result.bind f r

[<AutoOpen>]
module ResultComputationExpression =

    // Source: https://github.com/swlaschin/DomainModelingMadeFunctional/blob/master/src/OrderTaking/Result.fs
    type ResultBuilder() =
        member __.Return(x) = Ok x
        member __.Bind(x, f) = Result.bind f x

        member __.ReturnFrom(x) = x
        member __.Zero() = __.Return()

        member __.Delay(f) = f
        member __.Run(f) = f ()

        member __.While(guard, body) =
            if not (guard ()) then
                __.Zero()
            else
                __.Bind(body (), fun () -> __.While(guard, body))

        member __.TryWith(body, handler) =
            try
                __.ReturnFrom(body ())
            with e ->
                handler e

        member __.TryFinally(body, compensation) =
            try
                __.ReturnFrom(body())
            finally
                compensation()

        member __.Using(disposable : #System.IDisposable, body) =
            let body' = fun () -> body disposable
            __.TryFinally(body', fun () ->
                match disposable with
                    | null -> ()
                    | disp -> disp.Dispose()
            )

        member __.For(sequence : seq<_>, body) =
            __.Using(sequence.GetEnumerator(), fun enum ->
                __.While(enum.MoveNext,
                    __.Delay(fun () -> body enum.Current)
                )
            )

        member __.Combine (a, b) =
            __.Bind(a, fun () -> b ())

    let result = ResultBuilder()
