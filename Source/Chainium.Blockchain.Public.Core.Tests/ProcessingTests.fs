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
    // ChxTransfer
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Fact>]
    let ``Processing.processTxSet ChxTransfer`` () =
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
                    ActionType = "ChxTransfer"
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

        // ACT
        let output =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                Hashing.isValidChainiumAddress
                getChxBalanceState
                getHoldingState
                getAccountController
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
    let ``Processing.processTxSet ChxTransfer with insufficient balance`` () =
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
                    ActionType = "ChxTransfer"
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

        // ACT
        let output =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                Hashing.isValidChainiumAddress
                getChxBalanceState
                getHoldingState
                getAccountController
                validatorWallet.Address
                blockNumber
                txSet

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Amount
        let recipientChxBalance = initialChxState.[recipientWallet.Address].Amount
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Amount
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
    let ``Processing.processTxSet ChxTransfer with insufficient balance to cover fee`` () =
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
                    ActionType = "ChxTransfer"
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

        // ACT
        let output =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                Hashing.isValidChainiumAddress
                getChxBalanceState
                getHoldingState
                getAccountController
                validatorWallet.Address
                blockNumber
                txSet

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Amount
        let recipientChxBalance = initialChxState.[recipientWallet.Address].Amount
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Amount
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
    // AssetTransfer
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Fact>]
    let ``Processing.processTxSet AssetTransfer`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let senderAccountHash = AccountHash "Acc1"
        let recipientAccountHash = AccountHash "Acc2"
        let assetCode = AssetCode "EQ1"

        let initialChxState =
            [
                senderWallet.Address, {ChxBalanceState.Amount = ChxAmount 100M; Nonce = Nonce 10L}
                validatorWallet.Address, {ChxBalanceState.Amount = ChxAmount 100M; Nonce = Nonce 30L}
            ]
            |> Map.ofList

        let initialHoldingState =
            [
                (senderAccountHash, assetCode), {HoldingState.Amount = AssetAmount 50M}
                (recipientAccountHash, assetCode), {HoldingState.Amount = AssetAmount 0M}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let fee = ChxAmount 1M
        let amountToTransfer = AssetAmount 10M

        let txHash, txEnvelope =
            [
                {
                    ActionType = "AssetTransfer"
                    ActionData =
                        {
                            FromAccount = senderAccountHash |> fun (AccountHash h) -> h
                            ToAccount = recipientAccountHash |> fun (AccountHash h) -> h
                            AssetCode = assetCode |> fun (AssetCode c) -> c
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

        // ACT
        let output =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                Hashing.isValidChainiumAddress
                getChxBalanceState
                getHoldingState
                getAccountController
                validatorWallet.Address
                blockNumber
                txSet

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Amount - fee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Amount + fee
        let senderAssetBalance = initialHoldingState.[senderAccountHash, assetCode].Amount - amountToTransfer
        let recipientAssetBalance = initialHoldingState.[recipientAccountHash, assetCode].Amount + amountToTransfer

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = Success @>
        test <@ output.ChxBalances.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxBalances.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxBalances.[senderWallet.Address].Amount = senderChxBalance @>
        test <@ output.ChxBalances.[validatorWallet.Address].Amount = validatorChxBalance @>
        test <@ output.Holdings.[senderAccountHash, assetCode].Amount = senderAssetBalance @>
        test <@ output.Holdings.[recipientAccountHash, assetCode].Amount = recipientAssetBalance @>

    [<Fact>]
    let ``Processing.processTxSet AssetTransfer with insufficient balance`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let senderAccountHash = AccountHash "Acc1"
        let recipientAccountHash = AccountHash "Acc2"
        let assetCode = AssetCode "EQ1"

        let initialChxState =
            [
                senderWallet.Address, {ChxBalanceState.Amount = ChxAmount 100M; Nonce = Nonce 10L}
                validatorWallet.Address, {ChxBalanceState.Amount = ChxAmount 100M; Nonce = Nonce 30L}
            ]
            |> Map.ofList

        let initialHoldingState =
            [
                (senderAccountHash, assetCode), {HoldingState.Amount = AssetAmount 9M}
                (recipientAccountHash, assetCode), {HoldingState.Amount = AssetAmount 0M}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let fee = ChxAmount 1M
        let amountToTransfer = AssetAmount 10M

        let txHash, txEnvelope =
            [
                {
                    ActionType = "AssetTransfer"
                    ActionData =
                        {
                            FromAccount = senderAccountHash |> fun (AccountHash h) -> h
                            ToAccount = recipientAccountHash |> fun (AccountHash h) -> h
                            AssetCode = assetCode |> fun (AssetCode c) -> c
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

        // ACT
        let output =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                Hashing.isValidChainiumAddress
                getChxBalanceState
                getHoldingState
                getAccountController
                validatorWallet.Address
                blockNumber
                txSet

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Amount
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Amount
        let senderAssetBalance = initialHoldingState.[senderAccountHash, assetCode].Amount
        let recipientAssetBalance = initialHoldingState.[recipientAccountHash, assetCode].Amount
        let expectedStatus =
            (TxActionNumber 1s, TxErrorCode.InsufficientAssetHoldingBalance)
            |> TxActionError
            |> Failure

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash].Status = expectedStatus @>
        test <@ output.ChxBalances.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxBalances.[senderWallet.Address].Amount = senderChxBalance @>
        test <@ output.ChxBalances.[validatorWallet.Address].Amount = validatorChxBalance @>
        test <@ output.Holdings.[senderAccountHash, assetCode].Amount = senderAssetBalance @>
        test <@ output.Holdings.[recipientAccountHash, assetCode].Amount = recipientAssetBalance @>

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // AccountControllerChange
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    let addressToString = fun (ChainiumAddress a) -> a

    type AcctCntlrChangeTestExecution =
        {
            PrivateKey : PrivateKey
            Account : AccountHash
            CurrentController : ChainiumAddress
            NewController : ChainiumAddress
            Validator : ChainiumAddress
            Nonce : Nonce
            Fee : ChxAmount
            getAccountController : (AccountHash -> ChainiumAddress option)
            getChxBalanceState : (ChainiumAddress -> ChxBalanceState option)
            InitialChxState : Map<ChainiumAddress, ChxBalanceState>
            ExpectedErrorCode : TxErrorCode option
        }

    let acctCntlrTestCases ()=
        let account = Signing.generateWallet()
        let newController = Signing.generateWallet()
        let validator = Signing.generateWallet()

        let initialChxState =
            [
                account.Address, {ChxBalanceState.Amount = ChxAmount 100M; Nonce = Nonce 1L}
                validator.Address, {ChxBalanceState.Amount = ChxAmount 100M; Nonce = Nonce 1L}
                newController.Address, {ChxBalanceState.Amount = ChxAmount 100M; Nonce = Nonce 1L}
            ]
            |> Map.ofList

        let addressToHash accountAddress =
            accountAddress
            |> addressToString
            |> AccountHash

        [
            //account controller change success
            yield
                [|
                    {
                        PrivateKey = account.PrivateKey
                        Account = (account.Address |> addressToHash)
                        CurrentController = account.Address
                        NewController = newController.Address
                        Validator = validator.Address
                        Nonce = (Nonce 2L)
                        Fee = (ChxAmount 1M)
                        getAccountController = (fun _ -> account.Address |> Some)
                        getChxBalanceState = (fun _ -> initialChxState.[account.Address] |> Some)
                        InitialChxState = initialChxState;
                        ExpectedErrorCode = None
                    }
                |]
            //current signer is not controller
            yield
                [|
                    {
                        PrivateKey = newController.PrivateKey
                        Account = account.Address |> addressToHash
                        NewController = newController.Address
                        CurrentController = newController.Address
                        Validator = validator.Address
                        Nonce = Nonce 2L
                        Fee = ChxAmount 1M
                        getAccountController = (fun _ -> account.Address |> Some)
                        getChxBalanceState = (fun _ -> initialChxState.[account.Address] |> Some)
                        InitialChxState = initialChxState
                        ExpectedErrorCode = TxErrorCode.SenderIsNotSourceAccountController |> Some
                    }
                |]
        ]

    [<Theory>]
    [<MemberData("acctCntlrTestCases")>]
    let ``Processing.processTxSet AccountControllerChange``
        (testData : AcctCntlrChangeTestExecution)
        =
        let getHoldingState key =
            failwith "getHoldingState - should not be called"

        let txHash, txEnvelope =
            [
                {
                    ActionType = "AccountControllerChange"
                    ActionData =
                        {
                            AccountControllerChangeTxActionDto.AccountHash = testData.Account |> fun (AccountHash a) -> a
                            ControllerAddress = addressToString testData.NewController
                        }
                } :> obj
            ]
            |> Helpers.newTx testData.PrivateKey testData.Nonce testData.Fee

        let txSet = [txHash]

        let getTx _ = Ok txEnvelope

        let isValidAddress (chxAddress : ChainiumAddress) =
            chxAddress
            |> fun (ChainiumAddress a) -> a
            |> System.String.IsNullOrWhiteSpace
            |> not

        let blockNumber = BlockNumber 0L;

        let output =
            Processing.processTxSet
                getTx
                Signing.verifySignature
                isValidAddress
                testData.getChxBalanceState
                getHoldingState
                testData.getAccountController
                testData.Validator
                blockNumber
                txSet

        test <@ output.TxResults.Count = 1 @>
        match testData.ExpectedErrorCode with
        | Some errorCode ->
            let expectedStatus =
                (TxActionNumber 1s, errorCode)
                |> TxActionError
                |> Failure

            test <@ output.TxResults.[txHash].Status = expectedStatus @>
            test <@ output.ChxBalances.[testData.CurrentController].Nonce = testData.Nonce @>
            test <@ output.ChxBalances.[testData.CurrentController].Amount = testData.InitialChxState.[testData.CurrentController].Amount @>
            test <@ output.ChxBalances.[testData.Validator].Amount = testData.InitialChxState.[testData.Validator].Amount @>
            test <@ output.AccountControllerChanges.TryFind testData.Account = None @>
        | None ->
            let senderChxBalance = testData.InitialChxState.[testData.CurrentController].Amount - testData.Fee
            let validatorChxBalance = testData.InitialChxState.[testData.Validator].Amount + testData.Fee

            test <@ output.TxResults.[txHash].Status = Success @>
            test <@ output.ChxBalances.[testData.CurrentController].Nonce = testData.Nonce @>
            test <@ output.AccountControllerChanges.[testData.Account] = testData.NewController @>
            test <@ output.ChxBalances.[testData.CurrentController].Nonce = testData.Nonce @>
            test <@ output.ChxBalances.[testData.Validator].Nonce = testData.InitialChxState.[testData.Validator].Nonce @>
            test <@ output.ChxBalances.[testData.CurrentController].Amount = senderChxBalance @>
            test <@ output.ChxBalances.[testData.Validator].Amount = validatorChxBalance @>


