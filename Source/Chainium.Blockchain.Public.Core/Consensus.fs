namespace Chainium.Blockchain.Public.Core

open System
open Chainium.Common
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Core.Dtos

module Consensus =

    let calculateQuorumSupply quorumSupplyPercent (ChxAmount totalSupply) =
        Decimal.Round(totalSupply * quorumSupplyPercent / 100m, 18, MidpointRounding.AwayFromZero)
        |> ChxAmount

    let calculateValidatorThreshold maxValidatorCount (ChxAmount quorumSupply) =
        Decimal.Round(quorumSupply / (decimal maxValidatorCount), 18, MidpointRounding.AwayFromZero)
        |> ChxAmount

    let isValidator
        (getValidatorSnapshots : unit -> ValidatorSnapshot list)
        validatorAddress
        =

        getValidatorSnapshots ()
        |> List.exists (fun v -> v.ValidatorAddress = validatorAddress)

    let getBlockProposer (BlockNumber blockNumber) (validators : ValidatorSnapshot list) =
        let validatorIndex = blockNumber % (int64 validators.Length) |> Convert.ToInt32
        validators
        |> List.sortBy (fun v -> v.ValidatorAddress)
        |> List.item validatorIndex

    let isProposer
        (getValidators : unit -> ValidatorSnapshot list)
        blockNumber
        validatorAddress
        =

        let blockProposer =
            getValidators ()
            |> getBlockProposer blockNumber

        blockProposer.ValidatorAddress = validatorAddress

    let shouldProposeBlock
        getValidators
        blockCreationInterval
        validatorAddress
        lastAppliedBlockNumber
        (Timestamp lastBlockTimestamp)
        (Timestamp currentTimestamp)
        =

        lastAppliedBlockNumber = Synchronization.getLastAvailableBlockNumber ()
        && (lastBlockTimestamp + blockCreationInterval) <= currentTimestamp
        && isProposer getValidators (lastAppliedBlockNumber + 1L) validatorAddress
