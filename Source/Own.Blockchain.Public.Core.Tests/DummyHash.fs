namespace Own.Blockchain.Public.Core.Tests

open System
open Own.Blockchain.Public.Core.DomainTypes

module DummyHash =

    let create bytes =
        bytes
        |> Array.map (fun b ->
            match b % 10uy with
            | 0uy -> '.'
            | 1uy -> 'A'
            | 2uy -> 'B'
            | 3uy -> 'C'
            | 4uy -> 'D'
            | 5uy -> 'E'
            | 6uy -> 'F'
            | 7uy -> 'G'
            | 8uy -> 'H'
            | 9uy -> 'I'
            | b -> failwithf "%i is invalid value. Dymmy hash supports only bytes with values 0-9." b
        )
        |> String

    let decode (str : string) =
        str.ToCharArray()
        |> Array.map (function
            | '.' -> 0uy
            | 'A' -> 1uy
            | 'B' -> 2uy
            | 'C' -> 3uy
            | 'D' -> 4uy
            | 'E' -> 5uy
            | 'F' -> 6uy
            | 'G' -> 7uy
            | 'H' -> 8uy
            | 'I' -> 9uy
            | c -> failwithf "%s has invalid char %A. Dymmy hash supports only characters A-I and a dot for zero" str c
        )

    let merkleTree (hashes : string list) =
        hashes |> String.Concat |> MerkleTreeRoot
