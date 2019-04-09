namespace Own.Blockchain.Public.Core.Tests

open Xunit
open Swensen.Unquote
open Own.Blockchain.Common
open Own.Blockchain.Public.Core
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Core.Dtos

module SerializationTests =

    [<Fact>]
    let ``Serialization.deserializeTx transaction`` () =
        let expectedTx =
            {
                SenderAddress = "SomeAddress"
                Nonce = 10L
                ExpirationTime = 0L
                ActionFee = 20m
                Actions =
                    [
                        {
                            ActionType = "TransferChx"
                            ActionData =
                                {
                                    RecipientAddress = "Recipient"
                                    Amount = 20m
                                }
                        }
                        {
                            ActionType = "TransferAsset"
                            ActionData =
                                {
                                    FromAccountHash = "A"
                                    ToAccountHash = "B"
                                    AssetHash = "asset"
                                    Amount = 12m
                                }
                        }
                    ]
            }

        // Make a call to Newtonsoft to get JSON format.
        let resultTx =
            expectedTx
            |> Serialization.serializeTx

        match resultTx with
        | Ok rawTx ->
            let actualTxRes = Serialization.deserializeTx rawTx
            match actualTxRes with
            | Ok actualTx -> test <@ expectedTx = actualTx @>
            | Error e -> failwithf "%A" e
        | Error errors ->
            failwithf "%A" errors

    [<Fact>]
    let ``Serialization.deserializeTx unknown action type added`` () =
        let json =
            """
            {
                "Nonce": 120,
                "ActionFee": 20,
                "Actions": [
                    {
                        "ActionType": "TransferChx",
                        "ActionData": {
                            "RecipientAddress": "Recipient",
                            "Amount": 20.0
                        }
                    },
                    {
                        "ActionType": "TransferAsset",
                        "ActionData": {
                            "FromAccountHash": "A",
                            "ToAccountHash": "B",
                            "AssetHash": "asset",
                            "Amount": 12.0
                        }
                    },
                    {
                        "ActionType": "Unknown",
                        "ActionData": "Test"
                    }
                ]
            }
            """

        let result =
            json
            |> Conversion.stringToBytes
            |> Serialization.deserializeTx

        match result with
        | Ok tx ->
            test <@ tx.Actions.Length = 3 @>

            let numOfActionsByType transType =
                tx.Actions
                |> List.filter (fun a -> a.ActionData.GetType() = transType)
                |> List.length

            let chxActions = numOfActionsByType typeof<TransferChxTxActionDto>
            test <@ chxActions = 1 @>

            let assetActions = numOfActionsByType typeof<TransferAssetTxActionDto>
            test <@ assetActions = 1 @>

            let invalidActions = numOfActionsByType typeof<string>
            test <@ invalidActions = 1 @>
        | Error appErrors ->
            failwithf "%A" appErrors

    [<Fact>]
    let ``Serialization.deserializeTx invalid JSON for known action`` () =
        let json =
            """
            {
                "Nonce": 120,
                "ActionFee": 20,
                "Actions": [
                    {
                        "ActionType": "TransferChx",
                        "ActionData": {
                            "Recipient_Address": "Recipient",
                            "_Amount": 20.0
                        }
                    },
                    {
                        "ActionType": "TransferAsset",
                        "ActionData": {
                            "FromAccountHash": "A",
                            "ToAccountHash": "B",
                            "AssetHash": "asset",
                            "Amount": 12.0
                        }
                    },
                    {
                        "ActionType": "Unknown",
                        "ActionData": "Test"
                    }
                ]
            }
            """

        let result =
            json
            |> Conversion.stringToBytes
            |> Serialization.deserializeTx

        match result with
        | Ok tx ->
            failwithf "Unexpected result: %A" tx
        | Error appErrors ->
            test <@ appErrors.Head.Message.Contains("Could not find member 'Recipient_Address'") @>

    [<Fact>]
    let ``Serialization.deserializeTx unknown property in JSON for known action`` () =
        let json =
            """
            {
                "Nonce": 120,
                "ActionFee": 20,
                "Actions": [
                    {
                        "ActionType": "TransferChx",
                        "ActionData": {
                            "RecipientAddress": "Recipient",
                            "Foo": "Bar",
                            "Amount": 20.0
                        }
                    }
                ]
            }
            """

        let result =
            json
            |> Conversion.stringToBytes
            |> Serialization.deserializeTx

        match result with
        | Ok tx ->
            failwithf "Unexpected result: %A" tx
        | Error appErrors ->
            test <@ appErrors.Head.Message.Contains("Could not find member 'Foo'") @>

    [<Fact>]
    let ``Serialization.deserializeTx unknown property in JSON for action array item`` () =
        let json =
            """
            {
                "Nonce": 120,
                "ActionFee": 20,
                "Actions": [
                    {
                        "actionType": "TransferChx",
                        "Actiondata": {
                            "RecipientAddress": "Recipient",
                            "Amount": 20.0
                        }
                    },
                    {
                        "ActionType": "TransferChx",
                        "Foo1": "Bar1",
                        "Foo2": "Bar2",
                        "ActionData": {
                            "RecipientAddress": "Recipient",
                            "Amount": 20.0
                        }
                    }
                ]
            }
            """

        let result =
            json
            |> Conversion.stringToBytes
            |> Serialization.deserializeTx


        match result with
        | Ok tx ->
            failwithf "Unexpected result: %A" tx
        | Error appErrors ->
            test <@ appErrors.Length = 1 @>
            test <@ appErrors.Head.Message.Contains("Unexpected TX action list item properties: Foo1, Foo2") @>

    [<Fact>]
    let ``Serialization.deserializeTx unknown property in JSON for TX`` () =
        let json =
            """
            {
                "Nonce": 120,
                "Foo": "Bar",
                "ActionFee": 20,
                "Actions": [
                    {
                        "ActionType": "TransferChx",
                        "ActionData": {
                            "RecipientAddress": "Recipient",
                            "Amount": 20.0
                        }
                    }
                ]
            }
            """

        let result =
            json
            |> Conversion.stringToBytes
            |> Serialization.deserializeTx

        match result with
        | Ok tx ->
            failwithf "Unexpected result: %A" tx
        | Error appErrors ->
            test <@ appErrors.Head.Message.Contains("Could not find member 'Foo'") @>

    [<Fact>]
    let ``Serialization.deserializeTx invalid JSON`` () =
        let json =
            """
            {
                "Nonce": "InvalidValue",
                "ActionFee": 20,
                "Actions":
                    {
                        "ActionType": "TransferAsset",
                        "ActionData": {
                            "FromAccountHash": "A",
                            "ToAccountHash": "B",
                            "AssetHash": "asset",
                            "Amount": 12.0
                        }
                    },
                    {
                        "ActionType": "Unknown",
                        "ActionData": "Test"
                    }
                ]
            }
            """

        let result =
            json
            |> Conversion.stringToBytes
            |> Serialization.deserializeTx

        match result with
        | Ok tx ->
            failwith "This serialization attempt should fail"
        | Error appErrors ->
            test <@ appErrors.Length > 0 @>

    [<Fact>]
    let ``Serialization.deserializeTx CreateAssetEmission`` () =
        let expectedTxAction =
            {
                ActionType = "CreateAssetEmission"
                ActionData =
                    {
                        CreateAssetEmissionTxActionDto.EmissionAccountHash = "FooAccount"
                        AssetHash = "FooAsset"
                        Amount = 100m
                    }
            }

        let serializedTx =
            [ expectedTxAction |> box ]
            |> Helpers.newRawTxDto (BlockchainAddress "SomeAddress") 10L 0L 20m

        match Serialization.deserializeTx serializedTx with
        | Ok txDto ->
            test <@ txDto.Actions.Head = expectedTxAction @>
        | Error appErrors ->
            failwithf "%A" appErrors

    [<Fact>]
    let ``Serialization.deserializeTx CreateAccount`` () =
        let expectedTxAction =
            {
                ActionType = "CreateAccount"
                ActionData = CreateAccountTxActionDto()
            }

        let serializedTx =
            [ expectedTxAction |> box ]
            |> Helpers.newRawTxDto (BlockchainAddress "SomeAddress") 10L 0L 20m

        match Serialization.deserializeTx serializedTx with
        | Ok txDto ->
            test <@ txDto.Actions.Head.ActionType = expectedTxAction.ActionType @>
            test <@ txDto.Actions.Head.ActionData :? CreateAccountTxActionDto @>
        | Error appErrors ->
            failwithf "%A" appErrors

    [<Fact>]
    let ``Serialization.deserializeTx CreateAsset`` () =
        let expectedTxAction =
            {
                ActionType = "CreateAsset"
                ActionData = CreateAssetTxActionDto()
            }

        let serializedTx =
            [ expectedTxAction |> box ]
            |> Helpers.newRawTxDto (BlockchainAddress "SomeAddress") 10L 0L 20m

        match Serialization.deserializeTx serializedTx with
        | Ok txDto ->
            test <@ txDto.Actions.Head.ActionType = expectedTxAction.ActionType @>
            test <@ txDto.Actions.Head.ActionData :? CreateAssetTxActionDto @>
        | Error appErrors ->
            failwithf "%A" appErrors

    [<Fact>]
    let ``Serialization.deserializeTx SetAccountController`` () =
        let expectedTxAction =
            {
                ActionType = "SetAccountController"
                ActionData =
                    {
                        SetAccountControllerTxActionDto.AccountHash = "FooAccount"
                        ControllerAddress = "FooController"
                    }
            }

        let serializedTx =
            [ expectedTxAction |> box ]
            |> Helpers.newRawTxDto (BlockchainAddress "SomeAddress") 10L 0L 20m

        match Serialization.deserializeTx serializedTx with
        | Ok txDto ->
            test <@ txDto.Actions.Head = expectedTxAction @>
        | Error appErrors ->
            failwithf "%A" appErrors

    [<Fact>]
    let ``Serialization.deserializeTx SetAssetController`` () =
        let expectedTxAction =
            {
                ActionType = "SetAssetController"
                ActionData =
                    {
                        SetAssetControllerTxActionDto.AssetHash = "FooAsset"
                        ControllerAddress = "FooController"
                    }
            }

        let serializedTx =
            [ expectedTxAction |> box ]
            |> Helpers.newRawTxDto (BlockchainAddress "SomeAddress") 10L 0L 20m

        match Serialization.deserializeTx serializedTx with
        | Ok txDto ->
            test <@ txDto.Actions.Head = expectedTxAction @>
        | Error appErrors ->
            failwithf "%A" appErrors

    [<Fact>]
    let ``Serialization.deserializeTx SetAssetCode`` () =
        let expectedTxAction =
            {
                ActionType = "SetAssetCode"
                ActionData =
                    {
                        SetAssetCodeTxActionDto.AssetHash = "FooAsset"
                        AssetCode = "FooCode"
                    }
            }

        let serializedTx =
            [ expectedTxAction |> box ]
            |> Helpers.newRawTxDto (BlockchainAddress "SomeAddress") 10L 0L 20m

        match Serialization.deserializeTx serializedTx with
        | Ok txDto ->
            test <@ txDto.Actions.Head = expectedTxAction @>
        | Error appErrors ->
            failwithf "%A" appErrors

    [<Fact>]
    let ``Serialization.deserializeTx ConfigureValidator`` () =
        let expectedTxAction =
            {
                ActionType = "ConfigureValidator"
                ActionData =
                    {
                        ConfigureValidatorTxActionDto.NetworkAddress = "localhost:5000"
                        SharedRewardPercent = 0m
                        IsEnabled = true
                    }
            }

        let serializedTx =
            [ expectedTxAction |> box ]
            |> Helpers.newRawTxDto (BlockchainAddress "SomeAddress") 10L 0L 20m

        match Serialization.deserializeTx serializedTx with
        | Ok txDto ->
            test <@ txDto.Actions.Head = expectedTxAction @>
        | Error appErrors ->
            failwithf "%A" appErrors

    [<Fact>]
    let ``Serialization.deserializeTx DelegateStake`` () =
        let expectedTxAction =
            {
                ActionType = "DelegateStake"
                ActionData =
                    {
                        DelegateStakeTxActionDto.ValidatorAddress = "SomeValidator"
                        Amount = 1000m
                    }
            }

        let serializedTx =
            [ expectedTxAction |> box ]
            |> Helpers.newRawTxDto (BlockchainAddress "SomeAddress") 10L 0L 20m

        match Serialization.deserializeTx serializedTx with
        | Ok txDto ->
            test <@ txDto.Actions.Head = expectedTxAction @>
        | Error appErrors ->
            failwithf "%A" appErrors
