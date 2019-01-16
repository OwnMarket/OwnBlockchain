namespace Own.Blockchain.Public.Core

open System
open Own.Common
open Own.Blockchain.Public.Core.DomainTypes

module Validators =

    /// 2f + 1
    let calculateQualifiedMajority validatorCount =
        decimal validatorCount / 3m * 2m
        |> Math.Floor
        |> Convert.ToInt32
        |> (+) 1

    /// f + 1
    let calculateValidQuorum validatorCount =
        decimal validatorCount / 3m
        |> Math.Floor
        |> Convert.ToInt32
        |> (+) 1

    let getTopValidators
        getTopValidatorsByStake
        validatorThreshold
        =

        getTopValidatorsByStake validatorThreshold
        |> List.map Mapping.validatorSnapshotFromDto

    let getValidatorsAtHeight getBlock blockNumber =
        let configBlock =
            getBlock blockNumber
            >>= Blocks.extractBlockFromEnvelopeDto
            >>= (fun b ->
                if b.Configuration.IsSome then
                    Ok b // This block is the configuration block
                else
                    getBlock b.Header.ConfigurationBlockNumber
                    >>= Blocks.extractBlockFromEnvelopeDto
            )

        match configBlock with
        | Error e -> failwithf "Cannot get configuration block at height %i." blockNumber.Value
        | Ok block ->
            match block.Configuration with
            | None -> failwithf "Cannot find configuration in configuration block %i." block.Header.Number.Value
            | Some config ->
                match config.Validators with
                | [] -> failwithf "Cannot find validators in configuration block %i." block.Header.Number.Value
                | validators -> validators

    let getCurrentValidators getLastAppliedBlockNumber getBlock =
        getLastAppliedBlockNumber ()
        |> getValidatorsAtHeight getBlock

    let isValidator
        (getValidators : unit -> ValidatorSnapshot list)
        validatorAddress
        =

        getValidators ()
        |> List.exists (fun v -> v.ValidatorAddress = validatorAddress)

    let getProposer
        (BlockNumber blockNumber)
        (ConsensusRound consensusRound)
        (validators : ValidatorSnapshot list)
        =

        let validatorIndex = (blockNumber + int64 consensusRound) % (int64 validators.Length) |> Convert.ToInt32
        validators
        |> List.sortBy (fun v -> v.ValidatorAddress)
        |> List.item validatorIndex

    let getProposerAddress
        blockNumber
        consensusRound
        validators
        =

        (getProposer blockNumber consensusRound validators).ValidatorAddress
