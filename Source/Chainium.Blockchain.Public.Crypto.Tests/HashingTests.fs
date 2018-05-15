namespace Chainium.Blockchain.Public.Crypto.Tests

open System
open Xunit
open Swensen.Unquote
open Multiformats.Base
open Chainium.Common
open Chainium.Blockchain.Public.Crypto

module HashingTests =
    open System.Text
    open Chainium.Blockchain.Public.Core.DomainTypes

    let getBytes (str : String) = System.Text.Encoding.UTF8.GetBytes(str)

    [<Fact>]
    let ``Hashing.hash calculates same hash when executed multiple times for same input`` () =
        let message = getBytes "Chainium"

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
            |> List.map (fun i ->  (sprintf "%s %i" message i) |> getBytes |> Hashing.hash)

        let distinctHashes =
            allHashes
            |> List.distinct

        test <@ allHashes.Length = hashCount @>
        test <@ distinctHashes.Length = allHashes.Length @>

    [<Fact>]
    let ``Hashing.addressHash calculates same hash not longer than 20 bytes for same input`` () =
        let message = getBytes "Chainium"
        let hashCount = 10000
        let hashes =
            [1 .. hashCount]
            |> List.map (fun _ -> Hashing.addressHash message |> Multibase.Base58.Decode)
            |> List.distinct
        test <@ hashes.Length = 1 @>

        let longerThan20Bytes =
            hashes
            |> List.where(fun a -> a.Length <> 20)
        test <@ longerThan20Bytes.Length = 0 @>

    [<Fact>]
    let ``Hashing.addressHash calculates different hash not longer than 20 bytes for different input`` () =
        let hashCount = 10000
        let hashes =
            [1 .. hashCount]
            |> List.map (sprintf "Chainium %i" >> getBytes >> Hashing.addressHash >> Multibase.Base58.Decode)
            |> List.distinct
        test <@ hashes.Length = hashCount @>

        let longerThan20Bytes =
            hashes
            |> List.where(fun a -> a.Length <> 20)
        test <@ longerThan20Bytes.Length = 0 @>

    [<Fact>]
    let ``Hashing.merkleTree check if same root has been calculated for multiple runs`` ()=
        let transactionMocks =
            [
                for i in 1 .. 100 do yield sprintf "%i" i
                                      |> Encoding.UTF8.GetBytes
                                      |> Multibase.Base58.Encode
            ]
        

        let roots =
            [
                for i in 1 .. 100 do yield Hashing.merkleTree transactionMocks
                                      |> fun (MerkleTreeRoot r) -> r
            ]
            |> List.distinct

        test <@ roots.Length = 1 @>


        let bytes = 
            roots.Head
            |> Multibase.Base58.Decode

        test <@ bytes.Length = 32 @>

        

