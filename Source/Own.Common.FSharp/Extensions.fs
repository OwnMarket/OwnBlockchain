namespace Own.Common.FSharp

open System
open System.Net
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

    type IPAddress with
        member this.IsPrivate() =
            // IPv6 Loopback.
            if this.ToString() = "::1" then
                true
            else
                let ip = this.GetAddressBytes()
                match ip.[0] with
                | 10uy | 127uy -> true
                | 172uy -> ip.[1] >= 16uy && ip.[1] < 32uy
                | 192uy -> ip.[1] = 168uy
                | _ -> false

module Seq =

    let inline shuffleWithRandom (r : Random) xs =
        xs |> Seq.sortBy (fun _ -> r.Next())

    let inline shuffle xs =
        xs |> Seq.sortBy (fun _ -> Guid.NewGuid())

    let shuffleCrypto xs =
        let a = xs |> Seq.toArray

        use rng = new RNGCryptoServiceProvider ()
        let bytes = Array.zeroCreate a.Length
        rng.GetBytes bytes

        Array.zip bytes a |> Array.sortBy fst |> Array.map snd |> Array.toSeq

    let inline ofDict dictionary =
        dictionary
        |> Seq.map (|KeyValue|)

module List =

    let inline ofDict dictionary =
        dictionary
        |> Seq.ofDict
        |> List.ofSeq

    let inline shuffle xs =
        xs |> List.sortBy (fun _ -> Guid.NewGuid())

module Map =

    let inline ofDict dictionary =
        dictionary
        |> Seq.ofDict
        |> Map.ofSeq

    let inline keys (map : Map<'Key, 'Value>) =
        Map.fold (fun keys key _ -> key :: keys) [] map

    /// Produces a new Map by maping both key and value.
    let remap mapper =
        Map.toSeq >> Seq.map mapper >> Map.ofSeq

module Array =

    module AsyncParallel =

        let map f xs =
            xs
            |> Seq.map (fun x ->
                async {
                    return f x
                }
            )
            |> Async.Parallel
            |> Async.RunSynchronously

        let filter f xs =
            xs
            |> Seq.map (fun x ->
                async {
                    return x, f x
                }
            )
            |> Async.Parallel
            |> Async.RunSynchronously
            |> Array.filter snd
            |> Array.map fst

        let forall f xs =
            xs
            |> map f
            |> Array.forall id
