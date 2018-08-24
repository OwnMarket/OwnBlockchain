namespace Chainium.Blockchain.Public.Core

open System
open Chainium.Common
open Chainium.Blockchain.Public.Core.DomainTypes

module Consensus =

    let calculateQuorumSupply quorumSupplyPercent (ChxAmount totalSupply) =
        Decimal.Round(totalSupply * quorumSupplyPercent / 100m, 18, MidpointRounding.AwayFromZero)
        |> ChxAmount

    let calculateValidatorThreshold maxValidatorCount (ChxAmount quorumSupply) =
        Decimal.Round(quorumSupply / (decimal maxValidatorCount), 18, MidpointRounding.AwayFromZero)
        |> ChxAmount

    let getBlockProposer (BlockNumber blockNumber) (validators : _ list) =
        // This is a simple leader based protocol used as a temporary placeholder for real consensus implementation.
        let validatorIndex = blockNumber % (int64 validators.Length) |> Convert.ToInt32
        validators.[validatorIndex]
