namespace Own.Blockchain.Public.Core

open System
open Own.Common.FSharp
open Own.Blockchain.Public.Core.DomainTypes

module Forks =

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Fork types - each fork has its own record type containing all the properties related to it.
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    type DormantValidatorsFork = {
        BlockNumber : BlockNumber
    }

type Forks () =

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Forks
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    static let mutable _dormantValidatorsFork : Forks.DormantValidatorsFork option = None

    // Initialize all forks
    static member Init networkCode =

        _dormantValidatorsFork <-
            match networkCode with
            | "OWN_PUBLIC_BLOCKCHAIN_MAINNET" ->
                {
                    Forks.DormantValidatorsFork.BlockNumber = BlockNumber Int64.MaxValue // TODO FORK: Set for release
                }
                |> Some
            | "OWN_PUBLIC_BLOCKCHAIN_TESTNET" ->
                {
                    Forks.DormantValidatorsFork.BlockNumber = BlockNumber 1_000_000L
                }
                |> Some
            | "OWN_PUBLIC_BLOCKCHAIN_DEVNET" ->
                {
                    Forks.DormantValidatorsFork.BlockNumber = BlockNumber 1L
                }
                |> Some
            | "UNIT_TESTS" ->
                {
                    Forks.DormantValidatorsFork.BlockNumber = BlockNumber 6L
                }
                |> Some
            | _ ->
                {
                    Forks.DormantValidatorsFork.BlockNumber = BlockNumber 1L
                }
                |> Some

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Accessors
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    static member DormantValidators
        with get () =
            _dormantValidatorsFork |?> fun _ -> failwith "DormantValidatorsFork not initialized"
