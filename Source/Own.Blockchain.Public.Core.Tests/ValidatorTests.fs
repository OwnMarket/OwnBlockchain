namespace Own.Blockchain.Public.Core.Tests

open Xunit
open Swensen.Unquote
open Own.Common
open Own.Blockchain.Public.Core
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Core.Dtos

module ValidatorTests =

    [<Theory>]
    [<InlineData(4, 3)>]
    [<InlineData(5, 4)>]
    [<InlineData(6, 5)>]
    [<InlineData(7, 5)>]
    [<InlineData(8, 6)>]
    [<InlineData(9, 7)>]
    [<InlineData(10, 7)>]
    [<InlineData(20, 14)>]
    [<InlineData(31, 21)>]
    [<InlineData(100, 67)>]
    let ``Validators.calculateQualifiedMajority`` (validatorCount, expectedQualifiedMajority) =
        // ACT
        let actualQualifiedMajority = Validators.calculateQualifiedMajority validatorCount

        // ASSERT
        test <@ actualQualifiedMajority = expectedQualifiedMajority @>

    [<Theory>]
    [<InlineData(4, 2)>]
    [<InlineData(5, 2)>]
    [<InlineData(6, 3)>]
    [<InlineData(7, 3)>]
    [<InlineData(8, 3)>]
    [<InlineData(9, 4)>]
    [<InlineData(10, 4)>]
    [<InlineData(20, 7)>]
    [<InlineData(31, 11)>]
    [<InlineData(100, 34)>]
    let ``Validators.calculateValidQuorum`` (validatorCount, expectedValidQuorum) =
        // ACT
        let actualValidQuorum = Validators.calculateValidQuorum validatorCount

        // ASSERT
        test <@ actualValidQuorum = expectedValidQuorum @>

    [<Fact>]
    let ``Validators.getProposer`` () =
        // ARRANGE
        let blockNumber = BlockNumber 1L
        let consensusRound = ConsensusRound 0
        let validators =
            [
                {ValidatorAddress = "A"; NetworkAddress = "1"; SharedRewardPercent = 0m; TotalStake = 0m}
                {ValidatorAddress = "B"; NetworkAddress = "2"; SharedRewardPercent = 0m; TotalStake = 0m}
                {ValidatorAddress = "C"; NetworkAddress = "3"; SharedRewardPercent = 0m; TotalStake = 0m}
                {ValidatorAddress = "D"; NetworkAddress = "4"; SharedRewardPercent = 0m; TotalStake = 0m}
            ]
            |> List.map Mapping.validatorSnapshotFromDto

        let expectedValidator = validators.[1]

        // ACT
        let actualValidator = Validators.getProposer blockNumber consensusRound validators

        // ASSERT
        test <@ actualValidator = expectedValidator @>
