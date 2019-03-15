namespace Own.Blockchain.Public.Core

open System
open Own.Common.FSharp
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
        maxValidatorCount
        validatorThreshold
        validatorDeposit
        =

        getTopValidatorsByStake maxValidatorCount validatorThreshold validatorDeposit
        |> List.map Mapping.validatorSnapshotFromDto

    let getValidatorsAtHeight getBlock blockNumber =
        let configBlockNumber, config = Blocks.getConfigurationAtHeight getBlock blockNumber
        match config.Validators with
        | [] -> failwithf "Cannot find validators in configuration block %i" configBlockNumber.Value
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
        (validators : BlockchainAddress list)
        =

        if validators.IsEmpty then
            failwith "No validators to choose the proposer from"
        let validatorIndex = (blockNumber + int64 consensusRound) % (int64 validators.Length) |> Convert.ToInt32
        validators
        |> List.sort
        |> List.item validatorIndex
