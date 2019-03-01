namespace Own.Blockchain.Public.Core.Tests

open Xunit
open Swensen.Unquote
open Own.Common
open Own.Blockchain.Common
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
            ["A"; "B"; "C"; "D"]
            |> List.map BlockchainAddress

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
                ValidatorDepositLockTime = Helpers.validatorDepositLockTime
                ValidatorBlacklistTime = Helpers.validatorBlacklistTime
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

        let getValidatorState validatorAddress =
            if validatorAddress = validatorSnapshot.ValidatorAddress then
                Some {
                    ValidatorState.NetworkAddress = validatorSnapshot.NetworkAddress
                    SharedRewardPercent = validatorSnapshot.SharedRewardPercent
                    TimeToLockDeposit = 0s
                    TimeToBlacklist = 0s
                    IsEnabled = true
                }
            else
                None

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxBalanceStateFromStorage = getChxBalanceState
                GetAccountStateFromStorage = getAccountState
                GetValidatorStateFromStorage = getValidatorState
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
        test <@ depositLockTime = Helpers.validatorDepositLockTime @>

    [<Fact>]
    let ``Deposit lock - Decrease validator's time to lock deposit and time to blacklist on every config block`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let inactiveValidatorWallet = Signing.generateWallet ()

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
                ValidatorDepositLockTime = Helpers.validatorDepositLockTime
                ValidatorBlacklistTime = Helpers.validatorBlacklistTime
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

        let getValidatorState validatorAddress =
            if validatorAddress = validatorWallet.Address then
                Some {
                    ValidatorState.NetworkAddress = validatorSnapshot.NetworkAddress
                    SharedRewardPercent = validatorSnapshot.SharedRewardPercent
                    TimeToLockDeposit = 0s
                    TimeToBlacklist = 0s
                    IsEnabled = true
                }
            elif validatorAddress = inactiveValidatorWallet.Address then
                Some {
                    ValidatorState.NetworkAddress = NetworkAddress "some.inactive.validator.com:12345"
                    SharedRewardPercent = 0m
                    TimeToLockDeposit = Helpers.validatorDepositLockTime
                    TimeToBlacklist = Helpers.validatorBlacklistTime
                    IsEnabled = true
                }
            else
                None

        let getLockedAndBlacklistedValidators () =
            [
                validatorWallet.Address
                inactiveValidatorWallet.Address
            ]

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxBalanceStateFromStorage = getChxBalanceState
                GetAccountStateFromStorage = getAccountState
                GetValidatorStateFromStorage = getValidatorState
                GetLockedAndBlacklistedValidators = getLockedAndBlacklistedValidators
                ValidatorAddress = validatorWallet.Address
                BlockchainConfiguration = blockchainConfiguration
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        test <@ output.Validators.Count = 2 @>

        let activeValidatorDepositLockTime, activeValidatorBlacklistTime =
            output.Validators.[validatorWallet.Address]
            |> fun (s, c) -> s.TimeToLockDeposit, s.TimeToBlacklist

        let inactiveValidatorDepositLockTime, inactiveValidatorBlacklistTime =
            output.Validators.[inactiveValidatorWallet.Address]
            |> fun (s, c) -> s.TimeToLockDeposit, s.TimeToBlacklist

        test <@ activeValidatorDepositLockTime = Helpers.validatorDepositLockTime @>
        test <@ activeValidatorBlacklistTime = 0s @>
        test <@ inactiveValidatorDepositLockTime = Helpers.validatorDepositLockTime - 1s @>
        test <@ inactiveValidatorBlacklistTime = Helpers.validatorBlacklistTime - 1s @>

    [<Fact>]
    let ``Deposit lock - Prevent transferring the deposit while it's locked`` () =
        // INIT STATE
        let recipientWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()

        let initialChxState =
            [
                recipientWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 20L}
                validatorWallet.Address, {ChxBalanceState.Amount = ChxAmount 5000.001m; Nonce = Nonce 30L}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 31L
        let actionFee = ChxAmount 0.001m
        let amountToTransfer = ChxAmount 1m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "TransferChx"
                    ActionData =
                        {
                            RecipientAddress = recipientWallet.Address.Value
                            Amount = amountToTransfer.Value
                        }
                } :> obj
            ]
            |> Helpers.newTx validatorWallet nonce actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxBalanceState address =
            initialChxState |> Map.tryFind address

        let getValidatorState _ =
            Some {
                ValidatorState.NetworkAddress = NetworkAddress "val01.mainnet.weown.com:25718"
                SharedRewardPercent = 0m
                TimeToLockDeposit = 0s
                TimeToBlacklist = 0s
                IsEnabled = true
            }

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxBalanceStateFromStorage = getChxBalanceState
                GetValidatorStateFromStorage = getValidatorState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let recipientChxBalance = initialChxState.[recipientWallet.Address].Amount
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Amount
        let expectedStatus = (TxActionNumber 1s, TxErrorCode.InsufficientChxBalance) |> TxActionError |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>
        test <@ output.ChxBalances.[recipientWallet.Address].Nonce = initialChxState.[recipientWallet.Address].Nonce @>
        test <@ output.ChxBalances.[validatorWallet.Address].Nonce = nonce @>
        test <@ output.ChxBalances.[recipientWallet.Address].Amount = recipientChxBalance @>
        test <@ output.ChxBalances.[validatorWallet.Address].Amount = validatorChxBalance @>

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Equivocation Proof
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Fact>]
    let ``Processing.processChanges Equivocation Proof`` () =
        // INIT STATE
        let adversaryWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()

        let initialChxState =
            [
                adversaryWallet.Address, {ChxBalanceState.Amount = Helpers.validatorDeposit; Nonce = Nonce 10L}
                validatorWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 30L}
            ]
            |> Map.ofList

        // PREPARE
        let blockNumber = BlockNumber 1L
        let consensusRound = ConsensusRound 0

        let blockHashes =
            [
                "Block A" |> Conversion.stringToBytes |> Hashing.hash
                "Block B" |> Conversion.stringToBytes |> Hashing.hash
            ]
            |> List.sort

        let blockHash1 = blockHashes.[0]
        let blockHash2 = blockHashes.[1]

        let signature1 =
            blockHash1
            |> BlockHash
            |> Some
            |> ConsensusMessage.Vote
            |> Consensus.createConsensusMessageHash
                Hashing.decode
                Hashing.hash
                blockNumber
                consensusRound
            |> Signing.signHash Helpers.getNetworkId adversaryWallet.PrivateKey

        let signature2 =
            blockHash2
            |> BlockHash
            |> Some
            |> ConsensusMessage.Vote
            |> Consensus.createConsensusMessageHash
                Hashing.decode
                Hashing.hash
                blockNumber
                consensusRound
            |> Signing.signHash Helpers.getNetworkId adversaryWallet.PrivateKey

        let equivocationProofDto : EquivocationProofDto =
            {
                BlockNumber = blockNumber.Value
                ConsensusRound = consensusRound.Value
                ConsensusStep = ConsensusStep.Vote |> Mapping.consensusStepToCode
                BlockHash1 = blockHash1
                BlockHash2 = blockHash2
                Signature1 = signature1.Value
                Signature2 = signature2.Value
            }

        let equivocationProof =
            Validation.validateEquivocationProof
                (Signing.verifySignature Helpers.getNetworkId)
                Consensus.createConsensusMessageHash
                Hashing.decode
                Hashing.hash
                equivocationProofDto
            |> Result.handle id (failwithf "validateEquivocationProof FAILED: %A")

        // COMPOSE
        let getEquivocationProof _ =
            Ok equivocationProofDto

        let getChxBalanceState address =
            initialChxState |> Map.tryFind address

        let getValidatorState validatorAddress =
            if validatorAddress = validatorWallet.Address then
                Some {
                    ValidatorState.NetworkAddress = NetworkAddress "good.validator.com:12345"
                    SharedRewardPercent = 0m
                    TimeToLockDeposit = 3s
                    TimeToBlacklist = 0s
                    IsEnabled = true
                }
            elif validatorAddress = adversaryWallet.Address then
                Some {
                    ValidatorState.NetworkAddress = NetworkAddress "bad.validator.com:12345"
                    SharedRewardPercent = 0m
                    TimeToLockDeposit = 2s
                    TimeToBlacklist = 0s
                    IsEnabled = true
                }
            else
                None

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetEquivocationProof = getEquivocationProof
                GetChxBalanceStateFromStorage = getChxBalanceState
                GetValidatorStateFromStorage = getValidatorState
                Validators = [validatorWallet.Address; adversaryWallet.Address]
                ValidatorAddress = validatorWallet.Address
                BlockNumber = BlockNumber 2L
                EquivocationProofs = [equivocationProof.EquivocationProofHash]
            }
            |> Helpers.processChanges

        // ASSERT
        let adversaryChxBalance = initialChxState.[adversaryWallet.Address].Amount - Helpers.validatorDeposit
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Amount + Helpers.validatorDeposit
        let equivocationProofResult = output.EquivocationProofResults.[equivocationProof.EquivocationProofHash]
        let adversaryState = output.Validators.[adversaryWallet.Address] |> fst

        test <@ output.EquivocationProofResults.Count = 1 @>
        test <@ equivocationProofResult.DepositTaken = Helpers.validatorDeposit @>
        test <@ output.ChxBalances.[adversaryWallet.Address].Nonce = initialChxState.[adversaryWallet.Address].Nonce @>
        test <@ output.ChxBalances.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxBalances.[adversaryWallet.Address].Amount = adversaryChxBalance @>
        test <@ output.ChxBalances.[validatorWallet.Address].Amount = validatorChxBalance @>
        test <@ adversaryState.TimeToBlacklist = Helpers.validatorBlacklistTime @>
