namespace Own.Blockchain.Public.Core.Tests

open Xunit
open Swensen.Unquote
open Own.Common.FSharp
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
    [<InlineData(25, 17)>]
    [<InlineData(31, 21)>]
    [<InlineData(50, 34)>]
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
    [<InlineData(25, 9)>]
    [<InlineData(31, 11)>]
    [<InlineData(50, 17)>]
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
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = ChxAmount 100m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
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
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

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
                ValidatorsBlacklist = []
                DormantValidators = []
                ValidatorDepositLockTime = Helpers.validatorDepositLockTime
                ValidatorBlacklistTime = Helpers.validatorBlacklistTime
                MaxTxCountPerBlock = 1000
            }
            |> Some

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
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
                    LastProposedBlockNumber = None
                    LastProposedBlockTimestamp = None
                }
            else
                None

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetAccountStateFromStorage = getAccountState
                GetValidatorStateFromStorage = Some getValidatorState
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
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = ChxAmount 100m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
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
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

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
                ValidatorsBlacklist = []
                DormantValidators = []
                ValidatorDepositLockTime = Helpers.validatorDepositLockTime
                ValidatorBlacklistTime = Helpers.validatorBlacklistTime
                MaxTxCountPerBlock = 1000
            }
            |> Some

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
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
                    LastProposedBlockNumber = None
                    LastProposedBlockTimestamp = None
                }
            elif validatorAddress = inactiveValidatorWallet.Address then
                Some {
                    ValidatorState.NetworkAddress = NetworkAddress "some.inactive.validator.com:12345"
                    SharedRewardPercent = 0m
                    TimeToLockDeposit = Helpers.validatorDepositLockTime
                    TimeToBlacklist = Helpers.validatorBlacklistTime
                    IsEnabled = true
                    LastProposedBlockNumber = None
                    LastProposedBlockTimestamp = None
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
                GetChxAddressStateFromStorage = getChxAddressState
                GetAccountStateFromStorage = getAccountState
                GetValidatorStateFromStorage = Some getValidatorState
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
                recipientWallet.Address, {ChxAddressState.Nonce = Nonce 20L; Balance = ChxAmount 100m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 5000.001m}
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
                }
            ]
            |> Helpers.newTx validatorWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getValidatorState _ =
            Some {
                ValidatorState.NetworkAddress = NetworkAddress "val01.mainnet.weown.com:25718"
                SharedRewardPercent = 0m
                TimeToLockDeposit = 0s
                TimeToBlacklist = 0s
                IsEnabled = true
                LastProposedBlockNumber = None
                LastProposedBlockTimestamp = None
            }

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetValidatorStateFromStorage = Some getValidatorState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let recipientChxBalance = initialChxState.[recipientWallet.Address].Balance
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance
        let expectedStatus = (TxActionNumber 1s, TxErrorCode.InsufficientChxBalance) |> TxActionError |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>
        test <@ output.ChxAddresses.[recipientWallet.Address].Nonce = initialChxState.[recipientWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[recipientWallet.Address].Balance = recipientChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>

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
                adversaryWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = Helpers.validatorDeposit}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
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
            |> Vote
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
            |> Vote
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
                EquivocationValue1 = blockHash1
                EquivocationValue2 = blockHash2
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

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getValidatorState validatorAddress =
            if validatorAddress = validatorWallet.Address then
                Some {
                    ValidatorState.NetworkAddress = NetworkAddress "good.validator.com:12345"
                    SharedRewardPercent = 0m
                    TimeToLockDeposit = 3s
                    TimeToBlacklist = 0s
                    IsEnabled = true
                    LastProposedBlockNumber = None
                    LastProposedBlockTimestamp = None
                }
            elif validatorAddress = adversaryWallet.Address then
                Some {
                    ValidatorState.NetworkAddress = NetworkAddress "bad.validator.com:12345"
                    SharedRewardPercent = 0m
                    TimeToLockDeposit = 2s
                    TimeToBlacklist = 0s
                    IsEnabled = true
                    LastProposedBlockNumber = None
                    LastProposedBlockTimestamp = None
                }
            else
                None

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetEquivocationProof = getEquivocationProof
                GetChxAddressStateFromStorage = getChxAddressState
                GetValidatorStateFromStorage = Some getValidatorState
                Validators = [validatorWallet.Address; adversaryWallet.Address]
                ValidatorAddress = validatorWallet.Address
                BlockNumber = BlockNumber 2L
                EquivocationProofs = [equivocationProof.EquivocationProofHash]
            }
            |> Helpers.processChanges

        // ASSERT
        let adversaryChxBalance = initialChxState.[adversaryWallet.Address].Balance - Helpers.validatorDeposit
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + Helpers.validatorDeposit
        let equivocationProofResult = output.EquivocationProofResults.[equivocationProof.EquivocationProofHash]
        let adversaryState = output.Validators.[adversaryWallet.Address] |> fst

        test <@ output.EquivocationProofResults.Count = 1 @>
        test <@ equivocationProofResult.DepositDistribution.Length = 1 @>
        test <@ equivocationProofResult.DepositTaken = Helpers.validatorDeposit @>
        test <@ output.ChxAddresses.[adversaryWallet.Address].Nonce = initialChxState.[adversaryWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[adversaryWallet.Address].Balance = adversaryChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>
        test <@ adversaryState.TimeToBlacklist = Helpers.validatorBlacklistTime @>

    [<Fact>]
    let ``Processing.processChanges Equivocation Proof in the same block with validator TX`` () =
        // INIT STATE
        let validatorWallet = Signing.generateWallet ()
        let adversaryWallet = Signing.generateWallet ()
        let recipientWallet = Signing.generateWallet ()

        let initialChxState =
            [
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
                adversaryWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = Helpers.validatorDeposit + 1.1m}
                recipientWallet.Address, {ChxAddressState.Nonce = Nonce 20L; Balance = ChxAmount 50m}
            ]
            |> Map.ofList

        // PREPARE
        let blockNumber = BlockNumber 1L
        let consensusRound = ConsensusRound 0
        let amountToTransfer = ChxAmount 1m
        let nonce = Nonce 11L
        let actionFee = ChxAmount 0.1m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "TransferChx"
                    ActionData =
                        {
                            RecipientAddress = recipientWallet.Address.Value
                            Amount = amountToTransfer.Value
                        }
                }
            ]
            |> Helpers.newTx adversaryWallet nonce (Timestamp 0L) actionFee

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
            |> Vote
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
            |> Vote
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
                EquivocationValue1 = blockHash1
                EquivocationValue2 = blockHash2
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
        let getTx _ =
            Ok txEnvelope

        let getEquivocationProof _ =
            Ok equivocationProofDto

        let getChxAddressState address =
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
                GetTx = getTx
                GetEquivocationProof = getEquivocationProof
                GetChxAddressStateFromStorage = getChxAddressState
                GetValidatorStateFromStorage = getValidatorState
                Validators = [validatorWallet.Address; adversaryWallet.Address]
                ValidatorAddress = validatorWallet.Address
                BlockNumber = BlockNumber 2L
                TxSet = [txHash]
                EquivocationProofs = [equivocationProof.EquivocationProofHash]
            }
            |> Helpers.processChanges

        // ASSERT
        let validatorChxBalance =
            initialChxState.[validatorWallet.Address].Balance + Helpers.validatorDeposit + actionFee
        let adversaryChxBalance =
            initialChxState.[adversaryWallet.Address].Balance - Helpers.validatorDeposit - actionFee - amountToTransfer
        let recipientChxBalance =
            initialChxState.[recipientWallet.Address].Balance + amountToTransfer
        let equivocationProofResult = output.EquivocationProofResults.[equivocationProof.EquivocationProofHash]
        let txResult = output.TxResults.[txHash]
        let adversaryState = output.Validators.[adversaryWallet.Address] |> fst

        test <@ output.TxResults.Count = 1 @>
        test <@ txResult.Status = Success @>
        test <@ output.EquivocationProofResults.Count = 1 @>
        test <@ equivocationProofResult.DepositDistribution.Length = 1 @>
        test <@ equivocationProofResult.DepositTaken = Helpers.validatorDeposit @>
        test <@ output.ChxAddresses.[adversaryWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[recipientWallet.Address].Nonce = initialChxState.[recipientWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[adversaryWallet.Address].Balance = adversaryChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>
        test <@ output.ChxAddresses.[recipientWallet.Address].Balance = recipientChxBalance @>
        test <@ adversaryState.TimeToBlacklist = Helpers.validatorBlacklistTime @>

    [<Theory>]
    [<InlineData(1000, 333.3333333, 0.0000001)>]
    [<InlineData(5000, 1666.6666666, 0.0000002)>]
    let ``Processing.processChanges Equivocation Proof - deposit taken smaller due to rounding``
        (deposit : decimal, amountPerValidator : decimal, remainder : decimal)
        =

        let deposit = deposit |> ChxAmount
        let amountPerValidator = amountPerValidator |> ChxAmount
        let remainder = remainder |> ChxAmount

        // INIT STATE
        let adversaryWallet = Signing.generateWallet ()
        let val1Wallet = Signing.generateWallet ()
        let val2Wallet = Signing.generateWallet ()
        let val3Wallet = Signing.generateWallet ()

        let initialChxState =
            [
                adversaryWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = deposit}
                val1Wallet.Address, {ChxAddressState.Nonce = Nonce 20L; Balance = ChxAmount 100m}
                val2Wallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 200m}
                val3Wallet.Address, {ChxAddressState.Nonce = Nonce 40L; Balance = ChxAmount 300m}
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
            |> Vote
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
            |> Vote
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
                EquivocationValue1 = blockHash1
                EquivocationValue2 = blockHash2
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

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getValidatorState validatorAddress =
            let state =
                {
                    ValidatorState.NetworkAddress = NetworkAddress ""
                    SharedRewardPercent = 0m
                    TimeToLockDeposit = 3s
                    TimeToBlacklist = 0s
                    IsEnabled = true
                    LastProposedBlockNumber = None
                    LastProposedBlockTimestamp = None
                }
            if validatorAddress = val1Wallet.Address then
                Some {state with NetworkAddress = NetworkAddress "good1.validator.com:12345"}
            elif validatorAddress = val2Wallet.Address then
                Some {state with NetworkAddress = NetworkAddress "good2.validator.com:12345"}
            elif validatorAddress = val3Wallet.Address then
                Some {state with NetworkAddress = NetworkAddress "good3.validator.com:12345"}
            elif validatorAddress = adversaryWallet.Address then
                Some {state with NetworkAddress = NetworkAddress "bad.validator.com:12345"; TimeToLockDeposit = 2s}
            else
                None

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetEquivocationProof = getEquivocationProof
                GetChxAddressStateFromStorage = getChxAddressState
                GetValidatorStateFromStorage = Some getValidatorState
                Validators =
                    [
                        val1Wallet.Address
                        val2Wallet.Address
                        val3Wallet.Address
                        adversaryWallet.Address
                    ]
                ValidatorAddress = val1Wallet.Address
                BlockNumber = BlockNumber 2L
                EquivocationProofs = [equivocationProof.EquivocationProofHash]
            }
            |> Helpers.processChanges

        // ASSERT
        let adversaryChxBalance =
            initialChxState.[adversaryWallet.Address].Balance - deposit + remainder
        let validator1ChxBalance = initialChxState.[val1Wallet.Address].Balance + amountPerValidator
        let validator2ChxBalance = initialChxState.[val2Wallet.Address].Balance + amountPerValidator
        let validator3ChxBalance = initialChxState.[val3Wallet.Address].Balance + amountPerValidator
        let equivocationProofResult = output.EquivocationProofResults.[equivocationProof.EquivocationProofHash]
        let adversaryState = output.Validators.[adversaryWallet.Address] |> fst

        test <@ output.EquivocationProofResults.Count = 1 @>
        test <@ equivocationProofResult.DepositDistribution.Length = 3 @>
        test <@ equivocationProofResult.DepositTaken = deposit - remainder @>
        test <@ equivocationProofResult.DepositDistribution.[0].ValidatorAddress = val1Wallet.Address @>
        test <@ equivocationProofResult.DepositDistribution.[0].Amount = amountPerValidator @>
        test <@ equivocationProofResult.DepositDistribution.[1].ValidatorAddress = val2Wallet.Address @>
        test <@ equivocationProofResult.DepositDistribution.[1].Amount = amountPerValidator @>
        test <@ equivocationProofResult.DepositDistribution.[2].ValidatorAddress = val3Wallet.Address @>
        test <@ equivocationProofResult.DepositDistribution.[2].Amount = amountPerValidator @>

        test <@ output.ChxAddresses.[adversaryWallet.Address].Nonce = initialChxState.[adversaryWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[val1Wallet.Address].Nonce = initialChxState.[val1Wallet.Address].Nonce @>
        test <@ output.ChxAddresses.[val2Wallet.Address].Nonce = initialChxState.[val2Wallet.Address].Nonce @>
        test <@ output.ChxAddresses.[val3Wallet.Address].Nonce = initialChxState.[val3Wallet.Address].Nonce @>

        test <@ output.ChxAddresses.[adversaryWallet.Address].Balance = adversaryChxBalance @>
        test <@ output.ChxAddresses.[val1Wallet.Address].Balance = validator1ChxBalance @>
        test <@ output.ChxAddresses.[val2Wallet.Address].Balance = validator2ChxBalance @>
        test <@ output.ChxAddresses.[val3Wallet.Address].Balance = validator3ChxBalance @>

        test <@ adversaryState.TimeToBlacklist = Helpers.validatorBlacklistTime @>

    [<Theory>]
    [<InlineData(1000, 500, 0)>]
    [<InlineData(5000, 2500, 0)>]
    let ``Processing.processChanges Equivocation Proof - slashed deposit not given to blacklisted validators``
        (deposit : decimal, amountPerValidator : decimal, remainder : decimal)
        =

        let deposit = deposit |> ChxAmount
        let amountPerValidator = amountPerValidator |> ChxAmount
        let remainder = remainder |> ChxAmount

        // INIT STATE
        let adversaryWallet = Signing.generateWallet ()
        let val1Wallet = Signing.generateWallet ()
        let blacklistedValidatorWallet = Signing.generateWallet ()
        let val3Wallet = Signing.generateWallet ()

        let initialChxState =
            [
                adversaryWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = deposit}
                val1Wallet.Address, {ChxAddressState.Nonce = Nonce 20L; Balance = ChxAmount 100m}
                blacklistedValidatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 200m}
                val3Wallet.Address, {ChxAddressState.Nonce = Nonce 40L; Balance = ChxAmount 300m}
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
            |> Vote
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
            |> Vote
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
                EquivocationValue1 = blockHash1
                EquivocationValue2 = blockHash2
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

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getValidatorState validatorAddress =
            let state =
                {
                    ValidatorState.NetworkAddress = NetworkAddress ""
                    SharedRewardPercent = 0m
                    TimeToLockDeposit = 3s
                    TimeToBlacklist = 0s
                    IsEnabled = true
                    LastProposedBlockNumber = None
                    LastProposedBlockTimestamp = None
                }
            if validatorAddress = val1Wallet.Address then
                Some {state with NetworkAddress = NetworkAddress "good1.validator.com:12345"}
            elif validatorAddress = blacklistedValidatorWallet.Address then
                Some {state with NetworkAddress = NetworkAddress "blacklist.validator.com:12345"; TimeToBlacklist = 1s}
            elif validatorAddress = val3Wallet.Address then
                Some {state with NetworkAddress = NetworkAddress "good3.validator.com:12345"}
            elif validatorAddress = adversaryWallet.Address then
                Some {state with NetworkAddress = NetworkAddress "bad.validator.com:12345"; TimeToLockDeposit = 2s}
            else
                None

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetEquivocationProof = getEquivocationProof
                GetChxAddressStateFromStorage = getChxAddressState
                GetValidatorStateFromStorage = Some getValidatorState
                Validators =
                    [
                        val1Wallet.Address
                        blacklistedValidatorWallet.Address
                        val3Wallet.Address
                        adversaryWallet.Address
                    ]
                ValidatorAddress = val1Wallet.Address
                BlockNumber = BlockNumber 2L
                EquivocationProofs = [equivocationProof.EquivocationProofHash]
            }
            |> Helpers.processChanges

        // ASSERT
        let adversaryChxBalance =
            initialChxState.[adversaryWallet.Address].Balance - deposit + remainder
        let validator1ChxBalance = initialChxState.[val1Wallet.Address].Balance + amountPerValidator
        let validator2ChxBalance = initialChxState.[blacklistedValidatorWallet.Address].Balance
        let validator3ChxBalance = initialChxState.[val3Wallet.Address].Balance + amountPerValidator
        let equivocationProofResult = output.EquivocationProofResults.[equivocationProof.EquivocationProofHash]
        let adversaryState = output.Validators.[adversaryWallet.Address] |> fst

        test <@ output.EquivocationProofResults.Count = 1 @>
        test <@ equivocationProofResult.DepositDistribution.Length = 2 @>
        test <@ equivocationProofResult.DepositTaken = deposit - remainder @>
        test <@ equivocationProofResult.DepositDistribution.[0].ValidatorAddress = val1Wallet.Address @>
        test <@ equivocationProofResult.DepositDistribution.[0].Amount = amountPerValidator @>
        test <@ equivocationProofResult.DepositDistribution.[1].ValidatorAddress = val3Wallet.Address @>
        test <@ equivocationProofResult.DepositDistribution.[1].Amount = amountPerValidator @>

        test <@ output.ChxAddresses.ContainsKey(blacklistedValidatorWallet.Address) = false @>

        test <@ output.ChxAddresses.[adversaryWallet.Address].Nonce = initialChxState.[adversaryWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[val1Wallet.Address].Nonce = initialChxState.[val1Wallet.Address].Nonce @>
        test <@ output.ChxAddresses.[val3Wallet.Address].Nonce = initialChxState.[val3Wallet.Address].Nonce @>

        test <@ output.ChxAddresses.[adversaryWallet.Address].Balance = adversaryChxBalance @>
        test <@ output.ChxAddresses.[val1Wallet.Address].Balance = validator1ChxBalance @>
        test <@ output.ChxAddresses.[val3Wallet.Address].Balance = validator3ChxBalance @>

        test <@ adversaryState.TimeToBlacklist = Helpers.validatorBlacklistTime @>
