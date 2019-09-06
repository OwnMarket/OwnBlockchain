namespace Own.Blockchain.Public.Core.Tests

open System
open Xunit
open Swensen.Unquote
open Own.Common.FSharp
open Own.Blockchain.Common
open Own.Blockchain.Public.Core
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Crypto

module BlockTests =

    [<Fact>]
    let ``Blocks.createTxResultHash for Success`` () =
        let txHash = TxHash "ABC"
        let txStatus =
            {
                TxResult.Status = Success // Code 1 = A
                BlockNumber = BlockNumber 4L
            }

        // ACT
        let txResultHash = Blocks.createTxResultHash DummyHash.decode DummyHash.create (txHash, txStatus)

        // ASSERT
        test <@ txResultHash = "ABCA...........D" @>

    [<Fact>]
    let ``Blocks.createTxResultHash for TxError`` () =
        let txHash = TxHash "ABC"
        let txStatus =
            {
                TxResult.Status = TxErrorCode.NonceTooLow |> TxError |> Failure
                BlockNumber = BlockNumber 4L
            }

        // ACT
        let txResultHash = Blocks.createTxResultHash DummyHash.decode DummyHash.create (txHash, txStatus)

        // ASSERT
        test <@ txResultHash = "ABCB...........D" @>

    [<Fact>]
    let ``Blocks.createTxResultHash for TxActionError`` () =
        let txHash = TxHash "ABC"
        let txStatus =
            {
                TxResult.Status = (TxActionNumber 3s, TxErrorCode.NonceTooLow) |> TxActionError |> Failure
                BlockNumber = BlockNumber 4L
            }

        // ACT
        let txResultHash = Blocks.createTxResultHash DummyHash.decode DummyHash.create (txHash, txStatus)

        // ASSERT
        test <@ txResultHash = "ABCB...C.......D" @>

    [<Fact>]
    let ``Blocks.createEquivocationProofResultHash for 2 CHX deposit taken`` () =
        let equivocationProofHash = EquivocationProofHash "ABC"
        let equivocationProofStatus =
            {
                DepositTaken = ChxAmount 2m
                DepositDistribution =
                    [
                        {DistributedDeposit.ValidatorAddress = BlockchainAddress "DDD"; Amount = ChxAmount 1m}
                        {DistributedDeposit.ValidatorAddress = BlockchainAddress "EEE"; Amount = ChxAmount 1m}
                    ]
                BlockNumber = BlockNumber 4L
            }

        let expectedHash =
            [
                "ABC"
                "...B............"
                "DDD...A............"
                "EEE...A............"
                ".......D"
            ]
            |> String.Concat

        // ACT
        let equivocationProofResultHash =
            Blocks.createEquivocationProofResultHash
                DummyHash.decode
                DummyHash.create
                (equivocationProofHash, equivocationProofStatus)

        // ASSERT
        test <@ equivocationProofResultHash = expectedHash @>

    [<Fact>]
    let ``Blocks.createEquivocationProofResultHash for 0 CHX deposit taken`` () =
        let equivocationProofHash = EquivocationProofHash "ABC"
        let equivocationProofStatus =
            {
                DepositTaken = ChxAmount 0m
                DepositDistribution = []
                BlockNumber = BlockNumber 4L
            }

        // ACT
        let equivocationProofResultHash =
            Blocks.createEquivocationProofResultHash
                DummyHash.decode
                DummyHash.create
                (equivocationProofHash, equivocationProofStatus)

        // ASSERT
        test <@ equivocationProofResultHash = "ABC.......................D" @>

    [<Fact>]
    let ``Blocks.createChxAddressStateHash`` () =
        let address = BlockchainAddress "ABC"
        let state = {ChxAddressState.Nonce = Nonce 2L; Balance = ChxAmount 1m}

        // ACT
        let stateHash = Blocks.createChxAddressStateHash DummyHash.decode DummyHash.create (address, state)

        // ASSERT
        test <@ stateHash = "ABC.......B...A............" @>

    [<Fact>]
    let ``Blocks.createHoldingStateHash`` () =
        let accountHash = AccountHash "HHH"
        let assetHash = AssetHash "II"
        let state = {HoldingState.Balance = AssetAmount 7m; IsEmission = true}

        // ACT
        let stateHash = Blocks.createHoldingStateHash DummyHash.decode DummyHash.create (accountHash, assetHash, state)

        // ASSERT
        test <@ stateHash = "HHHII...G............A" @>

    [<Fact>]
    let ``Blocks.createVoteStateHash`` () =
        let accountHash = AccountHash "AAA"
        let assetHash = AssetHash "BBB"
        let resolutionHash = VotingResolutionHash "CCC"
        let state =
            {
                VoteState.VoteHash = VoteHash "DDD"
                VoteWeight = VoteWeight 5m |> Some
            }

        // ACT
        let stateHash =
            Blocks.createVoteStateHash DummyHash.decode DummyHash.create (accountHash, assetHash, resolutionHash, state)

        // ASSERT
        test <@ stateHash = "AAABBBCCCDDD...E............" @>

    [<Fact>]
    let ``Blocks.createEligibilityStateHash`` () =
        let accountHash = AccountHash "AAA"
        let assetHash = AssetHash "BBB"
        let state =
            {
                EligibilityState.Eligibility =
                    {
                        IsPrimaryEligible = true
                        IsSecondaryEligible = false
                    }
                KycControllerAddress = BlockchainAddress "CC"
            }

        // ACT
        let stateHash =
            Blocks.createEligibilityStateHash DummyHash.decode DummyHash.create (accountHash, assetHash, state)

        // ASSERT
        test <@ stateHash = "AAABBBA.CC" @>

    [<Fact>]
    let ``Blocks.createKycProviderStateHash`` () =
        let assetHash = AssetHash "AAA"
        let state =
            [
                BlockchainAddress "CC",
                KycProviderChange.Add
            ]
            |> Map.ofList

        // ACT
        let stateHash =
            Blocks.createKycProviderStateHash DummyHash.decode DummyHash.create (assetHash, state)

        // ASSERT
        test <@ stateHash = "AAACCA" @>

    [<Fact>]
    let ``Blocks.createAccountStateHash`` () =
        let accountHash = AccountHash "AAA"
        let controllerAddress = BlockchainAddress "CC"
        let state = {AccountState.ControllerAddress = controllerAddress}

        // ACT
        let stateHash = Blocks.createAccountStateHash DummyHash.decode DummyHash.create (accountHash, state)

        // ASSERT
        test <@ stateHash = "AAACC" @>

    [<Fact>]
    let ``Blocks.createAssetStateHash`` () =
        let assetHash = AssetHash "AAA"
        let assetCode = AssetCode "XXX" |> Some // X = 88 = 8 = H
        let controllerAddress = BlockchainAddress "CC"
        let state =
            {
                AssetState.AssetCode = assetCode
                ControllerAddress = controllerAddress
                IsEligibilityRequired = true
            }

        // ACT
        let stateHash = Blocks.createAssetStateHash DummyHash.decode DummyHash.create (assetHash, state)

        // ASSERT
        test <@ stateHash = "AAAHHHCCA" @>

    [<Fact>]
    let ``Blocks.createValidatorStateHash`` () =
        let validatorAddress = BlockchainAddress "AAA"
        let state =
            {
                ValidatorState.NetworkAddress = NetworkAddress "XXX" // X = 88 = 8 = H
                SharedRewardPercent = 4m
                TimeToLockDeposit = 3s
                TimeToBlacklist = 5s
                IsEnabled = true
            }

        // ACT
        let stateHash =
            Blocks.createValidatorStateHash
                DummyHash.decode
                DummyHash.create
                (validatorAddress, (state, ValidatorChange.Add))

        // ASSERT
        test <@ stateHash = "AAAHHH...D.............C.EA." @>

    [<Fact>]
    let ``Blocks.createValidatorSnapshotHash`` () =
        let validatorSnapshot =
            {
                ValidatorSnapshot.ValidatorAddress = BlockchainAddress "AAA"
                NetworkAddress = NetworkAddress "XXX" // X = 88 = 8 = H
                SharedRewardPercent = 4m
                TotalStake = ChxAmount 5m
            }

        // ACT
        let snapshotHash = Blocks.createValidatorSnapshotHash DummyHash.decode DummyHash.create validatorSnapshot

        // ASSERT
        test <@ snapshotHash = "AAAHHH...D...............E............" @>

    [<Fact>]
    let ``Blocks.createStakeStateHash`` () =
        let staker = BlockchainAddress "AAA"
        let validator = BlockchainAddress "BBB"
        let state = {StakeState.Amount = ChxAmount 5m}

        // ACT
        let stateHash = Blocks.createStakeStateHash DummyHash.decode DummyHash.create (staker, validator, state)

        // ASSERT
        test <@ stateHash = "AAABBB...E............" @>

    [<Fact>]
    let ``Blocks.createStakingRewardHash`` () =
        let stakingReward =
            {
                StakingReward.StakerAddress = BlockchainAddress "AAA"
                Amount = ChxAmount 6m
            }

        // ACT
        let stakingRewardHash = Blocks.createStakingRewardHash DummyHash.decode DummyHash.create stakingReward

        // ASSERT
        test <@ stakingRewardHash = "AAA...F............" @>

    [<Fact>]
    let ``Blocks.createBlockHash`` () =
        let blockNumber = BlockNumber 1L
        let previousBlockHash = BlockHash "B"
        let configurationBlockNumber = BlockNumber 0L
        let timestamp = Timestamp 3L
        let validatorAddress = BlockchainAddress "D"
        let txSetRoot = MerkleTreeRoot "E"
        let txResultSetRoot = MerkleTreeRoot "F"
        let equivocationProofsRoot = MerkleTreeRoot "G"
        let equivocationProofResultsRoot = MerkleTreeRoot "H"
        let stateRoot = MerkleTreeRoot "I"
        let stakingRewardsRoot = MerkleTreeRoot "A"
        let configurationRoot = MerkleTreeRoot "B"

        // ACT
        let (BlockHash blockHash) =
            Blocks.createBlockHash
                DummyHash.decode
                DummyHash.create
                blockNumber
                previousBlockHash
                configurationBlockNumber
                timestamp
                validatorAddress
                txSetRoot
                txResultSetRoot
                equivocationProofsRoot
                equivocationProofResultsRoot
                stateRoot
                stakingRewardsRoot
                configurationRoot

        // ASSERT
        test <@ blockHash = ".......AB...............CDEFGHIAB" @>

    [<Fact>]
    let ``Blocks.assembleBlock`` () =
        let blockNumber = BlockNumber 1L
        let previousBlockHash = BlockHash "B"
        let configurationBlockNumber = BlockNumber 0L
        let timestamp = Timestamp 3L
        let proposerAddress = BlockchainAddress "D"

        // TXs
        let txSet =
            ["AAA"; "BBB"; "CCC"]
            |> List.map TxHash

        let txResult1 : TxResult = {
            Status = Success
            BlockNumber = BlockNumber 5L
        }

        let txResult2 : TxResult = {
            Status = (TxActionNumber 7s, TxErrorCode.InsufficientChxBalance) |> TxActionError |> Failure
            BlockNumber = BlockNumber 5L
        }

        let txResult3 : TxResult = {
            Status = Success
            BlockNumber = BlockNumber 5L
        }

        let txResults =
            [txResult1; txResult2; txResult3]
            |> List.zip txSet
            |> Map.ofList

        // Equivocation Proofs
        let equivocationProofs =
            ["DDD"; "EEE"]
            |> List.map EquivocationProofHash

        let equivocationProofResult1 : EquivocationProofResult = {
            DepositTaken = ChxAmount 6m
            DepositDistribution =
                [
                    {DistributedDeposit.ValidatorAddress = BlockchainAddress "AA"; Amount = ChxAmount 2m}
                    {DistributedDeposit.ValidatorAddress = BlockchainAddress "BB"; Amount = ChxAmount 2m}
                    {DistributedDeposit.ValidatorAddress = BlockchainAddress "CC"; Amount = ChxAmount 2m}
                ]
            BlockNumber = BlockNumber 5L
        }

        let equivocationProofResult2 : EquivocationProofResult = {
            DepositTaken = ChxAmount 0m
            DepositDistribution = []
            BlockNumber = BlockNumber 5L
        }

        let equivocationProofResults =
            [equivocationProofResult1; equivocationProofResult2]
            |> List.zip equivocationProofs
            |> Map.ofList

        // State Changes
        let chxAddresses =
            [
                BlockchainAddress "HH", {ChxAddressState.Nonce = Nonce 7L; Balance = ChxAmount 5m}
                BlockchainAddress "II", {ChxAddressState.Nonce = Nonce 8L; Balance = ChxAmount 6m}
            ]
            |> Map.ofList

        let holdings =
            [
                (AccountHash "DDD", AssetHash "EEE"), {HoldingState.Balance = AssetAmount 1m; IsEmission = false}
                (AccountHash "FFF", AssetHash "GGG"), {HoldingState.Balance = AssetAmount 2m; IsEmission = true}
            ]
            |> Map.ofList

        let votes =
            let voteId1V =
                {
                    AccountHash = AccountHash "DDD"
                    AssetHash = AssetHash "EEE"
                    ResolutionHash = VotingResolutionHash "AAAA"
                }
            let voteId1W =
                {
                    AccountHash = AccountHash "DDD"
                    AssetHash = AssetHash "EEE"
                    ResolutionHash = VotingResolutionHash "BBBB"
                }
            let voteId1Y =
                {
                    AccountHash = AccountHash "DDD"
                    AssetHash = AssetHash "EEE"
                    ResolutionHash = VotingResolutionHash "CCCC"
                }
            let voteId2V =
                {
                    AccountHash = AccountHash "FFF"
                    AssetHash = AssetHash "GGG"
                    ResolutionHash = VotingResolutionHash "AAAA"
                }
            let voteId2W =
                {
                    AccountHash = AccountHash "FFF"
                    AssetHash = AssetHash "GGG"
                    ResolutionHash = VotingResolutionHash "BBBB"
                }
            let voteId2Y =
                {
                    AccountHash = AccountHash "FFF"
                    AssetHash = AssetHash "GGG"
                    ResolutionHash = VotingResolutionHash "CCCC"
                }
            [
                voteId1V, {VoteState.VoteHash = VoteHash "DA"; VoteWeight = 1m |> VoteWeight |> Some}
                voteId1W, {VoteState.VoteHash = VoteHash "DA"; VoteWeight = 1m |> VoteWeight |> Some}
                voteId1Y, {VoteState.VoteHash = VoteHash "AD"; VoteWeight = 1m |> VoteWeight |> Some}
                voteId2V, {VoteState.VoteHash = VoteHash "AD"; VoteWeight = 2m |> VoteWeight |> Some}
                voteId2W, {VoteState.VoteHash = VoteHash "AD"; VoteWeight = 2m |> VoteWeight |> Some}
                voteId2Y, {VoteState.VoteHash = VoteHash "DA"; VoteWeight = 2m |> VoteWeight |> Some}
            ]
            |> Map.ofList

        let eligibilities =
            [
                (AccountHash "DDD", AssetHash "EEE"),
                    {
                        EligibilityState.Eligibility = {IsPrimaryEligible = true; IsSecondaryEligible = true}
                        KycControllerAddress = BlockchainAddress "HH"
                    }
                (AccountHash "FFF", AssetHash "GGG"),
                    {
                        EligibilityState.Eligibility = {IsPrimaryEligible = false; IsSecondaryEligible = false}
                        KycControllerAddress = BlockchainAddress "HH"
                    }
            ]
            |> Map.ofList

        let kycProviders =
            [
                AssetHash "EEE",
                [
                    BlockchainAddress "AAA",
                    KycProviderChange.Add
                    BlockchainAddress "BBB",
                    KycProviderChange.Add
                ]
                |> Map.ofList

                AssetHash "FFF",
                [
                    BlockchainAddress "AAA",
                    KycProviderChange.Add
                ]
                |> Map.ofList

                AssetHash "GGG",
                [
                    BlockchainAddress "BBB",
                    KycProviderChange.Remove
                ]
                |> Map.ofList
            ]
            |> Map.ofList

        let accounts =
            [
                AccountHash "AAAA", {AccountState.ControllerAddress = BlockchainAddress "BBBB"}
                AccountHash "CCCC", {AccountState.ControllerAddress = BlockchainAddress "DDDD"}
            ]
            |> Map.ofList

        let assets =
            [
                AssetHash "EEEE",
                {
                    AssetState.AssetCode = Some (AssetCode "XXX") // X = 88 = 8 = H
                    ControllerAddress = BlockchainAddress "FFFF"
                    IsEligibilityRequired = false
                }
                AssetHash "GGGG",
                {
                    AssetState.AssetCode = None
                    ControllerAddress = BlockchainAddress "HHHH"
                    IsEligibilityRequired = true
                }
            ]
            |> Map.ofList

        let validators =
            [
                BlockchainAddress "AAAAA",
                (
                    {
                        ValidatorState.NetworkAddress = NetworkAddress "WWW" // W = 87 = 7 = G
                        SharedRewardPercent = 1m
                        TimeToLockDeposit = 3s
                        TimeToBlacklist = 4s
                        IsEnabled = true
                    },
                    ValidatorChange.Add
                )
                BlockchainAddress "BBBBB",
                (
                    {
                        ValidatorState.NetworkAddress = NetworkAddress "XXX" // X = 88 = 8 = H
                        SharedRewardPercent = 2m
                        TimeToLockDeposit = 3s
                        TimeToBlacklist = 5s
                        IsEnabled = true
                    },
                    ValidatorChange.Add
                )
                BlockchainAddress "CCCCC",
                (
                    {
                        ValidatorState.NetworkAddress = NetworkAddress "YYY" // Y = 89 = 9 = I
                        SharedRewardPercent = 3m
                        TimeToLockDeposit = 3s
                        TimeToBlacklist = 6s
                        IsEnabled = true
                    },
                    ValidatorChange.Add
                )
            ]
            |> Map.ofList

        let stakes =
            [
                (BlockchainAddress "HH", BlockchainAddress "AAAAA"), {StakeState.Amount = ChxAmount 1m}
                (BlockchainAddress "II", BlockchainAddress "BBBBB"), {StakeState.Amount = ChxAmount 2m}
            ]
            |> Map.ofList

        let stakingRewards =
            [
                BlockchainAddress "HH", ChxAmount 1m
                BlockchainAddress "II", ChxAmount 2m
            ]
            |> Map.ofList

        let tradeOrders =
            [
                // TODO DSX
            ]
            |> Map.ofList

        let processingOutput =
            {
                ProcessingOutput.TxResults = txResults
                EquivocationProofResults = equivocationProofResults
                ChxAddresses = chxAddresses
                Holdings = holdings
                Votes = votes
                Eligibilities = eligibilities
                KycProviders = kycProviders
                Accounts = accounts
                Assets = assets
                Validators = validators
                Stakes = stakes
                StakingRewards = stakingRewards
                TradeOrders = tradeOrders
            }

        // Blockchain Configuration
        let config =
            {
                BlockchainConfiguration.ConfigurationBlockDelta = 4
                Validators =
                    [
                        {
                            ValidatorSnapshot.ValidatorAddress = BlockchainAddress "AAAAA"
                            NetworkAddress = NetworkAddress "WWW" // W = 87 = 7 = G
                            SharedRewardPercent = 1m
                            TotalStake = ChxAmount 4m
                        }
                        {
                            ValidatorSnapshot.ValidatorAddress = BlockchainAddress "BBBBB"
                            NetworkAddress = NetworkAddress "XXX" // X = 88 = 8 = H
                            SharedRewardPercent = 2m
                            TotalStake = ChxAmount 5m
                        }
                        {
                            ValidatorSnapshot.ValidatorAddress = BlockchainAddress "CCCCC"
                            NetworkAddress = NetworkAddress "YYY" // Y = 89 = 9 = I
                            SharedRewardPercent = 3m
                            TotalStake = ChxAmount 6m
                        }
                    ]
                ValidatorsBlacklist =
                    [
                        BlockchainAddress "DD"
                        BlockchainAddress "EE"
                    ]
                ValidatorDepositLockTime = 7s
                ValidatorBlacklistTime = 8s
                MaxTxCountPerBlock = 9
            }

        // Merkle Roots
        let txSetRoot = "AAABBBCCC"

        let txResultSetRoot =
            [
                "AAAA...........E" // TxResult 1
                "BBBB...G.......E" // TxResult 2
                "CCCA...........E" // TxResult 3
            ]
            |> String.Concat

        let equivocationProofsRoot = "DDDEEE"

        let equivocationProofResultsRoot =
            [
                "DDD" // EquivocationProofResult 1
                    + "...F............"
                    + "AA...B............"
                    + "BB...B............"
                    + "CC...B............"
                    + ".......E"
                "EEE.......................E" // EquivocationProofResult 2
            ]
            |> String.Concat

        let stateRoot =
            [
                "HH.......G...E............" // CHX address 1
                "II.......H...F............" // CHX address 2
                "DDDEEE...A............." // Holding 1
                "FFFGGG...B............A" // Holding 2
                "DDDEEEAAAADA...A............" // Vote 1 Holding 1
                "DDDEEEBBBBDA...A............" // Vote 2 Holding 1
                "DDDEEECCCCAD...A............" // Vote 3 Holding 1
                "FFFGGGAAAAAD...B............" // Vote 1 Holding 2
                "FFFGGGBBBBAD...B............" // Vote 2 Holding 2
                "FFFGGGCCCCDA...B............" // Vote 3 Holding 2
                "DDDEEEAAHH" // Eligibility 1
                "FFFGGG..HH" // Eligibility 2
                "EEEAAAABBBA" // Add KYC controllers for Asset 1
                "FFFAAAA" // Add KYC controllers for Asset 2
                "GGGBBB." // Remove KYC controllers for Asset 3
                "AAAABBBB" // Account 1
                "CCCCDDDD" // Account 2
                "EEEEHHHFFFF." // Asset 1
                "GGGG.HHHHA" // Asset 2
                "AAAAAGGG...A.............C.DA." // Validator 1
                "BBBBBHHH...B.............C.EA." // Validator 2
                "CCCCCIII...C.............C.FA." // Validator 3
                "HHAAAAA...A............" // Stake 1
                "IIBBBBB...B............" // Stake 2
            ]
            |> String.Concat

        let stakingRewardRoot =
            [
                // Descending order by reward, then ascending by address
                "II...B............" // Staking reward 2
                "HH...A............" // Staking reward 1
            ]
            |> String.Concat

        let configRoot =
            [
                "...D" // ConfigurationBlockDelta
                "AAAAAGGG...A...............D............" // Validator 1
                "BBBBBHHH...B...............E............" // Validator 2
                "CCCCCIII...C...............F............" // Validator 3
                "DD" // Blacklisted validator 1
                "EE" // Blacklisted validator 2
                ".G" // ValidatorDepositLockTime
                ".H" // ValidatorBlacklistTime
                "...I" // MaxTxCountPerBlock
            ]
            |> String.Concat

        let blockHash =
            [
                ".......A" // blockNumber
                "B" // previousBlockHash
                "........" // configurationBlockNumber
                ".......C" // timestamp
                "D" // validator
                txSetRoot
                txResultSetRoot
                equivocationProofsRoot
                equivocationProofResultsRoot
                stateRoot
                stakingRewardRoot
                configRoot
            ]
            |> String.Concat

        // ACT
        let block =
            Blocks.assembleBlock
                DummyHash.decode
                DummyHash.create
                DummyHash.merkleTree
                proposerAddress
                blockNumber
                timestamp
                previousBlockHash
                configurationBlockNumber
                txSet
                equivocationProofs
                processingOutput
                (Some config)

        // ASSERT
        test <@ block.Header.Number = blockNumber @>
        test <@ block.Header.PreviousHash = previousBlockHash @>
        test <@ block.Header.Timestamp = timestamp @>
        test <@ block.Header.ProposerAddress = proposerAddress @>
        test <@ block.Header.TxSetRoot = MerkleTreeRoot txSetRoot @>
        test <@ block.Header.TxResultSetRoot = MerkleTreeRoot txResultSetRoot @>
        test <@ block.Header.EquivocationProofsRoot = MerkleTreeRoot equivocationProofsRoot @>
        test <@ block.Header.EquivocationProofResultsRoot = MerkleTreeRoot equivocationProofResultsRoot @>
        test <@ block.Header.StateRoot = MerkleTreeRoot stateRoot @>
        test <@ block.Header.StakingRewardsRoot = MerkleTreeRoot stakingRewardRoot @>
        test <@ block.Header.ConfigurationRoot = MerkleTreeRoot configRoot @>
        test <@ block.Header.Hash = BlockHash blockHash @>
        test <@ block.TxSet = [TxHash "AAA"; TxHash "BBB"; TxHash "CCC"] @>

    [<Fact>]
    let ``Blocks.assembleBlock and verify merkle proofs`` () =
        let wallet1 = Signing.generateWallet ()
        let wallet2 = Signing.generateWallet ()
        let proposerWallet = Signing.generateWallet ()
        let blockNumber = BlockNumber 1L
        let configurationBlockNumber = BlockNumber 0L
        let timestamp = Utils.getNetworkTimestamp () |> Timestamp

        let previousBlockHash =
            Signing.generateRandomBytes 64
            |> Hashing.hash
            |> BlockHash

        // TXs
        let txSet =
            ["Tx1"; "Tx2"; "Tx3"]
            |> List.map (Conversion.stringToBytes >> Hashing.hash >> TxHash)

        let txResult1 : TxResult = {
            Status = Success
            BlockNumber = BlockNumber 5L
        }

        let txResult2 : TxResult = {
            Status = (TxActionNumber 0s, TxErrorCode.InsufficientChxBalance) |> TxActionError |> Failure
            BlockNumber = BlockNumber 5L
        }

        let txResult3 : TxResult = {
            Status = Success
            BlockNumber = BlockNumber 5L
        }

        let txResults =
            [txResult1; txResult2; txResult3]
            |> List.zip txSet
            |> Map.ofList

        // Equivocation Proofs
        let equivocationProofs =
            ["Proof1"; "Proof2"]
            |> List.map (Conversion.stringToBytes >> Hashing.hash >> EquivocationProofHash)

        let equivocationProofResult1 : EquivocationProofResult = {
            DepositTaken = ChxAmount 6m
            DepositDistribution =
                [
                    {DistributedDeposit.ValidatorAddress = BlockchainAddress "AA"; Amount = ChxAmount 2m}
                    {DistributedDeposit.ValidatorAddress = BlockchainAddress "BB"; Amount = ChxAmount 2m}
                    {DistributedDeposit.ValidatorAddress = BlockchainAddress "CC"; Amount = ChxAmount 2m}
                ]
            BlockNumber = BlockNumber 5L
        }

        let equivocationProofResult2 : EquivocationProofResult = {
            DepositTaken = ChxAmount 0m
            DepositDistribution = []
            BlockNumber = BlockNumber 5L
        }

        let equivocationProofResults =
            [equivocationProofResult1; equivocationProofResult2]
            |> List.zip equivocationProofs
            |> Map.ofList

        // State Changes
        let chxAddresses =
            [
                wallet1.Address, {ChxAddressState.Nonce = Nonce 1L; Balance = ChxAmount 10m}
                wallet2.Address, {ChxAddressState.Nonce = Nonce 2L; Balance = ChxAmount 20m}
            ]
            |> Map.ofList

        let holdings =
            [
                (AccountHash "Acc1", AssetHash "Eq1"), {HoldingState.Balance = AssetAmount 100m; IsEmission = false}
                (AccountHash "Acc2", AssetHash "Eq2"), {HoldingState.Balance = AssetAmount 200m; IsEmission = false}
            ]
            |> Map.ofList

        let votes =
            let voteId1V =
                {
                    AccountHash = AccountHash "Acc1"
                    AssetHash = AssetHash "Eq1"
                    ResolutionHash = VotingResolutionHash "AAAA"
                }
            let voteId1W =
                {
                    AccountHash = AccountHash "Acc1"
                    AssetHash = AssetHash "Eq1"
                    ResolutionHash = VotingResolutionHash "BBBB"
                }
            let voteId1Y =
                {
                    AccountHash = AccountHash "Acc1"
                    AssetHash = AssetHash "Eq1"
                    ResolutionHash = VotingResolutionHash "CCCC"
                }
            let voteId2V =
                {
                    AccountHash = AccountHash "Acc2"
                    AssetHash = AssetHash "Eq2"
                    ResolutionHash = VotingResolutionHash "AAAA"
                }
            let voteId2W =
                {
                    AccountHash = AccountHash "Acc2"
                    AssetHash = AssetHash "Eq2"
                    ResolutionHash = VotingResolutionHash "BBBB"
                }
            let voteId2Y =
                {
                    AccountHash = AccountHash "Acc2"
                    AssetHash = AssetHash "Eq2"
                    ResolutionHash = VotingResolutionHash "CCCC"
                }
            [
                voteId1V, {VoteState.VoteHash = VoteHash "DA"; VoteWeight = 1m |> VoteWeight |> Some}
                voteId1W, {VoteState.VoteHash = VoteHash "DA"; VoteWeight = 1m |> VoteWeight |> Some}
                voteId1Y, {VoteState.VoteHash = VoteHash "AD"; VoteWeight = 1m |> VoteWeight |> Some}
                voteId2V, {VoteState.VoteHash = VoteHash "AD"; VoteWeight = 2m |> VoteWeight |> Some}
                voteId2W, {VoteState.VoteHash = VoteHash "AD"; VoteWeight = 2m |> VoteWeight |> Some}
                voteId2Y, {VoteState.VoteHash = VoteHash "DA"; VoteWeight = 2m |> VoteWeight |> Some}
            ]
            |> Map.ofList

        let eligibilities =
            [
                (AccountHash "DDD", AssetHash "EEE"),
                    {
                        EligibilityState.Eligibility = {IsPrimaryEligible = true; IsSecondaryEligible = true}
                        KycControllerAddress = BlockchainAddress "KK"
                    }
                (AccountHash "FFF", AssetHash "GGG"),
                    {
                        EligibilityState.Eligibility = {IsPrimaryEligible = true; IsSecondaryEligible = false}
                        KycControllerAddress = BlockchainAddress "KK"
                    }
            ]
            |> Map.ofList

        let kycProviders =
            [
                AssetHash "EEE",
                [
                    BlockchainAddress "AAA",
                    KycProviderChange.Add
                    BlockchainAddress "BBB",
                    KycProviderChange.Add
                ]
                |> Map.ofList

                AssetHash "FFF",
                [
                    BlockchainAddress "AAA",
                    KycProviderChange.Add
                ]
                |> Map.ofList

                AssetHash "GGG",
                [
                    BlockchainAddress "AAA",
                    KycProviderChange.Add
                ]
                |> Map.ofList
            ]
            |> Map.ofList

        let accounts =
            [
                AccountHash "AAAA", {AccountState.ControllerAddress = BlockchainAddress "BBBB"}
                AccountHash "CCCC", {AccountState.ControllerAddress = BlockchainAddress "DDDD"}
            ]
            |> Map.ofList

        let assets =
            [
                AssetHash "EEEE",
                {
                    AssetState.AssetCode = None
                    ControllerAddress = BlockchainAddress "FFFF"
                    IsEligibilityRequired = false
                }
                AssetHash "GGGG",
                {
                    AssetState.AssetCode = None
                    ControllerAddress = BlockchainAddress "HHHH"
                    IsEligibilityRequired = false
                }
            ]
            |> Map.ofList

        let validators =
            [
                BlockchainAddress "AAAAA",
                (
                    {
                        ValidatorState.NetworkAddress = NetworkAddress "WWW" // W = 87 = 7 = G
                        SharedRewardPercent = 1m
                        TimeToLockDeposit = 3s
                        TimeToBlacklist = 4s
                        IsEnabled = true
                    },
                    ValidatorChange.Add
                )
                BlockchainAddress "BBBBB",
                (
                    {
                        ValidatorState.NetworkAddress = NetworkAddress "XXX" // X = 88 = 8 = H
                        SharedRewardPercent = 2m
                        TimeToLockDeposit = 3s
                        TimeToBlacklist = 5s
                        IsEnabled = true
                    },
                    ValidatorChange.Add
                )
                BlockchainAddress "CCCCC",
                (
                    {
                        ValidatorState.NetworkAddress = NetworkAddress "YYY" // Y = 89 = 9 = I
                        SharedRewardPercent = 3m
                        TimeToLockDeposit = 3s
                        TimeToBlacklist = 6s
                        IsEnabled = true
                    },
                    ValidatorChange.Add
                )
            ]
            |> Map.ofList

        let stakes =
            [
                (BlockchainAddress "CC", BlockchainAddress "AAAAA"), {StakeState.Amount = ChxAmount 1m}
                (BlockchainAddress "DD", BlockchainAddress "BBBBB"), {StakeState.Amount = ChxAmount 2m}
            ]
            |> Map.ofList

        let stakingRewards =
            [
                BlockchainAddress "CC", ChxAmount 1m
                BlockchainAddress "DD", ChxAmount 2m
            ]
            |> Map.ofList

        let tradeOrders =
            [
                // TODO DSX
            ]
            |> Map.ofList

        let processingOutput =
            {
                ProcessingOutput.TxResults = txResults
                EquivocationProofResults = equivocationProofResults
                ChxAddresses = chxAddresses
                Holdings = holdings
                Votes = votes
                Eligibilities = eligibilities
                KycProviders = kycProviders
                Accounts = accounts
                Assets = assets
                Validators = validators
                Stakes = stakes
                StakingRewards = stakingRewards
                TradeOrders = tradeOrders
            }

        // ACT
        let block =
            Blocks.assembleBlock
                Hashing.decode
                Hashing.hash
                Hashing.merkleTree
                proposerWallet.Address
                blockNumber
                timestamp
                previousBlockHash
                configurationBlockNumber
                txSet
                equivocationProofs
                processingOutput
                None

        // ASSERT
        let stakingRewards =
            stakingRewards
            |> Map.toList
            |> List.map (fun (address, amount) ->
                {
                    StakingReward.StakerAddress = address
                    Amount = amount
                }
            )
            |> List.sortBy (fun r -> -r.Amount.Value, r.StakerAddress)

        test <@ block.Header.Number = blockNumber @>
        test <@ block.Header.PreviousHash = previousBlockHash @>
        test <@ block.Header.Timestamp = timestamp @>
        test <@ block.Header.ProposerAddress = proposerWallet.Address @>
        test <@ block.TxSet = txSet @>
        test <@ block.EquivocationProofs = equivocationProofs @>
        test <@ block.StakingRewards = stakingRewards @>
        test <@ block.Configuration = None @>

        let txSetMerkleProofs =
            txSet
            |> List.map (fun (TxHash h) -> h)
            |> Helpers.verifyMerkleProofs block.Header.TxSetRoot

        test <@ txSetMerkleProofs = List.replicate 3 true @>

        let txResultSetMerkleProofs =
            txSet
            |> List.map (fun h -> h, txResults.[h])
            |> List.map (Blocks.createTxResultHash Hashing.decode Hashing.hash)
            |> Helpers.verifyMerkleProofs block.Header.TxResultSetRoot

        test <@ txResultSetMerkleProofs = List.replicate 3 true @>

        let stateMerkleProofs =
            [
                chxAddresses
                |> Map.toList
                |> List.map (Blocks.createChxAddressStateHash Hashing.decode Hashing.hash)

                holdings
                |> Map.toList
                |> List.map (fun ((accountHash, assetHash), state) ->
                    Blocks.createHoldingStateHash Hashing.decode Hashing.hash (accountHash, assetHash, state)
                )

                votes
                |> Map.toList
                |> List.map (fun (voteId, state) ->
                    Blocks.createVoteStateHash
                        Hashing.decode
                        Hashing.hash
                        (voteId.AccountHash, voteId.AssetHash, voteId.ResolutionHash, state)
                )

                eligibilities
                |> Map.toList
                |> List.map (fun ((accountHash, assetHash), state) ->
                    Blocks.createEligibilityStateHash Hashing.decode Hashing.hash (accountHash, assetHash, state)
                )

                kycProviders
                |> Map.toList
                |> List.map (fun (hash, state) ->
                    Blocks.createKycProviderStateHash Hashing.decode Hashing.hash (hash, state)
                )

                accounts
                |> Map.toList
                |> List.map (Blocks.createAccountStateHash Hashing.decode Hashing.hash)

                assets
                |> Map.toList
                |> List.map (Blocks.createAssetStateHash Hashing.decode Hashing.hash)

                validators
                |> Map.toList
                |> List.map (Blocks.createValidatorStateHash Hashing.decode Hashing.hash)

                stakes
                |> Map.toList
                |> List.map (fun ((stakerAddress, validatorAddress), state) ->
                    (stakerAddress, validatorAddress, state)
                    |> Blocks.createStakeStateHash Hashing.decode Hashing.hash
                )
            ]
            |> List.concat
            |> Helpers.verifyMerkleProofs block.Header.StateRoot

        test <@ stateMerkleProofs = List.replicate 24 true @>

    [<Theory>]
    [<InlineData("RIGHT_PREVIOUS_BLOCK_HASH", true)>]
    [<InlineData("WRONG_PREVIOUS_BLOCK_HASH", false)>]
    let ``Blocks.isValidSuccessorBlock`` (previousBlockHashInTestedBlock, expectedSuccess) =
        let wallet1 = Signing.generateWallet ()
        let wallet2 = Signing.generateWallet ()
        let proposerWallet = Signing.generateWallet ()
        let blockNumber = BlockNumber 1L
        let configurationBlockNumber = BlockNumber 0L
        let timestamp = Utils.getNetworkTimestamp () |> Timestamp

        let previousBlockHash =
            "RIGHT_PREVIOUS_BLOCK_HASH"
            |> Conversion.stringToBytes
            |> Hashing.hash
            |> BlockHash

        // TXs
        let txSet =
            ["Tx1"; "Tx2"; "Tx3"]
            |> List.map (Conversion.stringToBytes >> Hashing.hash >> TxHash)

        let txResult1 : TxResult = {
            Status = Success
            BlockNumber = BlockNumber 5L
        }

        let txResult2 : TxResult = {
            Status = (TxActionNumber 0s, TxErrorCode.InsufficientChxBalance) |> TxActionError |> Failure
            BlockNumber = BlockNumber 5L
        }

        let txResult3 : TxResult = {
            Status = Success
            BlockNumber = BlockNumber 5L
        }

        let txResults =
            [txResult1; txResult2; txResult3]
            |> List.zip txSet
            |> Map.ofList

        // Equivocation Proofs
        let equivocationProofs =
            ["Proof1"; "Proof2"]
            |> List.map (Conversion.stringToBytes >> Hashing.hash >> EquivocationProofHash)

        let equivocationProofResult1 : EquivocationProofResult = {
            DepositTaken = ChxAmount 6m
            DepositDistribution =
                [
                    {DistributedDeposit.ValidatorAddress = BlockchainAddress "AA"; Amount = ChxAmount 2m}
                    {DistributedDeposit.ValidatorAddress = BlockchainAddress "BB"; Amount = ChxAmount 2m}
                    {DistributedDeposit.ValidatorAddress = BlockchainAddress "CC"; Amount = ChxAmount 2m}
                ]
            BlockNumber = BlockNumber 5L
        }

        let equivocationProofResult2 : EquivocationProofResult = {
            DepositTaken = ChxAmount 0m
            DepositDistribution = []
            BlockNumber = BlockNumber 5L
        }

        let equivocationProofResults =
            [equivocationProofResult1; equivocationProofResult2]
            |> List.zip equivocationProofs
            |> Map.ofList

        // State Changes
        let chxAddresses =
            [
                wallet1.Address, {ChxAddressState.Nonce = Nonce 1L; Balance = ChxAmount 10m}
                wallet2.Address, {ChxAddressState.Nonce = Nonce 2L; Balance = ChxAmount 20m}
            ]
            |> Map.ofList

        let holdings =
            [
                (AccountHash "Acc1", AssetHash "Eq1"), {HoldingState.Balance = AssetAmount 100m; IsEmission = false}
                (AccountHash "Acc2", AssetHash "Eq2"), {HoldingState.Balance = AssetAmount 200m; IsEmission = false}
            ]
            |> Map.ofList

        let votes =
            let voteId1V =
                {
                    AccountHash = AccountHash "Acc1"
                    AssetHash = AssetHash "Eq1"
                    ResolutionHash = VotingResolutionHash "AAAA"
                }
            let voteId1W =
                {
                    AccountHash = AccountHash "Acc1"
                    AssetHash = AssetHash "Eq1"
                    ResolutionHash = VotingResolutionHash "BBBB"
                }
            let voteId1Y =
                {
                    AccountHash = AccountHash "Acc1"
                    AssetHash = AssetHash "Eq1"
                    ResolutionHash = VotingResolutionHash "CCCC"
                }
            let voteId2V =
                {
                    AccountHash = AccountHash "Acc2"
                    AssetHash = AssetHash "Eq2"
                    ResolutionHash = VotingResolutionHash "AAAA"
                }
            let voteId2W =
                {
                    AccountHash = AccountHash "Acc2"
                    AssetHash = AssetHash "Eq2"
                    ResolutionHash = VotingResolutionHash "BBBB"
                }
            let voteId2Y =
                {
                    AccountHash = AccountHash "Acc2"
                    AssetHash = AssetHash "Eq2"
                    ResolutionHash = VotingResolutionHash "CCCC"
                }
            [
                voteId1V, {VoteState.VoteHash = VoteHash "DA"; VoteWeight = 1m |> VoteWeight |> Some}
                voteId1W, {VoteState.VoteHash = VoteHash "DA"; VoteWeight = 1m |> VoteWeight |> Some}
                voteId1Y, {VoteState.VoteHash = VoteHash "AD"; VoteWeight = 1m |> VoteWeight |> Some}
                voteId2V, {VoteState.VoteHash = VoteHash "AD"; VoteWeight = 2m |> VoteWeight |> Some}
                voteId2W, {VoteState.VoteHash = VoteHash "AD"; VoteWeight = 2m |> VoteWeight |> Some}
                voteId2Y, {VoteState.VoteHash = VoteHash "DA"; VoteWeight = 2m |> VoteWeight |> Some}
            ]
            |> Map.ofList

        let eligibilities =
            [
                (AccountHash "DDD", AssetHash "EEE"),
                    {
                        EligibilityState.Eligibility = {IsPrimaryEligible = true; IsSecondaryEligible = true}
                        KycControllerAddress = BlockchainAddress "KK"
                    }
                (AccountHash "FFF", AssetHash "GGG"),
                    {
                        EligibilityState.Eligibility = {IsPrimaryEligible = true; IsSecondaryEligible = false}
                        KycControllerAddress = BlockchainAddress "KK"
                    }
            ]
            |> Map.ofList

        let kycProviders =
            [
                AssetHash "EEE",
                [
                    BlockchainAddress "AAA",
                    KycProviderChange.Add
                    BlockchainAddress "BBB",
                    KycProviderChange.Add
                    BlockchainAddress "BBB",
                    KycProviderChange.Remove
                ]
                |> Map.ofList

                AssetHash "FFF",
                [
                    BlockchainAddress "AAA",
                    KycProviderChange.Add
                ]
                |> Map.ofList
            ]
            |> Map.ofList

        let accounts =
            [
                AccountHash "AAAA", {AccountState.ControllerAddress = BlockchainAddress "BBBB"}
                AccountHash "CCCC", {AccountState.ControllerAddress = BlockchainAddress "DDDD"}
            ]
            |> Map.ofList

        let assets =
            [
                AssetHash "EEEE",
                {
                    AssetState.AssetCode = None
                    ControllerAddress = BlockchainAddress "FFFF"
                    IsEligibilityRequired = false
                }
                AssetHash "GGGG",
                {
                    AssetState.AssetCode = None
                    ControllerAddress = BlockchainAddress "HHHH"
                    IsEligibilityRequired = false
                }
            ]
            |> Map.ofList

        let validators =
            [
                BlockchainAddress "AAAAA",
                (
                    {
                        ValidatorState.NetworkAddress = NetworkAddress "WWW" // W = 87 = 7 = G
                        SharedRewardPercent = 1m
                        TimeToLockDeposit = 3s
                        TimeToBlacklist = 4s
                        IsEnabled = true
                    },
                    ValidatorChange.Add
                )
                BlockchainAddress "BBBBB",
                (
                    {
                        ValidatorState.NetworkAddress = NetworkAddress "XXX" // X = 88 = 8 = H
                        SharedRewardPercent = 2m
                        TimeToLockDeposit = 3s
                        TimeToBlacklist = 5s
                        IsEnabled = true
                    },
                    ValidatorChange.Add
                )
                BlockchainAddress "CCCCC",
                (
                    {
                        ValidatorState.NetworkAddress = NetworkAddress "YYY" // Y = 89 = 9 = I
                        SharedRewardPercent = 3m
                        TimeToLockDeposit = 3s
                        TimeToBlacklist = 6s
                        IsEnabled = true
                    },
                    ValidatorChange.Add
                )
            ]
            |> Map.ofList

        let validatorSnapshots =
            [
                {
                    ValidatorSnapshot.ValidatorAddress = BlockchainAddress "AAAAA"
                    NetworkAddress = NetworkAddress "WWW" // W = 87 = 7 = G
                    SharedRewardPercent = 4m
                    TotalStake = ChxAmount 1m
                }
                {
                    ValidatorSnapshot.ValidatorAddress = BlockchainAddress "BBBBB"
                    NetworkAddress = NetworkAddress "XXX" // X = 88 = 8 = H
                    SharedRewardPercent = 5m
                    TotalStake = ChxAmount 2m
                }
            ]

        let stakes =
            [
                (BlockchainAddress "CC", BlockchainAddress "AAAAA"), {StakeState.Amount = ChxAmount 1m}
                (BlockchainAddress "DD", BlockchainAddress "BBBBB"), {StakeState.Amount = ChxAmount 2m}
            ]
            |> Map.ofList

        let stakingRewards =
            [
                BlockchainAddress "CC", ChxAmount 1m
                BlockchainAddress "DD", ChxAmount 2m
            ]
            |> Map.ofList

        let tradeOrders =
            [
                // TODO DSX
            ]
            |> Map.ofList

        let processingOutput =
            {
                ProcessingOutput.TxResults = txResults
                EquivocationProofResults = equivocationProofResults
                ChxAddresses = chxAddresses
                Holdings = holdings
                Votes = votes
                Eligibilities = eligibilities
                KycProviders = kycProviders
                Accounts = accounts
                Assets = assets
                Validators = validators
                Stakes = stakes
                StakingRewards = stakingRewards
                TradeOrders = tradeOrders
            }

        let assembledBlock =
            Blocks.assembleBlock
                Hashing.decode
                Hashing.hash
                Hashing.merkleTree
                proposerWallet.Address
                blockNumber
                timestamp
                previousBlockHash
                configurationBlockNumber
                txSet
                equivocationProofs
                processingOutput
                None

        let testedBlock =
            Blocks.assembleBlock
                Hashing.decode
                Hashing.hash
                Hashing.merkleTree
                proposerWallet.Address
                blockNumber
                timestamp
                (previousBlockHashInTestedBlock |> Conversion.stringToBytes |> Hashing.hash |> BlockHash)
                configurationBlockNumber
                txSet
                equivocationProofs
                processingOutput
                None

        // ACT
        let isValid =
            Blocks.isValidSuccessorBlock
                Hashing.decode
                Hashing.hash
                Hashing.merkleTree
                previousBlockHash
                testedBlock

        // ASSERT
        test <@ isValid = expectedSuccess @>
