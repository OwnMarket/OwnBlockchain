namespace Chainium.Blockchain.Public.Core.Tests

open System
open Xunit
open Swensen.Unquote
open Chainium.Blockchain.Public.Core
open Chainium.Blockchain.Public.Core.Dtos
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Crypto

module ValidationTests =

    let chAddress = ChainiumAddress "ch2Wt6j7sbaqbgKphYx9U95wZDX99L"
    let txHash = TxHash "SampleHash"
    let chxTransfer = "ChxTransfer"
    let assetTransfer = "AssetTransfer"

    [<Fact>]
    let ``Validation.validateTx.basicValidation single validation error`` () =
        let recipientWallet = Signing.generateWallet ()
        let testTx = {
            Nonce = -10L
            Fee = 20M
            Actions =
                [
                    {
                        ActionType = chxTransfer
                        ActionData =
                            {
                                ChxTransferTxActionDto.RecipientAddress =
                                    recipientWallet.Address |> fun (ChainiumAddress a) -> a
                                Amount = 20M
                            }
                    }
                    {
                        ActionType = assetTransfer
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
        let result = Validation.validateTx Hashing.isValidChainiumAddress chAddress txHash testTx

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
                        ActionType = chxTransfer
                        ActionData =
                            {
                                ChxTransferTxActionDto.RecipientAddress =
                                    recipientWallet.Address |> fun (ChainiumAddress a) -> a
                                Amount = 20M
                            }
                    }
                    {
                        ActionType = assetTransfer
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

        let result = Validation.validateTx Hashing.isValidChainiumAddress chAddress txHash testTx

        match result with
        | Ok t -> failwith "Validation should fail in case of this test."
        | Error errors ->
            test <@ errors.Length = 2 @>

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

        let result = Validation.validateTx Hashing.isValidChainiumAddress chAddress txHash testTx

        match result with
        | Ok t -> failwith "Validation should fail in case of this test."
        | Error errors ->
            test <@ errors.Length = 1 @>

    [<Fact>]
    let ``Validation.validateTx.chxTransfer invalid Amount`` () =
        let recipientWallet = Signing.generateWallet ()
        let testTx = {
            Nonce = 10L
            Fee = 1M
            Actions =
                [
                    {
                        ActionType = chxTransfer
                        ActionData =
                            {
                                ChxTransferTxActionDto.RecipientAddress =
                                    recipientWallet.Address |> fun (ChainiumAddress a) -> a
                                Amount = 0M
                            }
                    }
                ]
        }

        let result = Validation.validateTx Hashing.isValidChainiumAddress chAddress txHash testTx

        match result with
        | Ok t -> failwith "Validation should fail in case of this test."
        | Error errors ->
            test <@ errors.Length = 1 @>

    [<Fact>]
    let ``Validation.validateTx.chxTransfer invalid RecipientAddress`` () =
        let testTx = {
            Nonce = 10L
            Fee = 1M
            Actions =
                [
                    {
                        ActionType = chxTransfer
                        ActionData =
                            {
                                ChxTransferTxActionDto.RecipientAddress = ""
                                Amount = 10M
                            }
                    }
                ]
        }

        let result = Validation.validateTx Hashing.isValidChainiumAddress chAddress txHash testTx

        match result with
        | Ok t -> failwith "Validation should fail in case of this test."
        | Error errors ->
            test <@ errors.Length > 0 @>

    [<Fact>]
    let ``Validation.validateTx.assetTransfer invalid FromAccount`` () =
        let testTx = {
            Nonce = 10L
            Fee = 1M
            Actions =
                [
                    {
                        ActionType = assetTransfer
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

        let result = Validation.validateTx Hashing.isValidChainiumAddress chAddress txHash testTx

        match result with
        | Ok t -> failwith "Validation should fail in case of this test."
        | Error errors ->
            test <@ errors.Length = 1 @>

    [<Fact>]
    let ``Validation.validateTx.assetTransfer invalid ToAccount`` () =
        let testTx = {
            Nonce = 10L
            Fee = 1M
            Actions =
                [
                    {
                        ActionType = assetTransfer
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

        let result = Validation.validateTx Hashing.isValidChainiumAddress chAddress txHash testTx

        match result with
        | Ok t -> failwith "Validation should fail in case of this test."
        | Error errors ->
            test <@ errors.Length = 1 @>

    [<Fact>]
    let ``Validation.validateTx.assetTransfer invalid Asset`` () =
        let testTx = {
            Nonce = 10L
            Fee = 1M
            Actions =
                [
                    {
                        ActionType = assetTransfer
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

        let result = Validation.validateTx Hashing.isValidChainiumAddress chAddress txHash testTx

        match result with
        | Ok t -> failwith "Validation should fail in case of this test."
        | Error errors ->
            test <@ errors.Length = 1 @>

    [<Fact>]
    let ``Validation.validateTx.assetTransfer invalid Amount`` () =
        let testTx = {
            Nonce = 10L
            Fee = 1M
            Actions =
                [
                    {
                        ActionType = assetTransfer
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

        let result = Validation.validateTx Hashing.isValidChainiumAddress chAddress txHash testTx

        match result with
        | Ok t -> failwith "Validation should fail in case of this test."
        | Error errors ->
            test <@ errors.Length = 1 @>

    let getTx<'T> = function
        | ChxTransfer action -> box action :?> 'T
        | AssetTransfer action -> box action :?> 'T
        | AccountControllerChange action -> box action :?> 'T

    [<Fact>]
    let ``Validation.validateTx validate action`` () =
        let recipientWallet = Signing.generateWallet ()
        let testTx = {
            Nonce = 10L
            Fee = 1M
            Actions =
                [
                    {
                        ActionType = chxTransfer
                        ActionData =
                            {
                                ChxTransferTxActionDto.RecipientAddress =
                                    recipientWallet.Address |> fun (ChainiumAddress a) -> a
                                Amount = 10M
                            }
                    }
                    {
                        ActionType = assetTransfer
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

        let result = Validation.validateTx Hashing.isValidChainiumAddress chAddress txHash testTx

        match result with
        | Ok t ->
            let expectedChx = (testTx.Actions.[0].ActionData :?> ChxTransferTxActionDto)
            let actualChx = t.Actions.[0] |> getTx<ChxTransferTxAction>

            let expAsset = (testTx.Actions.[1].ActionData :?> AssetTransferTxActionDto)
            let actualAsset = t.Actions.[1] |> getTx<AssetTransferTxAction>

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
