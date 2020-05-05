namespace Own.Blockchain.Public.Core

open System
open Own.Common.FSharp
open Own.Blockchain.Public.Core.DomainTypes

module Forks =

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Fork types - each fork has its own record type containing all the properties related to it.
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    type DormantValidatorsFork = {
        /// Block number at which the dormant validator logic is activated.
        BlockNumber : BlockNumber

        /// Block number at which tracking of last proposed block, in the blockchain state, starts.
        TrackingStartBlockNumber : BlockNumber
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
                    Forks.DormantValidatorsFork.BlockNumber = BlockNumber 1_330_000L
                    Forks.DormantValidatorsFork.TrackingStartBlockNumber = BlockNumber 1_300_000L
                }
                |> Some
            | "OWN_PUBLIC_BLOCKCHAIN_TESTNET" ->
                {
                    Forks.DormantValidatorsFork.BlockNumber = BlockNumber 1_000_000L
                    Forks.DormantValidatorsFork.TrackingStartBlockNumber = BlockNumber 993_000L
                }
                |> Some
            | "OWN_PUBLIC_BLOCKCHAIN_DEVNET" ->
                {
                    Forks.DormantValidatorsFork.BlockNumber = BlockNumber 1L
                    Forks.DormantValidatorsFork.TrackingStartBlockNumber = BlockNumber 1L
                }
                |> Some
            | "UNIT_TESTS" ->
                {
                    Forks.DormantValidatorsFork.BlockNumber = BlockNumber 6L
                    Forks.DormantValidatorsFork.TrackingStartBlockNumber = BlockNumber 1L
                }
                |> Some
            | _ ->
                {
                    Forks.DormantValidatorsFork.BlockNumber = BlockNumber 1L
                    Forks.DormantValidatorsFork.TrackingStartBlockNumber = BlockNumber 1L
                }
                |> Some

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Accessors
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    static member DormantValidators
        with get () =
            _dormantValidatorsFork |?> fun _ -> failwith "DormantValidatorsFork not initialized"
