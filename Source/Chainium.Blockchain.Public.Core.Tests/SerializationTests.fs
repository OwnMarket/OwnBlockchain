
namespace Chainium.Blockchain.Public.Core.Tests

open System
open Xunit
open Swensen.Unquote
open Chainium.Common
open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Core
open Chainium.Blockchain.Public.Core.Dtos

module SerializationTests=
    [<Fact>]
    let ``Serialization.deserializeTx transaction`` () =
        let expectedTx = {
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
                            ActionType = "EquityTransfer"
                            ActionData =
                                {
                                    FromAccount = "A"
                                    ToAccount = "B"
                                    Equity= "equity"
                                    Amount = 12M
                                }
                        }
                    ]
            }

        //make a call to newtonsoft to get json format
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
    let ``Serialization.deserializeTx unknown transaction type added`` () =
        let json =
            """
            {
              "Nonce": 120,
              "Fee": 20,
              "Actions": [{
                  "ActionType": "ChxTransfer",
                  "ActionData": {
                      "RecipientAddress": "Recipient",
                      "Amount": 20.0
                  }
              },
              {
                  "ActionType": "EquityTransfer",
                  "ActionData": {
                      "FromAccount": "A",
                      "ToAccount": "B",
                      "Equity": "equity",
                      "Amount": 12.0
                  }
              },
              {
                  "ActionType": "Unknown",
                  "ActionData": "Test"
              }]
          }
          """

        let result =
            json
            |> Serialization.stringToBytes
            |> Serialization.deserializeTx

        match result with
        | Ok tx ->
            test <@ tx.Actions.Length = 3 @>


            let numOfActionsByType transType =
                tx.Actions
                |> List.filter(fun a -> a.ActionData.GetType() = transType)
                |> List.length

            let chxTransactions = numOfActionsByType typeof<ChxTransferTxActionDto>
            test <@ chxTransactions = 1 @>

            let equityTransactions = numOfActionsByType typeof<EquityTransferTxActionDto>
            test <@ equityTransactions = 1 @>

            let invalidTransactions = numOfActionsByType typeof<string>
            test <@ invalidTransactions = 1 @>
        | Error appErrors ->
            failwithf "%A" appErrors

    [<Fact>]
    let ``Serialization.deserializeTx invalid json for known transaction`` () =
        let json =
            """
            {
                "Nonce": 120,
                "Fee": 20,
                "Actions": [{
                    "ActionType": "ChxTransfer",
                    "ActionData": {
                        "Recipient_Address": "Recipient",
                        "_Amount": 20.0
                    }
                },
                {
                    "ActionType": "EquityTransfer",
                    "ActionData": {
                        "FromAccount": "A",
                        "ToAccount": "B",
                        "Equity": "equity",
                        "Amount": 12.0
                    }
                },
                {
                    "ActionType": "Unknown",
                    "ActionData": "Test"
                }]
            }
            """

        let result =
            json
            |> Serialization.stringToBytes
            |> Serialization.deserializeTx

        match result with
        | Ok tx ->
            test <@ tx.Actions.Length = 3 @>


            let numOfActionsByType transType =
                tx.Actions
                |> List.filter(fun a -> a.ActionData.GetType() = transType)
                |> List.length

            let chxTransactions = numOfActionsByType typeof<ChxTransferTxActionDto>
            test <@ chxTransactions = 1 @>

            let equityTransactions = numOfActionsByType typeof<EquityTransferTxActionDto>
            test <@ equityTransactions = 1 @>

            let invalidTransactions = numOfActionsByType typeof<string>
            test <@ invalidTransactions = 1 @>
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
                            "ActionType": "EquityTransfer",
                            "ActionData": {
                                "FromAccount": "A",
                                "ToAccount": "B",
                                "Equity": "equity",
                                "Amount": 12.0
                            }
                        },
                        {
                            "ActionType": "Unknown",
                            "ActionData": "Test"
                        }]
                    }
             """

        let result =
            json
            |> Serialization.stringToBytes
            |> Serialization.deserializeTx

        match result with
        | Ok tx ->
            failwith "This serialization attempt should fail"
        | Error appErrors ->
            test <@ appErrors.Length > 0 @>
