namespace Own.Blockchain.Public.Core.Tests

open Xunit
open Swensen.Unquote
open Own.Common
open Own.Blockchain.Common
open Own.Blockchain.Public.Core
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Core.Dtos
open Own.Blockchain.Public.Crypto

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
                    w1.Address, { ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 10L }
                    w2.Address, { ChxBalanceState.Amount = ChxAmount 200m; Nonce = Nonce 20L }
                ]
                |> Map.ofSeq

            fun (address : BlockchainAddress) -> data |> Map.tryFind address

        let getAvailableChxBalance address =
            (getChxBalanceState address).Value.Amount

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
            |> Processing.excludeUnprocessableTxs getChxBalanceState getAvailableChxBalance
            |> List.map (fun tx -> tx.TxHash.Value)

        test <@ txHashes = ["Tx1"; "Tx2"; "Tx3"; "Tx5"] @>

    [<Theory>]
    [<InlineData (1, 0, "Tx1")>]
    [<InlineData (3, 0, "Tx1; Tx3")>]
    [<InlineData (4, 1, "Tx1; Tx3")>]
    [<InlineData (4, 0, "Tx1; Tx3; Tx5")>]
    [<InlineData (5, 1, "Tx1; Tx3; Tx5")>]
    let ``Processing.excludeUnprocessableTxs excludes txs if CHX balance cannot cover the fees``
        (balance : decimal, staked : decimal, txHashes : string)
        =

        let balance = ChxAmount balance
        let expectedHashes = txHashes.Split("; ") |> Array.toList

        let w1 = Signing.generateWallet ()
        let w2 = Signing.generateWallet ()

        let getChxBalanceState =
            let data =
                [
                    w1.Address, { ChxBalanceState.Amount = balance; Nonce = Nonce 10L }
                    w2.Address, { ChxBalanceState.Amount = ChxAmount 200m; Nonce = Nonce 20L }
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
            |> Processing.excludeUnprocessableTxs getChxBalanceState getAvailableChxBalance
            |> List.map (fun tx -> tx.TxHash.Value)

        test <@ txHashes = expectedHashes @>

    [<Fact>]
    let ``Processing.orderTxSet puts txs in correct order`` () =
        let w1 = Signing.generateWallet ()
        let w2 = Signing.generateWallet ()

        let getChxBalanceState =
            let data =
                [
                    w1.Address, { ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 10L }
                    w2.Address, { ChxBalanceState.Amount = ChxAmount 200m; Nonce = Nonce 20L }
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
                senderWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 10L}
                recipientWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 20L}
                validatorWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 30L}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let fee = ChxAmount 1m
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
                } :> obj
            ]
            |> Helpers.newTx senderWallet nonce fee

        let txSet = [txHash]
        let blockNumber = BlockNumber 1L;

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxBalanceState address =
            initialChxState |> Map.tryFind address

        let getHoldingState _ =
            failwith "getHoldingState should not be called"

        let getAccountState _ =
            failwith "getAccountState should not be called"

        let getAssetState _ =
            failwith "getAssetState should not be called"

        let getValidatorState _ =
            failwith "getValidatorState should not be called"

        let getStakeState _ =
            failwith "getStakeState should not be called"

        let getTotalChxStaked _ = ChxAmount 0m

        // ACT
        let output =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                Hashing.isValidBlockchainAddress
                Hashing.deriveHash
                Hashing.hash
                getChxBalanceState
                getHoldingState
                getAccountState
                getAssetState
                getValidatorState
                getStakeState
                getTotalChxStaked
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
                senderWallet.Address, {ChxBalanceState.Amount = ChxAmount 9m; Nonce = Nonce 10L}
                recipientWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 20L}
                validatorWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 30L}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let fee = ChxAmount 1m
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
                } :> obj
            ]
            |> Helpers.newTx senderWallet nonce fee

        let txSet = [txHash]
        let blockNumber = BlockNumber 1L;

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxBalanceState address =
            initialChxState |> Map.tryFind address

        let getHoldingState _ =
            failwith "getHoldingState should not be called"

        let getAccountState _ =
            failwith "getAccountState should not be called"

        let getAssetState _ =
            failwith "getAssetState should not be called"

        let getValidatorState _ =
            failwith "getValidatorState should not be called"

        let getStakeState _ =
            failwith "getStakeState should not be called"

        let getTotalChxStaked _ = ChxAmount 0m

        // ACT
        let output =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                Hashing.isValidBlockchainAddress
                Hashing.deriveHash
                Hashing.hash
                getChxBalanceState
                getHoldingState
                getAccountState
                getAssetState
                getValidatorState
                getStakeState
                getTotalChxStaked
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
                senderWallet.Address, {ChxBalanceState.Amount = ChxAmount 10.5m; Nonce = Nonce 10L}
                recipientWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 20L}
                validatorWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 30L}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let fee = ChxAmount 1m
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
                } :> obj
            ]
            |> Helpers.newTx senderWallet nonce fee

        let txSet = [txHash]
        let blockNumber = BlockNumber 1L;

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxBalanceState address =
            initialChxState |> Map.tryFind address

        let getHoldingState _ =
            failwith "getHoldingState should not be called"

        let getAccountState _ =
            failwith "getAccountState should not be called"

        let getAssetState _ =
            failwith "getAssetState should not be called"

        let getValidatorState _ =
            failwith "getValidatorState should not be called"

        let getStakeState _ =
            failwith "getStakeState should not be called"

        let getTotalChxStaked _ = ChxAmount 0m

        // ACT
        let output =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                Hashing.isValidBlockchainAddress
                Hashing.deriveHash
                Hashing.hash
                getChxBalanceState
                getHoldingState
                getAccountState
                getAssetState
                getValidatorState
                getStakeState
                getTotalChxStaked
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
                senderWallet.Address, {ChxBalanceState.Amount = ChxAmount 0.5m; Nonce = Nonce 10L}
                recipientWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 20L}
                validatorWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 30L}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let fee = ChxAmount 1m
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
                } :> obj
            ]
            |> Helpers.newTx senderWallet nonce fee

        let txSet = [txHash]
        let blockNumber = BlockNumber 1L;

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxBalanceState address =
            initialChxState |> Map.tryFind address

        let getHoldingState _ =
            failwith "getHoldingState should not be called"

        let getAccountState _ =
            failwith "getAccountState should not be called"

        let getAssetState _ =
            failwith "getAssetState should not be called"

        let getValidatorState _ =
            failwith "getValidatorState should not be called"

        let getStakeState _ =
            failwith "getStakeState should not be called"

        let getTotalChxStaked _ = ChxAmount 0m

        let processTxSet () =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                Hashing.isValidBlockchainAddress
                Hashing.deriveHash
                Hashing.hash
                getChxBalanceState
                getHoldingState
                getAccountState
                getAssetState
                getValidatorState
                getStakeState
                getTotalChxStaked
                Helpers.minTxActionFee
                validatorWallet.Address
                blockNumber
                txSet

        // ASSERT
        raisesWith<exn>
            <@ processTxSet () @>
            (fun ex -> <@ ex.Message.StartsWith "Cannot process validator reward" @>)

    [<Fact>]
    let ``Processing.processTxSet TransferChx with insufficient balance to cover fee due to the staked CHX`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let recipientWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()

        let initialChxState =
            [
                senderWallet.Address, {ChxBalanceState.Amount = ChxAmount 11m; Nonce = Nonce 10L}
                recipientWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 20L}
                validatorWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 30L}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let fee = ChxAmount 1m
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
                } :> obj
            ]
            |> Helpers.newTx senderWallet nonce fee

        let txSet = [txHash]
        let blockNumber = BlockNumber 1L;

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxBalanceState address =
            initialChxState |> Map.tryFind address

        let getHoldingState _ =
            failwith "getHoldingState should not be called"

        let getAccountState _ =
            failwith "getAccountState should not be called"

        let getAssetState _ =
            failwith "getAssetState should not be called"

        let getValidatorState _ =
            failwith "getValidatorState should not be called"

        let getStakeState _ =
            failwith "getStakeState should not be called"

        let getTotalChxStaked address =
            if address = senderWallet.Address then
                ChxAmount 1m
            else
                ChxAmount 0m

        // ACT
        let output =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                Hashing.isValidBlockchainAddress
                Hashing.deriveHash
                Hashing.hash
                getChxBalanceState
                getHoldingState
                getAccountState
                getAssetState
                getValidatorState
                getStakeState
                getTotalChxStaked
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
                senderWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 10L}
                validatorWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 30L}
            ]
            |> Map.ofList

        let initialHoldingState =
            [
                (senderAccountHash, assetHash), {HoldingState.Amount = AssetAmount 50m}
                (recipientAccountHash, assetHash), {HoldingState.Amount = AssetAmount 0m}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let fee = ChxAmount 1m
        let amountToTransfer = AssetAmount 10m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "TransferAsset"
                    ActionData =
                        {
                            FromAccount = senderAccountHash.Value
                            ToAccount = recipientAccountHash.Value
                            AssetHash = assetHash.Value
                            Amount = amountToTransfer.Value
                        }
                } :> obj
            ]
            |> Helpers.newTx senderWallet nonce fee

        let txSet = [txHash]
        let blockNumber = BlockNumber 1L;

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxBalanceState address =
            initialChxState |> Map.tryFind address

        let getHoldingState key =
            initialHoldingState |> Map.tryFind key

        let getAccountState _ =
            Some {AccountState.ControllerAddress = senderWallet.Address}

        let getAssetState _ =
            failwith "getAssetState should not be called"

        let getValidatorState _ =
            failwith "getValidatorState should not be called"

        let getStakeState _ =
            failwith "getStakeState should not be called"

        let getTotalChxStaked _ = ChxAmount 0m

        // ACT
        let output =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                Hashing.isValidBlockchainAddress
                Hashing.deriveHash
                Hashing.hash
                getChxBalanceState
                getHoldingState
                getAccountState
                getAssetState
                getValidatorState
                getStakeState
                getTotalChxStaked
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
                senderWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 10L}
                validatorWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 30L}
            ]
            |> Map.ofList

        let initialHoldingState =
            [
                (senderAccountHash, assetHash), {HoldingState.Amount = AssetAmount 9m}
                (recipientAccountHash, assetHash), {HoldingState.Amount = AssetAmount 0m}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let fee = ChxAmount 1m
        let amountToTransfer = AssetAmount 10m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "TransferAsset"
                    ActionData =
                        {
                            FromAccount = senderAccountHash.Value
                            ToAccount = recipientAccountHash.Value
                            AssetHash = assetHash.Value
                            Amount = amountToTransfer.Value
                        }
                } :> obj
            ]
            |> Helpers.newTx senderWallet nonce fee

        let txSet = [txHash]
        let blockNumber = BlockNumber 1L;

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxBalanceState address =
            initialChxState |> Map.tryFind address

        let getHoldingState key =
            initialHoldingState |> Map.tryFind key

        let getAccountState _ =
            Some {AccountState.ControllerAddress = senderWallet.Address}

        let getAssetState _ =
            failwith "getAssetState should not be called"

        let getValidatorState _ =
            failwith "getValidatorState should not be called"

        let getStakeState _ =
            failwith "getStakeState should not be called"

        let getTotalChxStaked _ = ChxAmount 0m

        // ACT
        let output =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                Hashing.isValidBlockchainAddress
                Hashing.deriveHash
                Hashing.hash
                getChxBalanceState
                getHoldingState
                getAccountState
                getAssetState
                getValidatorState
                getStakeState
                getTotalChxStaked
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

    [<Fact>]
    let ``Processing.processTxSet TransferAsset fails if source account not found`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let senderAccountHash = AccountHash "Acc1"
        let recipientAccountHash = AccountHash "Acc2"
        let assetHash = AssetHash "EQ1"

        let initialChxState =
            [
                senderWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 10L}
                validatorWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 30L}
            ]
            |> Map.ofList

        let initialHoldingState =
            [
                (senderAccountHash, assetHash), {HoldingState.Amount = AssetAmount 9m}
                (recipientAccountHash, assetHash), {HoldingState.Amount = AssetAmount 0m}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let fee = ChxAmount 1m
        let amountToTransfer = AssetAmount 10m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "TransferAsset"
                    ActionData =
                        {
                            FromAccount = senderAccountHash.Value
                            ToAccount = recipientAccountHash.Value
                            AssetHash = assetHash.Value
                            Amount = amountToTransfer.Value
                        }
                } :> obj
            ]
            |> Helpers.newTx senderWallet nonce fee

        let txSet = [txHash]
        let blockNumber = BlockNumber 1L;

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxBalanceState address =
            initialChxState |> Map.tryFind address

        let getHoldingState key =
            initialHoldingState |> Map.tryFind key

        let getAccountState accountHash =
            if accountHash = recipientAccountHash then
                Some {AccountState.ControllerAddress = senderWallet.Address}
            else
                None

        let getAssetState _ =
            failwith "getAssetState should not be called"

        let getValidatorState _ =
            failwith "getValidatorState should not be called"

        let getStakeState _ =
            failwith "getStakeState should not be called"

        let getTotalChxStaked _ = ChxAmount 0m

        // ACT
        let output =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                Hashing.isValidBlockchainAddress
                Hashing.deriveHash
                Hashing.hash
                getChxBalanceState
                getHoldingState
                getAccountState
                getAssetState
                getValidatorState
                getStakeState
                getTotalChxStaked
                Helpers.minTxActionFee
                validatorWallet.Address
                blockNumber
                txSet

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Amount - fee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Amount + fee
        let expectedStatus =
            (TxActionNumber 1s, TxErrorCode.SourceAccountNotFound)
            |> TxActionError
            |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>
        test <@ output.ChxBalances.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxBalances.[senderWallet.Address].Amount = senderChxBalance @>
        test <@ output.ChxBalances.[validatorWallet.Address].Amount = validatorChxBalance @>
        test <@ output.Holdings = Map.empty @>

    [<Fact>]
    let ``Processing.processTxSet TransferAsset fails if destination account not found`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let senderAccountHash = AccountHash "Acc1"
        let recipientAccountHash = AccountHash "Acc2"
        let assetHash = AssetHash "EQ1"

        let initialChxState =
            [
                senderWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 10L}
                validatorWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 30L}
            ]
            |> Map.ofList

        let initialHoldingState =
            [
                (senderAccountHash, assetHash), {HoldingState.Amount = AssetAmount 9m}
                (recipientAccountHash, assetHash), {HoldingState.Amount = AssetAmount 0m}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let fee = ChxAmount 1m
        let amountToTransfer = AssetAmount 10m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "TransferAsset"
                    ActionData =
                        {
                            FromAccount = senderAccountHash.Value
                            ToAccount = recipientAccountHash.Value
                            AssetHash = assetHash.Value
                            Amount = amountToTransfer.Value
                        }
                } :> obj
            ]
            |> Helpers.newTx senderWallet nonce fee

        let txSet = [txHash]
        let blockNumber = BlockNumber 1L;

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxBalanceState address =
            initialChxState |> Map.tryFind address

        let getHoldingState key =
            initialHoldingState |> Map.tryFind key

        let getAccountState accountHash =
            if accountHash = senderAccountHash then
                Some {AccountState.ControllerAddress = senderWallet.Address}
            else
                None

        let getAssetState _ =
            failwith "getAssetState should not be called"

        let getValidatorState _ =
            failwith "getValidatorState should not be called"

        let getStakeState _ =
            failwith "getStakeState should not be called"

        let getTotalChxStaked _ = ChxAmount 0m

        // ACT
        let output =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                Hashing.isValidBlockchainAddress
                Hashing.deriveHash
                Hashing.hash
                getChxBalanceState
                getHoldingState
                getAccountState
                getAssetState
                getValidatorState
                getStakeState
                getTotalChxStaked
                Helpers.minTxActionFee
                validatorWallet.Address
                blockNumber
                txSet

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Amount - fee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Amount + fee
        let expectedStatus =
            (TxActionNumber 1s, TxErrorCode.DestinationAccountNotFound)
            |> TxActionError
            |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>
        test <@ output.ChxBalances.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxBalances.[senderWallet.Address].Amount = senderChxBalance @>
        test <@ output.ChxBalances.[validatorWallet.Address].Amount = validatorChxBalance @>
        test <@ output.Holdings = Map.empty @>

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // CreateAssetEmission
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Fact>]
    let ``Processing.processTxSet CreateAssetEmission`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let someOtherWallet = Signing.generateWallet ()
        let emissionAccountHash = AccountHash "Acc1"
        let assetHash = AssetHash "EQ1"

        let initialChxState =
            [
                senderWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 10L}
                validatorWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 30L}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let fee = ChxAmount 1m
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
                } :> obj
            ]
            |> Helpers.newTx senderWallet nonce fee

        let txSet = [txHash]
        let blockNumber = BlockNumber 1L;

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxBalanceState address =
            initialChxState |> Map.tryFind address

        let getHoldingState _ =
            None

        let getAccountState _ =
            Some {AccountState.ControllerAddress = someOtherWallet.Address}

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = senderWallet.Address}

        let getValidatorState _ =
            failwith "getValidatorState should not be called"

        let getStakeState _ =
            failwith "getStakeState should not be called"

        let getTotalChxStaked _ = ChxAmount 0m

        // ACT
        let output =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                Hashing.isValidBlockchainAddress
                Hashing.deriveHash
                Hashing.hash
                getChxBalanceState
                getHoldingState
                getAccountState
                getAssetState
                getValidatorState
                getStakeState
                getTotalChxStaked
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
        let someOtherWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let emissionAccountHash = AccountHash "Acc1"
        let assetHash = AssetHash "EQ1"

        let initialChxState =
            [
                senderWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 10L}
                validatorWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 30L}
            ]
            |> Map.ofList

        let initialHoldingState =
            [
                (emissionAccountHash, assetHash), {HoldingState.Amount = AssetAmount 30m}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let fee = ChxAmount 1m
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
                } :> obj
            ]
            |> Helpers.newTx senderWallet nonce fee

        let txSet = [txHash]
        let blockNumber = BlockNumber 1L;

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxBalanceState address =
            initialChxState |> Map.tryFind address

        let getHoldingState key =
            initialHoldingState |> Map.tryFind key

        let getAccountState _ =
            Some {AccountState.ControllerAddress = someOtherWallet.Address}

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = senderWallet.Address}

        let getValidatorState _ =
            failwith "getValidatorState should not be called"

        let getStakeState _ =
            failwith "getStakeState should not be called"

        let getTotalChxStaked _ = ChxAmount 0m

        // ACT
        let output =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                Hashing.isValidBlockchainAddress
                Hashing.deriveHash
                Hashing.hash
                getChxBalanceState
                getHoldingState
                getAccountState
                getAssetState
                getValidatorState
                getStakeState
                getTotalChxStaked
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
        let someOtherWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let currentControllerWallet = Signing.generateWallet ()
        let emissionAccountHash = AccountHash "Acc1"
        let assetHash = AssetHash "EQ1"

        let initialChxState =
            [
                senderWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 10L}
                validatorWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 30L}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let fee = ChxAmount 1m
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
                } :> obj
            ]
            |> Helpers.newTx senderWallet nonce fee

        let txSet = [txHash]
        let blockNumber = BlockNumber 1L;

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxBalanceState address =
            initialChxState |> Map.tryFind address

        let getHoldingState _ =
            None

        let getAccountState _ =
            Some {AccountState.ControllerAddress = someOtherWallet.Address}

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = currentControllerWallet.Address}

        let getValidatorState _ =
            failwith "getValidatorState should not be called"

        let getStakeState _ =
            failwith "getStakeState should not be called"

        let getTotalChxStaked _ = ChxAmount 0m

        // ACT
        let output =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                Hashing.isValidBlockchainAddress
                Hashing.deriveHash
                Hashing.hash
                getChxBalanceState
                getHoldingState
                getAccountState
                getAssetState
                getValidatorState
                getStakeState
                getTotalChxStaked
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

    [<Fact>]
    let ``Processing.processTxSet CreateAssetEmission fails if asset not found`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let someOtherWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let currentControllerWallet = Signing.generateWallet ()
        let emissionAccountHash = AccountHash "Acc1"
        let assetHash = AssetHash "EQ1"

        let initialChxState =
            [
                senderWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 10L}
                validatorWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 30L}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let fee = ChxAmount 1m
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
                } :> obj
            ]
            |> Helpers.newTx senderWallet nonce fee

        let txSet = [txHash]
        let blockNumber = BlockNumber 1L;

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxBalanceState address =
            initialChxState |> Map.tryFind address

        let getHoldingState _ =
            None

        let getAccountState _ =
            Some {AccountState.ControllerAddress = someOtherWallet.Address}

        let getAssetState _ =
            None

        let getValidatorState _ =
            failwith "getValidatorState should not be called"

        let getStakeState _ =
            failwith "getStakeState should not be called"

        let getTotalChxStaked _ = ChxAmount 0m

        // ACT
        let output =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                Hashing.isValidBlockchainAddress
                Hashing.deriveHash
                Hashing.hash
                getChxBalanceState
                getHoldingState
                getAccountState
                getAssetState
                getValidatorState
                getStakeState
                getTotalChxStaked
                Helpers.minTxActionFee
                validatorWallet.Address
                blockNumber
                txSet

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Amount - fee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Amount + fee
        let expectedStatus =
            (TxActionNumber 1s, TxErrorCode.AssetNotFound)
            |> TxActionError
            |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>
        test <@ output.ChxBalances.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxBalances.[senderWallet.Address].Amount = senderChxBalance @>
        test <@ output.ChxBalances.[validatorWallet.Address].Amount = validatorChxBalance @>
        test <@ output.Holdings = Map.empty @>

    [<Fact>]
    let ``Processing.processTxSet CreateAssetEmission fails if account not found`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let currentControllerWallet = Signing.generateWallet ()
        let emissionAccountHash = AccountHash "Acc1"
        let assetHash = AssetHash "EQ1"

        let initialChxState =
            [
                senderWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 10L}
                validatorWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 30L}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let fee = ChxAmount 1m
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
                } :> obj
            ]
            |> Helpers.newTx senderWallet nonce fee

        let txSet = [txHash]
        let blockNumber = BlockNumber 1L;

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxBalanceState address =
            initialChxState |> Map.tryFind address

        let getHoldingState _ =
            None

        let getAccountState _ =
            None

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = currentControllerWallet.Address}

        let getValidatorState _ =
            failwith "getValidatorState should not be called"

        let getStakeState _ =
            failwith "getStakeState should not be called"

        let getTotalChxStaked _ = ChxAmount 0m

        // ACT
        let output =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                Hashing.isValidBlockchainAddress
                Hashing.deriveHash
                Hashing.hash
                getChxBalanceState
                getHoldingState
                getAccountState
                getAssetState
                getValidatorState
                getStakeState
                getTotalChxStaked
                Helpers.minTxActionFee
                validatorWallet.Address
                blockNumber
                txSet

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Amount - fee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Amount + fee
        let expectedStatus =
            (TxActionNumber 1s, TxErrorCode.AccountNotFound)
            |> TxActionError
            |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>
        test <@ output.ChxBalances.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxBalances.[senderWallet.Address].Amount = senderChxBalance @>
        test <@ output.ChxBalances.[validatorWallet.Address].Amount = validatorChxBalance @>
        test <@ output.Holdings = Map.empty @>

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // CreateAccount
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Fact>]
    let ``Processing.processTxSet CreateAccount`` () =
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
        let fee = ChxAmount 1m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "CreateAccount"
                    ActionData = CreateAccountTxActionDto()
                } :> obj
            ]
            |> Helpers.newTx senderWallet nonce fee

        let txSet = [txHash]
        let blockNumber = BlockNumber 1L;

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

        let getChxBalanceState address =
            initialChxState |> Map.tryFind address

        let getHoldingState _ =
            None

        let getAccountState _ =
            None

        let getAssetState _ =
            failwith "getAssetState should not be called"

        let getValidatorState _ =
            failwith "getValidatorState should not be called"

        let getStakeState _ =
            failwith "getStakeState should not be called"

        let getTotalChxStaked _ = ChxAmount 0m

        // ACT
        let output =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                Hashing.isValidBlockchainAddress
                Hashing.deriveHash
                Hashing.hash
                getChxBalanceState
                getHoldingState
                getAccountState
                getAssetState
                getValidatorState
                getStakeState
                getTotalChxStaked
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
        test <@ output.Accounts.Count = 1 @>
        test <@ output.Accounts.[accountHash] = {ControllerAddress = senderWallet.Address} @>

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // CreateAsset
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Fact>]
    let ``Processing.processTxSet CreateAsset`` () =
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
        let fee = ChxAmount 1m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "CreateAsset"
                    ActionData = CreateAssetTxActionDto()
                } :> obj
            ]
            |> Helpers.newTx senderWallet nonce fee

        let txSet = [txHash]
        let blockNumber = BlockNumber 1L;

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

        let getChxBalanceState address =
            initialChxState |> Map.tryFind address

        let getHoldingState _ =
            None

        let getAccountState _ =
            failwith "getAccountState should not be called"

        let getAssetState _ =
            None

        let getValidatorState _ =
            failwith "getValidatorState should not be called"

        let getStakeState _ =
            failwith "getStakeState should not be called"

        let getTotalChxStaked _ = ChxAmount 0m

        // ACT
        let output =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                Hashing.isValidBlockchainAddress
                Hashing.deriveHash
                Hashing.hash
                getChxBalanceState
                getHoldingState
                getAccountState
                getAssetState
                getValidatorState
                getStakeState
                getTotalChxStaked
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
        test <@ output.Assets.Count = 1 @>
        test <@ output.Assets.[assetHash] = {AssetCode = None; ControllerAddress = senderWallet.Address} @>

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // SetAccountController
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Fact>]
    let ``Processing.processTxSet SetAccountController`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let newControllerWallet = Signing.generateWallet ()
        let accountHash = AccountHash "Acc1"

        let initialChxState =
            [
                senderWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 10L}
                validatorWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 30L}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let fee = ChxAmount 1m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "SetAccountController"
                    ActionData =
                        {
                            AccountHash = accountHash.Value
                            ControllerAddress = newControllerWallet.Address.Value
                        }
                } :> obj
            ]
            |> Helpers.newTx senderWallet nonce fee

        let txSet = [txHash]
        let blockNumber = BlockNumber 1L;

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxBalanceState address =
            initialChxState |> Map.tryFind address

        let getHoldingState _ =
            failwith "getHoldingState should not be called"

        let getAccountState _ =
            Some {AccountState.ControllerAddress = senderWallet.Address}

        let getAssetState _ =
            failwith "getAssetState should not be called"

        let getValidatorState _ =
            failwith "getValidatorState should not be called"

        let getStakeState _ =
            failwith "getStakeState should not be called"

        let getTotalChxStaked _ = ChxAmount 0m

        // ACT
        let output =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                Hashing.isValidBlockchainAddress
                Hashing.deriveHash
                Hashing.hash
                getChxBalanceState
                getHoldingState
                getAccountState
                getAssetState
                getValidatorState
                getStakeState
                getTotalChxStaked
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
        test <@ output.Accounts.[accountHash] = {AccountState.ControllerAddress = newControllerWallet.Address} @>

    [<Fact>]
    let ``Processing.processTxSet SetAccountController fails if account not found`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let newControllerWallet = Signing.generateWallet ()
        let accountHash = AccountHash "Acc1"

        let initialChxState =
            [
                senderWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 10L}
                validatorWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 30L}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let fee = ChxAmount 1m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "SetAccountController"
                    ActionData =
                        {
                            AccountHash = accountHash.Value
                            ControllerAddress = newControllerWallet.Address.Value
                        }
                } :> obj
            ]
            |> Helpers.newTx senderWallet nonce fee

        let txSet = [txHash]
        let blockNumber = BlockNumber 1L;

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxBalanceState address =
            initialChxState |> Map.tryFind address

        let getHoldingState _ =
            failwith "getHoldingState should not be called"

        let getAccountState _ =
            None

        let getAssetState _ =
            failwith "getAssetState should not be called"

        let getValidatorState _ =
            failwith "getValidatorState should not be called"

        let getStakeState _ =
            failwith "getStakeState should not be called"

        let getTotalChxStaked _ = ChxAmount 0m

        // ACT
        let output =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                Hashing.isValidBlockchainAddress
                Hashing.deriveHash
                Hashing.hash
                getChxBalanceState
                getHoldingState
                getAccountState
                getAssetState
                getValidatorState
                getStakeState
                getTotalChxStaked
                Helpers.minTxActionFee
                validatorWallet.Address
                blockNumber
                txSet

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Amount - fee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Amount + fee
        let expectedTxStatus =
            (TxActionNumber 1s, TxErrorCode.AccountNotFound)
            |> TxActionError
            |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedTxStatus @>
        test <@ output.ChxBalances.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxBalances.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxBalances.[senderWallet.Address].Amount = senderChxBalance @>
        test <@ output.ChxBalances.[validatorWallet.Address].Amount = validatorChxBalance @>
        test <@ output.Accounts = Map.empty @>

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
                senderWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 10L}
                validatorWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 30L}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let fee = ChxAmount 1m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "SetAccountController"
                    ActionData =
                        {
                            AccountHash = accountHash.Value
                            ControllerAddress = newControllerWallet.Address.Value
                        }
                } :> obj
            ]
            |> Helpers.newTx senderWallet nonce fee

        let txSet = [txHash]
        let blockNumber = BlockNumber 1L;

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxBalanceState address =
            initialChxState |> Map.tryFind address

        let getHoldingState _ =
            failwith "getHoldingState should not be called"

        let getAccountState _ =
            Some {AccountState.ControllerAddress = currentControllerWallet.Address}

        let getAssetState _ =
            failwith "getAssetState should not be called"

        let getValidatorState _ =
            failwith "getValidatorState should not be called"

        let getStakeState _ =
            failwith "getStakeState should not be called"

        let getTotalChxStaked _ = ChxAmount 0m

        // ACT
        let output =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                Hashing.isValidBlockchainAddress
                Hashing.deriveHash
                Hashing.hash
                getChxBalanceState
                getHoldingState
                getAccountState
                getAssetState
                getValidatorState
                getStakeState
                getTotalChxStaked
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
        test <@ output.Accounts.[accountHash] <> {AccountState.ControllerAddress = newControllerWallet.Address} @>
        test <@ output.Accounts.[accountHash] = {AccountState.ControllerAddress = currentControllerWallet.Address} @>

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // SetAssetController
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Fact>]
    let ``Processing.processTxSet SetAssetController`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let newControllerWallet = Signing.generateWallet ()
        let assetHash = AssetHash "Acc1"

        let initialChxState =
            [
                senderWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 10L}
                validatorWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 30L}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let fee = ChxAmount 1m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "SetAssetController"
                    ActionData =
                        {
                            AssetHash = assetHash.Value
                            ControllerAddress = newControllerWallet.Address.Value
                        }
                } :> obj
            ]
            |> Helpers.newTx senderWallet nonce fee

        let txSet = [txHash]
        let blockNumber = BlockNumber 1L;

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxBalanceState address =
            initialChxState |> Map.tryFind address

        let getHoldingState _ =
            failwith "getHoldingState should not be called"

        let getAccountState _ =
            failwith "getAccountState should not be called"

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = senderWallet.Address}

        let getValidatorState _ =
            failwith "getValidatorState should not be called"

        let getStakeState _ =
            failwith "getStakeState should not be called"

        let getTotalChxStaked _ = ChxAmount 0m

        // ACT
        let output =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                Hashing.isValidBlockchainAddress
                Hashing.deriveHash
                Hashing.hash
                getChxBalanceState
                getHoldingState
                getAccountState
                getAssetState
                getValidatorState
                getStakeState
                getTotalChxStaked
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
    let ``Processing.processTxSet SetAssetController fails if asset not found`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let newControllerWallet = Signing.generateWallet ()
        let assetHash = AssetHash "Acc1"

        let initialChxState =
            [
                senderWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 10L}
                validatorWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 30L}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let fee = ChxAmount 1m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "SetAssetController"
                    ActionData =
                        {
                            AssetHash = assetHash.Value
                            ControllerAddress = newControllerWallet.Address.Value
                        }
                } :> obj
            ]
            |> Helpers.newTx senderWallet nonce fee

        let txSet = [txHash]
        let blockNumber = BlockNumber 1L;

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxBalanceState address =
            initialChxState |> Map.tryFind address

        let getHoldingState _ =
            failwith "getHoldingState should not be called"

        let getAccountState _ =
            failwith "getAccountState should not be called"

        let getAssetState _ =
            None

        let getValidatorState _ =
            failwith "getValidatorState should not be called"

        let getStakeState _ =
            failwith "getStakeState should not be called"

        let getTotalChxStaked _ = ChxAmount 0m

        // ACT
        let output =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                Hashing.isValidBlockchainAddress
                Hashing.deriveHash
                Hashing.hash
                getChxBalanceState
                getHoldingState
                getAccountState
                getAssetState
                getValidatorState
                getStakeState
                getTotalChxStaked
                Helpers.minTxActionFee
                validatorWallet.Address
                blockNumber
                txSet

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Amount - fee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Amount + fee
        let expectedTxStatus =
            (TxActionNumber 1s, TxErrorCode.AssetNotFound)
            |> TxActionError
            |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedTxStatus @>
        test <@ output.ChxBalances.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxBalances.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxBalances.[senderWallet.Address].Amount = senderChxBalance @>
        test <@ output.ChxBalances.[validatorWallet.Address].Amount = validatorChxBalance @>
        test <@ output.Assets = Map.empty @>

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
                senderWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 10L}
                validatorWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 30L}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let fee = ChxAmount 1m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "SetAssetController"
                    ActionData =
                        {
                            AssetHash = assetHash.Value
                            ControllerAddress = newControllerWallet.Address.Value
                        }
                } :> obj
            ]
            |> Helpers.newTx senderWallet nonce fee

        let txSet = [txHash]
        let blockNumber = BlockNumber 1L;

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxBalanceState address =
            initialChxState |> Map.tryFind address

        let getHoldingState _ =
            failwith "getHoldingState should not be called"

        let getAccountState _ =
            failwith "getAccountState should not be called"

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = currentControllerWallet.Address}

        let getValidatorState _ =
            failwith "getValidatorState should not be called"

        let getStakeState _ =
            failwith "getStakeState should not be called"

        let getTotalChxStaked _ = ChxAmount 0m

        // ACT
        let output =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                Hashing.isValidBlockchainAddress
                Hashing.deriveHash
                Hashing.hash
                getChxBalanceState
                getHoldingState
                getAccountState
                getAssetState
                getValidatorState
                getStakeState
                getTotalChxStaked
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
        test <@ output.Assets.[assetHash].ControllerAddress <> newControllerWallet.Address @>
        test <@ output.Assets.[assetHash].ControllerAddress = currentControllerWallet.Address @>

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // SetAssetCode
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Fact>]
    let ``Processing.processTxSet SetAssetCode`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let assetHash = AssetHash "Foo"
        let assetCode = AssetCode "Bar"

        let initialChxState =
            [
                senderWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 10L}
                validatorWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 30L}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let fee = ChxAmount 1m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "SetAssetCode"
                    ActionData =
                        {
                            AssetHash = assetHash.Value
                            AssetCode = assetCode.Value
                        }
                } :> obj
            ]
            |> Helpers.newTx senderWallet nonce fee

        let txSet = [txHash]
        let blockNumber = BlockNumber 1L;

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxBalanceState address =
            initialChxState |> Map.tryFind address

        let getHoldingState _ =
            failwith "getHoldingState should not be called"

        let getAccountCode _ =
            failwith "getAccountCode should not be called"

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = senderWallet.Address}

        let getValidatorState _ =
            failwith "getValidatorState should not be called"

        let getStakeState _ =
            failwith "getStakeState should not be called"

        let getTotalChxStaked _ = ChxAmount 0m

        // ACT
        let output =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                Hashing.isValidBlockchainAddress
                Hashing.deriveHash
                Hashing.hash
                getChxBalanceState
                getHoldingState
                getAccountCode
                getAssetState
                getValidatorState
                getStakeState
                getTotalChxStaked
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
        test <@ output.Assets.[assetHash].AssetCode = Some assetCode @>

    [<Fact>]
    let ``Processing.processTxSet SetAssetCode fails if asset not found`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let assetHash = AssetHash "Foo"
        let assetCode = AssetCode "Bar"

        let initialChxState =
            [
                senderWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 10L}
                validatorWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 30L}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let fee = ChxAmount 1m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "SetAssetCode"
                    ActionData =
                        {
                            AssetHash = assetHash.Value
                            AssetCode = assetCode.Value
                        }
                } :> obj
            ]
            |> Helpers.newTx senderWallet nonce fee

        let txSet = [txHash]
        let blockNumber = BlockNumber 1L;

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxBalanceState address =
            initialChxState |> Map.tryFind address

        let getHoldingState _ =
            failwith "getHoldingState should not be called"

        let getAccountCode _ =
            failwith "getAccountCode should not be called"

        let getAssetState _ =
            None

        let getValidatorState _ =
            failwith "getValidatorState should not be called"

        let getStakeState _ =
            failwith "getStakeState should not be called"

        let getTotalChxStaked _ = ChxAmount 0m

        // ACT
        let output =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                Hashing.isValidBlockchainAddress
                Hashing.deriveHash
                Hashing.hash
                getChxBalanceState
                getHoldingState
                getAccountCode
                getAssetState
                getValidatorState
                getStakeState
                getTotalChxStaked
                Helpers.minTxActionFee
                validatorWallet.Address
                blockNumber
                txSet

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Amount - fee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Amount + fee
        let expectedTxStatus =
            (TxActionNumber 1s, TxErrorCode.AssetNotFound)
            |> TxActionError
            |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedTxStatus @>
        test <@ output.ChxBalances.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxBalances.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxBalances.[senderWallet.Address].Amount = senderChxBalance @>
        test <@ output.ChxBalances.[validatorWallet.Address].Amount = validatorChxBalance @>
        test <@ output.Assets = Map.empty @>

    [<Fact>]
    let ``Processing.processTxSet SetAssetCode fails if sender not current controller`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let currentControllerWallet = Signing.generateWallet ()
        let assetHash = AssetHash "Foo"
        let assetCode = AssetCode "Bar"

        let initialChxState =
            [
                senderWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 10L}
                validatorWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 30L}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let fee = ChxAmount 1m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "SetAssetCode"
                    ActionData =
                        {
                            AssetHash = assetHash.Value
                            AssetCode = assetCode.Value
                        }
                } :> obj
            ]
            |> Helpers.newTx senderWallet nonce fee

        let txSet = [txHash]
        let blockNumber = BlockNumber 1L;

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxBalanceState address =
            initialChxState |> Map.tryFind address

        let getHoldingState _ =
            failwith "getHoldingState should not be called"

        let getAccountCode _ =
            failwith "getAccountCode should not be called"

        let getAssetState _ =
            Some {AssetState.AssetCode = None; ControllerAddress = currentControllerWallet.Address}

        let getValidatorState _ =
            failwith "getValidatorState should not be called"

        let getStakeState _ =
            failwith "getStakeState should not be called"

        let getTotalChxStaked _ = ChxAmount 0m

        // ACT
        let output =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                Hashing.isValidBlockchainAddress
                Hashing.deriveHash
                Hashing.hash
                getChxBalanceState
                getHoldingState
                getAccountCode
                getAssetState
                getValidatorState
                getStakeState
                getTotalChxStaked
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
        test <@ output.Assets.[assetHash].AssetCode = None @>

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // SetValidatorNetworkAddress
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Fact>]
    let ``Processing.processTxSet SetValidatorNetworkAddress - updating existing network address`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let newNetworkAddress = "localhost:5000"

        let initialChxState =
            [
                senderWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 10L}
                validatorWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 30L}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let fee = ChxAmount 1m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "SetValidatorNetworkAddress"
                    ActionData =
                        {
                            NetworkAddress = newNetworkAddress
                        }
                } :> obj
            ]
            |> Helpers.newTx senderWallet nonce fee

        let txSet = [txHash]
        let blockNumber = BlockNumber 1L;

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxBalanceState address =
            initialChxState |> Map.tryFind address

        let getHoldingState _ =
            failwith "getHoldingState should not be called"

        let getAccountCode _ =
            failwith "getAccountCode should not be called"

        let getAssetState _ =
            failwith "getAssetState should not be called"

        let getValidatorState _ =
            Some {ValidatorState.NetworkAddress = "old-address:12345"}

        let getStakeState _ =
            failwith "getStakeState should not be called"

        let getTotalChxStaked _ = ChxAmount 0m

        // ACT
        let output =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                Hashing.isValidBlockchainAddress
                Hashing.deriveHash
                Hashing.hash
                getChxBalanceState
                getHoldingState
                getAccountCode
                getAssetState
                getValidatorState
                getStakeState
                getTotalChxStaked
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
        test <@ output.Validators.[senderWallet.Address].NetworkAddress = newNetworkAddress @>

    [<Fact>]
    let ``Processing.processTxSet SetValidatorNetworkAddress - inserting new network address`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let newNetworkAddress = "localhost:5000"

        let initialChxState =
            [
                senderWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 10L}
                validatorWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 30L}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let fee = ChxAmount 1m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "SetValidatorNetworkAddress"
                    ActionData =
                        {
                            NetworkAddress = newNetworkAddress
                        }
                } :> obj
            ]
            |> Helpers.newTx senderWallet nonce fee

        let txSet = [txHash]
        let blockNumber = BlockNumber 1L;

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxBalanceState address =
            initialChxState |> Map.tryFind address

        let getHoldingState _ =
            failwith "getHoldingState should not be called"

        let getAccountCode _ =
            failwith "getAccountCode should not be called"

        let getAssetState _ =
            failwith "getAssetState should not be called"

        let getValidatorState _ =
            None

        let getStakeState _ =
            failwith "getStakeState should not be called"

        let getTotalChxStaked _ = ChxAmount 0m

        // ACT
        let output =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                Hashing.isValidBlockchainAddress
                Hashing.deriveHash
                Hashing.hash
                getChxBalanceState
                getHoldingState
                getAccountCode
                getAssetState
                getValidatorState
                getStakeState
                getTotalChxStaked
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
        test <@ output.Validators.[senderWallet.Address].NetworkAddress = newNetworkAddress @>

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // DelegateStake
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Fact>]
    let ``Processing.processTxSet DelegateStake - updating existing stake`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let stakeValidatorAddress = (Signing.generateWallet ()).Address
        let stakeAmount = ChxAmount 10m
        let currentStakeAmount = ChxAmount 4m

        let initialChxState =
            [
                senderWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 10L}
                validatorWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 30L}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let fee = ChxAmount 1m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "DelegateStake"
                    ActionData =
                        {
                            ValidatorAddress = stakeValidatorAddress.Value
                            Amount = stakeAmount.Value
                        }
                } :> obj
            ]
            |> Helpers.newTx senderWallet nonce fee

        let txSet = [txHash]
        let blockNumber = BlockNumber 1L;

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxBalanceState address =
            initialChxState |> Map.tryFind address

        let getHoldingState _ =
            failwith "getHoldingState should not be called"

        let getAccountCode _ =
            failwith "getAccountCode should not be called"

        let getAssetState _ =
            failwith "getAssetState should not be called"

        let getValidatorState _ =
            failwith "getValidatorState should not be called"

        let getStakeState _ =
            Some {StakeState.Amount = currentStakeAmount}

        let getTotalChxStaked _ = currentStakeAmount

        // ACT
        let output =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                Hashing.isValidBlockchainAddress
                Hashing.deriveHash
                Hashing.hash
                getChxBalanceState
                getHoldingState
                getAccountCode
                getAssetState
                getValidatorState
                getStakeState
                getTotalChxStaked
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
        test <@ output.Stakes.[senderWallet.Address, stakeValidatorAddress].Amount = stakeAmount @>

    [<Fact>]
    let ``Processing.processTxSet DelegateStake - setting new stake`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let stakeValidatorAddress = (Signing.generateWallet ()).Address
        let stakeAmount = ChxAmount 10m

        let initialChxState =
            [
                senderWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 10L}
                validatorWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 30L}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let fee = ChxAmount 1m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "DelegateStake"
                    ActionData =
                        {
                            ValidatorAddress = stakeValidatorAddress.Value
                            Amount = stakeAmount.Value
                        }
                } :> obj
            ]
            |> Helpers.newTx senderWallet nonce fee

        let txSet = [txHash]
        let blockNumber = BlockNumber 1L;

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxBalanceState address =
            initialChxState |> Map.tryFind address

        let getHoldingState _ =
            failwith "getHoldingState should not be called"

        let getAccountCode _ =
            failwith "getAccountCode should not be called"

        let getAssetState _ =
            failwith "getAssetState should not be called"

        let getValidatorState _ =
            failwith "getValidatorState should not be called"

        let getStakeState _ =
            None

        let getTotalChxStaked _ = ChxAmount 0m

        // ACT
        let output =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                Hashing.isValidBlockchainAddress
                Hashing.deriveHash
                Hashing.hash
                getChxBalanceState
                getHoldingState
                getAccountCode
                getAssetState
                getValidatorState
                getStakeState
                getTotalChxStaked
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
        test <@ output.Stakes.[senderWallet.Address, stakeValidatorAddress].Amount = stakeAmount @>

    [<Fact>]
    let ``Processing.processTxSet DelegateStake - staking more than available balance`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let stakeValidatorAddress = (Signing.generateWallet ()).Address
        let stakeAmount = ChxAmount 101m

        let initialChxState =
            [
                senderWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 10L}
                validatorWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 30L}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let fee = ChxAmount 1m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "DelegateStake"
                    ActionData =
                        {
                            ValidatorAddress = stakeValidatorAddress.Value
                            Amount = stakeAmount.Value
                        }
                } :> obj
            ]
            |> Helpers.newTx senderWallet nonce fee

        let txSet = [txHash]
        let blockNumber = BlockNumber 1L;

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxBalanceState address =
            initialChxState |> Map.tryFind address

        let getHoldingState _ =
            failwith "getHoldingState should not be called"

        let getAccountCode _ =
            failwith "getAccountCode should not be called"

        let getAssetState _ =
            failwith "getAssetState should not be called"

        let getValidatorState _ =
            failwith "getValidatorState should not be called"

        let getStakeState _ =
            None

        let getTotalChxStaked _ = ChxAmount 0m

        // ACT
        let output =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                Hashing.isValidBlockchainAddress
                Hashing.deriveHash
                Hashing.hash
                getChxBalanceState
                getHoldingState
                getAccountCode
                getAssetState
                getValidatorState
                getStakeState
                getTotalChxStaked
                Helpers.minTxActionFee
                validatorWallet.Address
                blockNumber
                txSet

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Amount - fee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Amount + fee
        let expectedTxStatus =
            (TxActionNumber 1s, TxErrorCode.InsufficientChxBalance)
            |> TxActionError
            |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedTxStatus @>
        test <@ output.ChxBalances.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxBalances.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxBalances.[senderWallet.Address].Amount = senderChxBalance @>
        test <@ output.ChxBalances.[validatorWallet.Address].Amount = validatorChxBalance @>
        test <@ output.Stakes = Map.empty @>

    [<Fact>]
    let ``Processing.processTxSet DelegateStake - staking more than available balance due to the staked CHX`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let stakeValidatorAddress = (Signing.generateWallet ()).Address
        let stakeAmount = ChxAmount 50m
        let currentStakeAmount = ChxAmount 50m

        let initialChxState =
            [
                senderWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 10L}
                validatorWallet.Address, {ChxBalanceState.Amount = ChxAmount 100m; Nonce = Nonce 30L}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let fee = ChxAmount 1m

        let txHash, txEnvelope =
            [
                {
                    ActionType = "DelegateStake"
                    ActionData =
                        {
                            ValidatorAddress = stakeValidatorAddress.Value
                            Amount = stakeAmount.Value
                        }
                } :> obj
            ]
            |> Helpers.newTx senderWallet nonce fee

        let txSet = [txHash]
        let blockNumber = BlockNumber 1L;

        // COMPOSE
        let getTx _ =
            Ok txEnvelope

        let getChxBalanceState address =
            initialChxState |> Map.tryFind address

        let getHoldingState _ =
            failwith "getHoldingState should not be called"

        let getAccountCode _ =
            failwith "getAccountCode should not be called"

        let getAssetState _ =
            failwith "getAssetState should not be called"

        let getValidatorState _ =
            failwith "getValidatorState should not be called"

        let getStakeState _ =
            Some {StakeState.Amount = currentStakeAmount}

        let getTotalChxStaked _ = currentStakeAmount

        // ACT
        let output =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                Hashing.isValidBlockchainAddress
                Hashing.deriveHash
                Hashing.hash
                getChxBalanceState
                getHoldingState
                getAccountCode
                getAssetState
                getValidatorState
                getStakeState
                getTotalChxStaked
                Helpers.minTxActionFee
                validatorWallet.Address
                blockNumber
                txSet

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Amount - fee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Amount + fee
        let expectedTxStatus =
            (TxActionNumber 1s, TxErrorCode.InsufficientChxBalance)
            |> TxActionError
            |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedTxStatus @>
        test <@ output.ChxBalances.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxBalances.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxBalances.[senderWallet.Address].Amount = senderChxBalance @>
        test <@ output.ChxBalances.[validatorWallet.Address].Amount = validatorChxBalance @>
        test <@ output.Stakes = Map.empty @>
