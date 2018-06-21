namespace Chainium.Blockchain.Public.Core.Tests

open System
open Xunit
open Swensen.Unquote
open Chainium.Blockchain.Public.Core
open Chainium.Blockchain.Public.Core.Dtos
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Crypto

module ValidationTests =

    let chAddress = ChainiumAddress "CHMHABow7Liry6TqswwzxHnMfcYNbrJBAtp"
    let txHash = TxHash "SampleHash"
    let transferChx = "TransferChx"
    let transferAsset = "TransferAsset"
    let controllerChange = "SetAccountController"

    [<Fact>]
    let ``Validation.validateTx.basicValidation single validation error`` () =
        let recipientWallet = Signing.generateWallet ()
        let testTx = {
            Nonce = -10L
            Fee = 20M
            Actions =
                [
                    {
                        ActionType = transferChx
                        ActionData =
                            {
                                TransferChxTxActionDto.RecipientAddress =
                                    recipientWallet.Address |> fun (ChainiumAddress a) -> a
                                Amount = 20M
                            }
                    }
                    {
                        ActionType = transferAsset
                        ActionData =
                            {
                                FromAccount = "A"
                                ToAccount = "B"
                                AssetCode = "asset"
                                Amount = 12M
                            }
                    }
                ]
        }

        let expMessage = AppError "Nonce must be positive."
        let result = Validation.validateTx Hashing.isValidChainiumAddress Helpers.minTxActionFee chAddress txHash testTx

        match result with
        | Ok t -> failwith "Validation should fail in case of this test."
        | Error errors ->
            test <@ errors.Length = 1 @>
            test <@ errors.[0] = expMessage @>

    [<Fact>]
    let ``Validation.validateTx.basicValidation multiple validation errors`` () =
        let recipientWallet = Signing.generateWallet ()
        let testTx = {
            Nonce = -10L
            Fee = 0M
            Actions =
                [
                    {
                        ActionType = transferChx
                        ActionData =
                            {
                                TransferChxTxActionDto.RecipientAddress =
                                    recipientWallet.Address |> fun (ChainiumAddress a) -> a
                                Amount = 20M
                            }
                    }
                    {
                        ActionType = transferAsset
                        ActionData =
                            {
                                FromAccount = "A"
                                ToAccount = "B"
                                AssetCode = "asset"
                                Amount = 12M
                            }
                    }
                ]
        }

        let result = Validation.validateTx Hashing.isValidChainiumAddress Helpers.minTxActionFee chAddress txHash testTx

        match result with
        | Ok t -> failwith "Validation should fail in case of this test."
        | Error errors ->
            test <@ errors.Length = 3 @>

    [<Fact>]
    let ``Validation.validateTx.basicValidation unknown action type`` () =
        let testTx = {
            Nonce = 10L
            Fee = 1M
            Actions =
                [
                    {
                        ActionType = "Unknown"
                        ActionData = "Unknown"
                    }
                ]
        }

        let result = Validation.validateTx Hashing.isValidChainiumAddress Helpers.minTxActionFee chAddress txHash testTx

        match result with
        | Ok t -> failwith "Validation should fail in case of this test."
        | Error errors ->
            test <@ errors.Length = 1 @>

    [<Fact>]
    let ``Validation.validateTx.transferChx invalid Amount`` () =
        let recipientWallet = Signing.generateWallet ()
        let testTx = {
            Nonce = 10L
            Fee = 1M
            Actions =
                [
                    {
                        ActionType = transferChx
                        ActionData =
                            {
                                TransferChxTxActionDto.RecipientAddress =
                                    recipientWallet.Address |> fun (ChainiumAddress a) -> a
                                Amount = 0M
                            }
                    }
                ]
        }

        let result = Validation.validateTx Hashing.isValidChainiumAddress Helpers.minTxActionFee chAddress txHash testTx

        match result with
        | Ok t -> failwith "Validation should fail in case of this test."
        | Error errors ->
            test <@ errors.Length = 1 @>

    [<Fact>]
    let ``Validation.validateTx.transferChx invalid RecipientAddress`` () =
        let testTx = {
            Nonce = 10L
            Fee = 1M
            Actions =
                [
                    {
                        ActionType = transferChx
                        ActionData =
                            {
                                TransferChxTxActionDto.RecipientAddress = ""
                                Amount = 10M
                            }
                    }
                ]
        }

        let result = Validation.validateTx Hashing.isValidChainiumAddress Helpers.minTxActionFee chAddress txHash testTx

        match result with
        | Ok t -> failwith "Validation should fail in case of this test."
        | Error errors ->
            test <@ errors.Length > 0 @>

    [<Fact>]
    let ``Validation.validateTx.transferAsset invalid FromAccount`` () =
        let testTx = {
            Nonce = 10L
            Fee = 1M
            Actions =
                [
                    {
                        ActionType = transferAsset
                        ActionData =
                            {
                                FromAccount = ""
                                ToAccount = "B"
                                AssetCode = "asset"
                                Amount = 12M
                            }
                    }
                ]
        }

        let result = Validation.validateTx Hashing.isValidChainiumAddress Helpers.minTxActionFee chAddress txHash testTx

        match result with
        | Ok t -> failwith "Validation should fail in case of this test."
        | Error errors ->
            test <@ errors.Length = 1 @>

    [<Fact>]
    let ``Validation.validateTx.transferAsset invalid ToAccount`` () =
        let testTx = {
            Nonce = 10L
            Fee = 1M
            Actions =
                [
                    {
                        ActionType = transferAsset
                        ActionData =
                            {
                                FromAccount = "A"
                                ToAccount = ""
                                AssetCode = "asset"
                                Amount = 12M
                            }
                    }
                ]
        }

        let result = Validation.validateTx Hashing.isValidChainiumAddress Helpers.minTxActionFee chAddress txHash testTx

        match result with
        | Ok t -> failwith "Validation should fail in case of this test."
        | Error errors ->
            test <@ errors.Length = 1 @>

    [<Fact>]
    let ``Validation.validateTx.transferAsset invalid Asset`` () =
        let testTx = {
            Nonce = 10L
            Fee = 1M
            Actions =
                [
                    {
                        ActionType = transferAsset
                        ActionData =
                            {
                                FromAccount = "A"
                                ToAccount = "B"
                                AssetCode = ""
                                Amount = 12M
                            }
                    }
                ]
        }

        let result = Validation.validateTx Hashing.isValidChainiumAddress Helpers.minTxActionFee chAddress txHash testTx

        match result with
        | Ok t -> failwith "Validation should fail in case of this test."
        | Error errors ->
            test <@ errors.Length = 1 @>

    [<Fact>]
    let ``Validation.validateTx.transferAsset invalid Amount`` () =
        let testTx = {
            Nonce = 10L
            Fee = 1M
            Actions =
                [
                    {
                        ActionType = transferAsset
                        ActionData =
                            {
                                FromAccount = "A"
                                ToAccount = "B"
                                AssetCode = "asset"
                                Amount = 0M
                            }
                    }
                ]
        }

        let result = Validation.validateTx Hashing.isValidChainiumAddress Helpers.minTxActionFee chAddress txHash testTx

        match result with
        | Ok t -> failwith "Validation should fail in case of this test."
        | Error errors ->
            test <@ errors.Length = 1 @>

    let getTx<'T> = function
        | TransferChx action -> box action :?> 'T
        | TransferAsset action -> box action :?> 'T
        | SetAccountController action -> box action :?> 'T

    [<Fact>]
    let ``Validation.validateTx validate action`` () =
        let recipientWallet = Signing.generateWallet ()
        let testTx = {
            Nonce = 10L
            Fee = 1M
            Actions =
                [
                    {
                        ActionType = transferChx
                        ActionData =
                            {
                                TransferChxTxActionDto.RecipientAddress =
                                    recipientWallet.Address |> fun (ChainiumAddress a) -> a
                                Amount = 10M
                            }
                    }
                    {
                        ActionType = transferAsset
                        ActionData =
                            {
                                FromAccount = "A"
                                ToAccount = "B"
                                AssetCode = "asset"
                                Amount = 1M
                            }
                    }
                ]
        }

        let result = Validation.validateTx Hashing.isValidChainiumAddress Helpers.minTxActionFee chAddress txHash testTx

        match result with
        | Ok t ->
            let expectedChx = (testTx.Actions.[0].ActionData :?> TransferChxTxActionDto)
            let actualChx = t.Actions.[0] |> getTx<TransferChxTxAction>

            let expAsset = (testTx.Actions.[1].ActionData :?> TransferAssetTxActionDto)
            let actualAsset = t.Actions.[1] |> getTx<TransferAssetTxAction>

            test <@ t.Fee = ChxAmount testTx.Fee @>
            test <@ t.Nonce = Nonce testTx.Nonce @>
            test <@ t.TxHash = txHash @>
            test <@ t.Sender = chAddress @>
            test <@ actualChx.Amount = ChxAmount expectedChx.Amount @>
            test <@ actualChx.RecipientAddress = ChainiumAddress expectedChx.RecipientAddress @>
            test <@ actualAsset.FromAccountHash = AccountHash expAsset.FromAccount @>
            test <@ actualAsset.ToAccountHash = AccountHash expAsset.ToAccount @>
            test <@ actualAsset.AssetCode = AssetCode expAsset.AssetCode @>
            test <@ actualAsset.Amount = AssetAmount expAsset.Amount @>
        | Error errors ->
            failwithf "%A" errors

    let private isValidAddressMock (address : ChainiumAddress) =
        let item = address |> fun (ChainiumAddress a) -> a
        String.IsNullOrWhiteSpace(item) |> not

    [<Fact>]
    let ``Validation.validateTx.setAccountController validate action`` () =
        let expected =
            {
                SetAccountControllerTxActionDto.AccountHash = "A"
                ControllerAddress = chAddress |> fun (ChainiumAddress a) -> a
            }

        let tx = {
            Nonce = 10L
            Fee = 1M
            Actions =
                [
                    {
                        ActionType = controllerChange
                        ActionData = expected
                    }
                ]
        }

        match Validation.validateTx isValidAddressMock Helpers.minTxActionFee chAddress txHash tx with
        | Ok t ->
            let actual = getTx<SetAccountControllerTxAction> t.Actions.Head
            test <@ AccountHash expected.AccountHash = actual.AccountHash @>
            test <@ ChainiumAddress expected.ControllerAddress = actual.ControllerAddress @>
        | Error e -> failwithf "%A" e

    [<Fact>]
    let ``Validation.validateTx.setAccountController invalid action`` () =
        let expected =
            {
                SetAccountControllerTxActionDto.AccountHash = ""
                ControllerAddress = ""
            }

        let tx = {
            Nonce = 10L
            Fee = 1M
            Actions =
                [
                    {
                        ActionType = controllerChange
                        ActionData = expected
                    }
                ]
        }

        match Validation.validateTx isValidAddressMock Helpers.minTxActionFee chAddress txHash tx with
        | Ok t -> failwith "This test should fail."
        | Error e ->
            test <@ e.Length = 2 @>
