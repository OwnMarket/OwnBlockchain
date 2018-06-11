namespace Chainium.Blockchain.Public.Core.Tests

open Xunit
open Swensen.Unquote
open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Core
open Chainium.Blockchain.Public.Core.Dtos

module SerializationTests =

    [<Fact>]
    let ``Serialization.deserializeTx transaction`` () =
        let expectedTx =
            {
                Nonce = 10L
                Fee = 20M
                Actions =
                    [
                        {
                            ActionType = "ChxTransfer"
                            ActionData =
                                {
                                    RecipientAddress = "Recipient"
                                    Amount = 20M
                                }
                        }
                        {
                            ActionType = "AssetTransfer"
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

        // Make a call to Newtonsoft to get JSON format.
        let resultTx =
            expectedTx
            |> Serialization.serializeTx

        match resultTx with
        | Ok rawTx ->
            let actualTxRes = Serialization.deserializeTx rawTx
            match actualTxRes with
            | Ok actualTx -> test <@ expectedTx = actualTx @>
            | Error e-> failwithf "%A" e
        | Error errors ->
            failwithf "%A" errors

    [<Fact>]
    let ``Serialization.deserializeTx unknown action type added`` () =
        let json =
            """
            {
                "Nonce": 120,
                "Fee": 20,
                "Actions": [
                    {
                        "ActionType": "ChxTransfer",
                        "ActionData": {
                            "RecipientAddress": "Recipient",
                            "Amount": 20.0
                        }
                    },
                    {
                        "ActionType": "AssetTransfer",
                        "ActionData": {
                            "FromAccount": "A",
                            "ToAccount": "B",
                            "AssetCode": "asset",
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
                |> List.filter(fun a -> a.ActionData.GetType() = transType)
                |> List.length

            let chxActions = numOfActionsByType typeof<ChxTransferTxActionDto>
            test <@ chxActions = 1 @>

            let assetActions = numOfActionsByType typeof<AssetTransferTxActionDto>
            test <@ assetActions = 1 @>

            let invalidActions = numOfActionsByType typeof<string>
            test <@ invalidActions = 1 @>
        | Error appErrors ->
            failwithf "%A" appErrors

    [<Fact>]
    let ``Serialization.deserializeTx invalid json for known action`` () =
        let json =
            """
            {
                "Nonce": 120,
                "Fee": 20,
                "Actions": [
                    {
                        "ActionType": "ChxTransfer",
                        "ActionData": {
                            "Recipient_Address": "Recipient",
                            "_Amount": 20.0
                        }
                    },
                    {
                        "ActionType": "AssetTransfer",
                        "ActionData": {
                            "FromAccount": "A",
                            "ToAccount": "B",
                            "AssetCode": "asset",
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
                |> List.filter(fun a -> a.ActionData.GetType() = transType)
                |> List.length

            let chxActions = numOfActionsByType typeof<ChxTransferTxActionDto>
            test <@ chxActions = 1 @>

            let assetActions = numOfActionsByType typeof<AssetTransferTxActionDto>
            test <@ assetActions = 1 @>

            let invalidActions = numOfActionsByType typeof<string>
            test <@ invalidActions = 1 @>
        | Error appErrors ->
            failwithf "%A" appErrors

    [<Fact>]
    let ``Serialization.deserializeTx invalid json`` () =
        let json =
            """
            {
                "Nonce":"InvaliValue",
                "Fee": 20,
                "Actions":
                    {
                        "ActionType": "AssetTransfer",
                        "ActionData": {
                            "FromAccount": "A",
                            "ToAccount": "B",
                            "AssetCode": "asset",
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
    let ``Serialization.deserializeTx.accountControllerChange`` ()=
        let expectedTransaction =
            {
                ActionType = "AccountControllerChange"
                ActionData =
                    {
                        AccountControllerChangeTxActionDto.AccountHash = "FooAccount"
                        ControllerAddress = "FooController"
                    }
            }

        let serializedTx =
            [ expectedTransaction |> box ]
            |> Helpers.newTxDto 10L 20M

        match Serialization.deserializeTx serializedTx with
        | Ok r ->
            let actual = r.Actions.Head
            test <@ actual = expectedTransaction @>
        | Error appErrors ->
            failwithf "%A" appErrors
