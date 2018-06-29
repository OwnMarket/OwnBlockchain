namespace Chainium.Blockchain.Public.Core.Tests

open Xunit
open Swensen.Unquote
open Chainium.Common
open Chainium.Blockchain.Public.Core
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Core.Dtos
open Chainium.Blockchain.Public.Crypto

module ProcessingTests =

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Tx preparation
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Fact>]
    let ``Processing.excludeUnprocessableTxs excludes txs after nonce gap`` () =
        let w1 = Signing.generateWallet ()
        let w2 = Signing.generateWallet ()

        let getChxBalanceState =
            let data =
                [
                    w1.Address, { ChxBalanceState.Amount = ChxAmount 100M; Nonce = Nonce 10L }
                    w2.Address, { ChxBalanceState.Amount = ChxAmount 200M; Nonce = Nonce 20L }
                ]
                |> Map.ofSeq

            fun (address : ChainiumAddress) -> data |> Map.tryFind address

        let txSet =
            [
                Helpers.newPendingTxInfo (TxHash "Tx2") w1.Address (Nonce 12L) (ChxAmount 1M) 1s 2L
                Helpers.newPendingTxInfo (TxHash "Tx3") w1.Address (Nonce 10L) (ChxAmount 1M) 1s 3L
                Helpers.newPendingTxInfo (TxHash "Tx4") w1.Address (Nonce 14L) (ChxAmount 1M) 1s 4L
                Helpers.newPendingTxInfo (TxHash "Tx5") w1.Address (Nonce 11L) (ChxAmount 1M) 1s 5L
                Helpers.newPendingTxInfo (TxHash "Tx1") w2.Address (Nonce 21L) (ChxAmount 1M) 1s 1L
            ]

        // ACT
        let txHashes =
            txSet
            |> Processing.excludeUnprocessableTxs getChxBalanceState
            |> List.map (fun tx -> tx.TxHash |> fun (TxHash hash) -> hash)

        test <@ txHashes = ["Tx1"; "Tx2"; "Tx3"; "Tx5"] @>

    [<Theory>]
    [<InlineData (1, "Tx1")>]
    [<InlineData (3, "Tx1; Tx3")>]
    [<InlineData (4, "Tx1; Tx3; Tx5")>]
    let ``Processing.excludeUnprocessableTxs excludes txs if CHX balance cannot cover the fees``
        (balance : decimal, txHashes : string)
        =

        let balance = ChxAmount balance
        let expectedHashes = txHashes.Split("; ") |> Array.toList

        let w1 = Signing.generateWallet ()
        let w2 = Signing.generateWallet ()

        let getChxBalanceState =
            let data =
                [
                    w1.Address, { ChxBalanceState.Amount = balance; Nonce = Nonce 10L }
                    w2.Address, { ChxBalanceState.Amount = ChxAmount 200M; Nonce = Nonce 20L }
                ]
                |> Map.ofSeq

            fun (address : ChainiumAddress) -> data |> Map.tryFind address

        let txSet =
            [
                Helpers.newPendingTxInfo (TxHash "Tx2") w1.Address (Nonce 12L) (ChxAmount 1M) 1s 2L
                Helpers.newPendingTxInfo (TxHash "Tx3") w1.Address (Nonce 10L) (ChxAmount 1.5M) 2s 3L
                Helpers.newPendingTxInfo (TxHash "Tx4") w1.Address (Nonce 14L) (ChxAmount 1M) 1s 4L
                Helpers.newPendingTxInfo (TxHash "Tx5") w1.Address (Nonce 11L) (ChxAmount 1M) 1s 5L
                Helpers.newPendingTxInfo (TxHash "Tx1") w2.Address (Nonce 21L) (ChxAmount 1M) 1s 1L
            ]

        // ACT
        let txHashes =
            txSet
            |> Processing.excludeUnprocessableTxs getChxBalanceState
            |> List.map (fun tx -> tx.TxHash |> fun (TxHash hash) -> hash)

        test <@ txHashes = expectedHashes @>

    [<Fact>]
    let ``Processing.orderTxSet puts txs in correct order`` () =
        let w1 = Signing.generateWallet ()
        let w2 = Signing.generateWallet ()

        let getChxBalanceState =
            let data =
                [
                    w1.Address, { ChxBalanceState.Amount = ChxAmount 100M; Nonce = Nonce 10L }
                    w2.Address, { ChxBalanceState.Amount = ChxAmount 200M; Nonce = Nonce 20L }
                ]
                |> Map.ofSeq

            fun (address : ChainiumAddress) -> data.[address]

        let txSet =
            [
                Helpers.newPendingTxInfo (TxHash "Tx1") w2.Address (Nonce 21L) (ChxAmount 1M) 1s 1L
                Helpers.newPendingTxInfo (TxHash "Tx2") w1.Address (Nonce 12L) (ChxAmount 1M) 1s 2L
                Helpers.newPendingTxInfo (TxHash "Tx3") w1.Address (Nonce 10L) (ChxAmount 1M) 1s 3L
                Helpers.newPendingTxInfo (TxHash "Tx6") w2.Address (Nonce 21L) (ChxAmount 2M) 1s 6L
                Helpers.newPendingTxInfo (TxHash "Tx5") w1.Address (Nonce 11L) (ChxAmount 1M) 1s 5L
            ]

        // ACT
        let txHashes =
            txSet
            |> Processing.orderTxSet
            |> List.map (fun (TxHash hash) -> hash)

        test <@ txHashes = ["Tx6"; "Tx1"; "Tx3"; "Tx5"; "Tx2"] @>

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // TransferChx
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Fact>]
    let ``Processing.processTxSet TransferChx`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let recipientWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()

        let initialChxState =
            [
                senderWallet.Address, {ChxBalanceState.Amount = ChxAmount 100M; Nonce = Nonce 10L}
                recipientWallet.Address, {ChxBalanceState.Amount = ChxAmount 100M; Nonce = Nonce 20L}
                validatorWallet.Address, {ChxBalanceState.Amount = ChxAmount 100M; Nonce = Nonce 30L}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let fee = ChxAmount 1M
        let amountToTransfer = ChxAmount 10M

        let txHash, txEnvelope =
            [
                {
                    ActionType = "TransferChx"
                    ActionData =
                        {
                            RecipientAddress = recipientWallet.Address |> fun (ChainiumAddress a) -> a
                            Amount = amountToTransfer |> fun (ChxAmount a) -> a
                        }
                } :> obj
            ]
            |> Helpers.newTx senderWallet.PrivateKey nonce fee

        let txSet = [txHash]
        let blockNumber = BlockNumber 0L;

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxBalanceState address =
            initialChxState |> Map.tryFind address

        let getHoldingState _ =
            failwith "getHoldingState should not be called"

        let getAccountController _ =
            failwith "getAccountController should not be called"

        let getAssetState _ =
            failwith "getAssetState should not be called"

        // ACT
        let output =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                Hashing.isValidChainiumAddress
                getChxBalanceState
                getHoldingState
                getAccountController
                getAssetState
                Helpers.minTxActionFee
                validatorWallet.Address
                blockNumber
                txSet

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Amount - amountToTransfer - fee
        let recipientChxBalance = initialChxState.[recipientWallet.Address].Amount + amountToTransfer
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Amount + fee

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = Success @>
        test <@ output.ChxBalances.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxBalances.[recipientWallet.Address].Nonce = initialChxState.[recipientWallet.Address].Nonce @>
        test <@ output.ChxBalances.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxBalances.[senderWallet.Address].Amount = senderChxBalance @>
        test <@ output.ChxBalances.[recipientWallet.Address].Amount = recipientChxBalance @>
        test <@ output.ChxBalances.[validatorWallet.Address].Amount = validatorChxBalance @>

    [<Fact>]
    let ``Processing.processTxSet TransferChx with insufficient balance`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let recipientWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()

        let initialChxState =
            [
                senderWallet.Address, {ChxBalanceState.Amount = ChxAmount 10M; Nonce = Nonce 10L}
                recipientWallet.Address, {ChxBalanceState.Amount = ChxAmount 100M; Nonce = Nonce 20L}
                validatorWallet.Address, {ChxBalanceState.Amount = ChxAmount 100M; Nonce = Nonce 30L}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let fee = ChxAmount 1M
        let amountToTransfer = ChxAmount 10M

        let txHash, txEnvelope =
            [
                {
                    ActionType = "TransferChx"
                    ActionData =
                        {
                            RecipientAddress = recipientWallet.Address |> fun (ChainiumAddress a) -> a
                            Amount = amountToTransfer |> fun (ChxAmount a) -> a
                        }
                } :> obj
            ]
            |> Helpers.newTx senderWallet.PrivateKey nonce fee

        let txSet = [txHash]
        let blockNumber = BlockNumber 0L;

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxBalanceState address =
            initialChxState |> Map.tryFind address

        let getHoldingState _ =
            failwith "getHoldingState should not be called"

        let getAccountController _ =
            failwith "getAccountController should not be called"

        let getAssetState _ =
            failwith "getAssetState should not be called"

        // ACT
        let output =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                Hashing.isValidChainiumAddress
                getChxBalanceState
                getHoldingState
                getAccountController
                getAssetState
                Helpers.minTxActionFee
                validatorWallet.Address
                blockNumber
                txSet

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Amount - fee
        let recipientChxBalance = initialChxState.[recipientWallet.Address].Amount
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Amount + fee
        let expectedStatus = (TxActionNumber 1s, TxErrorCode.InsufficientChxBalance) |> TxActionError |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>
        test <@ output.ChxBalances.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxBalances.[recipientWallet.Address].Nonce = initialChxState.[recipientWallet.Address].Nonce @>
        test <@ output.ChxBalances.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxBalances.[senderWallet.Address].Amount = senderChxBalance @>
        test <@ output.ChxBalances.[recipientWallet.Address].Amount = recipientChxBalance @>
        test <@ output.ChxBalances.[validatorWallet.Address].Amount = validatorChxBalance @>

    [<Fact>]
    let ``Processing.processTxSet TransferChx with insufficient balance to cover fee`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let recipientWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()

        let initialChxState =
            [
                senderWallet.Address, {ChxBalanceState.Amount = ChxAmount 9.5M; Nonce = Nonce 10L}
                recipientWallet.Address, {ChxBalanceState.Amount = ChxAmount 100M; Nonce = Nonce 20L}
                validatorWallet.Address, {ChxBalanceState.Amount = ChxAmount 100M; Nonce = Nonce 30L}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let fee = ChxAmount 1M
        let amountToTransfer = ChxAmount 10M

        let txHash, txEnvelope =
            [
                {
                    ActionType = "TransferChx"
                    ActionData =
                        {
                            RecipientAddress = recipientWallet.Address |> fun (ChainiumAddress a) -> a
                            Amount = amountToTransfer |> fun (ChxAmount a) -> a
                        }
                } :> obj
            ]
            |> Helpers.newTx senderWallet.PrivateKey nonce fee

        let txSet = [txHash]
        let blockNumber = BlockNumber 0L;

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxBalanceState address =
            initialChxState |> Map.tryFind address

        let getHoldingState _ =
            failwith "getHoldingState should not be called"

        let getAccountController _ =
            failwith "getAccountController should not be called"

        let getAssetState _ =
            failwith "getAssetState should not be called"

        // ACT
        let output =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                Hashing.isValidChainiumAddress
                getChxBalanceState
                getHoldingState
                getAccountController
                getAssetState
                Helpers.minTxActionFee
                validatorWallet.Address
                blockNumber
                txSet

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Amount - fee
        let recipientChxBalance = initialChxState.[recipientWallet.Address].Amount
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Amount + fee
        let expectedStatus = (TxActionNumber 1s, TxErrorCode.InsufficientChxBalance) |> TxActionError |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>
        test <@ output.ChxBalances.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxBalances.[recipientWallet.Address].Nonce = initialChxState.[recipientWallet.Address].Nonce @>
        test <@ output.ChxBalances.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxBalances.[senderWallet.Address].Amount = senderChxBalance @>
        test <@ output.ChxBalances.[recipientWallet.Address].Amount = recipientChxBalance @>
        test <@ output.ChxBalances.[validatorWallet.Address].Amount = validatorChxBalance @>

    [<Fact>]
    let ``Processing.processTxSet TransferChx with insufficient balance to cover fee - simulated invalid state`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let recipientWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()

        let initialChxState =
            [
                senderWallet.Address, {ChxBalanceState.Amount = ChxAmount 0.5M; Nonce = Nonce 10L}
                recipientWallet.Address, {ChxBalanceState.Amount = ChxAmount 100M; Nonce = Nonce 20L}
                validatorWallet.Address, {ChxBalanceState.Amount = ChxAmount 100M; Nonce = Nonce 30L}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let fee = ChxAmount 1M
        let amountToTransfer = ChxAmount 10M

        let txHash, txEnvelope =
            [
                {
                    ActionType = "TransferChx"
                    ActionData =
                        {
                            RecipientAddress = recipientWallet.Address |> fun (ChainiumAddress a) -> a
                            Amount = amountToTransfer |> fun (ChxAmount a) -> a
                        }
                } :> obj
            ]
            |> Helpers.newTx senderWallet.PrivateKey nonce fee

        let txSet = [txHash]
        let blockNumber = BlockNumber 0L;

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxBalanceState address =
            initialChxState |> Map.tryFind address

        let getHoldingState _ =
            failwith "getHoldingState should not be called"

        let getAccountController _ =
            failwith "getAccountController should not be called"

        let getAssetState _ =
            failwith "getAssetState should not be called"

        let processTxSet () =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                Hashing.isValidChainiumAddress
                getChxBalanceState
                getHoldingState
                getAccountController
                getAssetState
                Helpers.minTxActionFee
                validatorWallet.Address
                blockNumber
                txSet

        // ASSERT
        raisesWith<exn>
            <@ processTxSet () @>
            (fun ex -> <@ ex.Message.StartsWith "Cannot process validator reward" @>)

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // TransferAsset
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Fact>]
    let ``Processing.processTxSet TransferAsset`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let senderAccountHash = AccountHash "Acc1"
        let recipientAccountHash = AccountHash "Acc2"
        let assetHash = AssetHash "EQ1"

        let initialChxState =
            [
                senderWallet.Address, {ChxBalanceState.Amount = ChxAmount 100M; Nonce = Nonce 10L}
                validatorWallet.Address, {ChxBalanceState.Amount = ChxAmount 100M; Nonce = Nonce 30L}
            ]
            |> Map.ofList

        let initialHoldingState =
            [
                (senderAccountHash, assetHash), {HoldingState.Amount = AssetAmount 50M}
                (recipientAccountHash, assetHash), {HoldingState.Amount = AssetAmount 0M}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let fee = ChxAmount 1M
        let amountToTransfer = AssetAmount 10M

        let txHash, txEnvelope =
            [
                {
                    ActionType = "TransferAsset"
                    ActionData =
                        {
                            FromAccount = senderAccountHash |> fun (AccountHash h) -> h
                            ToAccount = recipientAccountHash |> fun (AccountHash h) -> h
                            AssetHash = assetHash |> fun (AssetHash c) -> c
                            Amount = amountToTransfer |> fun (AssetAmount a) -> a
                        }
                } :> obj
            ]
            |> Helpers.newTx senderWallet.PrivateKey nonce fee

        let txSet = [txHash]
        let blockNumber = BlockNumber 0L;

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxBalanceState address =
            initialChxState |> Map.tryFind address

        let getHoldingState key =
            initialHoldingState |> Map.tryFind key

        let getAccountController _ =
            Some senderWallet.Address

        let getAssetState _ =
            failwith "getAssetState should not be called"

        // ACT
        let output =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                Hashing.isValidChainiumAddress
                getChxBalanceState
                getHoldingState
                getAccountController
                getAssetState
                Helpers.minTxActionFee
                validatorWallet.Address
                blockNumber
                txSet

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Amount - fee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Amount + fee
        let senderAssetBalance = initialHoldingState.[senderAccountHash, assetHash].Amount - amountToTransfer
        let recipientAssetBalance = initialHoldingState.[recipientAccountHash, assetHash].Amount + amountToTransfer

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = Success @>
        test <@ output.ChxBalances.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxBalances.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxBalances.[senderWallet.Address].Amount = senderChxBalance @>
        test <@ output.ChxBalances.[validatorWallet.Address].Amount = validatorChxBalance @>
        test <@ output.Holdings.[senderAccountHash, assetHash].Amount = senderAssetBalance @>
        test <@ output.Holdings.[recipientAccountHash, assetHash].Amount = recipientAssetBalance @>

    [<Fact>]
    let ``Processing.processTxSet TransferAsset with insufficient balance`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let senderAccountHash = AccountHash "Acc1"
        let recipientAccountHash = AccountHash "Acc2"
        let assetHash = AssetHash "EQ1"

        let initialChxState =
            [
                senderWallet.Address, {ChxBalanceState.Amount = ChxAmount 100M; Nonce = Nonce 10L}
                validatorWallet.Address, {ChxBalanceState.Amount = ChxAmount 100M; Nonce = Nonce 30L}
            ]
            |> Map.ofList

        let initialHoldingState =
            [
                (senderAccountHash, assetHash), {HoldingState.Amount = AssetAmount 9M}
                (recipientAccountHash, assetHash), {HoldingState.Amount = AssetAmount 0M}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let fee = ChxAmount 1M
        let amountToTransfer = AssetAmount 10M

        let txHash, txEnvelope =
            [
                {
                    ActionType = "TransferAsset"
                    ActionData =
                        {
                            FromAccount = senderAccountHash |> fun (AccountHash h) -> h
                            ToAccount = recipientAccountHash |> fun (AccountHash h) -> h
                            AssetHash = assetHash |> fun (AssetHash c) -> c
                            Amount = amountToTransfer |> fun (AssetAmount a) -> a
                        }
                } :> obj
            ]
            |> Helpers.newTx senderWallet.PrivateKey nonce fee

        let txSet = [txHash]
        let blockNumber = BlockNumber 0L;

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxBalanceState address =
            initialChxState |> Map.tryFind address

        let getHoldingState key =
            initialHoldingState |> Map.tryFind key

        let getAccountController _ =
            Some senderWallet.Address

        let getAssetState _ =
            failwith "getAssetState should not be called"

        // ACT
        let output =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                Hashing.isValidChainiumAddress
                getChxBalanceState
                getHoldingState
                getAccountController
                getAssetState
                Helpers.minTxActionFee
                validatorWallet.Address
                blockNumber
                txSet

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Amount - fee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Amount + fee
        let senderAssetBalance = initialHoldingState.[senderAccountHash, assetHash].Amount
        let recipientAssetBalance = initialHoldingState.[recipientAccountHash, assetHash].Amount
        let expectedStatus =
            (TxActionNumber 1s, TxErrorCode.InsufficientAssetHoldingBalance)
            |> TxActionError
            |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>
        test <@ output.ChxBalances.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxBalances.[senderWallet.Address].Amount = senderChxBalance @>
        test <@ output.ChxBalances.[validatorWallet.Address].Amount = validatorChxBalance @>
        test <@ output.Holdings.[senderAccountHash, assetHash].Amount = senderAssetBalance @>
        test <@ output.Holdings.[recipientAccountHash, assetHash].Amount = recipientAssetBalance @>

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // CreateAssetEmission
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Fact>]
    let ``Processing.processTxSet CreateAssetEmission`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let emissionAccountHash = AccountHash "Acc1"
        let assetHash = AssetHash "EQ1"

        let initialChxState =
            [
                senderWallet.Address, {ChxBalanceState.Amount = ChxAmount 100M; Nonce = Nonce 10L}
                validatorWallet.Address, {ChxBalanceState.Amount = ChxAmount 100M; Nonce = Nonce 30L}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let fee = ChxAmount 1M
        let emissionAmount = AssetAmount 10M

        let txHash, txEnvelope =
            [
                {
                    ActionType = "CreateAssetEmission"
                    ActionData =
                        {
                            EmissionAccountHash = emissionAccountHash |> fun (AccountHash h) -> h
                            AssetHash = assetHash |> fun (AssetHash c) -> c
                            Amount = emissionAmount |> fun (AssetAmount a) -> a
                        }
                } :> obj
            ]
            |> Helpers.newTx senderWallet.PrivateKey nonce fee

        let txSet = [txHash]
        let blockNumber = BlockNumber 0L;

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxBalanceState address =
            initialChxState |> Map.tryFind address

        let getHoldingState _ =
            None

        let getAccountController _ =
            failwith "getAccountController should not be called"

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = senderWallet.Address}

        // ACT
        let output =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                Hashing.isValidChainiumAddress
                getChxBalanceState
                getHoldingState
                getAccountController
                getAssetState
                Helpers.minTxActionFee
                validatorWallet.Address
                blockNumber
                txSet

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Amount - fee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Amount + fee

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = Success @>
        test <@ output.ChxBalances.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxBalances.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxBalances.[senderWallet.Address].Amount = senderChxBalance @>
        test <@ output.ChxBalances.[validatorWallet.Address].Amount = validatorChxBalance @>
        test <@ output.Holdings.[emissionAccountHash, assetHash].Amount = emissionAmount @>

    [<Fact>]
    let ``Processing.processTxSet CreateAssetEmission additional emission`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let emissionAccountHash = AccountHash "Acc1"
        let assetHash = AssetHash "EQ1"

        let initialChxState =
            [
                senderWallet.Address, {ChxBalanceState.Amount = ChxAmount 100M; Nonce = Nonce 10L}
                validatorWallet.Address, {ChxBalanceState.Amount = ChxAmount 100M; Nonce = Nonce 30L}
            ]
            |> Map.ofList

        let initialHoldingState =
            [
                (emissionAccountHash, assetHash), {HoldingState.Amount = AssetAmount 30M}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let fee = ChxAmount 1M
        let emissionAmount = AssetAmount 10M

        let txHash, txEnvelope =
            [
                {
                    ActionType = "CreateAssetEmission"
                    ActionData =
                        {
                            EmissionAccountHash = emissionAccountHash |> fun (AccountHash h) -> h
                            AssetHash = assetHash |> fun (AssetHash c) -> c
                            Amount = emissionAmount |> fun (AssetAmount a) -> a
                        }
                } :> obj
            ]
            |> Helpers.newTx senderWallet.PrivateKey nonce fee

        let txSet = [txHash]
        let blockNumber = BlockNumber 0L;

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxBalanceState address =
            initialChxState |> Map.tryFind address

        let getHoldingState key =
            initialHoldingState |> Map.tryFind key

        let getAccountController _ =
            failwith "getAccountController should not be called"

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = senderWallet.Address}

        // ACT
        let output =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                Hashing.isValidChainiumAddress
                getChxBalanceState
                getHoldingState
                getAccountController
                getAssetState
                Helpers.minTxActionFee
                validatorWallet.Address
                blockNumber
                txSet

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Amount - fee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Amount + fee
        let emittedAssetBalance = initialHoldingState.[emissionAccountHash, assetHash].Amount + emissionAmount

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = Success @>
        test <@ output.ChxBalances.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxBalances.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxBalances.[senderWallet.Address].Amount = senderChxBalance @>
        test <@ output.ChxBalances.[validatorWallet.Address].Amount = validatorChxBalance @>
        test <@ output.Holdings.[emissionAccountHash, assetHash].Amount = emittedAssetBalance @>

    [<Fact>]
    let ``Processing.processTxSet CreateAssetEmission fails if sender not current controller`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let currentControllerWallet = Signing.generateWallet ()
        let emissionAccountHash = AccountHash "Acc1"
        let assetHash = AssetHash "EQ1"

        let initialChxState =
            [
                senderWallet.Address, {ChxBalanceState.Amount = ChxAmount 100M; Nonce = Nonce 10L}
                validatorWallet.Address, {ChxBalanceState.Amount = ChxAmount 100M; Nonce = Nonce 30L}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let fee = ChxAmount 1M
        let emissionAmount = AssetAmount 10M

        let txHash, txEnvelope =
            [
                {
                    ActionType = "CreateAssetEmission"
                    ActionData =
                        {
                            EmissionAccountHash = emissionAccountHash |> fun (AccountHash h) -> h
                            AssetHash = assetHash |> fun (AssetHash c) -> c
                            Amount = emissionAmount |> fun (AssetAmount a) -> a
                        }
                } :> obj
            ]
            |> Helpers.newTx senderWallet.PrivateKey nonce fee

        let txSet = [txHash]
        let blockNumber = BlockNumber 0L;

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxBalanceState address =
            initialChxState |> Map.tryFind address

        let getHoldingState _ =
            None

        let getAccountController _ =
            failwith "getAccountController should not be called"

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = currentControllerWallet.Address}

        // ACT
        let output =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                Hashing.isValidChainiumAddress
                getChxBalanceState
                getHoldingState
                getAccountController
                getAssetState
                Helpers.minTxActionFee
                validatorWallet.Address
                blockNumber
                txSet

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Amount - fee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Amount + fee
        let expectedStatus =
            (TxActionNumber 1s, TxErrorCode.SenderIsNotAssetController)
            |> TxActionError
            |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>
        test <@ output.ChxBalances.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxBalances.[senderWallet.Address].Amount = senderChxBalance @>
        test <@ output.ChxBalances.[validatorWallet.Address].Amount = validatorChxBalance @>
        test <@ output.Holdings = Map.empty @>

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // SetAccountController
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Theory>]
    [<InlineData ("None")>]
    [<InlineData ("Sender")>]
    let ``Processing.processTxSet SetAccountController`` (currentControllerCase) =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let newControllerWallet = Signing.generateWallet ()
        let accountHash = AccountHash "Acc1"

        let initialChxState =
            [
                senderWallet.Address, {ChxBalanceState.Amount = ChxAmount 100M; Nonce = Nonce 10L}
                validatorWallet.Address, {ChxBalanceState.Amount = ChxAmount 100M; Nonce = Nonce 30L}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let fee = ChxAmount 1M

        let txHash, txEnvelope =
            [
                {
                    ActionType = "SetAccountController"
                    ActionData =
                        {
                            AccountHash = accountHash |> fun (AccountHash h) -> h
                            ControllerAddress = newControllerWallet.Address |> fun (ChainiumAddress a) -> a
                        }
                } :> obj
            ]
            |> Helpers.newTx senderWallet.PrivateKey nonce fee

        let txSet = [txHash]
        let blockNumber = BlockNumber 0L;

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxBalanceState address =
            initialChxState |> Map.tryFind address

        let getHoldingState _ =
            failwith "getHoldingState should not be called"

        let getAccountController _ =
            match currentControllerCase with
            | "None" -> None
            | "Sender" -> Some senderWallet.Address
            | c -> failwithf "Unhandled account controller case: %s" c

        let getAssetState _ =
            failwith "getAssetState should not be called"

        // ACT
        let output =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                Hashing.isValidChainiumAddress
                getChxBalanceState
                getHoldingState
                getAccountController
                getAssetState
                Helpers.minTxActionFee
                validatorWallet.Address
                blockNumber
                txSet

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Amount - fee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Amount + fee
        let accountController = newControllerWallet.Address

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = Success @>
        test <@ output.ChxBalances.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxBalances.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxBalances.[senderWallet.Address].Amount = senderChxBalance @>
        test <@ output.ChxBalances.[validatorWallet.Address].Amount = validatorChxBalance @>
        test <@ output.AccountControllers.[accountHash] = Some newControllerWallet.Address @>

    [<Fact>]
    let ``Processing.processTxSet SetAccountController fails if sender not current controller`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let currentControllerWallet = Signing.generateWallet ()
        let newControllerWallet = Signing.generateWallet ()
        let accountHash = AccountHash "Acc1"

        let initialChxState =
            [
                senderWallet.Address, {ChxBalanceState.Amount = ChxAmount 100M; Nonce = Nonce 10L}
                validatorWallet.Address, {ChxBalanceState.Amount = ChxAmount 100M; Nonce = Nonce 30L}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let fee = ChxAmount 1M

        let txHash, txEnvelope =
            [
                {
                    ActionType = "SetAccountController"
                    ActionData =
                        {
                            AccountHash = accountHash |> fun (AccountHash h) -> h
                            ControllerAddress = newControllerWallet.Address |> fun (ChainiumAddress a) -> a
                        }
                } :> obj
            ]
            |> Helpers.newTx senderWallet.PrivateKey nonce fee

        let txSet = [txHash]
        let blockNumber = BlockNumber 0L;

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxBalanceState address =
            initialChxState |> Map.tryFind address

        let getHoldingState _ =
            failwith "getHoldingState should not be called"

        let getAccountController _ =
            Some currentControllerWallet.Address

        let getAssetState _ =
            failwith "getAssetState should not be called"

        // ACT
        let output =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                Hashing.isValidChainiumAddress
                getChxBalanceState
                getHoldingState
                getAccountController
                getAssetState
                Helpers.minTxActionFee
                validatorWallet.Address
                blockNumber
                txSet

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Amount - fee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Amount + fee
        let expectedTxStatus =
            (TxActionNumber 1s, TxErrorCode.SenderIsNotSourceAccountController)
            |> TxActionError
            |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedTxStatus @>
        test <@ output.ChxBalances.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxBalances.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxBalances.[senderWallet.Address].Amount = senderChxBalance @>
        test <@ output.ChxBalances.[validatorWallet.Address].Amount = validatorChxBalance @>
        test <@ output.AccountControllers.[accountHash] = Some currentControllerWallet.Address @>

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // SetAssetController
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Theory>]
    [<InlineData ("None")>]
    [<InlineData ("Sender")>]
    let ``Processing.processTxSet SetAssetController`` (currentControllerCase) =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let newControllerWallet = Signing.generateWallet ()
        let assetHash = AssetHash "Acc1"

        let initialChxState =
            [
                senderWallet.Address, {ChxBalanceState.Amount = ChxAmount 100M; Nonce = Nonce 10L}
                validatorWallet.Address, {ChxBalanceState.Amount = ChxAmount 100M; Nonce = Nonce 30L}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let fee = ChxAmount 1M

        let txHash, txEnvelope =
            [
                {
                    ActionType = "SetAssetController"
                    ActionData =
                        {
                            AssetHash = assetHash |> fun (AssetHash h) -> h
                            ControllerAddress = newControllerWallet.Address |> fun (ChainiumAddress a) -> a
                        }
                } :> obj
            ]
            |> Helpers.newTx senderWallet.PrivateKey nonce fee

        let txSet = [txHash]
        let blockNumber = BlockNumber 0L;

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxBalanceState address =
            initialChxState |> Map.tryFind address

        let getHoldingState _ =
            failwith "getHoldingState should not be called"

        let getAccountController _ =
            failwith "getAccountController should not be called"

        let getAssetState _ =
            match currentControllerCase with
            | "None" -> None
            | "Sender" -> Some {AssetState.AssetCode = None; ControllerAddress = senderWallet.Address}
            | c -> failwithf "Unhandled asset controller case: %s" c

        // ACT
        let output =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                Hashing.isValidChainiumAddress
                getChxBalanceState
                getHoldingState
                getAccountController
                getAssetState
                Helpers.minTxActionFee
                validatorWallet.Address
                blockNumber
                txSet

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Amount - fee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Amount + fee

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = Success @>
        test <@ output.ChxBalances.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxBalances.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxBalances.[senderWallet.Address].Amount = senderChxBalance @>
        test <@ output.ChxBalances.[validatorWallet.Address].Amount = validatorChxBalance @>
        test <@ output.Assets.[assetHash].ControllerAddress = newControllerWallet.Address @>

    [<Fact>]
    let ``Processing.processTxSet SetAssetController fails if sender not current controller`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let currentControllerWallet = Signing.generateWallet ()
        let newControllerWallet = Signing.generateWallet ()
        let assetHash = AssetHash "Acc1"

        let initialChxState =
            [
                senderWallet.Address, {ChxBalanceState.Amount = ChxAmount 100M; Nonce = Nonce 10L}
                validatorWallet.Address, {ChxBalanceState.Amount = ChxAmount 100M; Nonce = Nonce 30L}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let fee = ChxAmount 1M

        let txHash, txEnvelope =
            [
                {
                    ActionType = "SetAssetController"
                    ActionData =
                        {
                            AssetHash = assetHash |> fun (AssetHash h) -> h
                            ControllerAddress = newControllerWallet.Address |> fun (ChainiumAddress a) -> a
                        }
                } :> obj
            ]
            |> Helpers.newTx senderWallet.PrivateKey nonce fee

        let txSet = [txHash]
        let blockNumber = BlockNumber 0L;

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxBalanceState address =
            initialChxState |> Map.tryFind address

        let getHoldingState _ =
            failwith "getHoldingState should not be called"

        let getAccountController _ =
            failwith "getAccountController should not be called"

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = currentControllerWallet.Address}

        // ACT
        let output =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                Hashing.isValidChainiumAddress
                getChxBalanceState
                getHoldingState
                getAccountController
                getAssetState
                Helpers.minTxActionFee
                validatorWallet.Address
                blockNumber
                txSet

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Amount - fee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Amount + fee
        let expectedTxStatus =
            (TxActionNumber 1s, TxErrorCode.SenderIsNotAssetController)
            |> TxActionError
            |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedTxStatus @>
        test <@ output.ChxBalances.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxBalances.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxBalances.[senderWallet.Address].Amount = senderChxBalance @>
        test <@ output.ChxBalances.[validatorWallet.Address].Amount = validatorChxBalance @>
        test <@ output.Assets.[assetHash].ControllerAddress = currentControllerWallet.Address @>
