namespace Own.Blockchain.Public.Core.Tests

open Xunit
open Swensen.Unquote
open Own.Common
open Own.Blockchain.Public.Core
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Core.Dtos
open Own.Blockchain.Public.Crypto

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

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Deposit locking and blacklisting
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Fact>]
    let ``Deposit lock - Set time to lock deposit for the validator included in the config block`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let validatorDepositLockTime = 2s

        let initialChxState =
            [
                senderWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 10L}
                validatorWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 30L}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let actionFee = ChxAmount 1m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "CreateAccount"
                    ActionData = CreateAccountTxActionDto()
                } :> obj
            ]
            |> Helpers.newTx senderWallet nonce actionFee

        let txSet = [txHash]

        let validatorSnapshot =
            {
                ValidatorSnapshot.ValidatorAddress = validatorWallet.Address
                NetworkAddress = NetworkAddress "val01.mainnet.weown.com:25718"
                SharedRewardPercent = 0m
                TotalStake = ChxAmount 500_000m
            }

        let blockchainConfiguration =
            {
                BlockchainConfiguration.ConfigurationBlockDelta = 10
                Validators = [validatorSnapshot]
                ValidatorDepositLockTime = validatorDepositLockTime
                ValidatorBlacklistTime = 5s
                MaxTxCountPerBlock = 1000
            }
            |> Some

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxBalanceState address =
            initialChxState |> Map.tryFind address

        let getAccountState _ =
            None

        let getValidatorState _ =
            Some {
                ValidatorState.NetworkAddress = validatorSnapshot.NetworkAddress
                SharedRewardPercent = validatorSnapshot.SharedRewardPercent
                TimeToLockDeposit = 0s
                TimeToBlacklist = 0s
                IsEnabled = true
            }

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxBalanceStateFromStorage = getChxBalanceState
                GetAccountStateFromStorage = getAccountState
                GetValidatorStateFromStorage = getValidatorState
                ValidatorDepositLockTime = validatorDepositLockTime
                ValidatorAddress = validatorWallet.Address
                BlockchainConfiguration = blockchainConfiguration
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        test <@ output.Validators.Count = 1 @>

        let depositLockTime =
            output.Validators.[validatorWallet.Address]
            |> fun (s, c) -> s.TimeToLockDeposit
        test <@ depositLockTime = validatorDepositLockTime @>
