namespace Own.Blockchain.Public.Core.Tests

open System
open Xunit
open Swensen.Unquote
open Own.Common
open Own.Blockchain.Common
open Own.Blockchain.Public.Core
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Core.Dtos

module ConsensusTests =

    [<Fact>]
    let ``Consensus.calculateQuorumSupply`` () =
        // ARRANGE
        let totalSupply = ChxAmount 1000m
        let quorumSupplyPercent = 33m
        let expectedQuorumSupply = ChxAmount 330m

        // ACT
        let actualQuorumSupply = Consensus.calculateQuorumSupply quorumSupplyPercent totalSupply

        // ASSERT
        test <@ actualQuorumSupply = expectedQuorumSupply @>

    [<Fact>]
    let ``Consensus.calculateQuorumSupply with rounding`` () =
        // ARRANGE
        let totalSupply = ChxAmount 1000m
        let quorumSupplyPercent = 99.99999999999999999955m
        let expectedQuorumSupply = ChxAmount 999.999999999999999996m

        // ACT
        let actualQuorumSupply = Consensus.calculateQuorumSupply quorumSupplyPercent totalSupply

        // ASSERT
        test <@ actualQuorumSupply = expectedQuorumSupply @>

    [<Fact>]
    let ``Consensus.calculateValidatorThreshold`` () =
        // ARRANGE
        let quorumSupply = ChxAmount 1000m
        let maxValidatorCount = 100
        let expectedValidatorThreshold = ChxAmount 10m

        // ACT
        let actualValidatorThreshold = Consensus.calculateValidatorThreshold maxValidatorCount quorumSupply

        // ASSERT
        test <@ actualValidatorThreshold = expectedValidatorThreshold @>

    [<Fact>]
    let ``Consensus.calculateValidatorThreshold with rounding`` () =
        // ARRANGE
        let quorumSupply = ChxAmount 1000m
        let maxValidatorCount = 11
        let expectedValidatorThreshold = ChxAmount 90.909090909090909091m

        // ACT
        let actualValidatorThreshold = Consensus.calculateValidatorThreshold maxValidatorCount quorumSupply

        // ASSERT
        test <@ actualValidatorThreshold = expectedValidatorThreshold @>

    [<Fact>]
    let ``Consensus.getBlockProposer`` () =
        // ARRANGE
        let blockNumber = BlockNumber 1L
        let validators =
            [
                {ValidatorSnapshotDto.ValidatorAddress = "A"; NetworkAddress = "1"; TotalStake = 0m}
                {ValidatorSnapshotDto.ValidatorAddress = "B"; NetworkAddress = "2"; TotalStake = 0m}
                {ValidatorSnapshotDto.ValidatorAddress = "C"; NetworkAddress = "3"; TotalStake = 0m}
                {ValidatorSnapshotDto.ValidatorAddress = "D"; NetworkAddress = "4"; TotalStake = 0m}
            ]
            |> List.map Mapping.validatorSnapshotFromDto

        let expectedValidator = validators.[1]

        // ACT
        let actualValidator = Consensus.getBlockProposer blockNumber validators

        // ASSERT
        test <@ actualValidator = expectedValidator @>
