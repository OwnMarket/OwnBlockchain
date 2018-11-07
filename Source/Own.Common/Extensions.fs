namespace Own.Common

open System
open System.Security.Cryptography

[<AutoOpen>]
module Extensions =

    type String with
        member this.IsNullOrEmpty() = String.IsNullOrEmpty(this)
        member this.IsNullOrWhiteSpace() = String.IsNullOrWhiteSpace(this)

    type Exception with

        member this.Flatten() =
            seq {
                yield this
                if notNull this.InnerException then
                    yield! this.InnerException.Flatten()
            }

        member this.AllMessages
            with get () =
                this.Flatten()
                |> Seq.map (fun ex -> sprintf "%s: %s" (ex.GetType().FullName) ex.Message)
                |> (fun messages -> String.Join(" >>> ", messages))

        member this.AllMessagesAndStackTraces
            with get () =
                this.Flatten()
                |> Seq.map (fun ex -> ex.StackTrace)
                |> (fun stackTraces -> String.Join("\n--- Inner Exception ---\n", stackTraces))
                |> sprintf "%s\n%s" this.AllMessages

module Map =

    let inline ofDict dictionary =
        dictionary
        |> Seq.map (|KeyValue|)
        |> Map.ofSeq

module Seq =

    let inline shuffleR (r : Random) xs =
        xs |> Seq.sortBy (fun _ -> r.Next())

    let inline shuffleG xs =
        xs |> Seq.sortBy (fun _ -> Guid.NewGuid())

    let shuffleCrypto xs =
        let a = xs |> Seq.toArray

        use rng = new RNGCryptoServiceProvider ()
        let bytes = Array.zeroCreate a.Length
        rng.GetBytes bytes

        Array.zip bytes a |> Array.sortBy fst |> Array.map snd
