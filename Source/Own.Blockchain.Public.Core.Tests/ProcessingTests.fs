namespace Own.Blockchain.Public.Core.Tests

open System
open Xunit
open Swensen.Unquote
open Own.Common.FSharp
open Own.Blockchain.Common
open Own.Blockchain.Public.Core
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Core.Dtos
open Own.Blockchain.Public.Crypto

module ProcessingTests =

    let accountHash1, accountHash2 =
        AccountHash "3dYWB8TyU17SFf3ZLZ7fpQxoQAneoxdn92XRf88ZdxYC",
        AccountHash "4NZXDMd2uKLTmkKVciu84pkSnzUtic6TKxD61grbGcm9"

    let assetHash1, assetHash2 =
        AssetHash "BPRi75qm2RYWa2QAtyGwyjDXp7BkS9jR1EWAmUqsdEsC",
        AssetHash "FnrfMcvwghb4qws7evxSTHdJ43aShxdRXWu3hZ8HX9wU"

    let resolutionHash1, resolutionHash2 =
        VotingResolutionHash "ETrivt162Fao3yEdsE1ZaBAdq9s6iRsGHQEBwDwUQYCd",
        VotingResolutionHash "CRjqV3DLh7jyCKZqj2pCdfw3s3ynXxEf5JMVm1rCYjmp"

    let voteHashYes, voteHashNo =
        VoteHash "AezU2ekUZu2h1D3UaW1PrGi2WkpBggdHf2iBGa4pGV4x",
        VoteHash "GiAukfYsxdyNBeh6N2DKVXEkZtASmjbciGfqpU5bSDmE"

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // TX preparation
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Fact>]
    let ``Processing.excludeUnprocessableTxs excludes TXs after nonce gap`` () =
        let w1 = Signing.generateWallet ()
        let w2 = Signing.generateWallet ()

        let getChxAddressState =
            let data =
                [
                    w1.Address, { ChxAddressState.Nonce = Nonce 10L; Balance = ChxAmount 100m }
                    w2.Address, { ChxAddressState.Nonce = Nonce 20L; Balance = ChxAmount 200m }
                ]
                |> Map.ofSeq

            fun (address : BlockchainAddress) -> data |> Map.tryFind address

        let getAvailableChxBalance address =
            match getChxAddressState address with
            | Some s -> s.Balance
            | None -> failwithf "getChxAddressState: Cannot get balance for address %s" address.Value

        let txSet =
            [
                Helpers.newPendingTxInfo (TxHash "Tx2") w1.Address (Nonce 12L) (ChxAmount 1m) 1s 2L
                Helpers.newPendingTxInfo (TxHash "Tx3") w1.Address (Nonce 10L) (ChxAmount 1m) 1s 3L
                Helpers.newPendingTxInfo (TxHash "Tx4") w1.Address (Nonce 14L) (ChxAmount 1m) 1s 4L
                Helpers.newPendingTxInfo (TxHash "Tx5") w1.Address (Nonce 11L) (ChxAmount 1m) 1s 5L
                Helpers.newPendingTxInfo (TxHash "Tx1") w2.Address (Nonce 21L) (ChxAmount 1m) 1s 1L
            ]

        // ACT
        let txHashes =
            txSet
            |> Processing.excludeUnprocessableTxs getChxAddressState getAvailableChxBalance
            |> List.map (fun tx -> tx.TxHash.Value)

        test <@ txHashes = ["Tx1"; "Tx2"; "Tx3"; "Tx5"] @>

    [<Theory>]
    [<InlineData(1, 0, "Tx1")>]
    [<InlineData(3, 0, "Tx1; Tx3")>]
    [<InlineData(4, 1, "Tx1; Tx3")>]
    [<InlineData(4, 0, "Tx1; Tx3; Tx5")>]
    [<InlineData(5, 1, "Tx1; Tx3; Tx5")>]
    let ``Processing.excludeUnprocessableTxs excludes TXs if CHX balance cannot cover the fees``
        (balance : decimal, staked : decimal, txHashes : string)
        =

        let balance = ChxAmount balance
        let expectedHashes = txHashes.Split("; ") |> Array.toList

        let w1 = Signing.generateWallet ()
        let w2 = Signing.generateWallet ()

        let getChxAddressState =
            let data =
                [
                    w1.Address, { ChxAddressState.Nonce = Nonce 10L; Balance = balance }
                    w2.Address, { ChxAddressState.Nonce = Nonce 20L; Balance = ChxAmount 200m }
                ]
                |> Map.ofSeq

            fun (address : BlockchainAddress) -> data |> Map.tryFind address

        let getAvailableChxBalance =
            let data =
                [
                    w1.Address, balance - staked
                    w2.Address, ChxAmount 200m
                ]
                |> Map.ofSeq

            fun (address : BlockchainAddress) -> data.[address]

        let txSet =
            [
                Helpers.newPendingTxInfo (TxHash "Tx2") w1.Address (Nonce 12L) (ChxAmount 1m) 1s 2L
                Helpers.newPendingTxInfo (TxHash "Tx3") w1.Address (Nonce 10L) (ChxAmount 1.5m) 2s 3L
                Helpers.newPendingTxInfo (TxHash "Tx4") w1.Address (Nonce 14L) (ChxAmount 1m) 1s 4L
                Helpers.newPendingTxInfo (TxHash "Tx5") w1.Address (Nonce 11L) (ChxAmount 1m) 1s 5L
                Helpers.newPendingTxInfo (TxHash "Tx1") w2.Address (Nonce 21L) (ChxAmount 1m) 1s 1L
            ]

        // ACT
        let txHashes =
            txSet
            |> Processing.excludeUnprocessableTxs getChxAddressState getAvailableChxBalance
            |> List.map (fun tx -> tx.TxHash.Value)

        test <@ txHashes = expectedHashes @>

    [<Fact>]
    let ``Processing.orderTxSet puts TXs in correct order`` () =
        let w1 = Signing.generateWallet ()
        let w2 = Signing.generateWallet ()

        let getChxAddressState =
            let data =
                [
                    w1.Address, { ChxAddressState.Nonce = Nonce 10L; Balance = ChxAmount 100m }
                    w2.Address, { ChxAddressState.Nonce = Nonce 20L; Balance = ChxAmount 200m }
                ]
                |> Map.ofSeq

            fun (address : BlockchainAddress) -> data.[address]

        let txSet =
            [
                Helpers.newPendingTxInfo (TxHash "Tx1") w2.Address (Nonce 21L) (ChxAmount 1m) 1s 1L
                Helpers.newPendingTxInfo (TxHash "Tx2") w1.Address (Nonce 12L) (ChxAmount 1m) 1s 2L
                Helpers.newPendingTxInfo (TxHash "Tx3") w1.Address (Nonce 10L) (ChxAmount 1m) 1s 3L
                Helpers.newPendingTxInfo (TxHash "Tx6") w2.Address (Nonce 21L) (ChxAmount 2m) 1s 6L
                Helpers.newPendingTxInfo (TxHash "Tx5") w1.Address (Nonce 11L) (ChxAmount 1m) 1s 5L
            ]

        // ACT
        let txHashes =
            txSet
            |> Processing.orderTxSet
            |> List.map (fun (TxHash hash) -> hash)

        test <@ txHashes = ["Tx6"; "Tx1"; "Tx3"; "Tx5"; "Tx2"] @>

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // TX level errors
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Fact>]
    let ``Processing.processChanges TX expired`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let recipientWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()

        let initialChxState =
            [
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = ChxAmount 9m}
                recipientWallet.Address, {ChxAddressState.Nonce = Nonce 20L; Balance = ChxAmount 100m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let actionFee = ChxAmount 1m
        let amountToTransfer = ChxAmount 5m

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
            |> Helpers.newTx senderWallet nonce (Timestamp 1L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                ValidatorAddress = validatorWallet.Address
                BlockTimestamp = Timestamp 2L
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - actionFee
        let recipientChxBalance = initialChxState.[recipientWallet.Address].Balance
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + actionFee
        let expectedStatus = TxErrorCode.TxExpired |> TxError |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>
        test <@ output.ChxAddresses.ContainsKey recipientWallet.Address = false @>
        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Reward distribution
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Fact>]
    let ``Processing.processChanges distributes rewards to stakers`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let recipientWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let staker1Wallet = Signing.generateWallet ()
        let staker2Wallet = Signing.generateWallet ()
        let staker3Wallet = Signing.generateWallet ()

        let initialChxState =
            [
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = ChxAmount 100m}
                recipientWallet.Address, {ChxAddressState.Nonce = Nonce 20L; Balance = ChxAmount 100m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
                staker1Wallet.Address, {ChxAddressState.Nonce = Nonce 0L; Balance = ChxAmount 1000m}
                staker2Wallet.Address, {ChxAddressState.Nonce = Nonce 0L; Balance = ChxAmount 1000m}
                staker3Wallet.Address, {ChxAddressState.Nonce = Nonce 0L; Balance = ChxAmount 1000m}
            ]
            |> Map.ofList

        let stake1 = ChxAmount 150m
        let stake2 = ChxAmount 100m
        let stake3 = ChxAmount 50m

        let actionFee = ChxAmount 1m
        let sharedRewardPercent = 60m
        let validatorReward = ChxAmount 0.4m
        let staker1Reward = ChxAmount 0.3m
        let staker2Reward = ChxAmount 0.2m
        let staker3Reward = ChxAmount 0.1m

        // PREPARE TX
        let nonce = Nonce 11L
        let amountToTransfer = ChxAmount 10m

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
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getTopStakers _ =
            [
                {StakerInfo.StakerAddress = staker1Wallet.Address; Amount = stake1}
                {StakerInfo.StakerAddress = staker2Wallet.Address; Amount = stake2}
                {StakerInfo.StakerAddress = staker3Wallet.Address; Amount = stake3}
            ]

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetTopStakers = getTopStakers
                ValidatorAddress = validatorWallet.Address
                SharedRewardPercent = sharedRewardPercent
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - amountToTransfer - actionFee
        let recipientChxBalance = initialChxState.[recipientWallet.Address].Balance + amountToTransfer
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + validatorReward
        let staker1ChxBalance = initialChxState.[staker1Wallet.Address].Balance + staker1Reward
        let staker2ChxBalance = initialChxState.[staker2Wallet.Address].Balance + staker2Reward
        let staker3ChxBalance = initialChxState.[staker3Wallet.Address].Balance + staker3Reward

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = Success @>

        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[recipientWallet.Address].Nonce = initialChxState.[recipientWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[staker1Wallet.Address].Nonce = initialChxState.[staker1Wallet.Address].Nonce @>
        test <@ output.ChxAddresses.[staker2Wallet.Address].Nonce = initialChxState.[staker2Wallet.Address].Nonce @>
        test <@ output.ChxAddresses.[staker3Wallet.Address].Nonce = initialChxState.[staker3Wallet.Address].Nonce @>

        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[recipientWallet.Address].Balance = recipientChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>
        test <@ output.ChxAddresses.[staker1Wallet.Address].Balance = staker1ChxBalance @>
        test <@ output.ChxAddresses.[staker2Wallet.Address].Balance = staker2ChxBalance @>
        test <@ output.ChxAddresses.[staker3Wallet.Address].Balance = staker3ChxBalance @>

    [<Fact>]
    let ``Processing.processChanges distributes rewards to stakers with proper decimalization`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let recipientWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let staker1Wallet = Signing.generateWallet ()
        let staker2Wallet = Signing.generateWallet ()
        let staker3Wallet = Signing.generateWallet ()

        let initialChxState =
            [
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = ChxAmount 100m}
                recipientWallet.Address, {ChxAddressState.Nonce = Nonce 20L; Balance = ChxAmount 100m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
                staker1Wallet.Address, {ChxAddressState.Nonce = Nonce 0L; Balance = ChxAmount 1000m}
                staker2Wallet.Address, {ChxAddressState.Nonce = Nonce 0L; Balance = ChxAmount 1000m}
                staker3Wallet.Address, {ChxAddressState.Nonce = Nonce 0L; Balance = ChxAmount 1000m}
            ]
            |> Map.ofList

        let stake1 = ChxAmount 100m
        let stake2 = ChxAmount 100m
        let stake3 = ChxAmount 100m

        let actionFee = ChxAmount 0.0000010m
        let sharedRewardPercent = 100m
        let validatorReward = ChxAmount 0.0000001m // Due to rounding
        let staker1Reward = ChxAmount 0.0000003m
        let staker2Reward = ChxAmount 0.0000003m
        let staker3Reward = ChxAmount 0.0000003m

        // PREPARE TX
        let nonce = Nonce 11L
        let amountToTransfer = ChxAmount 10m

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
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getTopStakers _ =
            [
                {StakerInfo.StakerAddress = staker1Wallet.Address; Amount = stake1}
                {StakerInfo.StakerAddress = staker2Wallet.Address; Amount = stake2}
                {StakerInfo.StakerAddress = staker3Wallet.Address; Amount = stake3}
            ]

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetTopStakers = getTopStakers
                ValidatorAddress = validatorWallet.Address
                SharedRewardPercent = sharedRewardPercent
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - amountToTransfer - actionFee
        let recipientChxBalance = initialChxState.[recipientWallet.Address].Balance + amountToTransfer
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + validatorReward
        let staker1ChxBalance = initialChxState.[staker1Wallet.Address].Balance + staker1Reward
        let staker2ChxBalance = initialChxState.[staker2Wallet.Address].Balance + staker2Reward
        let staker3ChxBalance = initialChxState.[staker3Wallet.Address].Balance + staker3Reward

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = Success @>

        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[recipientWallet.Address].Nonce = initialChxState.[recipientWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[staker1Wallet.Address].Nonce = initialChxState.[staker1Wallet.Address].Nonce @>
        test <@ output.ChxAddresses.[staker2Wallet.Address].Nonce = initialChxState.[staker2Wallet.Address].Nonce @>
        test <@ output.ChxAddresses.[staker3Wallet.Address].Nonce = initialChxState.[staker3Wallet.Address].Nonce @>

        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[recipientWallet.Address].Balance = recipientChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>
        test <@ output.ChxAddresses.[staker1Wallet.Address].Balance = staker1ChxBalance @>
        test <@ output.ChxAddresses.[staker2Wallet.Address].Balance = staker2ChxBalance @>
        test <@ output.ChxAddresses.[staker3Wallet.Address].Balance = staker3ChxBalance @>

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // TransferChx
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Fact>]
    let ``Processing.processChanges TransferChx`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let recipientWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()

        let initialChxState =
            [
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = ChxAmount 100m}
                recipientWallet.Address, {ChxAddressState.Nonce = Nonce 20L; Balance = ChxAmount 100m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let actionFee = ChxAmount 1m
        let amountToTransfer = ChxAmount 10m

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
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - amountToTransfer - actionFee
        let recipientChxBalance = initialChxState.[recipientWallet.Address].Balance + amountToTransfer
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + actionFee

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = Success @>
        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[recipientWallet.Address].Nonce = initialChxState.[recipientWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[recipientWallet.Address].Balance = recipientChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>

    [<Fact>]
    let ``Processing.processChanges TransferChx 100k times`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let recipientWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()

        let initialChxState =
            [
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 0L; Balance = ChxAmount 1000000m}
                recipientWallet.Address, {ChxAddressState.Nonce = Nonce 0L; Balance = ChxAmount 0m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 0L; Balance = ChxAmount 10000m}
            ]
            |> Map.ofList

        // PREPARE TX
        let actionFee = ChxAmount 0.001m
        let amountPerAction = ChxAmount 1m

        let txs =
            [
                for nonce in [1L .. 10L] ->
                    [
                        for _ in [1 .. 10000] ->
                            {
                                ActionType = "TransferChx"
                                ActionData =
                                    {
                                        RecipientAddress = recipientWallet.Address.Value
                                        Amount = amountPerAction.Value
                                    }
                            }
                    ]
                    |> Helpers.newTx senderWallet (Nonce nonce) (Timestamp 0L) actionFee
            ]

        let txSet = txs |> List.map fst

        // COMPOSE
        let getTx txHash =
            txs |> Map.ofList |> Map.find txHash |> Ok

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        // ACT
        let sw = System.Diagnostics.Stopwatch()
        sw.Start()
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                MaxActionCountPerTx = 10000
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges
        sw.Stop()

        // ASSERT
        Decimal.Round(100000m / decimal sw.ElapsedMilliseconds * 1000m).ToString()
        |> Log.successf "Processing speed: %s transfers per second"

        let totalAmountToTransfer = ChxAmount 100000m
        let totalFee = ChxAmount (actionFee.Value * 100000m)
        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - totalAmountToTransfer - totalFee
        let recipientChxBalance = initialChxState.[recipientWallet.Address].Balance + totalAmountToTransfer
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + totalFee

        test <@ output.TxResults.Count = 10 @>
        for txHash in txSet do
            test <@ output.TxResults.[txHash].Status = Success @>
        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = Nonce 10L @>
        test <@ output.ChxAddresses.[recipientWallet.Address].Nonce = initialChxState.[recipientWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[recipientWallet.Address].Balance = recipientChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>

    [<Fact>]
    let ``Processing.processChanges TransferChx with insufficient balance`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let recipientWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()

        let initialChxState =
            [
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = ChxAmount 9m}
                recipientWallet.Address, {ChxAddressState.Nonce = Nonce 20L; Balance = ChxAmount 100m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let actionFee = ChxAmount 1m
        let amountToTransfer = ChxAmount 10m

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
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - actionFee
        let recipientChxBalance = initialChxState.[recipientWallet.Address].Balance
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + actionFee
        let expectedStatus = (TxActionNumber 1s, TxErrorCode.InsufficientChxBalance) |> TxActionError |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>
        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[recipientWallet.Address].Nonce = initialChxState.[recipientWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[recipientWallet.Address].Balance = recipientChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>

    [<Fact>]
    let ``Processing.processChanges TransferChx fails with amount overflow`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let recipientWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()

        let initialChxState =
            [
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = ChxAmount 99_999_999_999m}
                recipientWallet.Address, {ChxAddressState.Nonce = Nonce 20L; Balance = ChxAmount 1000m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let actionFee = ChxAmount 1m
        let amountToTransfer = ChxAmount 99_999_999_990m

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
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let expectedStatus = (TxActionNumber 1s, TxErrorCode.ValueTooBig) |> TxActionError |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>

    [<Fact>]
    let ``Processing.processChanges TransferChx with insufficient balance to cover fee`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let recipientWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()

        let initialChxState =
            [
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = ChxAmount 10.5m}
                recipientWallet.Address, {ChxAddressState.Nonce = Nonce 20L; Balance = ChxAmount 100m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let actionFee = ChxAmount 1m
        let amountToTransfer = ChxAmount 10m

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
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - actionFee
        let recipientChxBalance = initialChxState.[recipientWallet.Address].Balance
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + actionFee
        let expectedStatus = (TxActionNumber 1s, TxErrorCode.InsufficientChxBalance) |> TxActionError |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>
        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[recipientWallet.Address].Nonce = initialChxState.[recipientWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[recipientWallet.Address].Balance = recipientChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>

    [<Fact>]
    let ``Processing.processChanges TransferChx - TX1 succeeds if it leaves enough CHX for TX2 fee`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let recipientWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()

        let initialChxState =
            [
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = ChxAmount 10m}
                recipientWallet.Address, {ChxAddressState.Nonce = Nonce 20L; Balance = ChxAmount 100m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        // PREPARE TX
        let actionFee = ChxAmount 0.01m

        let txHash1, txEnvelope1 =
            [
                {
                    ActionType = "TransferChx"
                    ActionData =
                        {
                            RecipientAddress = recipientWallet.Address.Value
                            Amount = 9.98m
                        }
                }
            ]
            |> Helpers.newTx senderWallet (Nonce 11L) (Timestamp 0L) actionFee

        let txHash2, txEnvelope2 =
            [
                {
                    ActionType = "TransferChx"
                    ActionData =
                        {
                            RecipientAddress = recipientWallet.Address.Value
                            Amount = 1m
                        }
                }
            ]
            |> Helpers.newTx senderWallet (Nonce 12L) (Timestamp 0L) actionFee

        let txSet = [txHash1; txHash2]

        // COMPOSE
        let getTx txHash =
            if txHash = txHash1 then
                Ok txEnvelope1
            elif txHash = txHash2 then
                Ok txEnvelope2
            else
                failwithf "Unexpected TX hash: %A" txHash

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let totalFee = actionFee * 2m
        let senderChxBalance = ChxAmount 0m
        let recipientChxBalance =
            initialChxState.[recipientWallet.Address].Balance
            + initialChxState.[senderWallet.Address].Balance
            - totalFee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + totalFee
        let expectedStatus1 = Success
        let expectedStatus2 = (TxActionNumber 1s, TxErrorCode.InsufficientChxBalance) |> TxActionError |> Failure

        test <@ output.TxResults.Count = 2 @>
        test <@ output.TxResults.[txHash1].Status = expectedStatus1 @>
        test <@ output.TxResults.[txHash2].Status = expectedStatus2 @>
        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = Nonce 12L @>
        test <@ output.ChxAddresses.[recipientWallet.Address].Nonce = initialChxState.[recipientWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[recipientWallet.Address].Balance = recipientChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>

    [<Fact>]
    let ``Processing.processChanges TransferChx - TX1 fails if it doesn't leave enough CHX for TX2 fee`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let recipientWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()

        let initialChxState =
            [
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = ChxAmount 10m}
                recipientWallet.Address, {ChxAddressState.Nonce = Nonce 20L; Balance = ChxAmount 100m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        // PREPARE TX
        let actionFee = ChxAmount 0.01m

        let txHash1, txEnvelope1 =
            [
                {
                    ActionType = "TransferChx"
                    ActionData =
                        {
                            RecipientAddress = recipientWallet.Address.Value
                            Amount = 9.99m
                        }
                }
            ]
            |> Helpers.newTx senderWallet (Nonce 11L) (Timestamp 0L) actionFee

        let txHash2, txEnvelope2 =
            [
                {
                    ActionType = "TransferChx"
                    ActionData =
                        {
                            RecipientAddress = recipientWallet.Address.Value
                            Amount = 1m
                        }
                }
            ]
            |> Helpers.newTx senderWallet (Nonce 12L) (Timestamp 0L) actionFee

        let txSet = [txHash1; txHash2]

        // COMPOSE
        let getTx txHash =
            if txHash = txHash1 then
                Ok txEnvelope1
            elif txHash = txHash2 then
                Ok txEnvelope2
            else
                failwithf "Unexpected TX hash: %A" txHash

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let totalFee = actionFee * 2m
        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - ChxAmount 1m - totalFee
        let recipientChxBalance = initialChxState.[recipientWallet.Address].Balance + ChxAmount 1m
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + totalFee
        let expectedStatus1 = (TxActionNumber 1s, TxErrorCode.InsufficientChxBalance) |> TxActionError |> Failure
        let expectedStatus2 = Success

        test <@ output.TxResults.Count = 2 @>
        test <@ output.TxResults.[txHash1].Status = expectedStatus1 @>
        test <@ output.TxResults.[txHash2].Status = expectedStatus2 @>
        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = Nonce 12L @>
        test <@ output.ChxAddresses.[recipientWallet.Address].Nonce = initialChxState.[recipientWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[recipientWallet.Address].Balance = recipientChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>

    [<Fact>]
    let ``Processing.processChanges TransferChx with insufficient balance to cover fee - simulated invalid state`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let recipientWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()

        let initialChxState =
            [
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = ChxAmount 0.5m}
                recipientWallet.Address, {ChxAddressState.Nonce = Nonce 20L; Balance = ChxAmount 100m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let actionFee = ChxAmount 1m
        let amountToTransfer = ChxAmount 10m

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
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let processChanges () =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        raisesWith<exn>
            <@ processChanges () @>
            (fun ex -> <@ ex.Message.StartsWith "Cannot process validator reward" @>)

    [<Fact>]
    let ``Processing.processChanges TransferChx with insufficient balance to cover fee due to the staked CHX`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let recipientWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()

        let initialChxState =
            [
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = ChxAmount 11m}
                recipientWallet.Address, {ChxAddressState.Nonce = Nonce 20L; Balance = ChxAmount 100m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let actionFee = ChxAmount 1m
        let amountToTransfer = ChxAmount 10m

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
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getTotalChxStaked address =
            if address = senderWallet.Address then
                ChxAmount 1m
            else
                ChxAmount 0m

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetTotalChxStakedFromStorage = getTotalChxStaked
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - actionFee
        let recipientChxBalance = initialChxState.[recipientWallet.Address].Balance
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + actionFee
        let expectedStatus = (TxActionNumber 1s, TxErrorCode.InsufficientChxBalance) |> TxActionError |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>
        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[recipientWallet.Address].Nonce = initialChxState.[recipientWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[recipientWallet.Address].Balance = recipientChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // TransferAsset
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Fact>]
    let ``Processing.processChanges TransferAsset`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let senderAccountHash = accountHash1
        let recipientAccountHash = accountHash2
        let assetHash = assetHash1

        let initialChxState =
            [
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = ChxAmount 100m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        let initialHoldingState =
            [
                (senderAccountHash, assetHash), {HoldingState.Balance = AssetAmount 50m; IsEmission = false}
                (recipientAccountHash, assetHash), {HoldingState.Balance = AssetAmount 0m; IsEmission = false}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let actionFee = ChxAmount 1m
        let amountToTransfer = AssetAmount 10m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "TransferAsset"
                    ActionData =
                        {
                            FromAccountHash = senderAccountHash.Value
                            ToAccountHash = recipientAccountHash.Value
                            AssetHash = assetHash.Value
                            Amount = amountToTransfer.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getHoldingState key =
            initialHoldingState |> Map.tryFind key

        let getAccountState _ =
            Some {AccountState.ControllerAddress = senderWallet.Address}

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = senderWallet.Address; IsEligibilityRequired = false}

        let getHoldingInTradeOrdersFromStorage _ =
            AssetAmount 0m

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetHoldingStateFromStorage = getHoldingState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                GetHoldingInTradeOrdersFromStorage = getHoldingInTradeOrdersFromStorage
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - actionFee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + actionFee
        let senderAssetBalance = initialHoldingState.[senderAccountHash, assetHash].Balance - amountToTransfer
        let recipientAssetBalance = initialHoldingState.[recipientAccountHash, assetHash].Balance + amountToTransfer

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = Success @>
        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>
        test <@ output.Holdings.[senderAccountHash, assetHash].Balance = senderAssetBalance @>
        test <@ output.Holdings.[recipientAccountHash, assetHash].Balance = recipientAssetBalance @>
        test <@ output.Holdings.[recipientAccountHash, assetHash].IsEmission = false @>

    [<Fact>]
    let ``Processing.processChanges TransferAsset, failure if amount too big`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let senderAccountHash = accountHash1
        let recipientAccountHash = accountHash2
        let assetHash = assetHash1

        let initialChxState =
            [
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = ChxAmount 100m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        let initialHoldingState =
            [
                (senderAccountHash, assetHash), {HoldingState.Balance = AssetAmount 99_999_999_999m; IsEmission = false}
                (recipientAccountHash, assetHash), {HoldingState.Balance = AssetAmount 999m; IsEmission = false}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let actionFee = ChxAmount 1m
        let amountToTransfer = AssetAmount 99_999_999_990m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "TransferAsset"
                    ActionData =
                        {
                            FromAccountHash = senderAccountHash.Value
                            ToAccountHash = recipientAccountHash.Value
                            AssetHash = assetHash.Value
                            Amount = amountToTransfer.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getHoldingState key =
            initialHoldingState |> Map.tryFind key

        let getAccountState _ =
            Some {AccountState.ControllerAddress = senderWallet.Address}

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = senderWallet.Address; IsEligibilityRequired = false}

        let getHoldingInTradeOrdersFromStorage _ =
            AssetAmount 0m

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetHoldingStateFromStorage = getHoldingState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                GetHoldingInTradeOrdersFromStorage = getHoldingInTradeOrdersFromStorage
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let expectedStatus =
            (TxActionNumber 1s, TxErrorCode.ValueTooBig)
            |> TxActionError
            |> Failure
        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>
        test <@ output.Holdings.[senderAccountHash, assetHash].Balance = AssetAmount 99_999_999_999m @>
        test <@ output.Holdings.[recipientAccountHash, assetHash].Balance = AssetAmount 999m @>

    [<Fact>]
    let ``Processing.processChanges TransferAsset success if transfer from emission to EligibleInPrimary`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let senderAccountHash = accountHash1
        let recipientAccountHash = accountHash2
        let assetHash = assetHash1

        let initialChxState =
            [
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = ChxAmount 100m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        let initialHoldingState =
            [
                (senderAccountHash, assetHash), {HoldingState.Balance = AssetAmount 50m; IsEmission = true}
                (recipientAccountHash, assetHash), {HoldingState.Balance = AssetAmount 0m; IsEmission = false}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let actionFee = ChxAmount 1m
        let amountToTransfer = AssetAmount 10m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "TransferAsset"
                    ActionData =
                        {
                            FromAccountHash = senderAccountHash.Value
                            ToAccountHash = recipientAccountHash.Value
                            AssetHash = assetHash.Value
                            Amount = amountToTransfer.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getHoldingState key =
            initialHoldingState |> Map.tryFind key

        let getEligibilityState _ =
            {
                EligibilityState.Eligibility = {IsPrimaryEligible = true; IsSecondaryEligible = false}
                KycControllerAddress = senderWallet.Address
            }
            |> Some

        let getAccountState _ =
            Some {AccountState.ControllerAddress = senderWallet.Address}

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = senderWallet.Address; IsEligibilityRequired = true}

        let getHoldingInTradeOrdersFromStorage _ =
            AssetAmount 0m

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetHoldingStateFromStorage = getHoldingState
                GetEligibilityStateFromStorage = getEligibilityState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                GetHoldingInTradeOrdersFromStorage = getHoldingInTradeOrdersFromStorage
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let senderAssetBalance = initialHoldingState.[senderAccountHash, assetHash].Balance - amountToTransfer
        let recipientAssetBalance = initialHoldingState.[recipientAccountHash, assetHash].Balance + amountToTransfer

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = Success @>
        test <@ output.Holdings.[senderAccountHash, assetHash].Balance = senderAssetBalance @>
        test <@ output.Holdings.[recipientAccountHash, assetHash].Balance = recipientAssetBalance @>

    [<Fact>]
    let ``Processing.processChanges TransferAsset success if transfer from non-emission to EligibleInSecondary`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let senderAccountHash = accountHash1
        let recipientAccountHash = accountHash2
        let assetHash = assetHash1

        let initialChxState =
            [
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = ChxAmount 100m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        let initialHoldingState =
            [
                (senderAccountHash, assetHash), {HoldingState.Balance = AssetAmount 50m; IsEmission = false}
                (recipientAccountHash, assetHash), {HoldingState.Balance = AssetAmount 0m; IsEmission = false}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let actionFee = ChxAmount 1m
        let amountToTransfer = AssetAmount 10m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "TransferAsset"
                    ActionData =
                        {
                            FromAccountHash = senderAccountHash.Value
                            ToAccountHash = recipientAccountHash.Value
                            AssetHash = assetHash.Value
                            Amount = amountToTransfer.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getHoldingState key =
            initialHoldingState |> Map.tryFind key

        let getEligibilityState _ =
            {
                EligibilityState.Eligibility = {IsPrimaryEligible = true; IsSecondaryEligible = true}
                KycControllerAddress = senderWallet.Address
            }
            |> Some

        let getAccountState _ =
            Some {AccountState.ControllerAddress = senderWallet.Address}

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = senderWallet.Address; IsEligibilityRequired = true}

        let getHoldingInTradeOrdersFromStorage _ =
            AssetAmount 0m

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetHoldingStateFromStorage = getHoldingState
                GetEligibilityStateFromStorage = getEligibilityState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                GetHoldingInTradeOrdersFromStorage = getHoldingInTradeOrdersFromStorage
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let senderAssetBalance = initialHoldingState.[senderAccountHash, assetHash].Balance - amountToTransfer
        let recipientAssetBalance = initialHoldingState.[recipientAccountHash, assetHash].Balance + amountToTransfer

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = Success @>
        test <@ output.Holdings.[senderAccountHash, assetHash].Balance = senderAssetBalance @>
        test <@ output.Holdings.[recipientAccountHash, assetHash].Balance = recipientAssetBalance @>

    [<Fact>]
    let ``Processing.processChanges TransferAsset fails if transfer from emission to NotEligibleInPrimary`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let senderAccountHash = accountHash1
        let recipientAccountHash = accountHash2
        let assetHash = assetHash1

        let initialChxState =
            [
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = ChxAmount 100m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        let initialHoldingState =
            [
                (senderAccountHash, assetHash), {HoldingState.Balance = AssetAmount 50m; IsEmission = true}
                (recipientAccountHash, assetHash), {HoldingState.Balance = AssetAmount 0m; IsEmission = false}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let actionFee = ChxAmount 1m
        let amountToTransfer = AssetAmount 10m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "TransferAsset"
                    ActionData =
                        {
                            FromAccountHash = senderAccountHash.Value
                            ToAccountHash = recipientAccountHash.Value
                            AssetHash = assetHash.Value
                            Amount = amountToTransfer.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getHoldingState key =
            initialHoldingState |> Map.tryFind key

        let getEligibilityState _ =
            {
                EligibilityState.Eligibility = {IsPrimaryEligible = false; IsSecondaryEligible = true}
                KycControllerAddress = senderWallet.Address
            }
            |> Some

        let getAccountState _ =
            Some {AccountState.ControllerAddress = senderWallet.Address}

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = senderWallet.Address; IsEligibilityRequired = true}

        let getHoldingInTradeOrdersFromStorage _ =
            AssetAmount 0m

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetHoldingStateFromStorage = getHoldingState
                GetEligibilityStateFromStorage = getEligibilityState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                GetHoldingInTradeOrdersFromStorage = getHoldingInTradeOrdersFromStorage
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let expectedStatus =
            (TxActionNumber 1s, TxErrorCode.NotEligibleInPrimary)
            |> TxActionError
            |> Failure
        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>
        test <@ output.Holdings.[senderAccountHash, assetHash].Balance = AssetAmount 50m @>
        test <@ output.Holdings.[recipientAccountHash, assetHash].Balance = AssetAmount 0m @>

    [<Fact>]
    let ``Processing.processChanges TransferAsset fails if transfer from non-emission to NotEligibleInSecondary`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let senderAccountHash = accountHash1
        let recipientAccountHash = accountHash2
        let assetHash = assetHash1

        let initialChxState =
            [
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = ChxAmount 100m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        let initialHoldingState =
            [
                (senderAccountHash, assetHash), {HoldingState.Balance = AssetAmount 50m; IsEmission = false}
                (recipientAccountHash, assetHash), {HoldingState.Balance = AssetAmount 0m; IsEmission = false}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let actionFee = ChxAmount 1m
        let amountToTransfer = AssetAmount 10m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "TransferAsset"
                    ActionData =
                        {
                            FromAccountHash = senderAccountHash.Value
                            ToAccountHash = recipientAccountHash.Value
                            AssetHash = assetHash.Value
                            Amount = amountToTransfer.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getHoldingState key =
            initialHoldingState |> Map.tryFind key

        let getEligibilityState _ =
            {
                EligibilityState.Eligibility = {IsPrimaryEligible = true; IsSecondaryEligible = false}
                KycControllerAddress = senderWallet.Address
            }
            |> Some

        let getAccountState _ =
            Some {AccountState.ControllerAddress = senderWallet.Address}

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = senderWallet.Address; IsEligibilityRequired = true}

        let getHoldingInTradeOrdersFromStorage _ =
            AssetAmount 0m

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetHoldingStateFromStorage = getHoldingState
                GetEligibilityStateFromStorage = getEligibilityState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                GetHoldingInTradeOrdersFromStorage = getHoldingInTradeOrdersFromStorage
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let expectedStatus =
            (TxActionNumber 1s, TxErrorCode.NotEligibleInSecondary)
            |> TxActionError
            |> Failure
        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>
        test <@ output.Holdings.[senderAccountHash, assetHash].Balance = AssetAmount 50m @>
        test <@ output.Holdings.[recipientAccountHash, assetHash].Balance = AssetAmount 0m @>

    [<Fact>]
    let ``Processing.processChanges TransferAsset with insufficient balance`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let senderAccountHash = accountHash1
        let recipientAccountHash = accountHash2
        let assetHash = assetHash1

        let initialChxState =
            [
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = ChxAmount 100m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        let initialHoldingState =
            [
                (senderAccountHash, assetHash), {HoldingState.Balance = AssetAmount 9m; IsEmission = false}
                (recipientAccountHash, assetHash), {HoldingState.Balance = AssetAmount 0m; IsEmission = false}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let actionFee = ChxAmount 1m
        let amountToTransfer = AssetAmount 10m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "TransferAsset"
                    ActionData =
                        {
                            FromAccountHash = senderAccountHash.Value
                            ToAccountHash = recipientAccountHash.Value
                            AssetHash = assetHash.Value
                            Amount = amountToTransfer.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getHoldingState key =
            initialHoldingState |> Map.tryFind key

        let getAccountState _ =
            Some {AccountState.ControllerAddress = senderWallet.Address}

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = senderWallet.Address; IsEligibilityRequired = false}

        let getHoldingInTradeOrdersFromStorage _ =
            AssetAmount 0m

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetHoldingStateFromStorage = getHoldingState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                GetHoldingInTradeOrdersFromStorage = getHoldingInTradeOrdersFromStorage
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - actionFee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + actionFee
        let senderAssetBalance = initialHoldingState.[senderAccountHash, assetHash].Balance
        let recipientAssetBalance = initialHoldingState.[recipientAccountHash, assetHash].Balance
        let expectedStatus =
            (TxActionNumber 1s, TxErrorCode.InsufficientAssetHoldingBalance)
            |> TxActionError
            |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>
        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>
        test <@ output.Holdings.[senderAccountHash, assetHash].Balance = senderAssetBalance @>
        test <@ output.Holdings.[recipientAccountHash, assetHash].Balance = recipientAssetBalance @>

    [<Fact>]
    let ``Processing.processChanges TransferAsset fails if source account not found`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let senderAccountHash = accountHash1
        let recipientAccountHash = accountHash2
        let assetHash = assetHash1

        let initialChxState =
            [
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = ChxAmount 100m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        let initialHoldingState =
            [
                (senderAccountHash, assetHash), {HoldingState.Balance = AssetAmount 9m; IsEmission = false}
                (recipientAccountHash, assetHash), {HoldingState.Balance = AssetAmount 0m; IsEmission = false}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let actionFee = ChxAmount 1m
        let amountToTransfer = AssetAmount 10m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "TransferAsset"
                    ActionData =
                        {
                            FromAccountHash = senderAccountHash.Value
                            ToAccountHash = recipientAccountHash.Value
                            AssetHash = assetHash.Value
                            Amount = amountToTransfer.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getHoldingState key =
            initialHoldingState |> Map.tryFind key

        let getAccountState accountHash =
            if accountHash = recipientAccountHash then
                Some {AccountState.ControllerAddress = senderWallet.Address}
            else
                None

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetHoldingStateFromStorage = getHoldingState
                GetAccountStateFromStorage = getAccountState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - actionFee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + actionFee
        let expectedStatus =
            (TxActionNumber 1s, TxErrorCode.SourceAccountNotFound)
            |> TxActionError
            |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>
        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>
        test <@ output.Holdings = Map.empty @>

    [<Fact>]
    let ``Processing.processChanges TransferAsset fails if destination account not found`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let senderAccountHash = accountHash1
        let recipientAccountHash = accountHash2
        let assetHash = assetHash1

        let initialChxState =
            [
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = ChxAmount 100m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        let initialHoldingState =
            [
                (senderAccountHash, assetHash), {HoldingState.Balance = AssetAmount 9m; IsEmission = false}
                (recipientAccountHash, assetHash), {HoldingState.Balance = AssetAmount 0m; IsEmission = false}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let actionFee = ChxAmount 1m
        let amountToTransfer = AssetAmount 10m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "TransferAsset"
                    ActionData =
                        {
                            FromAccountHash = senderAccountHash.Value
                            ToAccountHash = recipientAccountHash.Value
                            AssetHash = assetHash.Value
                            Amount = amountToTransfer.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getHoldingState key =
            initialHoldingState |> Map.tryFind key

        let getAccountState accountHash =
            if accountHash = senderAccountHash then
                Some {AccountState.ControllerAddress = senderWallet.Address}
            else
                None

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetHoldingStateFromStorage = getHoldingState
                GetAccountStateFromStorage = getAccountState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - actionFee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + actionFee
        let expectedStatus =
            (TxActionNumber 1s, TxErrorCode.DestinationAccountNotFound)
            |> TxActionError
            |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>
        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>
        test <@ output.Holdings = Map.empty @>

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // SubmitVote
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Fact>]
    let ``Processing.processChanges SubmitVote success insert`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let accountHash = accountHash1
        let assetHash = assetHash1
        let resolutionHash = resolutionHash1
        let voteHash = voteHashYes

        let initialChxState =
            [
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = ChxAmount 100m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        let initialHoldingState =
            [
                (accountHash, assetHash), {HoldingState.Balance = AssetAmount 50m; IsEmission = false}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let actionFee = ChxAmount 1m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "SubmitVote"
                    ActionData =
                        {
                            AccountHash = accountHash.Value
                            AssetHash = assetHash.Value
                            ResolutionHash = resolutionHash.Value
                            VoteHash = voteHash.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getHoldingState key =
            initialHoldingState |> Map.tryFind key

        let getVoteState _ =
            None

        let getAccountState _ =
            Some {AccountState.ControllerAddress = senderWallet.Address}

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = senderWallet.Address; IsEligibilityRequired = false}

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetHoldingStateFromStorage = getHoldingState
                GetVoteStateFromStorage = getVoteState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT

        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - actionFee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + actionFee

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = Success @>

        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>

        test <@ output.Votes.Count = 1 @>
        test <@ output.Votes.[{AccountHash = accountHash; AssetHash = assetHash; ResolutionHash = resolutionHash}]
            = {VoteHash = voteHash; VoteWeight = None} @>

    [<Fact>]
    let ``Processing.processChanges SubmitVote success insert and update unweighted`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let accountHash = accountHash1
        let assetHash = assetHash1

        let initialChxState =
            [
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = ChxAmount 100m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        let initialHoldingState =
            [
                (accountHash, assetHash), {HoldingState.Balance = AssetAmount 50m; IsEmission = false}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce1 = Nonce 11L
        let nonce2 = nonce1 + 1L

        let actionFee = ChxAmount 1m

        // Vote Yes on RS1
        let txHash1, txEnvelope1 =
            [
                {
                    ActionType = "SubmitVote"
                    ActionData =
                        {
                            AccountHash = accountHash.Value
                            AssetHash = assetHash.Value
                            ResolutionHash = resolutionHash1.Value
                            VoteHash = voteHashYes.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce1 (Timestamp 0L) actionFee

        // Vote Yes on RS2
        let txHash2, txEnvelope2 =
            [
                {
                    ActionType = "SubmitVote"
                    ActionData =
                        {
                            AccountHash = accountHash.Value
                            AssetHash = assetHash.Value
                            ResolutionHash = resolutionHash2.Value
                            VoteHash = voteHashYes.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce2 (Timestamp 0L) actionFee

        let txSet = [txHash1; txHash2]

        // COMPOSE
        let getTx txHash =
            if txHash = txHash1 then Ok txEnvelope1
            elif txHash = txHash2 then Ok txEnvelope2
            else Result.appError "Invalid TX hash"

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getHoldingState key =
            initialHoldingState |> Map.tryFind key

        let getVoteState (voteId: VoteId) =
            // No vote for rsh1
            if voteId.ResolutionHash = resolutionHash1 then
                None
            // Existing non weighted vote for rsh2
            elif voteId.ResolutionHash = resolutionHash2 then
                Some {VoteState.VoteHash = voteHashNo; VoteWeight = None}
            else
                None

        let getAccountState _ =
            Some {AccountState.ControllerAddress = senderWallet.Address}

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = senderWallet.Address; IsEligibilityRequired = false}

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetHoldingStateFromStorage = getHoldingState
                GetVoteStateFromStorage = getVoteState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        test <@ output.TxResults.Count = 2 @>
        test <@ output.TxResults.[txHash1].Status = Success @>
        test <@ output.TxResults.[txHash2].Status = Success @>
        test <@ output.Votes.Count = 2 @>
        test <@ output.Votes.[{AccountHash = accountHash; AssetHash = assetHash; ResolutionHash = resolutionHash1}]
            = {VoteHash = voteHashYes; VoteWeight = None} @>
        test <@ output.Votes.[{AccountHash = accountHash; AssetHash = assetHash; ResolutionHash = resolutionHash2}]
            = {VoteHash = voteHashYes; VoteWeight = None} @>

    [<Fact>]
    let ``Processing.processChanges SubmitVote fails if no holding`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let accountHash = accountHash1
        let assetHash = assetHash1
        let resolutionHash = resolutionHash1
        let voteHash = voteHashYes

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
                    ActionType = "SubmitVote"
                    ActionData =
                        {
                            AccountHash = accountHash.Value
                            AssetHash = assetHash.Value
                            ResolutionHash = resolutionHash.Value
                            VoteHash = voteHash.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getHoldingState _ =
            None

        let getVoteState _ =
            None

        let getAccountState _ =
            Some {AccountState.ControllerAddress = senderWallet.Address}

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = senderWallet.Address; IsEligibilityRequired = false}

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetHoldingStateFromStorage = getHoldingState
                GetVoteStateFromStorage = getVoteState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let expectedStatus =
            (TxActionNumber 1s, TxErrorCode.HoldingNotFound) |> TxActionError |> Failure
        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>

        test <@ output.Votes = Map.empty @>

    [<Fact>]
    let ``Processing.processChanges SubmitVote fails if asset or account not found`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let otherWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()

        let resolutionHash = resolutionHash1
        let voteHash = voteHashYes

        let initialChxState =
            [
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = ChxAmount 100m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let actionFee = ChxAmount 1m

        let txHash1, txEnvelope1 =
            [
                {
                    ActionType = "SubmitVote"
                    ActionData =
                        {
                            AccountHash = accountHash1.Value
                            AssetHash = assetHash1.Value
                            ResolutionHash = resolutionHash.Value
                            VoteHash = voteHash.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txHash2, txEnvelope2 =
            [
                {
                    ActionType = "SubmitVote"
                    ActionData =
                        {
                            AccountHash = accountHash2.Value
                            AssetHash = assetHash2.Value
                            ResolutionHash = resolutionHash.Value
                            VoteHash = voteHash.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet (nonce + 1) (Timestamp 0L) actionFee

        let txSet = [txHash1; txHash2]

        // COMPOSE
        let getTx txHash =
            if txHash = txHash1 then Ok txEnvelope1
            elif txHash = txHash2 then Ok txEnvelope2
            else Result.appError "Invalid TX hash"

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getVoteState _ =
            None

        let getAccountState accountHash =
            if accountHash = accountHash1 then
                Some {AccountState.ControllerAddress = senderWallet.Address}
            elif accountHash = accountHash2 then
                None
            else
                None

        let getAssetState assetHash =
            if assetHash = assetHash1 then
                None
            elif assetHash = assetHash2 then
                {
                    AssetState.AssetCode = None
                    ControllerAddress = senderWallet.Address
                    IsEligibilityRequired = false
                }
                |> Some
            else
                None

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetVoteStateFromStorage = getVoteState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let expectedStatusTxHash1 =
            (TxActionNumber 1s, TxErrorCode.AssetNotFound) |> TxActionError |> Failure
        let expectedStatusTxHash2 =
            (TxActionNumber 1s, TxErrorCode.AccountNotFound) |> TxActionError |> Failure

        test <@ output.TxResults.Count = 2 @>
        test <@ output.TxResults.[txHash1].Status = expectedStatusTxHash1 @>
        test <@ output.TxResults.[txHash2].Status = expectedStatusTxHash2 @>
        test <@ output.Votes = Map.empty @>

    [<Fact>]
    let ``Processing.processChanges SubmitVote fails if sender is not account controller`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let otherWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let accountHash = accountHash1
        let assetHash = assetHash1

        let resolutionHash = resolutionHash1
        let voteHash = voteHashYes

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
                    ActionType = "SubmitVote"
                    ActionData =
                        {
                            AccountHash = accountHash.Value
                            AssetHash = assetHash.Value
                            ResolutionHash = resolutionHash.Value
                            VoteHash = voteHash.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getVoteState _ =
            None

        let getAccountState _ =
            Some {AccountState.ControllerAddress = otherWallet.Address}

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = senderWallet.Address; IsEligibilityRequired = false}

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetVoteStateFromStorage = getVoteState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let expectedStatus =
            (TxActionNumber 1s, TxErrorCode.SenderIsNotSourceAccountController) |> TxActionError |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>
        test <@ output.Votes = Map.empty @>

    [<Fact>]
    let ``Processing.processChanges SubmitVote fails if vote is already weighted`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let otherWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let accountHash = accountHash1
        let assetHash = assetHash1

        let resolutionHash = resolutionHash1
        let voteHashYes = voteHashYes
        let voteHashNo = voteHashNo

        let initialChxState =
            [
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = ChxAmount 100m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        let initialHoldingState =
            [
                (accountHash, assetHash), {HoldingState.Balance = AssetAmount 50m; IsEmission = false}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let actionFee = ChxAmount 1m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "SubmitVote"
                    ActionData =
                        {
                            AccountHash = accountHash.Value
                            AssetHash = assetHash.Value
                            ResolutionHash = resolutionHash.Value
                            VoteHash = voteHashYes.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getHoldingState key =
            initialHoldingState |> Map.tryFind key

        let getVoteState _ =
            Some {VoteState.VoteHash = voteHashNo; VoteWeight = 1m |> VoteWeight |> Some}

        let getAccountState _ =
            Some {AccountState.ControllerAddress = senderWallet.Address}

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = senderWallet.Address; IsEligibilityRequired = false}

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetHoldingStateFromStorage = getHoldingState
                GetVoteStateFromStorage = getVoteState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let expectedStatus =
            (TxActionNumber 1s, TxErrorCode.VoteIsAlreadyWeighted) |> TxActionError |> Failure

        let voteId = {AccountHash = accountHash; AssetHash = assetHash; ResolutionHash = resolutionHash}
        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>
        test <@ output.Votes.Count = 1 @>
        test <@ output.Votes.[voteId].VoteHash = voteHashNo @>

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // SubmitVoteWeight
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Fact>]
    let ``Processing.processChanges SubmitVoteWeight success update`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let accountHash = accountHash1
        let assetHash = assetHash1
        let resolutionHash = resolutionHash1
        let voteHash = voteHashYes
        let voteWeight = VoteWeight 1m

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
                    ActionType = "SubmitVoteWeight"
                    ActionData =
                        {
                            AccountHash = accountHash.Value
                            AssetHash = assetHash.Value
                            ResolutionHash = resolutionHash.Value
                            VoteWeight = voteWeight.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getVoteState _ =
            Some {VoteState.VoteHash = voteHash; VoteWeight = None}

        let getAccountState _ =
            Some {AccountState.ControllerAddress = senderWallet.Address}

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = senderWallet.Address; IsEligibilityRequired = false}

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetVoteStateFromStorage = getVoteState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT

        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - actionFee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + actionFee

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = Success @>

        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>

        test <@ output.Votes.Count = 1 @>
        test <@ output.Votes.[{AccountHash = accountHash; AssetHash = assetHash; ResolutionHash = resolutionHash}]
            = {VoteHash = voteHash; VoteWeight = Some voteWeight} @>

    [<Fact>]
    let ``Processing.processChanges SubmitVoteWeight fails if vote not found`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let accountHash = accountHash1
        let assetHash = assetHash1
        let resolutionHash = resolutionHash1
        let voteHash = voteHashYes
        let voteWeight = VoteWeight 1m

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
                    ActionType = "SubmitVoteWeight"
                    ActionData =
                        {
                            AccountHash = accountHash.Value
                            AssetHash = assetHash.Value
                            ResolutionHash = resolutionHash.Value
                            VoteWeight = voteWeight.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getVoteState _ =
            None

        let getAccountState _ =
            Some {AccountState.ControllerAddress = senderWallet.Address}

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = senderWallet.Address; IsEligibilityRequired = false}

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetVoteStateFromStorage = getVoteState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let expectedStatus = (TxActionNumber 1s, TxErrorCode.VoteNotFound) |> TxActionError |> Failure
        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>
        test <@ output.Votes = Map.empty @>

    [<Fact>]
    let ``Processing.processChanges SubmitVoteWeight fails is sender is not asset controller`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let otherWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let accountHash = accountHash1
        let assetHash = assetHash1
        let resolutionHash = resolutionHash1
        let voteHash = voteHashYes
        let voteWeight = VoteWeight 1m

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
                    ActionType = "SubmitVoteWeight"
                    ActionData =
                        {
                            AccountHash = accountHash.Value
                            AssetHash = assetHash.Value
                            ResolutionHash = resolutionHash.Value
                            VoteWeight = voteWeight.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getVoteState _ =
            Some {VoteState.VoteHash = voteHash; VoteWeight = None}

        let getAccountState _ =
            Some {AccountState.ControllerAddress = senderWallet.Address}

        let getAssetState _ =
            {
                AssetState.AssetCode = None
                ControllerAddress = otherWallet.Address
                IsEligibilityRequired = false
            }
            |> Some

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetVoteStateFromStorage = getVoteState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let expectedStatus = (TxActionNumber 1s, TxErrorCode.SenderIsNotAssetController) |> TxActionError |> Failure
        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>
        test <@ output.Votes = Map.empty @>

    [<Fact>]
    let ``Processing.processChanges SubmitVoteWeight if asset or account not found`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let otherWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()

        let resolutionHash = resolutionHash1
        let voteWeight = VoteWeight 1m

        let initialChxState =
            [
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = ChxAmount 100m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let actionFee = ChxAmount 1m

        let txHash1, txEnvelope1 =
            [
                {
                    ActionType = "SubmitVoteWeight"
                    ActionData =
                        {
                            AccountHash = accountHash1.Value
                            AssetHash = assetHash1.Value
                            ResolutionHash = resolutionHash.Value
                            VoteWeight = voteWeight.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txHash2, txEnvelope2 =
            [
                {
                    ActionType = "SubmitVoteWeight"
                    ActionData =
                        {
                            AccountHash = accountHash2.Value
                            AssetHash = assetHash2.Value
                            ResolutionHash = resolutionHash.Value
                            VoteWeight = voteWeight.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet (nonce + 1) (Timestamp 0L) actionFee

        let txSet = [txHash1; txHash2]

        // COMPOSE
        let getTx txHash =
            if txHash = txHash1 then Ok txEnvelope1
            elif txHash = txHash2 then Ok txEnvelope2
            else Result.appError "Invalid TX hash"

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getVoteState _ =
            None

        let getAccountState accountHash =
            if accountHash = accountHash1 then
                Some {AccountState.ControllerAddress = senderWallet.Address}
            elif accountHash = accountHash2 then
                None
            else
                None

        let getAssetState assetHash =
            if assetHash = assetHash1 then
                None
            elif assetHash = assetHash2 then
                {
                    AssetState.AssetCode = None
                    ControllerAddress = senderWallet.Address
                    IsEligibilityRequired = false
                }
                |> Some
            else
                None

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetVoteStateFromStorage = getVoteState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let expectedStatusTxHash1 =
            (TxActionNumber 1s, TxErrorCode.AssetNotFound) |> TxActionError |> Failure
        let expectedStatusTxHash2 =
            (TxActionNumber 1s, TxErrorCode.AccountNotFound) |> TxActionError |> Failure

        test <@ output.TxResults.Count = 2 @>
        test <@ output.TxResults.[txHash1].Status = expectedStatusTxHash1 @>
        test <@ output.TxResults.[txHash2].Status = expectedStatusTxHash2 @>
        test <@ output.Votes = Map.empty @>

    [<Fact>]
    let ``Processing.processChanges SubmitVote and SubmitVoteWeight separate Tx`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let accountHash = accountHash1
        let assetHash = assetHash1
        let voteWeight = VoteWeight 1m

        let initialChxState =
            [
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = ChxAmount 100m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        let initialHoldingState =
            [
                (accountHash, assetHash), {HoldingState.Balance = AssetAmount 50m; IsEmission = false}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce1 = Nonce 11L
        let nonce2 = nonce1 + 1L

        let actionFee = ChxAmount 1m

        // Vote Yes on RS1
        let txHash1, txEnvelope1 =
            [
                {
                    ActionType = "SubmitVote"
                    ActionData =
                        {
                            AccountHash = accountHash.Value
                            AssetHash = assetHash.Value
                            ResolutionHash = resolutionHash1.Value
                            VoteHash = voteHashYes.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce1 (Timestamp 0L) actionFee

        // Weight vote on RS1
        let txHash2, txEnvelope2 =
            [
                {
                    ActionType = "SubmitVoteWeight"
                    ActionData =
                        {
                            AccountHash = accountHash.Value
                            AssetHash = assetHash.Value
                            ResolutionHash = resolutionHash1.Value
                            VoteWeight = voteWeight.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce2 (Timestamp 0L) actionFee

        let txSet = [txHash1; txHash2]

        // COMPOSE
        let getTx txHash =
            if txHash = txHash1 then Ok txEnvelope1
            elif txHash = txHash2 then Ok txEnvelope2
            else Result.appError "Invalid TX hash"

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getHoldingState key =
            initialHoldingState |> Map.tryFind key

        let getVoteState (voteId: VoteId) =
            // No vote for rsh1
            if voteId.ResolutionHash = resolutionHash1 then
                None
            else
                None

        let getAccountState _ =
            Some {AccountState.ControllerAddress = senderWallet.Address}

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = senderWallet.Address; IsEligibilityRequired = false}

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetHoldingStateFromStorage = getHoldingState
                GetVoteStateFromStorage = getVoteState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        test <@ output.TxResults.Count = 2 @>
        test <@ output.TxResults.[txHash1].Status = Success @>
        test <@ output.TxResults.[txHash2].Status = Success @>
        test <@ output.Votes.Count = 1 @>
        test <@ output.Votes.[{AccountHash = accountHash; AssetHash = assetHash; ResolutionHash = resolutionHash1}]
            = {VoteHash = voteHashYes; VoteWeight = Some voteWeight} @>

    [<Fact>]
    let ``Processing.processChanges SubmitVote and SubmitVoteWeight same Tx`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let accountHash = accountHash1
        let assetHash = assetHash1
        let voteWeight = VoteWeight 1m

        let initialChxState =
            [
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = ChxAmount 100m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        let initialHoldingState =
            [
                (accountHash, assetHash), {HoldingState.Balance = AssetAmount 50m; IsEmission = false}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce1 = Nonce 11L

        let actionFee = ChxAmount 1m

        // Vote Yes on RS1
        let txHash1, txEnvelope1 =
            [
                {
                    ActionType = "SubmitVote"
                    ActionData =
                        {
                            AccountHash = accountHash.Value
                            AssetHash = assetHash.Value
                            ResolutionHash = resolutionHash1.Value
                            VoteHash = voteHashYes.Value
                        }
                }
                {
                    ActionType = "SubmitVoteWeight"
                    ActionData =
                        {
                            AccountHash = accountHash.Value
                            AssetHash = assetHash.Value
                            ResolutionHash = resolutionHash1.Value
                            VoteWeight = voteWeight.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce1 (Timestamp 0L) actionFee

        let txSet = [txHash1]

        // COMPOSE
        let getTx txHash =
            if txHash = txHash1 then Ok txEnvelope1
            else Result.appError "Invalid TX hash"

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getHoldingState key =
            initialHoldingState |> Map.tryFind key

        let getVoteState (voteId: VoteId) =
            // No vote for rsh1
            if voteId.ResolutionHash = resolutionHash1 then
                None
            else
                None

        let getAccountState _ =
            Some {AccountState.ControllerAddress = senderWallet.Address}

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = senderWallet.Address; IsEligibilityRequired = false}

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetHoldingStateFromStorage = getHoldingState
                GetVoteStateFromStorage = getVoteState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash1].Status = Success @>
        test <@ output.Votes.Count = 1 @>
        test <@ output.Votes.[{AccountHash = accountHash; AssetHash = assetHash; ResolutionHash = resolutionHash1}]
            = {VoteHash = voteHashYes; VoteWeight = Some voteWeight} @>

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // SetAccountEligibility
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Fact>]
    let ``Processing.processChanges SetAccountEligibility insert success`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let accountHash = accountHash1
        let assetHash = assetHash1

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
                    ActionType = "SetAccountEligibility"
                    ActionData =
                        {
                            AccountHash = accountHash.Value
                            AssetHash = assetHash.Value
                            IsPrimaryEligible = true
                            IsSecondaryEligible = true
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getVoteState _ =
            None

        let getEligibilityState _ =
            None

        let getKycProvidersState _ =
            [senderWallet.Address]

        let getAccountState _ =
            Some {AccountState.ControllerAddress = senderWallet.Address}

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = senderWallet.Address; IsEligibilityRequired = false}

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetVoteStateFromStorage = getVoteState
                GetEligibilityStateFromStorage = getEligibilityState
                GetKycProvidersFromStorage = getKycProvidersState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let expectedEligibilityState =
            {
                EligibilityState.Eligibility =
                    {
                        IsPrimaryEligible = true
                        IsSecondaryEligible = true
                    }
                KycControllerAddress = senderWallet.Address
            }
        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = Success @>
        test <@ output.Eligibilities.Count = 1 @>
        test <@ output.Eligibilities.[(accountHash, assetHash)] = expectedEligibilityState @>

    [<Fact>]
    let ``Processing.processChanges SetAccountEligibility update success`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let accountHash = accountHash1
        let assetHash = assetHash1

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
                    ActionType = "SetAccountEligibility"
                    ActionData =
                        {
                            AccountHash = accountHash.Value
                            AssetHash = assetHash.Value
                            IsPrimaryEligible = true
                            IsSecondaryEligible = false
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getVoteState _ =
            None

        let getEligibilityState _ =
            {
                EligibilityState.Eligibility = {IsPrimaryEligible = true; IsSecondaryEligible = true}
                KycControllerAddress = senderWallet.Address
            }
            |> Some

        let getKycProvidersState _ =
            [senderWallet.Address]

        let getAccountState _ =
            Some {AccountState.ControllerAddress = senderWallet.Address}

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = senderWallet.Address; IsEligibilityRequired = false}

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetVoteStateFromStorage = getVoteState
                GetEligibilityStateFromStorage = getEligibilityState
                GetKycProvidersFromStorage = getKycProvidersState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let expectedEligibilityState =
            {
                EligibilityState.Eligibility =
                    {
                        IsPrimaryEligible = true
                        IsSecondaryEligible = false
                    }
                KycControllerAddress = senderWallet.Address
            }
        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = Success @>
        test <@ output.Eligibilities.Count = 1 @>
        test <@ output.Eligibilities.[(accountHash, assetHash)] = expectedEligibilityState @>

    [<Fact>]
    let ``Processing.processChanges SetAccountEligibility insert and update fails if not approved KYC provider`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let otherWallet = Signing.generateWallet ()
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

        let txHash1, txEnvelope1 =
            [
                {
                    ActionType = "SetAccountEligibility"
                    ActionData =
                        {
                            AccountHash = accountHash1.Value
                            AssetHash = assetHash1.Value
                            IsPrimaryEligible = true
                            IsSecondaryEligible = true
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txHash2, txEnvelope2 =
            [
                {
                    ActionType = "SetAccountEligibility"
                    ActionData =
                        {
                            AccountHash = accountHash2.Value
                            AssetHash = assetHash2.Value
                            IsPrimaryEligible = true
                            IsSecondaryEligible = false
                        }
                }
            ]
            |> Helpers.newTx senderWallet (nonce + 1) (Timestamp 0L) actionFee

        let txSet = [txHash1; txHash2]

        // COMPOSE
        let getTx txHash =
            if txHash = txHash1 then Ok txEnvelope1
            elif txHash = txHash2 then Ok txEnvelope2
            else Result.appError "Invalid txHash"

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getVoteState _ =
            None

        let getEligibilityState (_, assetHash) =
            if assetHash = assetHash1 then
                None
            elif assetHash = assetHash2 then
                {
                    EligibilityState.Eligibility = {IsPrimaryEligible = true; IsSecondaryEligible = true}
                    KycControllerAddress = senderWallet.Address
                }
                |> Some
            else
                None

        let getKycProvidersState _ =
            []

        let getAccountState _ =
            Some {AccountState.ControllerAddress = senderWallet.Address}

        let getAssetState _ =
            {
                AssetState.AssetCode = None
                ControllerAddress = otherWallet.Address
                IsEligibilityRequired = false
            }
            |> Some

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetVoteStateFromStorage = getVoteState
                GetEligibilityStateFromStorage = getEligibilityState
                GetKycProvidersFromStorage = getKycProvidersState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let expectedStatus =
            (TxActionNumber 1s, TxErrorCode.SenderIsNotApprovedKycProvider)
            |> TxActionError
            |> Failure

        test <@ output.TxResults.Count = 2 @>
        test <@ output.TxResults.[txHash1].Status = expectedStatus @>
        test <@ output.TxResults.[txHash2].Status = expectedStatus @>
        test <@ output.Eligibilities = Map.empty @>

    [<Fact>]
    let ``Processing.processChanges SetAccountEligibility update fails if approved KYC provider but not current`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let otherWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let accountHash = accountHash1
        let assetHash = assetHash1

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
                    ActionType = "SetAccountEligibility"
                    ActionData =
                        {
                            AccountHash = accountHash.Value
                            AssetHash = assetHash.Value
                            IsPrimaryEligible = true
                            IsSecondaryEligible = false
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getVoteState _ =
            None

        let getEligibilityState _ =
            {
                EligibilityState.Eligibility = {IsPrimaryEligible = true; IsSecondaryEligible = true}
                KycControllerAddress = otherWallet.Address
            }
            |> Some

        let getKycProvidersState _ =
            [senderWallet.Address]

        let getAccountState _ =
            Some {AccountState.ControllerAddress = senderWallet.Address}

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = senderWallet.Address; IsEligibilityRequired = false}

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetVoteStateFromStorage = getVoteState
                GetEligibilityStateFromStorage = getEligibilityState
                GetKycProvidersFromStorage = getKycProvidersState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let expectedStatus =
            (TxActionNumber 1s, TxErrorCode.SenderIsNotCurrentKycController)
            |> TxActionError
            |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>
        test <@ output.Eligibilities.Count = 1 @>
        test <@ output.Eligibilities.[(accountHash, assetHash)].KycControllerAddress = otherWallet.Address @>

    [<Fact>]
    let ``Processing.processChanges SetAccountEligibility insert and update fails if asset or account not found`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let otherWallet = Signing.generateWallet ()
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

        let txHash1, txEnvelope1 =
            [
                {
                    ActionType = "SetAccountEligibility"
                    ActionData =
                        {
                            AccountHash = accountHash1.Value
                            AssetHash = assetHash1.Value
                            IsPrimaryEligible = true
                            IsSecondaryEligible = true
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txHash2, txEnvelope2 =
            [
                {
                    ActionType = "SetAccountEligibility"
                    ActionData =
                        {
                            AccountHash = accountHash2.Value
                            AssetHash = assetHash2.Value
                            IsPrimaryEligible = true
                            IsSecondaryEligible = true
                        }
                }
            ]
            |> Helpers.newTx senderWallet (nonce + 1) (Timestamp 0L) actionFee

        let txSet = [txHash1; txHash2]

        // COMPOSE
        let getTx txHash =
            if txHash = txHash1 then Ok txEnvelope1
            elif txHash = txHash2 then Ok txEnvelope2
            else Result.appError "Invalid TX hash"

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getVoteState _ =
            None

        let getEligibilityState _ =
            None

        let getKycProvidersState _ =
            []

        let getAccountState accountHash =
            if accountHash = accountHash1 then
                Some {AccountState.ControllerAddress = senderWallet.Address}
            elif accountHash = accountHash2 then
                None
            else
                None

        let getAssetState assetHash =
            if assetHash = assetHash1 then
                None
            elif assetHash = assetHash2 then
                {
                    AssetState.AssetCode = None
                    ControllerAddress = senderWallet.Address
                    IsEligibilityRequired = false
                }
                |> Some
            else
                None

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetVoteStateFromStorage = getVoteState
                GetEligibilityStateFromStorage = getEligibilityState
                GetKycProvidersFromStorage = getKycProvidersState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let expectedStatusTxHash1 =
            (TxActionNumber 1s, TxErrorCode.AssetNotFound)
            |> TxActionError
            |> Failure
        let expectedStatusTxHash2 =
            (TxActionNumber 1s, TxErrorCode.AccountNotFound)
            |> TxActionError
            |> Failure

        test <@ output.TxResults.Count = 2 @>
        test <@ output.TxResults.[txHash1].Status = expectedStatusTxHash1 @>
        test <@ output.TxResults.[txHash2].Status = expectedStatusTxHash2 @>
        test <@ output.Eligibilities = Map.empty @>

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // SetAssetEligibility
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Fact>]
    let ``Processing.processChanges SetAssetEligibility update success`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let assetHash = assetHash1

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
                    ActionType = "SetAssetEligibility"
                    ActionData =
                        {
                            AssetHash = assetHash.Value
                            IsEligibilityRequired = true
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getVoteState _ =
            None

        let getEligibilityState _ =
            None

        let getKycProvidersState _ =
            [senderWallet.Address]

        let getAccountState _ =
            Some {AccountState.ControllerAddress = senderWallet.Address}

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = senderWallet.Address; IsEligibilityRequired = false}

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetVoteStateFromStorage = getVoteState
                GetEligibilityStateFromStorage = getEligibilityState
                GetKycProvidersFromStorage = getKycProvidersState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let expectedAssetState =
            {
                AssetState.AssetCode = None
                ControllerAddress = senderWallet.Address
                IsEligibilityRequired = true
            }
        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = Success @>
        test <@ output.Assets.Count = 1 @>
        test <@ output.Assets.[assetHash] = expectedAssetState @>

    [<Fact>]
    let ``Processing.processChanges SetAssetEligibility fails if asset not found`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let assetHash = assetHash1

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
                    ActionType = "SetAssetEligibility"
                    ActionData =
                        {
                            AssetHash = assetHash.Value
                            IsEligibilityRequired = true
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getVoteState _ =
            None

        let getEligibilityState _ =
            None

        let getKycProvidersState _ =
            [senderWallet.Address]

        let getAccountState _ =
            Some {AccountState.ControllerAddress = senderWallet.Address}

        let getAssetState _ =
            None

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetVoteStateFromStorage = getVoteState
                GetEligibilityStateFromStorage = getEligibilityState
                GetKycProvidersFromStorage = getKycProvidersState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let expectedStatus =
            (TxActionNumber 1s, TxErrorCode.AssetNotFound)
            |> TxActionError
            |> Failure
        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>
        test <@ output.Assets = Map.empty @>

    [<Fact>]
    let ``Processing.processChanges SetAssetEligibility fails is sender is not asset controller`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let otherWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let assetHash = assetHash1

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
                    ActionType = "SetAssetEligibility"
                    ActionData =
                        {
                            AssetHash = assetHash.Value
                            IsEligibilityRequired = true
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getVoteState _ =
            None

        let getEligibilityState _ =
            None

        let getKycProvidersState _ =
            [senderWallet.Address]

        let getAccountState _ =
            Some {AccountState.ControllerAddress = senderWallet.Address}

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = otherWallet.Address; IsEligibilityRequired = false}

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetVoteStateFromStorage = getVoteState
                GetEligibilityStateFromStorage = getEligibilityState
                GetKycProvidersFromStorage = getKycProvidersState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let expectedStatus =
            (TxActionNumber 1s, TxErrorCode.SenderIsNotAssetController)
            |> TxActionError
            |> Failure

        let expectedAssetState =
            {
                AssetState.AssetCode = None
                ControllerAddress = otherWallet.Address
                IsEligibilityRequired = false
            }

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>
        test <@ output.Assets.Count = 1 @>
        test <@ output.Assets.[assetHash] = expectedAssetState @>

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // ChangeKycControllerAddress
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Fact>]
    let ``Processing.processChanges ChangeKycControllerAddress`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let otherWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let accountHash = accountHash1
        let assetHash = assetHash1

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
                    ActionType = "ChangeKycControllerAddress"
                    ActionData =
                        {
                            AccountHash = accountHash.Value
                            AssetHash = assetHash.Value
                            KycControllerAddress = otherWallet.Address.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getVoteState _ =
            None

        let getEligibilityState _ =
            {
                EligibilityState.Eligibility = {IsPrimaryEligible = true; IsSecondaryEligible = true}
                KycControllerAddress = senderWallet.Address
            }
            |> Some

        let getKycProvidersState _ =
            [senderWallet.Address]

        let getAccountState _ =
            Some {AccountState.ControllerAddress = senderWallet.Address}

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = senderWallet.Address; IsEligibilityRequired = false}

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetVoteStateFromStorage = getVoteState
                GetEligibilityStateFromStorage = getEligibilityState
                GetKycProvidersFromStorage = getKycProvidersState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let expectedEligibilityState =
            {
                EligibilityState.Eligibility =
                    {
                        IsPrimaryEligible = true
                        IsSecondaryEligible = true
                    }
                KycControllerAddress = otherWallet.Address
            }

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = Success @>
        test <@ output.Eligibilities.Count = 1 @>
        test <@ output.Eligibilities.[(accountHash, assetHash)] = expectedEligibilityState @>

    [<Fact>]
    let ``Processing.processChanges ChangeKycControllerAddress fails if asset or account not found`` () =
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

        let txHash1, txEnvelope1 =
            [
                {
                    ActionType = "ChangeKycControllerAddress"
                    ActionData =
                        {
                            AccountHash = accountHash1.Value
                            AssetHash = assetHash1.Value
                            KycControllerAddress = senderWallet.Address.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txHash2, txEnvelope2 =
            [
                {
                    ActionType = "ChangeKycControllerAddress"
                    ActionData =
                        {
                            AccountHash = accountHash2.Value
                            AssetHash = assetHash2.Value
                            KycControllerAddress = senderWallet.Address.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet (nonce + 1) (Timestamp 0L) actionFee

        let txSet = [txHash1; txHash2]

        // COMPOSE
        let getTx txHash =
            if txHash = txHash1 then Ok txEnvelope1
            elif txHash = txHash2 then Ok txEnvelope2
            else Result.appError "Invalid TX hash"

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getVoteState _ =
            None

        let getEligibilityState _ =
            {
                EligibilityState.Eligibility = {IsPrimaryEligible = true; IsSecondaryEligible = true}
                KycControllerAddress = senderWallet.Address
            }
            |> Some

        let getKycProvidersState _ =
            [senderWallet.Address]

        let getAccountState accountHash =
            if accountHash = accountHash1 then
                Some {AccountState.ControllerAddress = senderWallet.Address}
            elif accountHash = accountHash2 then
                None
            else
                None

        let getAssetState assetHash =
            if assetHash = assetHash1 then
                None
            elif assetHash = assetHash2 then
                {
                    AssetState.AssetCode = None
                    ControllerAddress = senderWallet.Address
                    IsEligibilityRequired = false
                }
                |> Some
            else
                None

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetVoteStateFromStorage = getVoteState
                GetEligibilityStateFromStorage = getEligibilityState
                GetKycProvidersFromStorage = getKycProvidersState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let expectedStatusTxHash1 =
            (TxActionNumber 1s, TxErrorCode.AssetNotFound)
            |> TxActionError
            |> Failure
        let expectedStatusTxHash2 =
            (TxActionNumber 1s, TxErrorCode.AccountNotFound)
            |> TxActionError
            |> Failure

        test <@ output.TxResults.Count = 2 @>
        test <@ output.TxResults.[txHash1].Status = expectedStatusTxHash1 @>
        test <@ output.TxResults.[txHash2].Status = expectedStatusTxHash2 @>
        test <@ output.Eligibilities = Map.empty @>

    [<Fact>]
    let ``Processing.processChanges ChangeKycControllerAddress fails if no eligibility`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let otherWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let accountHash = accountHash1
        let assetHash = assetHash1

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
                    ActionType = "ChangeKycControllerAddress"
                    ActionData =
                        {
                            AccountHash = accountHash.Value
                            AssetHash = assetHash.Value
                            KycControllerAddress = otherWallet.Address.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getVoteState _ =
            None

        let getEligibilityState _ =
            None

        let getKycProvidersState _ =
            [senderWallet.Address]

        let getAccountState _ =
            Some {AccountState.ControllerAddress = senderWallet.Address}

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = senderWallet.Address; IsEligibilityRequired = false}

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetVoteStateFromStorage = getVoteState
                GetEligibilityStateFromStorage = getEligibilityState
                GetKycProvidersFromStorage = getKycProvidersState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let expectedStatus =
            (TxActionNumber 1s, TxErrorCode.EligibilityNotFound)
            |> TxActionError
            |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>
        test <@ output.Eligibilities = Map.empty @>

    [<Fact>]
    let ``Processing.processChanges ChangeKycControllerAddress fails SenderIsKycCtrlNotApprovedKycProvider`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let otherWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let accountHash = accountHash1
        let assetHash = assetHash1

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
                    ActionType = "ChangeKycControllerAddress"
                    ActionData =
                        {
                            AccountHash = accountHash.Value
                            AssetHash = assetHash.Value
                            KycControllerAddress = otherWallet.Address.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getVoteState _ =
            None

        let getEligibilityState _ =
            {
                EligibilityState.Eligibility = {IsPrimaryEligible = true; IsSecondaryEligible = true}
                KycControllerAddress = senderWallet.Address
            }
            |> Some

        let getKycProvidersState _ =
            []

        let getAccountState _ =
            Some {AccountState.ControllerAddress = senderWallet.Address}

        let getAssetState _ =
            {
                AssetState.AssetCode = None
                ControllerAddress = otherWallet.Address
                IsEligibilityRequired = false
            }
            |> Some

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetVoteStateFromStorage = getVoteState
                GetEligibilityStateFromStorage = getEligibilityState
                GetKycProvidersFromStorage = getKycProvidersState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let expectedStatus =
            (TxActionNumber 1s, TxErrorCode.SenderIsNotAssetControllerOrApprovedKycProvider)
            |> TxActionError
            |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>
        test <@ output.Eligibilities.Count = 1 @>
        test <@ output.Eligibilities.[(accountHash, assetHash)].KycControllerAddress = senderWallet.Address @>

    [<Fact>]
    let ``Processing.processChanges ChangeKycControllerAddress ok SenderIsNotApprovedKycProviderButAssetCtrl`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let otherWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let accountHash = accountHash1
        let assetHash = assetHash1

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
                    ActionType = "ChangeKycControllerAddress"
                    ActionData =
                        {
                            AccountHash = accountHash.Value
                            AssetHash = assetHash.Value
                            KycControllerAddress = otherWallet.Address.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getVoteState _ =
            None

        let getEligibilityState _ =
            {
                EligibilityState.Eligibility = {IsPrimaryEligible = true; IsSecondaryEligible = true}
                KycControllerAddress = senderWallet.Address
            }
            |> Some

        let getKycProvidersState _ =
            []

        let getAccountState _ =
            Some {AccountState.ControllerAddress = senderWallet.Address}

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = senderWallet.Address; IsEligibilityRequired = false}

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetVoteStateFromStorage = getVoteState
                GetEligibilityStateFromStorage = getEligibilityState
                GetKycProvidersFromStorage = getKycProvidersState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let expectedEligibilityState =
            {
                EligibilityState.Eligibility =
                    {
                        IsPrimaryEligible = true
                        IsSecondaryEligible = true
                    }
                KycControllerAddress = otherWallet.Address
            }

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = Success @>
        test <@ output.Eligibilities.Count = 1 @>
        test <@ output.Eligibilities.[(accountHash, assetHash)] = expectedEligibilityState @>

    [<Fact>]
    let ``Processing.processChanges ChangeKycControllerAddress fails SenderIsNotAssetCtrlOrApprovedKycProvider`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let otherWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let accountHash = accountHash1
        let assetHash = assetHash1

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
                    ActionType = "ChangeKycControllerAddress"
                    ActionData =
                        {
                            AccountHash = accountHash.Value
                            AssetHash = assetHash.Value
                            KycControllerAddress = otherWallet.Address.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getVoteState _ =
            None

        let getEligibilityState _ =
            {
                EligibilityState.Eligibility = {IsPrimaryEligible = true; IsSecondaryEligible = true}
                KycControllerAddress = otherWallet.Address
            }
            |> Some

        let getKycProvidersState _ =
            [senderWallet.Address]

        let getAccountState _ =
            Some {AccountState.ControllerAddress = senderWallet.Address}

        let getAssetState _ =
            {
                AssetState.AssetCode = None
                ControllerAddress = otherWallet.Address
                IsEligibilityRequired = false
            }
            |> Some

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetVoteStateFromStorage = getVoteState
                GetEligibilityStateFromStorage = getEligibilityState
                GetKycProvidersFromStorage = getKycProvidersState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let expectedStatus =
            (TxActionNumber 1s, TxErrorCode.SenderIsNotAssetControllerOrApprovedKycProvider)
            |> TxActionError
            |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>
        test <@ output.Eligibilities.Count = 1 @>
        test <@ output.Eligibilities.[(accountHash, assetHash)].KycControllerAddress = otherWallet.Address @>

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // AddKycProvider
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Fact>]
    let ``Processing.processChanges AddKycProvider`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let assetHash = assetHash1

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
                    ActionType = "AddKycProvider"
                    ActionData =
                        {
                            AssetHash = assetHash.Value
                            ProviderAddress = senderWallet.Address.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getVoteState _ =
            None

        let getEligibilityState _ =
            None

        let getKycProvidersState _ =
            []

        let getAccountState _ =
            Some {AccountState.ControllerAddress = senderWallet.Address}

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = senderWallet.Address; IsEligibilityRequired = false}

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetVoteStateFromStorage = getVoteState
                GetEligibilityStateFromStorage = getEligibilityState
                GetKycProvidersFromStorage = getKycProvidersState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = Success @>
        test <@ output.KycProviders.Count = 1 @>
        test <@ output.KycProviders.[assetHash].[senderWallet.Address] = KycProviderChange.Add @>

    [<Fact>]
    let ``Processing.processChanges AddKycProvider fails if provider already exists`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let assetHash = assetHash1

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
                    ActionType = "AddKycProvider"
                    ActionData =
                        {
                            AssetHash = assetHash.Value
                            ProviderAddress = senderWallet.Address.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getVoteState _ =
            None

        let getEligibilityState _ =
            None

        let getKycProvidersState _ =
            [senderWallet.Address]

        let getAccountState _ =
            Some {AccountState.ControllerAddress = senderWallet.Address}

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = senderWallet.Address; IsEligibilityRequired = false}

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetVoteStateFromStorage = getVoteState
                GetEligibilityStateFromStorage = getEligibilityState
                GetKycProvidersFromStorage = getKycProvidersState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let expectedStatus =
            (TxActionNumber 1s, TxErrorCode.KycProviderAlreadyExists)
            |> TxActionError
            |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>
        test <@ output.KycProviders.Count = 1 @>

    [<Fact>]
    let ``Processing.processChanges AddKycProvider various errors`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let otherWallet = Signing.generateWallet ()
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

        let txHash1, txEnvelope1 =
            [
                {
                    ActionType = "AddKycProvider"
                    ActionData =
                        {
                            AssetHash = assetHash1.Value
                            ProviderAddress = senderWallet.Address.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txHash2, txEnvelope2 =
            [
                {
                    ActionType = "AddKycProvider"
                    ActionData =
                        {
                            AssetHash = assetHash2.Value
                            ProviderAddress = senderWallet.Address.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet (nonce + 1) (Timestamp 0L) actionFee

        let txSet = [txHash1; txHash2]

        // COMPOSE
        let getTx txHash =
            if txHash = txHash1 then Ok txEnvelope1
            elif txHash = txHash2 then Ok txEnvelope2
            else Result.appError "Invalid TxHash"

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getVoteState _ =
            None

        let getEligibilityState _ =
            None

        let getAccountState _ =
            Some {AccountState.ControllerAddress = senderWallet.Address}

        let getAssetState assetHash =
            if assetHash = assetHash1 then
                None
            else
                {
                    AssetState.AssetCode = None
                    ControllerAddress = otherWallet.Address
                    IsEligibilityRequired = false
                }
                |> Some

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetVoteStateFromStorage = getVoteState
                GetEligibilityStateFromStorage = getEligibilityState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let expectedStatusTxHash1 =
            (TxActionNumber 1s, TxErrorCode.AssetNotFound)
            |> TxActionError
            |> Failure

        let expectedStatusTxHash2 =
            (TxActionNumber 1s, TxErrorCode.SenderIsNotAssetController)
            |> TxActionError
            |> Failure

        test <@ output.TxResults.Count = 2 @>
        test <@ output.TxResults.[txHash1].Status = expectedStatusTxHash1 @>
        test <@ output.TxResults.[txHash2].Status = expectedStatusTxHash2 @>
        test <@ output.KycProviders = Map.empty @>

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // RemoveKycProvider
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Fact>]
    let ``Processing.processChanges RemoveKycProvider`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let assetHash = assetHash1

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
                    ActionType = "RemoveKycProvider"
                    ActionData =
                        {
                            AssetHash = assetHash.Value
                            ProviderAddress = senderWallet.Address.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getVoteState _ =
            None

        let getEligibilityState _ =
            None

        let getAccountState _ =
            Some {AccountState.ControllerAddress = senderWallet.Address}

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = senderWallet.Address; IsEligibilityRequired = false}

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetVoteStateFromStorage = getVoteState
                GetEligibilityStateFromStorage = getEligibilityState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = Success @>
        test <@ output.KycProviders.Count = 1 @>
        test <@ output.KycProviders.[assetHash].[senderWallet.Address] = KycProviderChange.Remove @>

    [<Fact>]
    let ``Processing.processChanges RemoveKycProvider various errors`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let otherWallet = Signing.generateWallet ()
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

        let txHash1, txEnvelope1 =
            [
                {
                    ActionType = "RemoveKycProvider"
                    ActionData =
                        {
                            AssetHash = assetHash1.Value
                            ProviderAddress = senderWallet.Address.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txHash2, txEnvelope2 =
            [
                {
                    ActionType = "RemoveKycProvider"
                    ActionData =
                        {
                            AssetHash = assetHash2.Value
                            ProviderAddress = senderWallet.Address.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet (nonce + 1) (Timestamp 0L) actionFee

        let txSet = [txHash1; txHash2]

        // COMPOSE
        let getTx txHash =
            if txHash = txHash1 then Ok txEnvelope1
            elif txHash = txHash2 then Ok txEnvelope2
            else Result.appError "Invalid TxHash"

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getVoteState _ =
            None

        let getEligibilityState _ =
            None

        let getAccountState _ =
            Some {AccountState.ControllerAddress = senderWallet.Address}

        let getAssetState assetHash =
            if assetHash = assetHash1 then
                None
            else
                {
                    AssetState.AssetCode = None
                    ControllerAddress = otherWallet.Address
                    IsEligibilityRequired = false
                }
                |> Some

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetVoteStateFromStorage = getVoteState
                GetEligibilityStateFromStorage = getEligibilityState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let expectedStatusTxHash1 =
            (TxActionNumber 1s, TxErrorCode.AssetNotFound)
            |> TxActionError
            |> Failure

        let expectedStatusTxHash2 =
            (TxActionNumber 1s, TxErrorCode.SenderIsNotAssetController)
            |> TxActionError
            |> Failure

        test <@ output.TxResults.Count = 2 @>
        test <@ output.TxResults.[txHash1].Status = expectedStatusTxHash1 @>
        test <@ output.TxResults.[txHash2].Status = expectedStatusTxHash2 @>
        test <@ output.KycProviders = Map.empty @>

    [<Fact>]
    let ``Processing.processChanges Add and RemoveKycProvider mixed`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let otherWallet = Signing.generateWallet ()
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

        let txHash1, txEnvelope1 =
            [
                {
                    ActionType = "AddKycProvider"
                    ActionData =
                        {
                            AssetHash = assetHash1.Value
                            ProviderAddress = senderWallet.Address.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txHash2, txEnvelope2 =
            [
                {
                    ActionType = "RemoveKycProvider"
                    ActionData =
                        {
                            AssetHash = assetHash2.Value
                            ProviderAddress = senderWallet.Address.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet (nonce + 1) (Timestamp 0L) actionFee

        let txSet = [txHash1; txHash2]

        // COMPOSE
        let getTx txHash =
            if txHash = txHash1 then Ok txEnvelope1
            elif txHash = txHash2 then Ok txEnvelope2
            else Result.appError "Invalid TxHash"

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getVoteState _ =
            None

        let getEligibilityState _ =
            None

        let getKycProvidersState _ =
            []

        let getAccountState _ =
            Some {AccountState.ControllerAddress = senderWallet.Address}

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = senderWallet.Address; IsEligibilityRequired = false}

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetVoteStateFromStorage = getVoteState
                GetEligibilityStateFromStorage = getEligibilityState
                GetKycProvidersFromStorage = getKycProvidersState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        test <@ output.TxResults.Count = 2 @>
        test <@ output.TxResults.[txHash1].Status = Success @>
        test <@ output.TxResults.[txHash2].Status = Success @>
        test <@ output.KycProviders.Count = 2 @>
        test <@ output.KycProviders.[assetHash1].[senderWallet.Address] = KycProviderChange.Add @>
        test <@ output.KycProviders.[assetHash2].[senderWallet.Address] = KycProviderChange.Remove @>

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // CreateAssetEmission
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Fact>]
    let ``Processing.processChanges CreateAssetEmission`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let someOtherWallet = Signing.generateWallet ()
        let emissionAccountHash = accountHash1
        let assetHash = assetHash1

        let initialChxState =
            [
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = ChxAmount 100m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let actionFee = ChxAmount 1m
        let emissionAmount = AssetAmount 10m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "CreateAssetEmission"
                    ActionData =
                        {
                            EmissionAccountHash = emissionAccountHash.Value
                            AssetHash = assetHash.Value
                            Amount = emissionAmount.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getHoldingState _ =
            None

        let getAccountState _ =
            Some {AccountState.ControllerAddress = someOtherWallet.Address}

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = senderWallet.Address; IsEligibilityRequired = false}

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetHoldingStateFromStorage = getHoldingState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - actionFee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + actionFee

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = Success @>
        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>
        test <@ output.Holdings.[emissionAccountHash, assetHash].Balance = emissionAmount @>
        test <@ output.Holdings.[emissionAccountHash, assetHash].IsEmission = true @>

    [<Fact>]
    let ``Processing.processChanges CreateAssetEmission additional emission`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let someOtherWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let emissionAccountHash = accountHash1
        let assetHash = assetHash1

        let initialChxState =
            [
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = ChxAmount 100m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        let initialHoldingState =
            [
                (emissionAccountHash, assetHash), {HoldingState.Balance = AssetAmount 30m; IsEmission = false}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let actionFee = ChxAmount 1m
        let emissionAmount = AssetAmount 10m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "CreateAssetEmission"
                    ActionData =
                        {
                            EmissionAccountHash = emissionAccountHash.Value
                            AssetHash = assetHash.Value
                            Amount = emissionAmount.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getHoldingState key =
            initialHoldingState |> Map.tryFind key

        let getAccountState _ =
            Some {AccountState.ControllerAddress = someOtherWallet.Address}

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = senderWallet.Address; IsEligibilityRequired = false}

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetHoldingStateFromStorage = getHoldingState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - actionFee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + actionFee
        let emittedAssetBalance = initialHoldingState.[emissionAccountHash, assetHash].Balance + emissionAmount

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = Success @>
        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>
        test <@ output.Holdings.[emissionAccountHash, assetHash].Balance = emittedAssetBalance @>
        test <@ output.Holdings.[emissionAccountHash, assetHash].IsEmission = true @>

    [<Fact>]
    let ``Processing.processChanges CreateAssetEmission fails if amount overflow`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let someOtherWallet = Signing.generateWallet ()
        let emissionAccountHash = accountHash1
        let assetHash = assetHash1

        let initialChxState =
            [
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = ChxAmount 100m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let actionFee = ChxAmount 1m
        let emissionAmount = AssetAmount 999m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "CreateAssetEmission"
                    ActionData =
                        {
                            EmissionAccountHash = emissionAccountHash.Value
                            AssetHash = assetHash.Value
                            Amount = emissionAmount.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getHoldingState _ =
            Some { HoldingState.Balance = AssetAmount 99_999_999_999m; IsEmission = true }

        let getAccountState _ =
            Some {AccountState.ControllerAddress = someOtherWallet.Address}

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = senderWallet.Address; IsEligibilityRequired = false}

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetHoldingStateFromStorage = getHoldingState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let expectedStatus =
            (TxActionNumber 1s, TxErrorCode.ValueTooBig)
            |> TxActionError
            |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>

    [<Fact>]
    let ``Processing.processChanges CreateAssetEmission fails if sender not current controller`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let someOtherWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let currentControllerWallet = Signing.generateWallet ()
        let emissionAccountHash = accountHash1
        let assetHash = assetHash1

        let initialChxState =
            [
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = ChxAmount 100m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let actionFee = ChxAmount 1m
        let emissionAmount = AssetAmount 10m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "CreateAssetEmission"
                    ActionData =
                        {
                            EmissionAccountHash = emissionAccountHash.Value
                            AssetHash = assetHash.Value
                            Amount = emissionAmount.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getHoldingState _ =
            None

        let getAccountState _ =
            Some {AccountState.ControllerAddress = someOtherWallet.Address}

        let getAssetState _ =
            {
                AssetState.AssetCode = None
                ControllerAddress = currentControllerWallet.Address
                IsEligibilityRequired = false
            }
            |> Some

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetHoldingStateFromStorage = getHoldingState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - actionFee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + actionFee
        let expectedStatus =
            (TxActionNumber 1s, TxErrorCode.SenderIsNotAssetController)
            |> TxActionError
            |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>
        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>
        test <@ output.Holdings = Map.empty @>

    [<Fact>]
    let ``Processing.processChanges CreateAssetEmission fails if asset not found`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let someOtherWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let currentControllerWallet = Signing.generateWallet ()
        let emissionAccountHash = accountHash1
        let assetHash = assetHash1

        let initialChxState =
            [
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = ChxAmount 100m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let actionFee = ChxAmount 1m
        let emissionAmount = AssetAmount 10m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "CreateAssetEmission"
                    ActionData =
                        {
                            EmissionAccountHash = emissionAccountHash.Value
                            AssetHash = assetHash.Value
                            Amount = emissionAmount.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getHoldingState _ =
            None

        let getAccountState _ =
            Some {AccountState.ControllerAddress = someOtherWallet.Address}

        let getAssetState _ =
            None

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetHoldingStateFromStorage = getHoldingState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - actionFee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + actionFee
        let expectedStatus =
            (TxActionNumber 1s, TxErrorCode.AssetNotFound)
            |> TxActionError
            |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>
        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>
        test <@ output.Holdings = Map.empty @>

    [<Fact>]
    let ``Processing.processChanges CreateAssetEmission fails if account not found`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let currentControllerWallet = Signing.generateWallet ()
        let emissionAccountHash = accountHash1
        let assetHash = assetHash1

        let initialChxState =
            [
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = ChxAmount 100m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let actionFee = ChxAmount 1m
        let emissionAmount = AssetAmount 10m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "CreateAssetEmission"
                    ActionData =
                        {
                            EmissionAccountHash = emissionAccountHash.Value
                            AssetHash = assetHash.Value
                            Amount = emissionAmount.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getHoldingState _ =
            None

        let getAccountState _ =
            None

        let getAssetState _ =
            {
                AssetState.AssetCode = None
                ControllerAddress = currentControllerWallet.Address
                IsEligibilityRequired = false
            }
            |> Some

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetHoldingStateFromStorage = getHoldingState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - actionFee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + actionFee
        let expectedStatus =
            (TxActionNumber 1s, TxErrorCode.AccountNotFound)
            |> TxActionError
            |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>
        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>
        test <@ output.Holdings = Map.empty @>

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // CreateAccount
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Fact>]
    let ``Processing.processChanges CreateAccount`` () =
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

        let accountHash =
            [
                Hashing.decode senderWallet.Address.Value
                nonce.Value |> Conversion.int64ToBytes
                1s |> Conversion.int16ToBytes
            ]
            |> Array.concat
            |> Hashing.hash
            |> AccountHash

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getHoldingState _ =
            None

        let getAccountState _ =
            None

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetHoldingStateFromStorage = getHoldingState
                GetAccountStateFromStorage = getAccountState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - actionFee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + actionFee

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = Success @>
        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>
        test <@ output.Accounts.Count = 1 @>
        test <@ output.Accounts.[accountHash] = {ControllerAddress = senderWallet.Address} @>

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // CreateAsset
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Fact>]
    let ``Processing.processChanges CreateAsset`` () =
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
                    ActionType = "CreateAsset"
                    ActionData = CreateAssetTxActionDto()
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        let assetHash =
            [
                Hashing.decode senderWallet.Address.Value
                nonce.Value |> Conversion.int64ToBytes
                1s |> Conversion.int16ToBytes
            ]
            |> Array.concat
            |> Hashing.hash
            |> AssetHash

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getHoldingState _ =
            None

        let getAssetState _ =
            None

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetHoldingStateFromStorage = getHoldingState
                GetAssetStateFromStorage = getAssetState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - actionFee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + actionFee
        let expectedAssetState =
            {
                AssetState.AssetCode = None
                ControllerAddress = senderWallet.Address
                IsEligibilityRequired = false
            }

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = Success @>
        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>
        test <@ output.Assets.Count = 1 @>
        test <@ output.Assets.[assetHash] = expectedAssetState @>

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // SetAccountController
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Fact>]
    let ``Processing.processChanges SetAccountController`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let newControllerWallet = Signing.generateWallet ()
        let accountHash = accountHash1

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
                    ActionType = "SetAccountController"
                    ActionData =
                        {
                            AccountHash = accountHash.Value
                            ControllerAddress = newControllerWallet.Address.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getAccountState _ =
            Some {AccountState.ControllerAddress = senderWallet.Address}

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetAccountStateFromStorage = getAccountState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - actionFee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + actionFee

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = Success @>
        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>
        test <@ output.Accounts.[accountHash] = {AccountState.ControllerAddress = newControllerWallet.Address} @>

    [<Fact>]
    let ``Processing.processChanges SetAccountController fails if account not found`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let newControllerWallet = Signing.generateWallet ()
        let accountHash = accountHash1

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
                    ActionType = "SetAccountController"
                    ActionData =
                        {
                            AccountHash = accountHash.Value
                            ControllerAddress = newControllerWallet.Address.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getAccountState _ =
            None

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetAccountStateFromStorage = getAccountState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - actionFee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + actionFee
        let expectedTxStatus =
            (TxActionNumber 1s, TxErrorCode.AccountNotFound)
            |> TxActionError
            |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedTxStatus @>
        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>
        test <@ output.Accounts = Map.empty @>

    [<Fact>]
    let ``Processing.processChanges SetAccountController fails if sender not current controller`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let currentControllerWallet = Signing.generateWallet ()
        let newControllerWallet = Signing.generateWallet ()
        let accountHash = accountHash1

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
                    ActionType = "SetAccountController"
                    ActionData =
                        {
                            AccountHash = accountHash.Value
                            ControllerAddress = newControllerWallet.Address.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getAccountState _ =
            Some {AccountState.ControllerAddress = currentControllerWallet.Address}

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetAccountStateFromStorage = getAccountState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - actionFee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + actionFee
        let expectedTxStatus =
            (TxActionNumber 1s, TxErrorCode.SenderIsNotSourceAccountController)
            |> TxActionError
            |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedTxStatus @>
        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>
        test <@ output.Accounts.[accountHash] <> {AccountState.ControllerAddress = newControllerWallet.Address} @>
        test <@ output.Accounts.[accountHash] = {AccountState.ControllerAddress = currentControllerWallet.Address} @>

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // SetAssetController
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Fact>]
    let ``Processing.processChanges SetAssetController`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let newControllerWallet = Signing.generateWallet ()
        let assetHash = assetHash1

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
                    ActionType = "SetAssetController"
                    ActionData =
                        {
                            AssetHash = assetHash.Value
                            ControllerAddress = newControllerWallet.Address.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = senderWallet.Address; IsEligibilityRequired = false}

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetAssetStateFromStorage = getAssetState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - actionFee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + actionFee

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = Success @>
        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>
        test <@ output.Assets.[assetHash].ControllerAddress = newControllerWallet.Address @>

    [<Fact>]
    let ``Processing.processChanges SetAssetController fails if asset not found`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let newControllerWallet = Signing.generateWallet ()
        let assetHash = assetHash1

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
                    ActionType = "SetAssetController"
                    ActionData =
                        {
                            AssetHash = assetHash.Value
                            ControllerAddress = newControllerWallet.Address.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getAssetState _ =
            None

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetAssetStateFromStorage = getAssetState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - actionFee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + actionFee
        let expectedTxStatus =
            (TxActionNumber 1s, TxErrorCode.AssetNotFound)
            |> TxActionError
            |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedTxStatus @>
        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>
        test <@ output.Assets = Map.empty @>

    [<Fact>]
    let ``Processing.processChanges SetAssetController fails if sender not current controller`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let currentControllerWallet = Signing.generateWallet ()
        let newControllerWallet = Signing.generateWallet ()
        let assetHash = assetHash1

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
                    ActionType = "SetAssetController"
                    ActionData =
                        {
                            AssetHash = assetHash.Value
                            ControllerAddress = newControllerWallet.Address.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getAssetState _ =
            {
                AssetState.AssetCode = None
                ControllerAddress = currentControllerWallet.Address
                IsEligibilityRequired = false
            }
            |> Some

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetAssetStateFromStorage = getAssetState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - actionFee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + actionFee
        let expectedTxStatus =
            (TxActionNumber 1s, TxErrorCode.SenderIsNotAssetController)
            |> TxActionError
            |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedTxStatus @>
        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>
        test <@ output.Assets.[assetHash].ControllerAddress <> newControllerWallet.Address @>
        test <@ output.Assets.[assetHash].ControllerAddress = currentControllerWallet.Address @>

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // SetAssetCode
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Fact>]
    let ``Processing.processChanges SetAssetCode`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let assetHash = assetHash1
        let assetCode = AssetCode "BAR"

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
                    ActionType = "SetAssetCode"
                    ActionData =
                        {
                            AssetHash = assetHash.Value
                            AssetCode = assetCode.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = senderWallet.Address; IsEligibilityRequired = false}

        let getAssetHashByCode _ =
            None

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetAssetStateFromStorage = getAssetState
                GetAssetHashByCodeFromStorage = getAssetHashByCode
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - actionFee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + actionFee

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = Success @>
        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>
        test <@ output.Assets.[assetHash].AssetCode = Some assetCode @>

    [<Fact>]
    let ``Processing.processChanges SetAssetCode fails if asset not found`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let assetHash = assetHash1
        let assetCode = AssetCode "BAR"

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
                    ActionType = "SetAssetCode"
                    ActionData =
                        {
                            AssetHash = assetHash.Value
                            AssetCode = assetCode.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getAssetState _ =
            None

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetAssetStateFromStorage = getAssetState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - actionFee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + actionFee
        let expectedTxStatus =
            (TxActionNumber 1s, TxErrorCode.AssetNotFound)
            |> TxActionError
            |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedTxStatus @>
        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>
        test <@ output.Assets = Map.empty @>

    [<Fact>]
    let ``Processing.processChanges SetAssetCode fails if assetCode already exists`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let assetHash = assetHash1
        let assetCode = AssetCode "BAR"

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
                    ActionType = "SetAssetCode"
                    ActionData =
                        {
                            AssetHash = assetHash.Value
                            AssetCode = assetCode.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getAssetState _ =
            {
                AssetState.AssetCode = None
                ControllerAddress = senderWallet.Address
                IsEligibilityRequired = false
            }
            |> Some

        let getAssetHashByCode _ =
            assetHash1 |> Some

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetAssetStateFromStorage = getAssetState
                GetAssetHashByCodeFromStorage = getAssetHashByCode
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let expectedTxStatus =
            (TxActionNumber 1s, TxErrorCode.AssetCodeAlreadyExists)
            |> TxActionError
            |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedTxStatus @>
        test <@ output.Assets.[assetHash].AssetCode = None @>

    [<Fact>]
    let ``Processing.processChanges SetAssetCode fails if sender not current controller`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let currentControllerWallet = Signing.generateWallet ()
        let assetHash = assetHash1
        let assetCode = AssetCode "BAR"

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
                    ActionType = "SetAssetCode"
                    ActionData =
                        {
                            AssetHash = assetHash.Value
                            AssetCode = assetCode.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getAssetState _ =
            {
                AssetState.AssetCode = None
                ControllerAddress = currentControllerWallet.Address
                IsEligibilityRequired = false
            }
            |> Some

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetAssetStateFromStorage = getAssetState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - actionFee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + actionFee
        let expectedTxStatus =
            (TxActionNumber 1s, TxErrorCode.SenderIsNotAssetController)
            |> TxActionError
            |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedTxStatus @>
        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>
        test <@ output.Assets.[assetHash].AssetCode = None @>

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Asset management with multiple actions
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Fact>]
    let ``Processing.processChanges Create and completely distribute emitted asset`` () =
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
        let actionFee = ChxAmount 1m
        let assetHash = Hashing.deriveHash senderWallet.Address (Nonce 11L) (TxActionNumber 1s) |> AssetHash
        let emissionAccountHash = Hashing.deriveHash senderWallet.Address (Nonce 11L) (TxActionNumber 2s) |> AccountHash
        let accountHash1 = Hashing.deriveHash senderWallet.Address (Nonce 12L) (TxActionNumber 1s) |> AccountHash
        let accountHash2 = Hashing.deriveHash senderWallet.Address (Nonce 13L) (TxActionNumber 1s) |> AccountHash

        let txHash1, txEnvelope1 =
            [
                {
                    ActionType = "CreateAsset"
                    ActionData = CreateAssetTxActionDto()
                }
                {
                    ActionType = "CreateAccount"
                    ActionData = CreateAssetTxActionDto()
                }
                {
                    ActionType = "CreateAssetEmission"
                    ActionData =
                        {
                            CreateAssetEmissionTxActionDto.AssetHash = assetHash.Value
                            EmissionAccountHash = emissionAccountHash.Value
                            Amount = 30m
                        }
                }
            ]
            |> Helpers.newTx senderWallet (Nonce 11L) (Timestamp 0L) actionFee

        let txHash2, txEnvelope2 =
            [
                {
                    ActionType = "CreateAccount"
                    ActionData = CreateAssetTxActionDto()
                }
                {
                    ActionType = "TransferAsset"
                    ActionData =
                        {
                            TransferAssetTxActionDto.FromAccountHash = emissionAccountHash.Value
                            ToAccountHash = accountHash1.Value
                            AssetHash = assetHash.Value
                            Amount = 10m
                        }
                }
            ]
            |> Helpers.newTx senderWallet (Nonce 12L) (Timestamp 0L) actionFee

        let txHash3, txEnvelope3 =
            [
                {
                    ActionType = "CreateAccount"
                    ActionData = CreateAssetTxActionDto()
                }
                {
                    ActionType = "TransferAsset"
                    ActionData =
                        {
                            TransferAssetTxActionDto.FromAccountHash = emissionAccountHash.Value
                            ToAccountHash = accountHash2.Value
                            AssetHash = assetHash.Value
                            Amount = 20m
                        }
                }
            ]
            |> Helpers.newTx senderWallet (Nonce 13L) (Timestamp 0L) actionFee

        let txSet = [txHash1; txHash2; txHash3]

        // COMPOSE
        let getTx txHash =
            if txHash = txHash1 then Ok txEnvelope1
            elif txHash = txHash2 then Ok txEnvelope2
            elif txHash = txHash3 then Ok txEnvelope3
            else failwithf "Unknown TX hash: %s" txHash.Value

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getHoldingState _ =
            None

        let getAccountState _ =
            None

        let getAssetState _ =
            None

        let getHoldingInTradeOrdersFromStorage _ =
            AssetAmount 0m

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetHoldingStateFromStorage = getHoldingState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                GetHoldingInTradeOrdersFromStorage = getHoldingInTradeOrdersFromStorage
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - actionFee * 7m
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + actionFee * 7m
        let expectedAssetState =
            {
                AssetState.AssetCode = None
                ControllerAddress = senderWallet.Address
                IsEligibilityRequired = false
            }

        test <@ output.TxResults.Count = 3 @>
        for txHash in txSet do
            test <@ output.TxResults.[txHash].Status = Success @>
        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = Nonce 13L @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>
        test <@ output.Assets.Count = 1 @>
        test <@ output.Assets.[assetHash] = expectedAssetState @>
        test <@ output.Accounts.Count = 3 @>
        test <@ output.Accounts.[emissionAccountHash].ControllerAddress = senderWallet.Address @>
        test <@ output.Accounts.[accountHash1].ControllerAddress = senderWallet.Address @>
        test <@ output.Accounts.[accountHash2].ControllerAddress = senderWallet.Address @>
        test <@ output.Holdings.[emissionAccountHash, assetHash].IsEmission = true @>
        test <@ output.Holdings.[accountHash1, assetHash].IsEmission = false @>
        test <@ output.Holdings.[accountHash2, assetHash].IsEmission = false @>
        test <@ output.Holdings.[emissionAccountHash, assetHash].Balance = AssetAmount 0m @>
        test <@ output.Holdings.[accountHash1, assetHash].Balance = AssetAmount 10m @>
        test <@ output.Holdings.[accountHash2, assetHash].Balance = AssetAmount 20m @>

    [<Fact>]
    let ``Processing.processChanges Do not store empty state for the asset created in failed TX`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let assetHash = assetHash1

        let initialChxState =
            [
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = ChxAmount 100m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        let initialHoldingState =
            [
                (accountHash1, assetHash), {HoldingState.Balance = AssetAmount 15m; IsEmission = false}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let actionFee = ChxAmount 1m
        let amountToTransfer = AssetAmount 20m
        let accountHash2 = Hashing.deriveHash senderWallet.Address nonce (TxActionNumber 1s) |> AccountHash

        let txHash, txEnvelope =
            [
                {
                    ActionType = "CreateAccount"
                    ActionData = CreateAccountTxActionDto()
                }
                {
                    ActionType = "TransferAsset"
                    ActionData =
                        {
                            FromAccountHash = accountHash1.Value
                            ToAccountHash = accountHash2.Value
                            AssetHash = assetHash.Value
                            Amount = amountToTransfer.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getHoldingState key =
            initialHoldingState |> Map.tryFind key

        let getAccountState accountHash =
            if accountHash = accountHash1 then
                Some {AccountState.ControllerAddress = senderWallet.Address}
            else
                None

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = senderWallet.Address; IsEligibilityRequired = false}

        let getHoldingInTradeOrdersFromStorage _ =
            AssetAmount 0m

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetHoldingStateFromStorage = getHoldingState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                GetHoldingInTradeOrdersFromStorage = getHoldingInTradeOrdersFromStorage
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - actionFee * 2m
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + actionFee * 2m
        let senderAssetBalance = initialHoldingState.[accountHash1, assetHash].Balance
        let expectedStatus =
            (TxActionNumber 2s, TxErrorCode.InsufficientAssetHoldingBalance)
            |> TxActionError
            |> Failure
        let expectedAssetState =
            {
                AssetState.AssetCode = None
                ControllerAddress = senderWallet.Address
                IsEligibilityRequired = false
            }

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>
        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>

        test <@ output.Assets.Count = 1 @>
        test <@ output.Assets.[assetHash] = expectedAssetState @>

        test <@ output.Accounts.Count = 1 @>
        test <@ output.Accounts.ContainsKey accountHash1 @>
        test <@ output.Accounts.ContainsKey accountHash2 = false @>

        test <@ output.Holdings.Count = 1 @>
        test <@ output.Holdings.[accountHash1, assetHash].Balance = senderAssetBalance @>
        test <@ output.Holdings.ContainsKey(accountHash2, assetHash) = false@>

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // ConfigureValidator
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Fact>]
    let ``Processing.processChanges ConfigureValidator - updating existing config`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let newNetworkAddress = NetworkAddress "localhost:5000"
        let newSharedRewardPercent = 10m

        let initialChxState =
            [
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = Helpers.validatorDeposit + 10m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let actionFee = ChxAmount 1m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "ConfigureValidator"
                    ActionData =
                        {
                            ConfigureValidatorTxActionDto.NetworkAddress = newNetworkAddress.Value
                            SharedRewardPercent = newSharedRewardPercent
                            IsEnabled = false
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getValidatorState _ =
            Some {
                ValidatorState.NetworkAddress = NetworkAddress "old-address:12345"
                SharedRewardPercent = 6m
                TimeToLockDeposit = 0s
                TimeToBlacklist = 0s
                IsEnabled = true
            }

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetValidatorStateFromStorage = getValidatorState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - actionFee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + actionFee

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = Success @>
        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>

        let validatorState, validatorChange = output.Validators.[senderWallet.Address]
        test <@ validatorState.NetworkAddress = newNetworkAddress @>
        test <@ validatorState.SharedRewardPercent = newSharedRewardPercent @>
        test <@ validatorState.IsEnabled = false @>
        test <@ validatorChange = ValidatorChange.Update @>

    [<Fact>]
    let ``Processing.processChanges ConfigureValidator - inserting new config`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let newNetworkAddress = NetworkAddress "localhost:5000"
        let newSharedRewardPercent = 10m

        let initialChxState =
            [
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = Helpers.validatorDeposit + 10m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let actionFee = ChxAmount 1m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "ConfigureValidator"
                    ActionData =
                        {
                            ConfigureValidatorTxActionDto.NetworkAddress = newNetworkAddress.Value
                            SharedRewardPercent = newSharedRewardPercent
                            IsEnabled = true
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - actionFee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + actionFee

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = Success @>
        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>

        let validatorState, validatorChange = output.Validators.[senderWallet.Address]
        test <@ validatorState.NetworkAddress = newNetworkAddress @>
        test <@ validatorState.SharedRewardPercent = newSharedRewardPercent @>
        test <@ validatorChange = ValidatorChange.Add @>

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // RemoveValidator
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Fact>]
    let ``Processing.processChanges RemoveValidator`` () =
        // INIT STATE
        let senderValidatorWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let newNetworkAddress = NetworkAddress "localhost:5000"
        let stakerAddress1, stakerAddress2 = BlockchainAddress "AAA", BlockchainAddress "BBB"

        let initialChxState =
            [
                senderValidatorWallet.Address,
                    {ChxAddressState.Nonce = Nonce 10L; Balance = Helpers.validatorDeposit + 10m}
                validatorWallet.Address,
                    {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let actionFee = ChxAmount 1m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "RemoveValidator"
                    ActionData = RemoveValidatorTxActionDto()
                }
            ]
            |> Helpers.newTx senderValidatorWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getValidatorState _ =
            {
                ValidatorState.NetworkAddress = newNetworkAddress
                SharedRewardPercent = 5m
                TimeToLockDeposit = 0s
                TimeToBlacklist = 0s
                IsEnabled = true
            }
            |> Some

        let getStakeState (stakerAddress, _) =
            if stakerAddress = stakerAddress1 then
                {StakeState.Amount = ChxAmount 100m}
            elif stakerAddress = stakerAddress2 then
                {StakeState.Amount = ChxAmount 80m}
            else
                failwithf "getStakeState should not be called for %A" stakerAddress
            |> Some

        let getStakers _ =
            [
                stakerAddress1
                stakerAddress2
            ]

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetValidatorStateFromStorage = getValidatorState
                GetStakeStateFromStorage = getStakeState
                GetStakersFromStorage = getStakers
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let senderChxBalance = initialChxState.[senderValidatorWallet.Address].Balance - actionFee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + actionFee

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = Success @>
        test <@ output.ChxAddresses.[senderValidatorWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[senderValidatorWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>

        let validatorState, validatorChange = output.Validators.[senderValidatorWallet.Address]
        test <@ validatorState.NetworkAddress = newNetworkAddress @>
        test <@ validatorChange = ValidatorChange.Remove @>
        test <@ output.Stakes.[(stakerAddress1, senderValidatorWallet.Address)].Amount = ChxAmount 0m @>
        test <@ output.Stakes.[(stakerAddress2, senderValidatorWallet.Address)].Amount = ChxAmount 0m @>

    [<Fact>]
    let ``Processing.processChanges RemoveValidator fails if validator not found`` () =
        // INIT STATE
        let senderValidatorWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let newNetworkAddress = NetworkAddress "localhost:5000"
        let stakerAddress1, stakerAddress2 = BlockchainAddress "AAA", BlockchainAddress "BBB"

        let initialChxState =
            [
                senderValidatorWallet.Address,
                    {ChxAddressState.Nonce = Nonce 10L; Balance = Helpers.validatorDeposit + 10m}
                validatorWallet.Address,
                    {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let actionFee = ChxAmount 1m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "RemoveValidator"
                    ActionData = RemoveValidatorTxActionDto()
                }
            ]
            |> Helpers.newTx senderValidatorWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getStakeState (stakerAddress, _) =
            if stakerAddress = stakerAddress1 then
                {StakeState.Amount = ChxAmount 100m}
            elif stakerAddress = stakerAddress2 then
                {StakeState.Amount = ChxAmount 80m}
            else
                failwithf "getStakeState should not be called for %A" stakerAddress
            |> Some

        let getStakers _ =
            [
                stakerAddress1
                stakerAddress2
            ]

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetStakeStateFromStorage = getStakeState
                GetStakersFromStorage = getStakers
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let senderChxBalance = initialChxState.[senderValidatorWallet.Address].Balance - actionFee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + actionFee
        let expectedStatus = (TxActionNumber 1s, TxErrorCode.ValidatorNotFound) |> TxActionError |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>
        test <@ output.ChxAddresses.[senderValidatorWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[senderValidatorWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>

    [<Fact>]
    let ``Processing.processChanges RemoveValidator fails if validator is blacklisted`` () =
        // INIT STATE
        let senderValidatorWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let newNetworkAddress = NetworkAddress "localhost:5000"
        let stakerAddress1, stakerAddress2 = BlockchainAddress "AAA", BlockchainAddress "BBB"

        let initialChxState =
            [
                senderValidatorWallet.Address,
                    {ChxAddressState.Nonce = Nonce 10L; Balance = Helpers.validatorDeposit + 10m}
                validatorWallet.Address,
                    {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let actionFee = ChxAmount 1m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "RemoveValidator"
                    ActionData = RemoveValidatorTxActionDto()
                }
            ]
            |> Helpers.newTx senderValidatorWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getValidatorState _ =
            {
                ValidatorState.NetworkAddress = newNetworkAddress
                SharedRewardPercent = 5m
                TimeToLockDeposit = 0s
                TimeToBlacklist = 2s
                IsEnabled = true
            }
            |> Some

        let getStakeState (stakerAddress, _) =
            if stakerAddress = stakerAddress1 then
                {StakeState.Amount = ChxAmount 100m}
            elif stakerAddress = stakerAddress2 then
                {StakeState.Amount = ChxAmount 80m}
            else
                failwithf "getStakeState should not be called for %A" stakerAddress
            |> Some

        let getStakers _ =
            [
                stakerAddress1
                stakerAddress2
            ]

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetValidatorStateFromStorage = getValidatorState
                GetStakeStateFromStorage = getStakeState
                GetStakersFromStorage = getStakers
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let senderChxBalance = initialChxState.[senderValidatorWallet.Address].Balance - actionFee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + actionFee
        let expectedStatus = (TxActionNumber 1s, TxErrorCode.ValidatorIsBlacklisted) |> TxActionError |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>
        test <@ output.ChxAddresses.[senderValidatorWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[senderValidatorWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>

    [<Fact>]
    let ``Processing.processChanges RemoveValidator fails if validator deposit is locked`` () =
        // INIT STATE
        let senderValidatorWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let newNetworkAddress = NetworkAddress "localhost:5000"
        let stakerAddress1, stakerAddress2 = BlockchainAddress "AAA", BlockchainAddress "BBB"

        let initialChxState =
            [
                senderValidatorWallet.Address,
                    {ChxAddressState.Nonce = Nonce 10L; Balance = Helpers.validatorDeposit + 10m}
                validatorWallet.Address,
                    {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let actionFee = ChxAmount 1m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "RemoveValidator"
                    ActionData = RemoveValidatorTxActionDto()
                }
            ]
            |> Helpers.newTx senderValidatorWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getValidatorState _ =
            {
                ValidatorState.NetworkAddress = newNetworkAddress
                SharedRewardPercent = 5m
                TimeToLockDeposit = 2s
                TimeToBlacklist = 0s
                IsEnabled = true
            }
            |> Some

        let getStakeState (stakerAddress, _) =
            if stakerAddress = stakerAddress1 then
                {StakeState.Amount = ChxAmount 100m}
            elif stakerAddress = stakerAddress2 then
                {StakeState.Amount = ChxAmount 80m}
            else
                failwithf "getStakeState should not be called for %A" stakerAddress
            |> Some

        let getStakers _ =
            [
                stakerAddress1
                stakerAddress2
            ]

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetValidatorStateFromStorage = getValidatorState
                GetStakeStateFromStorage = getStakeState
                GetStakersFromStorage = getStakers
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let senderChxBalance = initialChxState.[senderValidatorWallet.Address].Balance - actionFee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + actionFee
        let expectedStatus = (TxActionNumber 1s, TxErrorCode.ValidatorDepositLocked) |> TxActionError |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>
        test <@ output.ChxAddresses.[senderValidatorWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[senderValidatorWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // DelegateStake
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Theory>]
    [<InlineData(40, 10, 50)>]
    [<InlineData(40, -10, 30)>]
    let ``Processing.processChanges DelegateStake - increasing existing stake``
        (currentStakeAmount : decimal, stakeChangeAmount : decimal, newStakeAmount : decimal)
        =

        // INIT STATE
        let currentStakeAmount = currentStakeAmount |> ChxAmount
        let stakeChangeAmount = stakeChangeAmount |> ChxAmount
        let newStakeAmount = newStakeAmount |> ChxAmount
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let stakeValidatorAddress = (Signing.generateWallet ()).Address

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
                    ActionType = "DelegateStake"
                    ActionData =
                        {
                            ValidatorAddress = stakeValidatorAddress.Value
                            Amount = stakeChangeAmount.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getStakeState _ =
            Some {StakeState.Amount = currentStakeAmount}

        let getTotalChxStaked _ = currentStakeAmount

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetStakeStateFromStorage = getStakeState
                GetTotalChxStakedFromStorage = getTotalChxStaked
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - actionFee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + actionFee

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = Success @>
        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>
        test <@ output.Stakes.[senderWallet.Address, stakeValidatorAddress].Amount = newStakeAmount @>

    [<Fact>]
    let ``Processing.processChanges DelegateStake - setting new stake`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let stakeValidatorAddress = (Signing.generateWallet ()).Address
        let stakeAmount = ChxAmount 10m

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
                    ActionType = "DelegateStake"
                    ActionData =
                        {
                            ValidatorAddress = stakeValidatorAddress.Value
                            Amount = stakeAmount.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getStakeState _ =
            None

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetStakeStateFromStorage = getStakeState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - actionFee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + actionFee

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = Success @>
        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>
        test <@ output.Stakes.[senderWallet.Address, stakeValidatorAddress].Amount = stakeAmount @>

    [<Fact>]
    let ``Processing.processChanges DelegateStake - staking more than available balance`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let stakeValidatorAddress = (Signing.generateWallet ()).Address
        let stakeAmount = ChxAmount 101m

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
                    ActionType = "DelegateStake"
                    ActionData =
                        {
                            ValidatorAddress = stakeValidatorAddress.Value
                            Amount = stakeAmount.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getStakeState _ =
            None

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetStakeStateFromStorage = getStakeState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - actionFee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + actionFee
        let expectedTxStatus =
            (TxActionNumber 1s, TxErrorCode.InsufficientChxBalance)
            |> TxActionError
            |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedTxStatus @>
        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>
        test <@ output.Stakes = Map.empty @>

    [<Fact>]
    let ``Processing.processChanges DelegateStake - staking more than available balance due to the staked CHX`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let stakeValidatorAddress = (Signing.generateWallet ()).Address
        let stakeAmount = ChxAmount 40m
        let currentStakeAmount = ChxAmount 60m

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
                    ActionType = "DelegateStake"
                    ActionData =
                        {
                            ValidatorAddress = stakeValidatorAddress.Value
                            Amount = stakeAmount.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getStakeState _ =
            Some {StakeState.Amount = currentStakeAmount}

        let getTotalChxStaked _ = currentStakeAmount

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetStakeStateFromStorage = getStakeState
                GetTotalChxStakedFromStorage = getTotalChxStaked
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - actionFee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + actionFee
        let expectedTxStatus =
            (TxActionNumber 1s, TxErrorCode.InsufficientChxBalance)
            |> TxActionError
            |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedTxStatus @>
        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>
        test <@ output.Stakes = Map.empty @>

    [<Fact>]
    let ``Processing.processChanges DelegateStake - decreasing stake for more than already staked`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let stakeValidatorAddress = (Signing.generateWallet ()).Address
        let stakeAmount = ChxAmount -51m
        let currentStakeAmount = ChxAmount 50m

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
                    ActionType = "DelegateStake"
                    ActionData =
                        {
                            ValidatorAddress = stakeValidatorAddress.Value
                            Amount = stakeAmount.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getStakeState _ =
            Some {StakeState.Amount = currentStakeAmount}

        let getTotalChxStaked _ = currentStakeAmount

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetStakeStateFromStorage = getStakeState
                GetTotalChxStakedFromStorage = getTotalChxStaked
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - actionFee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + actionFee
        let expectedTxStatus =
            (TxActionNumber 1s, TxErrorCode.InsufficientStake)
            |> TxActionError
            |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedTxStatus @>
        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>
        test <@ output.Stakes.[senderWallet.Address, stakeValidatorAddress].Amount = currentStakeAmount @>

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Trade Orders
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Theory>]
    [<InlineData("BUY", "Buy", "MARKET", "Market", 0, 0, 0, false, "IOC", "ImmediateOrCancel")>]
    [<InlineData("BUY", "Buy", "LIMIT", "Limit", 12, 0, 0, false, "GTC", "GoodTilCancelled")>]
    [<InlineData("BUY", "Buy", "STOP_MARKET", "StopMarket", 0, 11, 0, false, "IOC", "ImmediateOrCancel")>]
    [<InlineData("BUY", "Buy", "STOP_LIMIT", "StopLimit", 12, 11, 0, false, "GTC", "GoodTilCancelled")>]
    [<InlineData(
        "BUY", "Buy", "TRAILING_STOP_MARKET", "TrailingStopMarket", 0, 11, 2, false, "IOC", "ImmediateOrCancel")>]
    [<InlineData(
        "BUY", "Buy", "TRAILING_STOP_LIMIT", "TrailingStopLimit", 12, 11, 3, false, "GTC", "GoodTilCancelled")>]
    [<InlineData(
        "BUY", "Buy", "TRAILING_STOP_MARKET", "TrailingStopMarket", 0, 11, 20, true, "IOC", "ImmediateOrCancel")>]
    [<InlineData(
        "BUY", "Buy", "TRAILING_STOP_LIMIT", "TrailingStopLimit", 12, 11, 30, true, "GTC", "GoodTilCancelled")>]
    [<InlineData("SELL", "Sell", "MARKET", "Market", 0, 0, 0, false, "IOC", "ImmediateOrCancel")>]
    [<InlineData("SELL", "Sell", "LIMIT", "Limit", 10, 0, 0, false, "GTC", "GoodTilCancelled")>]
    [<InlineData("SELL", "Sell", "STOP_MARKET", "StopMarket", 0, 11, 0, false, "IOC", "ImmediateOrCancel")>]
    [<InlineData("SELL", "Sell", "STOP_LIMIT", "StopLimit", 10, 11, 0, false, "GTC", "GoodTilCancelled")>]
    [<InlineData(
        "SELL", "Sell", "TRAILING_STOP_MARKET", "TrailingStopMarket", 0, 11, 2, false, "IOC", "ImmediateOrCancel")>]
    [<InlineData(
        "SELL", "Sell", "TRAILING_STOP_LIMIT", "TrailingStopLimit", 10, 11, 3, false, "GTC", "GoodTilCancelled")>]
    [<InlineData(
        "SELL", "Sell", "TRAILING_STOP_MARKET", "TrailingStopMarket", 0, 11, 20, true, "IOC", "ImmediateOrCancel")>]
    [<InlineData(
        "SELL", "Sell", "TRAILING_STOP_LIMIT", "TrailingStopLimit", 10, 11, 30, true, "GTC", "GoodTilCancelled")>]
    let ``Processing.processChanges PlaceTradeOrder - propagates values into order book``
        (
            sideCode : string,
            sideCaseName : string,
            orderTypeCode : string,
            orderTypeCaseName : string,
            limitPrice : decimal,
            stopPrice : decimal,
            trailingOffset : decimal,
            trailingOffsetIsPercentage : bool,
            timeInForceCode : string,
            timeInForceCaseName : string
        )
        =

        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()

        let initialChxState =
            [
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = Helpers.validatorDeposit + 10m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let actionFee = ChxAmount 1m

        let tradeOrderHash = Hashing.deriveHash senderWallet.Address nonce (TxActionNumber 1s) |> TradeOrderHash
        let orderAmount = 100m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "PlaceTradeOrder"
                    ActionData =
                        {
                            PlaceTradeOrderTxActionDto.AccountHash = Helpers.randomHash ()
                            BaseAssetHash = Helpers.randomHash ()
                            QuoteAssetHash = Helpers.randomHash ()
                            Side = sideCode
                            Amount = orderAmount
                            OrderType = orderTypeCode
                            LimitPrice = limitPrice
                            StopPrice = stopPrice
                            TrailingOffset = trailingOffset
                            TrailingOffsetIsPercentage = trailingOffsetIsPercentage
                            TimeInForce = timeInForceCode
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getAccountState _ =
            Some {AccountState.ControllerAddress = senderWallet.Address}

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = senderWallet.Address; IsEligibilityRequired = false}

        let getHoldingState _ =
            Some {HoldingState.Balance = AssetAmount 2000m; IsEmission = false}

        let getTradeOrderState _ =
            None

        let getTradeOrdersFromStorage _ =
            []

        let getTradingPairState _ =
            {
                TradingPairState.IsEnabled = true
            }
            |> Some

        let getHoldingInTradeOrdersFromStorage _ =
            AssetAmount 0m

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                GetHoldingStateFromStorage = getHoldingState
                GetTradeOrderStateFromStorage = getTradeOrderState
                GetTradeOrdersFromStorage = getTradeOrdersFromStorage
                GetHoldingInTradeOrdersFromStorage = getHoldingInTradeOrdersFromStorage
                GetTradingPairStateFromStorage = getTradingPairState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - actionFee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + actionFee

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = Success @>
        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>

        test <@ output.TradeOrders.Count = 1 @>
        let tradeOrderState, tradeOrderChange = output.TradeOrders.[tradeOrderHash]
        test <@ unionCaseName tradeOrderState.Side = sideCaseName @>
        test <@ tradeOrderState.Amount = AssetAmount orderAmount @>
        test <@ unionCaseName tradeOrderState.OrderType = orderTypeCaseName @>
        test <@ tradeOrderState.LimitPrice = AssetAmount limitPrice @>
        test <@ tradeOrderState.StopPrice = AssetAmount stopPrice @>
        test <@ tradeOrderState.TrailingOffset = AssetAmount trailingOffset @>
        test <@ tradeOrderState.TrailingOffsetIsPercentage = trailingOffsetIsPercentage @>
        test <@ unionCaseName tradeOrderState.TimeInForce = timeInForceCaseName @>

        test <@ tradeOrderState.IsExecutable = not tradeOrderState.IsStopOrder @>
        test <@ tradeOrderState.AmountFilled.Value = 0m @>

        if tradeOrderState.TimeInForce = TradeOrderTimeInForce.ImmediateOrCancel && not tradeOrderState.IsStopOrder then
            test <@ tradeOrderState.Status = TradeOrderStatus.Cancelled TradeOrderCancelReason.TriggeredByTimeInForce @>
            test <@ tradeOrderChange = TradeOrderChange.Remove @>
        else
            test <@ tradeOrderState.Status = TradeOrderStatus.Open @>
            test <@ tradeOrderChange = TradeOrderChange.Add @>

    [<Fact>]
    let ``Processing.processChanges PlaceTradeOrder - Error: AccountNotFound`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()

        let initialChxState =
            [
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = Helpers.validatorDeposit + 10m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let actionFee = ChxAmount 1m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "PlaceTradeOrder"
                    ActionData =
                        {
                            PlaceTradeOrderTxActionDto.AccountHash = Helpers.randomHash ()
                            BaseAssetHash = Helpers.randomHash ()
                            QuoteAssetHash = Helpers.randomHash ()
                            Side = "BUY"
                            Amount = 100m
                            OrderType = "MARKET"
                            LimitPrice = 0m
                            StopPrice = 0m
                            TrailingOffset = 0m
                            TrailingOffsetIsPercentage = false
                            TimeInForce = "IOC"
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getAccountState _ =
            None

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = senderWallet.Address; IsEligibilityRequired = false}

        let getTradingPairState _ =
            {
                TradingPairState.IsEnabled = true
            }
            |> Some

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                GetTradingPairStateFromStorage = getTradingPairState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - actionFee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + actionFee
        let expectedStatus = (TxActionNumber 1s, TxErrorCode.AccountNotFound) |> TxActionError |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>
        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>

        test <@ output.TradeOrders.Count = 0 @>

    [<Fact>]
    let ``Processing.processChanges PlaceTradeOrder - Error: TradingPairNotFound`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()

        let initialChxState =
            [
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = Helpers.validatorDeposit + 10m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let actionFee = ChxAmount 1m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "PlaceTradeOrder"
                    ActionData =
                        {
                            PlaceTradeOrderTxActionDto.AccountHash = Helpers.randomHash ()
                            BaseAssetHash = Helpers.randomHash ()
                            QuoteAssetHash = Helpers.randomHash ()
                            Side = "BUY"
                            Amount = 100m
                            OrderType = "MARKET"
                            LimitPrice = 0m
                            StopPrice = 0m
                            TrailingOffset = 0m
                            TrailingOffsetIsPercentage = false
                            TimeInForce = "IOC"
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getAccountState _ =
            Some {AccountState.ControllerAddress = senderWallet.Address}

        let getAssetState assetHash =
            {AssetState.AssetCode = None; ControllerAddress = senderWallet.Address; IsEligibilityRequired = false}
            |> Some

        let getTradingPairState _ =
            None

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                GetTradingPairStateFromStorage = getTradingPairState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - actionFee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + actionFee
        let expectedStatus = (TxActionNumber 1s, TxErrorCode.TradingPairNotFound) |> TxActionError |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>
        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>

        test <@ output.TradeOrders.Count = 0 @>

    [<Fact>]
    let ``Processing.processChanges PlaceTradeOrder - Error: SenderIsNotSourceAccountController`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let accountControllerWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()

        let initialChxState =
            [
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = Helpers.validatorDeposit + 10m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let actionFee = ChxAmount 1m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "PlaceTradeOrder"
                    ActionData =
                        {
                            PlaceTradeOrderTxActionDto.AccountHash = Helpers.randomHash ()
                            BaseAssetHash = Helpers.randomHash ()
                            QuoteAssetHash = Helpers.randomHash ()
                            Side = "BUY"
                            Amount = 100m
                            OrderType = "MARKET"
                            LimitPrice = 0m
                            StopPrice = 0m
                            TrailingOffset = 0m
                            TrailingOffsetIsPercentage = false
                            TimeInForce = "IOC"
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getAccountState _ =
            Some {AccountState.ControllerAddress = accountControllerWallet.Address}

        let getAssetState assetHash =
            Some {AssetState.AssetCode = None; ControllerAddress = senderWallet.Address; IsEligibilityRequired = false}

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - actionFee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + actionFee
        let expectedStatus =
            (TxActionNumber 1s, TxErrorCode.SenderIsNotSourceAccountController) |> TxActionError |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>
        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>

        test <@ output.TradeOrders.Count = 0 @>

    [<Fact>]
    let ``Processing.processChanges PlaceTradeOrder SELL then TransferAsset - InsufficientAssetHoldingBalance`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()

        let initialChxState =
            [
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = Helpers.validatorDeposit + 10m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let actionFee = ChxAmount 1m

        let accountHash1 = Helpers.randomHash ()
        let accountHash2 = Helpers.randomHash ()
        let assetHash1 = Helpers.randomHash ()
        let assetHash2 = Helpers.randomHash ()

        let txHash, txEnvelope =
            [
                {
                    ActionType = "PlaceTradeOrder"
                    ActionData =
                        {
                            PlaceTradeOrderTxActionDto.AccountHash = accountHash1
                            BaseAssetHash = assetHash1
                            QuoteAssetHash = assetHash2
                            Side = "SELL"
                            Amount = 100m
                            OrderType = "MARKET"
                            LimitPrice = 0m
                            StopPrice = 0m
                            TrailingOffset = 0m
                            TrailingOffsetIsPercentage = false
                            TimeInForce = "IOC"
                        }
                }
                {
                    ActionType = "TransferAsset"
                    ActionData =
                        {
                            TransferAssetTxActionDto.FromAccountHash = accountHash1
                            ToAccountHash = accountHash2
                            AssetHash = assetHash1
                            Amount = 100m
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getAccountState _ =
            Some {AccountState.ControllerAddress = senderWallet.Address}

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = senderWallet.Address; IsEligibilityRequired = false}

        let getHoldingStateFromStorage _ =
            Some {HoldingState.Balance = AssetAmount 150m; IsEmission = false}

        let getHoldingInTradeOrdersFromStorage _ =
            AssetAmount 0m

        let getTradingPairStateFromStorage _ =
            Some {TradingPairState.IsEnabled = true}

        let getTradeOrderStateFromStorage _ =
            None

        let getTradeOrdersFromStorage _ =
            []

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                GetHoldingStateFromStorage = getHoldingStateFromStorage
                GetHoldingInTradeOrdersFromStorage = getHoldingInTradeOrdersFromStorage
                GetTradingPairStateFromStorage = getTradingPairStateFromStorage
                GetTradeOrderStateFromStorage = getTradeOrderStateFromStorage
                GetTradeOrdersFromStorage = getTradeOrdersFromStorage
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let totalFee = actionFee * 2m // Two actions
        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - totalFee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + totalFee
        let expectedStatus =
            (TxActionNumber 2s, TxErrorCode.InsufficientAssetHoldingBalance) |> TxActionError |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>
        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>

        test <@ output.TradeOrders.Count = 0 @>

    [<Fact>]
    let ``Processing.processChanges PlaceTradeOrder BUY then TransferAsset - InsufficientAssetHoldingBalance`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()

        let initialChxState =
            [
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = Helpers.validatorDeposit + 10m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let actionFee = ChxAmount 1m

        let accountHash1 = Helpers.randomHash ()
        let accountHash2 = Helpers.randomHash ()
        let assetHash1 = Helpers.randomHash ()
        let assetHash2 = Helpers.randomHash ()

        let txHash, txEnvelope =
            [
                {
                    ActionType = "PlaceTradeOrder"
                    ActionData =
                        {
                            PlaceTradeOrderTxActionDto.AccountHash = accountHash1
                            BaseAssetHash = assetHash1
                            QuoteAssetHash = assetHash2
                            Side = "BUY"
                            Amount = 100m
                            OrderType = "LIMIT"
                            LimitPrice = 3m
                            StopPrice = 0m
                            TrailingOffset = 0m
                            TrailingOffsetIsPercentage = false
                            TimeInForce = "IOC"
                        }
                }
                {
                    ActionType = "TransferAsset"
                    ActionData =
                        {
                            TransferAssetTxActionDto.FromAccountHash = accountHash1
                            ToAccountHash = accountHash2
                            AssetHash = assetHash2
                            Amount = 100m
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getAccountState _ =
            Some {AccountState.ControllerAddress = senderWallet.Address}

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = senderWallet.Address; IsEligibilityRequired = false}

        let getHoldingStateFromStorage _ =
            Some {HoldingState.Balance = AssetAmount 350m; IsEmission = false}

        let getHoldingInTradeOrdersFromStorage _ =
            AssetAmount 0m

        let getTradingPairStateFromStorage _ =
            Some {TradingPairState.IsEnabled = true}

        let getTradeOrderStateFromStorage _ =
            None

        let getTradeOrdersFromStorage _ =
            []

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                GetHoldingStateFromStorage = getHoldingStateFromStorage
                GetHoldingInTradeOrdersFromStorage = getHoldingInTradeOrdersFromStorage
                GetTradingPairStateFromStorage = getTradingPairStateFromStorage
                GetTradeOrderStateFromStorage = getTradeOrderStateFromStorage
                GetTradeOrdersFromStorage = getTradeOrdersFromStorage
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let totalFee = actionFee * 2m // Two actions
        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - totalFee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + totalFee
        let expectedStatus =
            (TxActionNumber 2s, TxErrorCode.InsufficientAssetHoldingBalance) |> TxActionError |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>
        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>

        test <@ output.TradeOrders.Count = 0 @>

    [<Fact>]
    let ``Processing.processChanges PlaceTradeOrder twice - InsufficientBaseAssetBalance`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()

        let initialChxState =
            [
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = Helpers.validatorDeposit + 10m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let actionFee = ChxAmount 1m

        let accountHash1 = Helpers.randomHash ()
        let accountHash2 = Helpers.randomHash ()
        let assetHash1 = Helpers.randomHash ()
        let assetHash2 = Helpers.randomHash ()

        let txHash, txEnvelope =
            [
                {
                    ActionType = "PlaceTradeOrder"
                    ActionData =
                        {
                            PlaceTradeOrderTxActionDto.AccountHash = accountHash1
                            BaseAssetHash = assetHash1
                            QuoteAssetHash = assetHash2
                            Side = "SELL"
                            Amount = 100m
                            OrderType = "MARKET"
                            LimitPrice = 0m
                            StopPrice = 0m
                            TrailingOffset = 0m
                            TrailingOffsetIsPercentage = false
                            TimeInForce = "IOC"
                        }
                }
                {
                    ActionType = "PlaceTradeOrder"
                    ActionData =
                        {
                            PlaceTradeOrderTxActionDto.AccountHash = accountHash1
                            BaseAssetHash = assetHash1
                            QuoteAssetHash = assetHash2
                            Side = "SELL"
                            Amount = 100m
                            OrderType = "MARKET"
                            LimitPrice = 0m
                            StopPrice = 0m
                            TrailingOffset = 0m
                            TrailingOffsetIsPercentage = false
                            TimeInForce = "IOC"
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getAccountState _ =
            Some {AccountState.ControllerAddress = senderWallet.Address}

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = senderWallet.Address; IsEligibilityRequired = false}

        let getHoldingStateFromStorage _ =
            Some {HoldingState.Balance = AssetAmount 150m; IsEmission = false}

        let getHoldingInTradeOrdersFromStorage _ =
            AssetAmount 0m

        let getTradingPairStateFromStorage _ =
            Some {TradingPairState.IsEnabled = true}

        let getTradeOrderStateFromStorage _ =
            None

        let getTradeOrdersFromStorage _ =
            []

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                GetHoldingStateFromStorage = getHoldingStateFromStorage
                GetHoldingInTradeOrdersFromStorage = getHoldingInTradeOrdersFromStorage
                GetTradingPairStateFromStorage = getTradingPairStateFromStorage
                GetTradeOrderStateFromStorage = getTradeOrderStateFromStorage
                GetTradeOrdersFromStorage = getTradeOrdersFromStorage
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let totalFee = actionFee * 2m // Two actions
        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - totalFee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + totalFee
        let expectedStatus =
            (TxActionNumber 2s, TxErrorCode.InsufficientBaseAssetBalance) |> TxActionError |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>
        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>

        test <@ output.TradeOrders.Count = 0 @>

    [<Fact>]
    let ``Processing.processChanges PlaceTradeOrder twice - InsufficientQuoteAssetBalance`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()

        let initialChxState =
            [
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = Helpers.validatorDeposit + 10m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let actionFee = ChxAmount 1m

        let accountHash1 = Helpers.randomHash ()
        let accountHash2 = Helpers.randomHash ()
        let assetHash1 = Helpers.randomHash ()
        let assetHash2 = Helpers.randomHash ()

        let txHash, txEnvelope =
            [
                {
                    ActionType = "PlaceTradeOrder"
                    ActionData =
                        {
                            PlaceTradeOrderTxActionDto.AccountHash = accountHash1
                            BaseAssetHash = assetHash1
                            QuoteAssetHash = assetHash2
                            Side = "BUY"
                            Amount = 100m
                            OrderType = "LIMIT"
                            LimitPrice = 3m
                            StopPrice = 0m
                            TrailingOffset = 0m
                            TrailingOffsetIsPercentage = false
                            TimeInForce = "IOC"
                        }
                }
                {
                    ActionType = "PlaceTradeOrder"
                    ActionData =
                        {
                            PlaceTradeOrderTxActionDto.AccountHash = accountHash1
                            BaseAssetHash = assetHash1
                            QuoteAssetHash = assetHash2
                            Side = "BUY"
                            Amount = 100m
                            OrderType = "LIMIT"
                            LimitPrice = 3m
                            StopPrice = 0m
                            TrailingOffset = 0m
                            TrailingOffsetIsPercentage = false
                            TimeInForce = "IOC"
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getAccountState _ =
            Some {AccountState.ControllerAddress = senderWallet.Address}

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = senderWallet.Address; IsEligibilityRequired = false}

        let getHoldingStateFromStorage _ =
            Some {HoldingState.Balance = AssetAmount 350m; IsEmission = false}

        let getHoldingInTradeOrdersFromStorage _ =
            AssetAmount 0m

        let getTradingPairStateFromStorage _ =
            Some {TradingPairState.IsEnabled = true}

        let getTradeOrderStateFromStorage _ =
            None

        let getTradeOrdersFromStorage _ =
            []

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                GetHoldingStateFromStorage = getHoldingStateFromStorage
                GetHoldingInTradeOrdersFromStorage = getHoldingInTradeOrdersFromStorage
                GetTradingPairStateFromStorage = getTradingPairStateFromStorage
                GetTradeOrderStateFromStorage = getTradeOrderStateFromStorage
                GetTradeOrdersFromStorage = getTradeOrdersFromStorage
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let totalFee = actionFee * 2m // Two actions
        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - totalFee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + totalFee
        let expectedStatus =
            (TxActionNumber 2s, TxErrorCode.InsufficientQuoteAssetBalance) |> TxActionError |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>
        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>

        test <@ output.TradeOrders.Count = 0 @>

    [<Fact>]
    let ``Processing.processChanges CancelTradeOrder - Error: TradeOrderNotFound`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()

        let initialChxState =
            [
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = Helpers.validatorDeposit + 10m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let actionFee = ChxAmount 1m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "CancelTradeOrder"
                    ActionData =
                        {
                            CancelTradeOrderTxActionDto.TradeOrderHash = Helpers.randomHash ()
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getTradeOrderState _ =
            None

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetTradeOrderStateFromStorage = getTradeOrderState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - actionFee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + actionFee
        let expectedStatus =
            (TxActionNumber 1s, TxErrorCode.TradeOrderNotFound) |> TxActionError |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>
        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>

        test <@ output.TradeOrders.Count = 0 @>

    [<Fact>]
    let ``Processing.processChanges CancelTradeOrder - Exception: Cannot get state for account `` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()

        let initialChxState =
            [
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = Helpers.validatorDeposit + 10m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let actionFee = ChxAmount 1m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "CancelTradeOrder"
                    ActionData =
                        {
                            CancelTradeOrderTxActionDto.TradeOrderHash = Helpers.randomHash ()
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getTradeOrderState _ =
            {
                TradeOrderState.BlockNumber = BlockNumber 1L
                TxPosition = 2
                ActionNumber = TxActionNumber 3s
                AccountHash = Helpers.randomHash () |> AccountHash
                BaseAssetHash = Helpers.randomHash () |> AssetHash
                QuoteAssetHash = Helpers.randomHash () |> AssetHash
                Side = Buy
                Amount = AssetAmount 100m
                OrderType = TradeOrderType.Market
                LimitPrice = AssetAmount 0m
                StopPrice = AssetAmount 0m
                TrailingOffset = AssetAmount 0m
                TrailingOffsetIsPercentage = false
                TimeInForce = TradeOrderTimeInForce.ImmediateOrCancel
                IsExecutable = true
                AmountFilled = AssetAmount 30m
                Status = TradeOrderStatus.Open
            }
            |> Some

        let getAccountState _ =
            None

        // ACT
        let processChanges () =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetAccountStateFromStorage = getAccountState
                GetTradeOrderStateFromStorage = getTradeOrderState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        raisesWith<exn>
            <@ processChanges () @>
            (fun ex -> <@ ex.Message.StartsWith "Cannot get state for account" @>)

    [<Fact>]
    let ``Processing.processChanges CancelTradeOrder - Error: SenderIsNotSourceAccountController`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let accountControllerWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()

        let initialChxState =
            [
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = Helpers.validatorDeposit + 10m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let actionFee = ChxAmount 1m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "CancelTradeOrder"
                    ActionData =
                        {
                            CancelTradeOrderTxActionDto.TradeOrderHash = Helpers.randomHash ()
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getTradeOrderState _ =
            {
                TradeOrderState.BlockNumber = BlockNumber 1L
                TxPosition = 2
                ActionNumber = TxActionNumber 3s
                AccountHash = Helpers.randomHash () |> AccountHash
                BaseAssetHash = Helpers.randomHash () |> AssetHash
                QuoteAssetHash = Helpers.randomHash () |> AssetHash
                Side = Buy
                Amount = AssetAmount 100m
                OrderType = TradeOrderType.Market
                LimitPrice = AssetAmount 0m
                StopPrice = AssetAmount 0m
                TrailingOffset = AssetAmount 0m
                TrailingOffsetIsPercentage = false
                TimeInForce = TradeOrderTimeInForce.ImmediateOrCancel
                IsExecutable = true
                AmountFilled = AssetAmount 30m
                Status = TradeOrderStatus.Open
            }
            |> Some

        let getAccountState _ =
            Some {AccountState.ControllerAddress = accountControllerWallet.Address}

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetAccountStateFromStorage = getAccountState
                GetTradeOrderStateFromStorage = getTradeOrderState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - actionFee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + actionFee
        let expectedStatus =
            (TxActionNumber 1s, TxErrorCode.SenderIsNotSourceAccountController) |> TxActionError |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>
        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>

        test <@ output.TradeOrders.Count = 0 @>

    [<Fact>]
    let ``Processing.processChanges CancelTradeOrder - Order removed from state`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()

        let initialChxState =
            [
                senderWallet.Address, {ChxAddressState.Nonce = Nonce 10L; Balance = Helpers.validatorDeposit + 10m}
                validatorWallet.Address, {ChxAddressState.Nonce = Nonce 30L; Balance = ChxAmount 100m}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let actionFee = ChxAmount 1m

        let tradeOrderHash = Helpers.randomHash () |> TradeOrderHash

        let txHash, txEnvelope =
            [
                {
                    ActionType = "CancelTradeOrder"
                    ActionData =
                        {
                            CancelTradeOrderTxActionDto.TradeOrderHash = tradeOrderHash.Value
                        }
                }
            ]
            |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee

        let txSet = [txHash]

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxAddressState address =
            initialChxState |> Map.tryFind address

        let getTradeOrderState _ =
            {
                TradeOrderState.BlockNumber = BlockNumber 1L
                TxPosition = 2
                ActionNumber = TxActionNumber 3s
                AccountHash = Helpers.randomHash () |> AccountHash
                BaseAssetHash = Helpers.randomHash () |> AssetHash
                QuoteAssetHash = Helpers.randomHash () |> AssetHash
                Side = Buy
                Amount = AssetAmount 100m
                OrderType = TradeOrderType.Market
                LimitPrice = AssetAmount 0m
                StopPrice = AssetAmount 0m
                TrailingOffset = AssetAmount 0m
                TrailingOffsetIsPercentage = false
                TimeInForce = TradeOrderTimeInForce.ImmediateOrCancel
                IsExecutable = true
                AmountFilled = AssetAmount 30m
                Status = TradeOrderStatus.Open
            }
            |> Some

        let getAccountState _ =
            Some {AccountState.ControllerAddress = senderWallet.Address}

        // ACT
        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetAccountStateFromStorage = getAccountState
                GetTradeOrderStateFromStorage = getTradeOrderState
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
            }
            |> Helpers.processChanges

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Balance - actionFee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Balance + actionFee

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = Success @>
        test <@ output.ChxAddresses.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxAddresses.[senderWallet.Address].Balance = senderChxBalance @>
        test <@ output.ChxAddresses.[validatorWallet.Address].Balance = validatorChxBalance @>

        test <@ output.TradeOrders.Count = 1 @>
        let tradeOrderState, tradeOrderChange = output.TradeOrders.[tradeOrderHash]
        test <@ tradeOrderState.Side = Buy @>
        test <@ tradeOrderState.Amount.Value = 100m @>
        test <@ tradeOrderState.OrderType = TradeOrderType.Market @>
        test <@ tradeOrderState.LimitPrice.Value = 0m @>
        test <@ tradeOrderState.StopPrice.Value = 0m @>
        test <@ tradeOrderState.TrailingOffset.Value = 0m @>
        test <@ tradeOrderState.TrailingOffsetIsPercentage = false @>
        test <@ tradeOrderState.TimeInForce = TradeOrderTimeInForce.ImmediateOrCancel @>
        test <@ tradeOrderChange = TradeOrderChange.Remove @>
