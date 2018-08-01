namespace Chainium.Blockchain.Public.Core.Tests

open System
open Xunit
open Swensen.Unquote
open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Core
open Chainium.Blockchain.Public.Core.Dtos
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Crypto

module ValidationTests =

    let chAddress = ChainiumAddress "CHMHABow7Liry6TqswwzxHnMfcYNbrJBAtp"
    let txHash = TxHash "SampleHash"
    let transferChxActionType = "TransferChx"
    let transferAssetActionType = "TransferAsset"
    let createAssetEmissionActionType = "CreateAssetEmission"
    let createAccountActionType = "CreateAccount"
    let createAssetActionType = "CreateAsset"
    let setAccountControllerActionType = "SetAccountController"
    let setAssetControllerActionType = "SetAssetController"
    let setAssetCodeActionType = "SetAssetCode"
    let setValidatorNetworkAddressActionType = "SetValidatorNetworkAddress"
    let setStakeActionType = "SetStake"

    [<Fact>]
    let ``Validation.validateTx BasicValidation single validation error`` () =
        let recipientWallet = Signing.generateWallet ()
        let testTx = {
            SenderAddress = chAddress |> fun (ChainiumAddress a) -> a
            Nonce = -10L
            Fee = 20M
            Actions =
                [
                    {
                        ActionType = transferChxActionType
                        ActionData =
                            {
                                TransferChxTxActionDto.RecipientAddress =
                                    recipientWallet.Address |> fun (ChainiumAddress a) -> a
                                Amount = 20M
                            }
                    }
                    {
                        ActionType = transferAssetActionType
                        ActionData =
                            {
                                FromAccount = "A"
                                ToAccount = "B"
                                AssetHash = "asset"
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
    let ``Validation.validateTx BasicValidation multiple validation errors`` () =
        let recipientWallet = Signing.generateWallet ()
        let testTx = {
            SenderAddress = ""
            Nonce = -10L
            Fee = 0M
            Actions =
                [
                    {
                        ActionType = transferChxActionType
                        ActionData =
                            {
                                TransferChxTxActionDto.RecipientAddress =
                                    recipientWallet.Address |> fun (ChainiumAddress a) -> a
                                Amount = 20M
                            }
                    }
                    {
                        ActionType = transferAssetActionType
                        ActionData =
                            {
                                FromAccount = "A"
                                ToAccount = "B"
                                AssetHash = "asset"
                                Amount = 12M
                            }
                    }
                ]
        }

        let result = Validation.validateTx Hashing.isValidChainiumAddress Helpers.minTxActionFee chAddress txHash testTx

        match result with
        | Ok t -> failwith "Validation should fail in case of this test."
        | Error errors ->
            test <@ errors.Length = 4 @>

    [<Fact>]
    let ``Validation.validateTx BasicValidation unknown action type`` () =
        let testTx = {
            SenderAddress = chAddress |> fun (ChainiumAddress a) -> a
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
    let ``Validation.validateTx TransferChx invalid Amount`` () =
        let recipientWallet = Signing.generateWallet ()
        let testTx = {
            SenderAddress = chAddress |> fun (ChainiumAddress a) -> a
            Nonce = 10L
            Fee = 1M
            Actions =
                [
                    {
                        ActionType = transferChxActionType
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
    let ``Validation.validateTx TransferChx invalid RecipientAddress`` () =
        let testTx = {
            SenderAddress = chAddress |> fun (ChainiumAddress a) -> a
            Nonce = 10L
            Fee = 1M
            Actions =
                [
                    {
                        ActionType = transferChxActionType
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
    let ``Validation.validateTx TransferAsset invalid FromAccount`` () =
        let testTx = {
            SenderAddress = chAddress |> fun (ChainiumAddress a) -> a
            Nonce = 10L
            Fee = 1M
            Actions =
                [
                    {
                        ActionType = transferAssetActionType
                        ActionData =
                            {
                                FromAccount = ""
                                ToAccount = "B"
                                AssetHash = "asset"
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
    let ``Validation.validateTx TransferAsset invalid ToAccount`` () =
        let testTx = {
            SenderAddress = chAddress |> fun (ChainiumAddress a) -> a
            Nonce = 10L
            Fee = 1M
            Actions =
                [
                    {
                        ActionType = transferAssetActionType
                        ActionData =
                            {
                                FromAccount = "A"
                                ToAccount = ""
                                AssetHash = "asset"
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
    let ``Validation.validateTx TransferAsset invalid Asset`` () =
        let testTx = {
            SenderAddress = chAddress |> fun (ChainiumAddress a) -> a
            Nonce = 10L
            Fee = 1M
            Actions =
                [
                    {
                        ActionType = transferAssetActionType
                        ActionData =
                            {
                                FromAccount = "A"
                                ToAccount = "B"
                                AssetHash = ""
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
    let ``Validation.validateTx TransferAsset invalid Amount`` () =
        let testTx = {
            SenderAddress = chAddress |> fun (ChainiumAddress a) -> a
            Nonce = 10L
            Fee = 1M
            Actions =
                [
                    {
                        ActionType = transferAssetActionType
                        ActionData =
                            {
                                FromAccount = "A"
                                ToAccount = "B"
                                AssetHash = "asset"
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
    let ``Validation.validateTx validate action`` () =
        let recipientWallet = Signing.generateWallet ()
        let testTx = {
            SenderAddress = chAddress |> fun (ChainiumAddress a) -> a
            Nonce = 10L
            Fee = 1M
            Actions =
                [
                    {
                        ActionType = transferChxActionType
                        ActionData =
                            {
                                TransferChxTxActionDto.RecipientAddress =
                                    recipientWallet.Address |> fun (ChainiumAddress a) -> a
                                Amount = 10M
                            }
                    }
                    {
                        ActionType = transferAssetActionType
                        ActionData =
                            {
                                FromAccount = "A"
                                ToAccount = "B"
                                AssetHash = "asset"
                                Amount = 1M
                            }
                    }
                ]
        }

        let result = Validation.validateTx Hashing.isValidChainiumAddress Helpers.minTxActionFee chAddress txHash testTx

        match result with
        | Ok t ->
            let expectedChx = (testTx.Actions.[0].ActionData :?> TransferChxTxActionDto)
            let actualChx = t.Actions.[0] |> Helpers.extractActionData<TransferChxTxAction>

            let expAsset = (testTx.Actions.[1].ActionData :?> TransferAssetTxActionDto)
            let actualAsset = t.Actions.[1] |> Helpers.extractActionData<TransferAssetTxAction>

            test <@ t.Fee = ChxAmount testTx.Fee @>
            test <@ t.Nonce = Nonce testTx.Nonce @>
            test <@ t.TxHash = txHash @>
            test <@ t.Sender = chAddress @>
            test <@ actualChx.Amount = ChxAmount expectedChx.Amount @>
            test <@ actualChx.RecipientAddress = ChainiumAddress expectedChx.RecipientAddress @>
            test <@ actualAsset.FromAccountHash = AccountHash expAsset.FromAccount @>
            test <@ actualAsset.ToAccountHash = AccountHash expAsset.ToAccount @>
            test <@ actualAsset.AssetHash = AssetHash expAsset.AssetHash @>
            test <@ actualAsset.Amount = AssetAmount expAsset.Amount @>
        | Error errors ->
            failwithf "%A" errors

    let private isValidAddressMock (address : ChainiumAddress) =
        let item = address |> fun (ChainiumAddress a) -> a
        String.IsNullOrWhiteSpace(item) |> not

    [<Fact>]
    let ``Validation.validateTx CreateAssetEmission valid action`` () =
        let expected =
            {
                CreateAssetEmissionTxActionDto.EmissionAccountHash = "AAA"
                AssetHash = "BBB"
                Amount = 100M
            }

        let tx = {
            SenderAddress = chAddress |> fun (ChainiumAddress a) -> a
            Nonce = 10L
            Fee = 1M
            Actions =
                [
                    {
                        ActionType = createAssetEmissionActionType
                        ActionData = expected
                    }
                ]
        }

        match Validation.validateTx isValidAddressMock Helpers.minTxActionFee chAddress txHash tx with
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
                Amount = 0M
            }

        let tx = {
            SenderAddress = chAddress |> fun (ChainiumAddress a) -> a
            Nonce = 10L
            Fee = 1M
            Actions =
                [
                    {
                        ActionType = setAccountControllerActionType
                        ActionData = expected
                    }
                ]
        }

        match Validation.validateTx isValidAddressMock Helpers.minTxActionFee chAddress txHash tx with
        | Ok t -> failwith "This test should fail."
        | Error e ->
            test <@ e.Length = 3 @>

    [<Fact>]
    let ``Validation.validateTx CreateAccount valid action`` () =
        let tx = {
            SenderAddress = chAddress |> fun (ChainiumAddress a) -> a
            Nonce = 10L
            Fee = 1M
            Actions =
                [
                    {
                        ActionType = createAccountActionType
                        ActionData = CreateAccountTxActionDto()
                    }
                ]
        }

        match Validation.validateTx isValidAddressMock Helpers.minTxActionFee chAddress txHash tx with
        | Ok t ->
            test <@ t.Actions.Head = CreateAccount @>
        | Error e -> failwithf "%A" e

    [<Fact>]
    let ``Validation.validateTx CreateAsset valid action`` () =
        let tx = {
            SenderAddress = chAddress |> fun (ChainiumAddress a) -> a
            Nonce = 10L
            Fee = 1M
            Actions =
                [
                    {
                        ActionType = createAssetActionType
                        ActionData = CreateAssetTxActionDto()
                    }
                ]
        }

        match Validation.validateTx isValidAddressMock Helpers.minTxActionFee chAddress txHash tx with
        | Ok t ->
            test <@ t.Actions.Head = CreateAsset @>
        | Error e -> failwithf "%A" e

    [<Fact>]
    let ``Validation.validateTx SetAccountController valid action`` () =
        let expected =
            {
                SetAccountControllerTxActionDto.AccountHash = "A"
                ControllerAddress = chAddress |> fun (ChainiumAddress a) -> a
            }

        let tx = {
            SenderAddress = chAddress |> fun (ChainiumAddress a) -> a
            Nonce = 10L
            Fee = 1M
            Actions =
                [
                    {
                        ActionType = setAccountControllerActionType
                        ActionData = expected
                    }
                ]
        }

        match Validation.validateTx isValidAddressMock Helpers.minTxActionFee chAddress txHash tx with
        | Ok t ->
            let actual = Helpers.extractActionData<SetAccountControllerTxAction> t.Actions.Head
            test <@ AccountHash expected.AccountHash = actual.AccountHash @>
            test <@ ChainiumAddress expected.ControllerAddress = actual.ControllerAddress @>
        | Error e -> failwithf "%A" e

    [<Fact>]
    let ``Validation.validateTx SetAccountController invalid action`` () =
        let expected =
            {
                SetAccountControllerTxActionDto.AccountHash = ""
                ControllerAddress = ""
            }

        let tx = {
            SenderAddress = chAddress |> fun (ChainiumAddress a) -> a
            Nonce = 10L
            Fee = 1M
            Actions =
                [
                    {
                        ActionType = setAccountControllerActionType
                        ActionData = expected
                    }
                ]
        }

        match Validation.validateTx isValidAddressMock Helpers.minTxActionFee chAddress txHash tx with
        | Ok t -> failwith "This test should fail."
        | Error e ->
            test <@ e.Length = 2 @>

    [<Fact>]
    let ``Validation.validateTx SetAssetController valid action`` () =
        let expected =
            {
                SetAssetControllerTxActionDto.AssetHash = "A"
                ControllerAddress = chAddress |> fun (ChainiumAddress a) -> a
            }

        let tx = {
            SenderAddress = chAddress |> fun (ChainiumAddress a) -> a
            Nonce = 10L
            Fee = 1M
            Actions =
                [
                    {
                        ActionType = setAssetControllerActionType
                        ActionData = expected
                    }
                ]
        }

        match Validation.validateTx isValidAddressMock Helpers.minTxActionFee chAddress txHash tx with
        | Ok t ->
            let actual = Helpers.extractActionData<SetAssetControllerTxAction> t.Actions.Head
            test <@ AssetHash expected.AssetHash = actual.AssetHash @>
            test <@ ChainiumAddress expected.ControllerAddress = actual.ControllerAddress @>
        | Error e -> failwithf "%A" e

    [<Fact>]
    let ``Validation.validateTx SetAssetController invalid action`` () =
        let expected =
            {
                SetAssetControllerTxActionDto.AssetHash = ""
                ControllerAddress = ""
            }

        let tx = {
            SenderAddress = chAddress |> fun (ChainiumAddress a) -> a
            Nonce = 10L
            Fee = 1M
            Actions =
                [
                    {
                        ActionType = setAssetControllerActionType
                        ActionData = expected
                    }
                ]
        }

        match Validation.validateTx isValidAddressMock Helpers.minTxActionFee chAddress txHash tx with
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
            SenderAddress = chAddress |> fun (ChainiumAddress a) -> a
            Nonce = 10L
            Fee = 1M
            Actions =
                [
                    {
                        ActionType = setAssetCodeActionType
                        ActionData = expected
                    }
                ]
        }

        match Validation.validateTx isValidAddressMock Helpers.minTxActionFee chAddress txHash tx with
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
                AssetCode = ""
            }

        let tx = {
            SenderAddress = chAddress |> fun (ChainiumAddress a) -> a
            Nonce = 10L
            Fee = 1M
            Actions =
                [
                    {
                        ActionType = setAssetCodeActionType
                        ActionData = expected
                    }
                ]
        }

        match Validation.validateTx isValidAddressMock Helpers.minTxActionFee chAddress txHash tx with
        | Ok t -> failwith "This test should fail."
        | Error e ->
            test <@ e.Length = 2 @>

    [<Fact>]
    let ``Validation.validateTx SetValidatorNetworkAddress valid action`` () =
        let expected =
            {
                SetValidatorNetworkAddressTxActionDto.NetworkAddress = "A"
            }

        let tx = {
            SenderAddress = chAddress |> fun (ChainiumAddress a) -> a
            Nonce = 10L
            Fee = 1M
            Actions =
                [
                    {
                        ActionType = setValidatorNetworkAddressActionType
                        ActionData = expected
                    }
                ]
        }

        match Validation.validateTx isValidAddressMock Helpers.minTxActionFee chAddress txHash tx with
        | Ok t ->
            let actual = Helpers.extractActionData<SetValidatorNetworkAddressTxAction> t.Actions.Head
            test <@ expected.NetworkAddress = actual.NetworkAddress @>
        | Error e -> failwithf "%A" e

    [<Fact>]
    let ``Validation.validateTx SetValidatorNetworkAddress invalid action`` () =
        let expected =
            {
                SetValidatorNetworkAddressTxActionDto.NetworkAddress = ""
            }

        let tx = {
            SenderAddress = chAddress |> fun (ChainiumAddress a) -> a
            Nonce = 10L
            Fee = 1M
            Actions =
                [
                    {
                        ActionType = setValidatorNetworkAddressActionType
                        ActionData = expected
                    }
                ]
        }

        match Validation.validateTx isValidAddressMock Helpers.minTxActionFee chAddress txHash tx with
        | Ok t -> failwith "This test should fail."
        | Error e ->
            test <@ e.Length = 1 @>

    [<Fact>]
    let ``Validation.validateTx SetStake valid action`` () =
        let expected =
            {
                SetStakeTxActionDto.ValidatorAddress = "A"
                Amount = 1000M
            }

        let tx = {
            SenderAddress = chAddress |> fun (ChainiumAddress a) -> a
            Nonce = 10L
            Fee = 1M
            Actions =
                [
                    {
                        ActionType = setStakeActionType
                        ActionData = expected
                    }
                ]
        }

        match Validation.validateTx isValidAddressMock Helpers.minTxActionFee chAddress txHash tx with
        | Ok t ->
            let actual = Helpers.extractActionData<SetStakeTxAction> t.Actions.Head
            test <@ ChainiumAddress expected.ValidatorAddress = actual.ValidatorAddress @>
            test <@ ChxAmount expected.Amount = actual.Amount @>
        | Error e -> failwithf "%A" e

    [<Fact>]
    let ``Validation.validateTx SetStake invalid action`` () =
        let expected =
            {
                SetStakeTxActionDto.ValidatorAddress = ""
                Amount = -1M
            }

        let tx = {
            SenderAddress = chAddress |> fun (ChainiumAddress a) -> a
            Nonce = 10L
            Fee = 1M
            Actions =
                [
                    {
                        ActionType = setStakeActionType
                        ActionData = expected
                    }
                ]
        }

        match Validation.validateTx isValidAddressMock Helpers.minTxActionFee chAddress txHash tx with
        | Ok t -> failwith "This test should fail."
        | Error e ->
            test <@ e.Length = 2 @>
