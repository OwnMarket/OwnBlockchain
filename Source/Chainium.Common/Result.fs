namespace Chainium.Common

open System

module Result =

    let combine combineOks combineErrors f1 f2 x =
        match (f1 x), (f2 x) with
        | Ok v1, Ok v2 -> Ok (combineOks v1 v2)
        | Error e1, Ok _  -> Error e1
        | Ok _ , Error e2 -> Error e2
        | Error e1, Error e2 -> Error (combineErrors e1 e2)

[<AutoOpen>]
module ResultOperators =

    let (>>=) r f = Result.bind f r

    let (&&&) f1 f2 x = Result.combine (fun _ v2 -> v2) (@) f1 f2 x

    let (&&&!) f1 f2 x = Result.combine (@) (@) f1 f2 x

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
