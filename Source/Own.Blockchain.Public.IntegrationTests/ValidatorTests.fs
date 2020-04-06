namespace Own.Blockchain.Public.IntegrationTests

open System
open Xunit
open Swensen.Unquote
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Core.Dtos
open Own.Blockchain.Public.Crypto
open Own.Blockchain.Public.Node

module ValidatorTests =

    [<Fact>]
    let ``Validator not included in the config if the available balance is less than required deposit`` () =
        // ARRANGE
        Helpers.resetTestData ()

        let validator1Wallet = Signing.generateWallet ()
        let validator2Wallet = Signing.generateWallet ()
        let stakerWallet = Signing.generateWallet ()

        [
            validator1Wallet.Address.Value, Config.ValidatorDeposit
            validator2Wallet.Address.Value, Config.ValidatorDeposit - 1m // Won't have sufficient deposit.
        ]
        |> List.iter (fun (address, deposit) ->
            {
                BlockchainAddress = address
                ChxAddressState =
                    {
                        ChxAddressStateDto.Nonce = 0L
                        Balance = deposit
                    }
            }
            |> Helpers.addChxAddress

            {
                ValidatorAddress = address
                NetworkAddress = sprintf "%s.weown.com:25718" address
                SharedRewardPercent = 0m
                TimeToLockDeposit = 0s
                TimeToBlacklist = 0s
                IsEnabled = true
                LastProposedBlockNumber = Nullable()
                LastProposedBlockTimestamp = Nullable()
            }
            |> Helpers.addValidator

            {
                StakeInfoDto.StakerAddress = stakerWallet.Address.Value
                ValidatorAddress = address
                StakeState =
                    {
                        Amount = Config.ValidatorThreshold
                    }
            }
            |> Helpers.addStake
        )

        // ACT
        let topValidators =
            Composition.getTopValidatorsByStake
                Config.MaxValidatorCount
                (ChxAmount Config.ValidatorThreshold)
                (ChxAmount Config.ValidatorDeposit)

        // ASSERT
        test <@ topValidators.Length = 1 @>
        test <@ topValidators.[0].ValidatorAddress = validator1Wallet.Address.Value @>

    [<Fact>]
    let ``Validator not included in the config if blacklisted`` () =
        // ARRANGE
        Helpers.resetTestData ()

        let validator1Wallet = Signing.generateWallet ()
        let validator2Wallet = Signing.generateWallet ()
        let stakerWallet = Signing.generateWallet ()

        [
            validator1Wallet.Address.Value, 1s // Blacklisted
            validator2Wallet.Address.Value, 0s
        ]
        |> List.iter (fun (address, timeToBlacklist) ->
            {
                BlockchainAddress = address
                ChxAddressState =
                    {
                        ChxAddressStateDto.Nonce = 0L
                        Balance = Config.ValidatorDeposit
                    }
            }
            |> Helpers.addChxAddress

            {
                ValidatorAddress = address
                NetworkAddress = sprintf "%s.weown.com:25718" address
                SharedRewardPercent = 0m
                TimeToLockDeposit = 0s
                TimeToBlacklist = timeToBlacklist
                IsEnabled = true
                LastProposedBlockNumber = Nullable()
                LastProposedBlockTimestamp = Nullable()
            }
            |> Helpers.addValidator

            {
                StakeInfoDto.StakerAddress = stakerWallet.Address.Value
                ValidatorAddress = address
                StakeState =
                    {
                        Amount = Config.ValidatorThreshold
                    }
            }
            |> Helpers.addStake
        )

        // ACT
        let topValidators =
            Composition.getTopValidatorsByStake
                Config.MaxValidatorCount
                (ChxAmount Config.ValidatorThreshold)
                (ChxAmount Config.ValidatorDeposit)

        // ASSERT
        test <@ topValidators.Length = 1 @>
        test <@ topValidators.[0].ValidatorAddress = validator2Wallet.Address.Value @>
