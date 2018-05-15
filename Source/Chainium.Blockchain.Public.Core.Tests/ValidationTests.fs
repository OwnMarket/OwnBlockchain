
namespace Chainium.Blockchain.Public.Core.Tests

open System
open Xunit
open Swensen.Unquote
open Chainium.Common
open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Core
open Chainium.Blockchain.Public.Core.Dtos
open Chainium.Blockchain.Public.Core.DomainTypes

module ValidationTests =
    let chAddress = ChainiumAddress "ch2Wt6j7sbaqbgKphYx9U95wZDX99L"
    let txHash = TxHash "SampleHash"
    let chxTransfer = "ChxTransfer"
    let equityTransfer = "EquityTransfer"

    [<Fact>]
    let ``Validation.validateTx.basicValidation single validation error`` () =
        let testTx = {
            Nonce = -10L
            Fee = 20M
            Actions =
                [
                    {
                        ActionType = chxTransfer
                        ActionData =
                            {
                                ChxTransferTxActionDto.RecipientAddress = "Recipient"
                                Amount = 20M
                            }
                    }
                    {
                        ActionType = equityTransfer
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


        let expMessage = AppError "Nonce cannot be negative number.";
        let result = Validation.validateTx chAddress txHash testTx

        match result with
        | Ok t -> failwith "Validation should fail in case of this test."
        | Error errors ->
            test <@ errors.Length = 1 @>
            test <@ errors.[0] = expMessage @>

    [<Fact>]
    let ``Validation.validateTx.basicValidation multiple validation errors`` () =
        let testTx = {
            Nonce = -10L
            Fee = 0M
            Actions =
                [
                    {
                        ActionType = chxTransfer
                        ActionData =
                            {
                                ChxTransferTxActionDto.RecipientAddress = "Recipient"
                                Amount = 20M
                            }
                    }
                    {
                        ActionType = equityTransfer
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

        let result = Validation.validateTx chAddress txHash testTx

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

        let result = Validation.validateTx chAddress txHash testTx

        match result with
        | Ok t -> failwith "Validation should fail in case of this test."
        | Error errors ->
            test <@ errors.Length = 1 @>

    [<Fact>]
    let ``Validation.validateTx.chxTransfer invalid Amount`` () =
        let testTx = {
            Nonce = 10L
            Fee = 1M
            Actions =
                [
                    {
                        ActionType = chxTransfer
                        ActionData =
                            {
                                ChxTransferTxActionDto.RecipientAddress = "Recipient"
                                Amount = 0M
                            }
                    }
                ]
        }

        let result = Validation.validateTx chAddress txHash testTx

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

        let result = Validation.validateTx chAddress txHash testTx

        match result with
        | Ok t -> failwith "Validation should fail in case of this test."
        | Error errors ->
            test <@ errors.Length = 1 @>

    [<Fact>]
    let ``Validation.validateTx.equityTransfer invalid FromAccount`` () =
        let testTx = {
            Nonce = 10L
            Fee = 1M
            Actions =
                [
                   {
                        ActionType = equityTransfer
                        ActionData =
                            {
                                FromAccount = ""
                                ToAccount = "B"
                                Equity= "equity"
                                Amount = 12M
                            }
                    }
                ]
        }

        let result = Validation.validateTx chAddress txHash testTx

        match result with
        | Ok t -> failwith "Validation should fail in case of this test."
        | Error errors ->
            test <@ errors.Length = 1 @>

    [<Fact>]
    let ``Validation.validateTx.equityTransfer invalid ToAccount`` () =
        let testTx = {
            Nonce = 10L
            Fee = 1M
            Actions =
                [
                   {
                        ActionType = equityTransfer
                        ActionData =
                            {
                                FromAccount = "A"
                                ToAccount = ""
                                Equity= "equity"
                                Amount = 12M
                            }
                    }
                ]
        }

        let result = Validation.validateTx chAddress txHash testTx

        match result with
        | Ok t -> failwith "Validation should fail in case of this test."
        | Error errors ->
            test <@ errors.Length = 1 @>

    [<Fact>]
    let ``Validation.validateTx.equityTransfer invalid Equity`` () =
        let testTx = {
            Nonce = 10L
            Fee = 1M
            Actions =
                [
                   {
                        ActionType = equityTransfer
                        ActionData =
                            {
                                FromAccount = "A"
                                ToAccount = "B"
                                Equity= ""
                                Amount = 12M
                            }
                    }
                ]
        }

        let result = Validation.validateTx chAddress txHash testTx

        match result with
        | Ok t -> failwith "Validation should fail in case of this test."
        | Error errors ->
            test <@ errors.Length = 1 @>

    [<Fact>]
    let ``Validation.validateTx.equityTransfer invalid Amount`` () =
        let testTx = {
            Nonce = 10L
            Fee = 1M
            Actions =
                [
                   {
                        ActionType = equityTransfer
                        ActionData =
                            {
                                FromAccount = "A"
                                ToAccount = "B"
                                Equity= "equity"
                                Amount = 0M
                            }
                    }
                ]
        }

        let result = Validation.validateTx chAddress txHash testTx

        match result with
        | Ok t -> failwith "Validation should fail in case of this test."
        | Error errors ->
            test <@ errors.Length = 1 @>

    let getTx<'T> = function
        | ChxTransfer chx -> box chx :?> 'T
        | EquityTransfer eq -> box eq :?> 'T

    [<Fact>]
    let ``Validation.validateTx validate action`` () =
        let testTx = {
            Nonce = 10L
            Fee = 1M
            Actions =
                [
                    {
                        ActionType = chxTransfer
                        ActionData =
                            {
                                ChxTransferTxActionDto.RecipientAddress = "Recipient"
                                Amount = 10M
                            }
                    }
                    {
                        ActionType = equityTransfer
                        ActionData =
                            {
                                FromAccount = "A"
                                ToAccount = "B"
                                Equity= "equity"
                                Amount = 1M
                            }
                    }
                ]
        }

        let result = Validation.validateTx chAddress txHash testTx

        match result with
        | Ok t ->
           let expectedChx = (testTx.Actions.[0].ActionData :?> ChxTransferTxActionDto)
           let actualChx = t.Actions.[0] |> getTx<ChxTransferTxAction>

           let expEq = (testTx.Actions.[1].ActionData :?> EquityTransferTxActionDto)
           let actualEq = t.Actions.[1] |> getTx<EquityTransferTxAction>

           test
                <@
                    t.Fee = ChxAmount testTx.Fee
                    && t.Nonce = testTx.Nonce
                    && t.TxHash = txHash
                    && t.Sender = chAddress
                    && actualChx.Amount = ChxAmount expectedChx.Amount
                    && actualChx.RecipientAddress = ChainiumAddress expectedChx.RecipientAddress
                    && actualEq.FromAccountHash = AccountHash expEq.FromAccount
                    && actualEq.ToAccountHash = AccountHash expEq.ToAccount
                    && actualEq.EquityID = EquityID expEq.Equity
                    && actualEq.Amount = EquityAmount expEq.Amount
                @>
        | Error errors ->
            failwithf "%A" errors
