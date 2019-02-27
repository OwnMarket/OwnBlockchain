namespace Own.Blockchain.Public.Core.Tests

open System
open Xunit
open Swensen.Unquote
open Own.Blockchain.Common
open Own.Blockchain.Public.Core
open Own.Blockchain.Public.Core.Dtos
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Crypto

module ValidationTests =

    let chAddress = BlockchainAddress "CHaLUjmvaJs2Yn6dyrLpsjVzcpWg6GENkEw"
    let txHash = TxHash "SampleHash"
    let transferChxActionType = "TransferChx"
    let transferAssetActionType = "TransferAsset"
    let createAssetEmissionActionType = "CreateAssetEmission"
    let createAccountActionType = "CreateAccount"
    let createAssetActionType = "CreateAsset"
    let setAccountControllerActionType = "SetAccountController"
    let setAssetControllerActionType = "SetAssetController"
    let setAssetCodeActionType = "SetAssetCode"
    let configureValidatorActionType = "ConfigureValidator"
    let delegateStakeActionType = "DelegateStake"

    [<Fact>]
    let ``Validation.validateTx BasicValidation single validation error`` () =
        let recipientWallet = Signing.generateWallet ()
        let testTx = {
            SenderAddress = chAddress.Value
            Nonce = -10L
            ActionFee = 20m
            Actions =
                [
                    {
                        ActionType = transferChxActionType
                        ActionData =
                            {
                                TransferChxTxActionDto.RecipientAddress = recipientWallet.Address.Value
                                Amount = 20m
                            }
                    }
                    {
                        ActionType = transferAssetActionType
                        ActionData =
                            {
                                TransferAssetTxActionDto.FromAccountHash = "A"
                                ToAccountHash = "B"
                                AssetHash = "asset"
                                Amount = 12m
                            }
                    }
                ]
        }

        let expMessage = AppError "Nonce must be positive."
        let result =
            Validation.validateTx Hashing.isValidBlockchainAddress Helpers.maxActionCountPerTx chAddress txHash testTx

        match result with
        | Ok t -> failwith "Validation should fail in case of this test."
        | Error errors ->
            test <@ errors.Length = 1 @>
            test <@ errors.[0] = expMessage @>

    [<Fact>]
    let ``Validation.validateTx BasicValidation multiple validation errors`` () =
        let recipientWallet = Signing.generateWallet ()
        let testTx = {
            SenderAddress = ""
            Nonce = -10L
            ActionFee = 0m
            Actions =
                [
                    {
                        ActionType = transferChxActionType
                        ActionData =
                            {
                                TransferChxTxActionDto.RecipientAddress = recipientWallet.Address.Value
                                Amount = 20m
                            }
                    }
                    {
                        ActionType = transferAssetActionType
                        ActionData =
                            {
                                TransferAssetTxActionDto.FromAccountHash = "A"
                                ToAccountHash = "B"
                                AssetHash = "asset"
                                Amount = 12m
                            }
                    }
                ]
        }

        let result =
            Validation.validateTx Hashing.isValidBlockchainAddress Helpers.maxActionCountPerTx chAddress txHash testTx

        match result with
        | Ok t -> failwith "Validation should fail in case of this test."
        | Error errors ->
            test <@ errors.Length = 3 @>

    [<Fact>]
    let ``Validation.validateTx BasicValidation unknown action type`` () =
        let testTx = {
            SenderAddress = chAddress.Value
            Nonce = 10L
            ActionFee = 1m
            Actions =
                [
                    {
                        ActionType = "Unknown"
                        ActionData = "Unknown"
                    }
                ]
        }

        let result =
            Validation.validateTx Hashing.isValidBlockchainAddress Helpers.maxActionCountPerTx chAddress txHash testTx

        match result with
        | Ok t -> failwith "Validation should fail in case of this test."
        | Error errors ->
            test <@ errors.Length = 1 @>

    [<Fact>]
    let ``Validation.validateTx TransferChx invalid Amount`` () =
        let recipientWallet = Signing.generateWallet ()
        let testTx = {
            SenderAddress = chAddress.Value
            Nonce = 10L
            ActionFee = 1m
            Actions =
                [
                    {
                        ActionType = transferChxActionType
                        ActionData =
                            {
                                TransferChxTxActionDto.RecipientAddress = recipientWallet.Address.Value
                                Amount = 0m
                            }
                    }
                ]
        }

        let result =
            Validation.validateTx Hashing.isValidBlockchainAddress Helpers.maxActionCountPerTx chAddress txHash testTx

        match result with
        | Ok t -> failwith "Validation should fail in case of this test."
        | Error errors ->
            test <@ errors.Length = 1 @>

    [<Fact>]
    let ``Validation.validateTx TransferChx invalid Amount, too many decimals`` () =
        let recipientWallet = Signing.generateWallet ()
        let testTx = {
            SenderAddress = chAddress.Value
            Nonce = 10L
            ActionFee = 1m
            Actions =
                [
                    {
                        ActionType = transferChxActionType
                        ActionData =
                            {
                                TransferChxTxActionDto.RecipientAddress = recipientWallet.Address.Value
                                Amount = 12.12345678m
                            }
                    }
                ]
        }

        let result =
            Validation.validateTx Hashing.isValidBlockchainAddress Helpers.maxActionCountPerTx chAddress txHash testTx

        match result with
        | Ok t -> failwith "Validation should fail in case of this test."
        | Error errors ->
            test <@ errors.Length = 1 @>

    [<Fact>]
    let ``Validation.validateTx TransferChx invalid RecipientAddress`` () =
        let testTx = {
            SenderAddress = chAddress.Value
            Nonce = 10L
            ActionFee = 1m
            Actions =
                [
                    {
                        ActionType = transferChxActionType
                        ActionData =
                            {
                                TransferChxTxActionDto.RecipientAddress = ""
                                Amount = 10m
                            }
                    }
                ]
        }

        let result =
            Validation.validateTx Hashing.isValidBlockchainAddress Helpers.maxActionCountPerTx chAddress txHash testTx

        match result with
        | Ok t -> failwith "Validation should fail in case of this test."
        | Error errors ->
            test <@ errors.Length > 0 @>

    [<Fact>]
    let ``Validation.validateTx TransferAsset invalid FromAccountHash`` () =
        let testTx = {
            SenderAddress = chAddress.Value
            Nonce = 10L
            ActionFee = 1m
            Actions =
                [
                    {
                        ActionType = transferAssetActionType
                        ActionData =
                            {
                                TransferAssetTxActionDto.FromAccountHash = ""
                                ToAccountHash = "B"
                                AssetHash = "asset"
                                Amount = 12m
                            }
                    }
                ]
        }

        let result =
            Validation.validateTx Hashing.isValidBlockchainAddress Helpers.maxActionCountPerTx chAddress txHash testTx

        match result with
        | Ok t -> failwith "Validation should fail in case of this test."
        | Error errors ->
            test <@ errors.Length = 1 @>

    [<Fact>]
    let ``Validation.validateTx TransferAsset invalid ToAccountHash`` () =
        let testTx = {
            SenderAddress = chAddress.Value
            Nonce = 10L
            ActionFee = 1m
            Actions =
                [
                    {
                        ActionType = transferAssetActionType
                        ActionData =
                            {
                                TransferAssetTxActionDto.FromAccountHash = "A"
                                ToAccountHash = ""
                                AssetHash = "asset"
                                Amount = 12m
                            }
                    }
                ]
        }

        let result =
            Validation.validateTx Hashing.isValidBlockchainAddress Helpers.maxActionCountPerTx chAddress txHash testTx

        match result with
        | Ok t -> failwith "Validation should fail in case of this test."
        | Error errors ->
            test <@ errors.Length = 1 @>

    [<Fact>]
    let ``Validation.validateTx TransferAsset same FromAccountHash and ToAccountHash`` () =
        let testTx = {
            SenderAddress = chAddress.Value
            Nonce = 10L
            ActionFee = 1m
            Actions =
                [
                    {
                        ActionType = transferAssetActionType
                        ActionData =
                            {
                                TransferAssetTxActionDto.FromAccountHash = "A"
                                ToAccountHash = "A"
                                AssetHash = "asset"
                                Amount = 12m
                            }
                    }
                ]
        }

        let result =
            Validation.validateTx Hashing.isValidBlockchainAddress Helpers.maxActionCountPerTx chAddress txHash testTx

        match result with
        | Ok t -> failwith "Validation should fail in case of this test."
        | Error errors ->
            test <@ errors.Length = 1 @>

    [<Fact>]
    let ``Validation.validateTx TransferAsset invalid Asset`` () =
        let testTx = {
            SenderAddress = chAddress.Value
            Nonce = 10L
            ActionFee = 1m
            Actions =
                [
                    {
                        ActionType = transferAssetActionType
                        ActionData =
                            {
                                TransferAssetTxActionDto.FromAccountHash = "A"
                                ToAccountHash = "B"
                                AssetHash = ""
                                Amount = 12m
                            }
                    }
                ]
        }

        let result =
            Validation.validateTx Hashing.isValidBlockchainAddress Helpers.maxActionCountPerTx chAddress txHash testTx

        match result with
        | Ok t -> failwith "Validation should fail in case of this test."
        | Error errors ->
            test <@ errors.Length = 1 @>

    [<Fact>]
    let ``Validation.validateTx TransferAsset invalid Amount`` () =
        let testTx = {
            SenderAddress = chAddress.Value
            Nonce = 10L
            ActionFee = 1m
            Actions =
                [
                    {
                        ActionType = transferAssetActionType
                        ActionData =
                            {
                                TransferAssetTxActionDto.FromAccountHash = "A"
                                ToAccountHash = "B"
                                AssetHash = "asset"
                                Amount = 0m
                            }
                    }
                ]
        }

        let result =
            Validation.validateTx Hashing.isValidBlockchainAddress Helpers.maxActionCountPerTx chAddress txHash testTx

        match result with
        | Ok t -> failwith "Validation should fail in case of this test."
        | Error errors ->
            test <@ errors.Length = 1 @>

    [<Fact>]
    let ``Validation.validateTx TransferAsset invalid Amount, too many decimals`` () =
        let testTx = {
            SenderAddress = chAddress.Value
            Nonce = 10L
            ActionFee = 1m
            Actions =
                [
                    {
                        ActionType = transferAssetActionType
                        ActionData =
                            {
                                TransferAssetTxActionDto.FromAccountHash = "A"
                                ToAccountHash = "B"
                                AssetHash = "asset"
                                Amount = 10.12345678m
                            }
                    }
                ]
        }

        let result =
            Validation.validateTx Hashing.isValidBlockchainAddress Helpers.maxActionCountPerTx chAddress txHash testTx

        match result with
        | Ok t -> failwith "Validation should fail in case of this test."
        | Error errors ->
            test <@ errors.Length = 1 @>

    [<Fact>]
    let ``Validation.validateTx validate action`` () =
        let recipientWallet = Signing.generateWallet ()
        let testTx = {
            SenderAddress = chAddress.Value
            Nonce = 10L
            ActionFee = 1m
            Actions =
                [
                    {
                        ActionType = transferChxActionType
                        ActionData =
                            {
                                TransferChxTxActionDto.RecipientAddress = recipientWallet.Address.Value
                                Amount = 10m
                            }
                    }
                    {
                        ActionType = transferAssetActionType
                        ActionData =
                            {
                                TransferAssetTxActionDto.FromAccountHash = "A"
                                ToAccountHash = "B"
                                AssetHash = "asset"
                                Amount = 1m
                            }
                    }
                ]
        }

        let result =
            Validation.validateTx Hashing.isValidBlockchainAddress Helpers.maxActionCountPerTx chAddress txHash testTx

        match result with
        | Ok t ->
            let expectedChx = (testTx.Actions.[0].ActionData :?> TransferChxTxActionDto)
            let actualChx = t.Actions.[0] |> Helpers.extractActionData<TransferChxTxAction>

            let expAsset = (testTx.Actions.[1].ActionData :?> TransferAssetTxActionDto)
            let actualAsset = t.Actions.[1] |> Helpers.extractActionData<TransferAssetTxAction>

            test <@ t.ActionFee = ChxAmount testTx.ActionFee @>
            test <@ t.Nonce = Nonce testTx.Nonce @>
            test <@ t.TxHash = txHash @>
            test <@ t.Sender = chAddress @>
            test <@ actualChx.Amount = ChxAmount expectedChx.Amount @>
            test <@ actualChx.RecipientAddress = BlockchainAddress expectedChx.RecipientAddress @>
            test <@ actualAsset.FromAccountHash = AccountHash expAsset.FromAccountHash @>
            test <@ actualAsset.ToAccountHash = AccountHash expAsset.ToAccountHash @>
            test <@ actualAsset.AssetHash = AssetHash expAsset.AssetHash @>
            test <@ actualAsset.Amount = AssetAmount expAsset.Amount @>
        | Error errors ->
            failwithf "%A" errors

    let private isValidAddressMock (address : BlockchainAddress) =
        let item = address.Value
        String.IsNullOrWhiteSpace(item) |> not

    [<Fact>]
    let ``Validation.validateTx CreateAssetEmission valid action`` () =
        let expected =
            {
                CreateAssetEmissionTxActionDto.EmissionAccountHash = "AAA"
                AssetHash = "BBB"
                Amount = 100m
            }

        let tx = {
            SenderAddress = chAddress.Value
            Nonce = 10L
            ActionFee = 1m
            Actions =
                [
                    {
                        ActionType = createAssetEmissionActionType
                        ActionData = expected
                    }
                ]
        }

        match Validation.validateTx isValidAddressMock Helpers.maxActionCountPerTx chAddress txHash tx with
        | Ok t ->
            let actual = Helpers.extractActionData<CreateAssetEmissionTxAction> t.Actions.Head
            test <@ AccountHash expected.EmissionAccountHash = actual.EmissionAccountHash @>
            test <@ AssetHash expected.AssetHash = actual.AssetHash @>
            test <@ AssetAmount expected.Amount = actual.Amount @>
        | Error e -> failwithf "%A" e

    [<Fact>]
    let ``Validation.validateTx CreateAssetEmission invalid action`` () =
        let expected =
            {
                CreateAssetEmissionTxActionDto.EmissionAccountHash = ""
                AssetHash = ""
                Amount = 0m
            }

        let tx = {
            SenderAddress = chAddress.Value
            Nonce = 10L
            ActionFee = 1m
            Actions =
                [
                    {
                        ActionType = setAccountControllerActionType
                        ActionData = expected
                    }
                ]
        }

        match Validation.validateTx isValidAddressMock Helpers.maxActionCountPerTx chAddress txHash tx with
        | Ok t -> failwith "This test should fail."
        | Error e ->
            test <@ e.Length = 3 @>

    [<Fact>]
    let ``Validation.validateTx CreateAccount valid action`` () =
        let tx = {
            SenderAddress = chAddress.Value
            Nonce = 10L
            ActionFee = 1m
            Actions =
                [
                    {
                        ActionType = createAccountActionType
                        ActionData = CreateAccountTxActionDto()
                    }
                ]
        }

        match Validation.validateTx isValidAddressMock Helpers.maxActionCountPerTx chAddress txHash tx with
        | Ok t ->
            test <@ t.Actions.Head = CreateAccount @>
        | Error e -> failwithf "%A" e

    [<Fact>]
    let ``Validation.validateTx CreateAsset valid action`` () =
        let tx = {
            SenderAddress = chAddress.Value
            Nonce = 10L
            ActionFee = 1m
            Actions =
                [
                    {
                        ActionType = createAssetActionType
                        ActionData = CreateAssetTxActionDto()
                    }
                ]
        }

        match Validation.validateTx isValidAddressMock Helpers.maxActionCountPerTx chAddress txHash tx with
        | Ok t ->
            test <@ t.Actions.Head = CreateAsset @>
        | Error e -> failwithf "%A" e

    [<Fact>]
    let ``Validation.validateTx SetAccountController valid action`` () =
        let expected =
            {
                SetAccountControllerTxActionDto.AccountHash = "A"
                ControllerAddress = chAddress.Value
            }

        let tx = {
            SenderAddress = chAddress.Value
            Nonce = 10L
            ActionFee = 1m
            Actions =
                [
                    {
                        ActionType = setAccountControllerActionType
                        ActionData = expected
                    }
                ]
        }

        match Validation.validateTx isValidAddressMock Helpers.maxActionCountPerTx chAddress txHash tx with
        | Ok t ->
            let actual = Helpers.extractActionData<SetAccountControllerTxAction> t.Actions.Head
            test <@ AccountHash expected.AccountHash = actual.AccountHash @>
            test <@ BlockchainAddress expected.ControllerAddress = actual.ControllerAddress @>
        | Error e -> failwithf "%A" e

    [<Fact>]
    let ``Validation.validateTx SetAccountController invalid action`` () =
        let expected =
            {
                SetAccountControllerTxActionDto.AccountHash = ""
                ControllerAddress = ""
            }

        let tx = {
            SenderAddress = chAddress.Value
            Nonce = 10L
            ActionFee = 1m
            Actions =
                [
                    {
                        ActionType = setAccountControllerActionType
                        ActionData = expected
                    }
                ]
        }

        match Validation.validateTx isValidAddressMock Helpers.maxActionCountPerTx chAddress txHash tx with
        | Ok t -> failwith "This test should fail."
        | Error e ->
            test <@ e.Length = 2 @>

    [<Fact>]
    let ``Validation.validateTx SetAssetController valid action`` () =
        let expected =
            {
                SetAssetControllerTxActionDto.AssetHash = "A"
                ControllerAddress = chAddress.Value
            }

        let tx = {
            SenderAddress = chAddress.Value
            Nonce = 10L
            ActionFee = 1m
            Actions =
                [
                    {
                        ActionType = setAssetControllerActionType
                        ActionData = expected
                    }
                ]
        }

        match Validation.validateTx isValidAddressMock Helpers.maxActionCountPerTx chAddress txHash tx with
        | Ok t ->
            let actual = Helpers.extractActionData<SetAssetControllerTxAction> t.Actions.Head
            test <@ AssetHash expected.AssetHash = actual.AssetHash @>
            test <@ BlockchainAddress expected.ControllerAddress = actual.ControllerAddress @>
        | Error e -> failwithf "%A" e

    [<Fact>]
    let ``Validation.validateTx SetAssetController invalid action`` () =
        let expected =
            {
                SetAssetControllerTxActionDto.AssetHash = ""
                ControllerAddress = ""
            }

        let tx = {
            SenderAddress = chAddress.Value
            Nonce = 10L
            ActionFee = 1m
            Actions =
                [
                    {
                        ActionType = setAssetControllerActionType
                        ActionData = expected
                    }
                ]
        }

        match Validation.validateTx isValidAddressMock Helpers.maxActionCountPerTx chAddress txHash tx with
        | Ok t -> failwith "This test should fail."
        | Error e ->
            test <@ e.Length = 2 @>

    [<Fact>]
    let ``Validation.validateTx SetAssetCode valid action`` () =
        let expected =
            {
                SetAssetCodeTxActionDto.AssetHash = "A"
                AssetCode = "B"
            }

        let tx = {
            SenderAddress = chAddress.Value
            Nonce = 10L
            ActionFee = 1m
            Actions =
                [
                    {
                        ActionType = setAssetCodeActionType
                        ActionData = expected
                    }
                ]
        }

        match Validation.validateTx isValidAddressMock Helpers.maxActionCountPerTx chAddress txHash tx with
        | Ok t ->
            let actual = Helpers.extractActionData<SetAssetCodeTxAction> t.Actions.Head
            test <@ AssetHash expected.AssetHash = actual.AssetHash @>
            test <@ AssetCode expected.AssetCode = actual.AssetCode @>
        | Error e -> failwithf "%A" e

    [<Fact>]
    let ``Validation.validateTx SetAssetCode invalid action`` () =
        let expected =
            {
                SetAssetCodeTxActionDto.AssetHash = ""
                AssetCode = "A"
            }

        let tx = {
            SenderAddress = chAddress.Value
            Nonce = 10L
            ActionFee = 1m
            Actions =
                [
                    {
                        ActionType = setAssetCodeActionType
                        ActionData = expected
                    }
                ]
        }

        match Validation.validateTx isValidAddressMock Helpers.maxActionCountPerTx chAddress txHash tx with
        | Ok t -> failwith "This test should fail."
        | Error e ->
            test <@ e.Length = 1 @>

    [<Fact>]
    let ``Validation.validateTx SetAssetCode code is too long`` () =
        let expected =
            {
                SetAssetCodeTxActionDto.AssetHash = "A"
                AssetCode = "ABCDEFGHIJK0123456789"
            }

        let tx = {
            SenderAddress = chAddress.Value
            Nonce = 10L
            ActionFee = 1m
            Actions =
                [
                    {
                        ActionType = setAssetCodeActionType
                        ActionData = expected
                    }
                ]
        }

        match Validation.validateTx isValidAddressMock Helpers.maxActionCountPerTx chAddress txHash tx with
        | Ok t -> failwith "This test should fail."
        | Error e ->
            test <@ e.Length = 1 @>

    [<Fact>]
    let ``Validation.validateTx SetAssetCode code has invalid chars`` () =
        let expected =
            {
                SetAssetCodeTxActionDto.AssetHash = "A"
                AssetCode = "AaabcZ"
            }

        let tx = {
            SenderAddress = chAddress.Value
            Nonce = 10L
            ActionFee = 1m
            Actions =
                [
                    {
                        ActionType = setAssetCodeActionType
                        ActionData = expected
                    }
                ]
        }

        match Validation.validateTx isValidAddressMock Helpers.maxActionCountPerTx chAddress txHash tx with
        | Ok t -> failwith "This test should fail."
        | Error e ->
            test <@ e.Length = 1 @>

    [<Fact>]
    let ``Validation.validateTx ConfigureValidator valid action`` () =
        let expected =
            {
                ConfigureValidatorTxActionDto.NetworkAddress = "A"
                SharedRewardPercent = 42m
                IsEnabled = true
            }

        let tx = {
            SenderAddress = chAddress.Value
            Nonce = 10L
            ActionFee = 1m
            Actions =
                [
                    {
                        ActionType = configureValidatorActionType
                        ActionData = expected
                    }
                ]
        }

        match Validation.validateTx isValidAddressMock Helpers.maxActionCountPerTx chAddress txHash tx with
        | Ok t ->
            let actual = Helpers.extractActionData<ConfigureValidatorTxAction> t.Actions.Head
            test <@ expected.NetworkAddress = actual.NetworkAddress.Value @>
            test <@ expected.SharedRewardPercent = actual.SharedRewardPercent @>
        | Error e -> failwithf "%A" e

    [<Fact>]
    let ``Validation.validateTx ConfigureValidator invalid action`` () =
        let expected =
            {
                ConfigureValidatorTxActionDto.NetworkAddress = ""
                SharedRewardPercent = 0m
                IsEnabled = true
            }

        let tx = {
            SenderAddress = chAddress.Value
            Nonce = 10L
            ActionFee = 1m
            Actions =
                [
                    {
                        ActionType = configureValidatorActionType
                        ActionData = expected
                    }
                ]
        }

        match Validation.validateTx isValidAddressMock Helpers.maxActionCountPerTx chAddress txHash tx with
        | Ok t -> failwith "This test should fail."
        | Error e ->
            test <@ e.Length = 1 @>

    [<Fact>]
    let ``Validation.validateTx DelegateStake valid action`` () =
        let expected =
            {
                DelegateStakeTxActionDto.ValidatorAddress = "A"
                Amount = 1000m
            }

        let tx = {
            SenderAddress = chAddress.Value
            Nonce = 10L
            ActionFee = 1m
            Actions =
                [
                    {
                        ActionType = delegateStakeActionType
                        ActionData = expected
                    }
                ]
        }

        match Validation.validateTx isValidAddressMock Helpers.maxActionCountPerTx chAddress txHash tx with
        | Ok t ->
            let actual = Helpers.extractActionData<DelegateStakeTxAction> t.Actions.Head
            test <@ BlockchainAddress expected.ValidatorAddress = actual.ValidatorAddress @>
            test <@ ChxAmount expected.Amount = actual.Amount @>
        | Error e -> failwithf "%A" e

    [<Fact>]
    let ``Validation.validateTx DelegateStake invalid action`` () =
        let expected =
            {
                DelegateStakeTxActionDto.ValidatorAddress = ""
                Amount = 0m
            }

        let tx = {
            SenderAddress = chAddress.Value
            Nonce = 10L
            ActionFee = 1m
            Actions =
                [
                    {
                        ActionType = delegateStakeActionType
                        ActionData = expected
                    }
                ]
        }

        match Validation.validateTx isValidAddressMock Helpers.maxActionCountPerTx chAddress txHash tx with
        | Ok t -> failwith "This test should fail."
        | Error e ->
            test <@ e.Length = 2 @>
