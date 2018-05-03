namespace Chainium.Blockchain.Public.Crypto.Tests

open System
open Xunit
open Swensen.Unquote
open Chainium.Common
open Chainium.Blockchain.Public.Crypto

module HashingTests =

    [<Fact>]
    let ``Hashing.hash calculates same hash when executed multiple times for same input`` () =
        let message = "Chainium"

        let hashes =
            [1 .. 10000]
            |> List.map (fun _ -> Hashing.hash message)
            |> List.distinct

        test <@ hashes.Length = 1 @>

    [<Fact>]
    let ``Hashing.hash calculates different hash for different input`` () =
        let message = "Chainium"
        let hashCount = 10000

        let allHashes =
            [1 .. hashCount]
            |> List.map (fun i -> Hashing.hash (sprintf "%s %i" message i))

        let distinctHashes =
            allHashes
            |> List.distinct

        test <@ allHashes.Length = hashCount @>
        test <@ distinctHashes.Length = allHashes.Length @>
