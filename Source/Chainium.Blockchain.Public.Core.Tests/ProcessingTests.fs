namespace Chainium.Blockchain.Public.Core.Tests

open System
open Xunit
open Swensen.Unquote
open Chainium.Common
open Chainium.Blockchain.Common
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
                Helpers.newPendingTxInfo (TxHash "Tx2") w1.Address (Nonce 12L) (ChxAmount 1M) 2L
                Helpers.newPendingTxInfo (TxHash "Tx3") w1.Address (Nonce 10L) (ChxAmount 1M) 3L
                Helpers.newPendingTxInfo (TxHash "Tx4") w1.Address (Nonce 14L) (ChxAmount 1M) 4L
                Helpers.newPendingTxInfo (TxHash "Tx5") w1.Address (Nonce 11L) (ChxAmount 1M) 5L
                Helpers.newPendingTxInfo (TxHash "Tx1") w2.Address (Nonce 21L) (ChxAmount 1M) 1L
            ]

        // ACT
        let txHashes =
            txSet
            |> Processing.excludeUnprocessableTxs getChxBalanceState
            |> List.map (fun tx -> tx.TxHash |> fun (TxHash hash) -> hash)

        test <@ txHashes = ["Tx1"; "Tx2"; "Tx3"; "Tx5"] @>

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
                Helpers.newPendingTxInfo (TxHash "Tx1") w2.Address (Nonce 21L) (ChxAmount 1M) 1L
                Helpers.newPendingTxInfo (TxHash "Tx2") w1.Address (Nonce 12L) (ChxAmount 1M) 2L
                Helpers.newPendingTxInfo (TxHash "Tx3") w1.Address (Nonce 10L) (ChxAmount 1M) 3L
                Helpers.newPendingTxInfo (TxHash "Tx6") w2.Address (Nonce 21L) (ChxAmount 2M) 6L
                Helpers.newPendingTxInfo (TxHash "Tx5") w1.Address (Nonce 11L) (ChxAmount 1M) 5L
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
                getChxBalanceState
                getHoldingState
                getAccountController
                validatorWallet.Address
                txSet

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Amount - amountToTransfer - fee
        let recipientChxBalance = initialChxState.[recipientWallet.Address].Amount + amountToTransfer
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Amount + fee

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash] = Success @>
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
                getChxBalanceState
                getHoldingState
                getAccountController
                validatorWallet.Address
                txSet

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Amount
        let recipientChxBalance = initialChxState.[recipientWallet.Address].Amount
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Amount

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash] = Failure [AppError "Insufficient CHX balance."] @>
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
                getChxBalanceState
                getHoldingState
                getAccountController
                validatorWallet.Address
                txSet

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Amount
        let recipientChxBalance = initialChxState.[recipientWallet.Address].Amount
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Amount

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash] = Failure [AppError "Insufficient CHX balance."] @>
        test <@ output.ChxBalances.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxBalances.[recipientWallet.Address].Nonce = initialChxState.[recipientWallet.Address].Nonce @>
        test <@ output.ChxBalances.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxBalances.[senderWallet.Address].Amount = senderChxBalance @>
        test <@ output.ChxBalances.[recipientWallet.Address].Amount = recipientChxBalance @>
        test <@ output.ChxBalances.[validatorWallet.Address].Amount = validatorChxBalance @>

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // EquityTransfer
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Fact>]
    let ``Processing.processTxSet EquityTransfer`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let senderAccountHash = AccountHash "Acc1"
        let recipientAccountHash = AccountHash "Acc2"
        let equityID = EquityID "EQ1"

        let initialChxState =
            [
                senderWallet.Address, {ChxBalanceState.Amount = ChxAmount 100M; Nonce = Nonce 10L}
                validatorWallet.Address, {ChxBalanceState.Amount = ChxAmount 100M; Nonce = Nonce 30L}
            ]
            |> Map.ofList

        let initialHoldingState =
            [
                (senderAccountHash, equityID), {HoldingState.Amount = EquityAmount 50M; Nonce = Nonce 10L}
                (recipientAccountHash, equityID), {HoldingState.Amount = EquityAmount 0M; Nonce = Nonce 20L}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let fee = ChxAmount 1M
        let amountToTransfer = EquityAmount 10M

        let txHash, txEnvelope =
            [
                {
                    ActionType = "EquityTransfer"
                    ActionData =
                        {
                            FromAccount = senderAccountHash |> fun (AccountHash h) -> h
                            ToAccount = recipientAccountHash |> fun (AccountHash h) -> h
                            Equity = equityID |> fun (EquityID e) -> e
                            Amount = amountToTransfer |> fun (EquityAmount a) -> a
                        }
                } :> obj
            ]
            |> Helpers.newTx senderWallet.PrivateKey nonce fee

        let txSet = [txHash]

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
                getChxBalanceState
                getHoldingState
                getAccountController
                validatorWallet.Address
                txSet

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Amount - fee
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Amount + fee
        let senderEquityBalance = initialHoldingState.[senderAccountHash, equityID].Amount - amountToTransfer
        let recipientEquityBalance = initialHoldingState.[recipientAccountHash, equityID].Amount + amountToTransfer

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash] = Success @>
        test <@ output.ChxBalances.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxBalances.[validatorWallet.Address].Nonce = initialChxState.[validatorWallet.Address].Nonce @>
        test <@ output.ChxBalances.[senderWallet.Address].Amount = senderChxBalance @>
        test <@ output.ChxBalances.[validatorWallet.Address].Amount = validatorChxBalance @>
        test <@ output.Holdings.[senderAccountHash, equityID].Amount = senderEquityBalance @>
        test <@ output.Holdings.[recipientAccountHash, equityID].Amount = recipientEquityBalance @>

    [<Fact>]
    let ``Processing.processTxSet EquityTransfer with insufficient balance`` () =
        // INIT STATE
        let senderWallet = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let senderAccountHash = AccountHash "Acc1"
        let recipientAccountHash = AccountHash "Acc2"
        let equityID = EquityID "EQ1"

        let initialChxState =
            [
                senderWallet.Address, {ChxBalanceState.Amount = ChxAmount 100M; Nonce = Nonce 10L}
                validatorWallet.Address, {ChxBalanceState.Amount = ChxAmount 100M; Nonce = Nonce 30L}
            ]
            |> Map.ofList

        let initialHoldingState =
            [
                (senderAccountHash, equityID), {HoldingState.Amount = EquityAmount 9M; Nonce = Nonce 10L}
                (recipientAccountHash, equityID), {HoldingState.Amount = EquityAmount 0M; Nonce = Nonce 20L}
            ]
            |> Map.ofList

        // PREPARE TX
        let nonce = Nonce 11L
        let fee = ChxAmount 1M
        let amountToTransfer = EquityAmount 10M

        let txHash, txEnvelope =
            [
                {
                    ActionType = "EquityTransfer"
                    ActionData =
                        {
                            FromAccount = senderAccountHash |> fun (AccountHash h) -> h
                            ToAccount = recipientAccountHash |> fun (AccountHash h) -> h
                            Equity = equityID |> fun (EquityID e) -> e
                            Amount = amountToTransfer |> fun (EquityAmount a) -> a
                        }
                } :> obj
            ]
            |> Helpers.newTx senderWallet.PrivateKey nonce fee

        let txSet = [txHash]

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
                getChxBalanceState
                getHoldingState
                getAccountController
                validatorWallet.Address
                txSet

        // ASSERT
        let senderChxBalance = initialChxState.[senderWallet.Address].Amount
        let validatorChxBalance = initialChxState.[validatorWallet.Address].Amount
        let senderEquityBalance = initialHoldingState.[senderAccountHash, equityID].Amount
        let recipientEquityBalance = initialHoldingState.[recipientAccountHash, equityID].Amount

        test <@ output.TxResults.Count = 1 @>
        test <@ output.TxResults.[txHash] = Failure [AppError "Insufficient equity holding balance."] @>
        test <@ output.ChxBalances.[senderWallet.Address].Nonce = nonce @>
        test <@ output.ChxBalances.[senderWallet.Address].Amount = senderChxBalance @>
        test <@ output.ChxBalances.[validatorWallet.Address].Amount = validatorChxBalance @>
        test <@ output.Holdings.[senderAccountHash, equityID].Amount = senderEquityBalance @>
        test <@ output.Holdings.[recipientAccountHash, equityID].Amount = recipientEquityBalance @>
