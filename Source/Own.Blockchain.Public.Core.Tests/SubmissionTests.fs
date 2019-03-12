namespace Own.Blockchain.Public.Core.Tests

open Xunit
open Swensen.Unquote
open Own.Common.FSharp
open Own.Blockchain.Common
open Own.Blockchain.Public.Core
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Core.Dtos
open Own.Blockchain.Public.Crypto

module SubmissionTests =

    [<Theory>]
    [<InlineData(1, "Available CHX balance is insufficient to cover the fee.")>]
    [<InlineData(10, "Available CHX balance is insufficient to cover the fee for all pending transactions.")>]
    let ``Workflows.submitTx fails on insufficient CHX balance to cover Tx fee`` (balance : decimal, error : string) =
        // ARRANGE
        let senderWallet = Signing.generateWallet ()
        let recipientWallet = Signing.generateWallet ()
        let nonce = Nonce 5L
        let actionFee = ChxAmount 1m
        let totalTxFee = actionFee * 2m // Two actions
        let totalPendingTxsFee = ChxAmount 9m
        let senderBalance = ChxAmount balance

        let txHash, txEnvelopeDto =
            [
                {
                    ActionType = "TransferChx"
                    ActionData =
                        {
                            RecipientAddress = recipientWallet.Address.Value
                            Amount = 10m
                        }
                } :> obj
                {
                    ActionType = "TransferChx"
                    ActionData =
                        {
                            RecipientAddress = recipientWallet.Address.Value
                            Amount = 10m
                        }
                } :> obj
            ]
            |> Helpers.newTx senderWallet nonce actionFee

        let expectedResult : Result<TxHash, AppErrors> = Error [AppError error]

        // COMPOSE
        let getAvailableChxBalance =
            let data =
                [
                    senderWallet.Address, senderBalance
                ]
                |> Map.ofSeq

            fun (address : BlockchainAddress) ->
                data.[address]

        let getTotalFeeForPendingTxs _ =
            totalPendingTxsFee

        let saveTx _ =
            failwith "saveTx shouldn't be called"

        let saveTxToDb _ =
            failwith "saveTxToDb shouldn't be called"

        // ACT
        let result =
            Workflows.submitTx
                Helpers.verifySignature
                Hashing.decode
                Hashing.isValidBlockchainAddress
                Hashing.hash
                getAvailableChxBalance
                getTotalFeeForPendingTxs
                saveTx
                saveTxToDb
                Helpers.maxActionCountPerTx
                Helpers.minTxActionFee
                false
                txEnvelopeDto

        // ASSERT
        test <@ result = expectedResult @>

    [<Theory>]
    [<InlineData(1)>]
    [<InlineData(4)>]
    let ``Workflows.submitTx fails if action count is greater than MaxActionCountPerTx`` maxActionCountPerTx =
        // ARRANGE
        let senderWallet = Signing.generateWallet ()
        let recipientWallet = Signing.generateWallet ()
        let nonce = Nonce 5L
        let actionFee = ChxAmount 1m
        let senderBalance = ChxAmount 100m

        let txHash, txEnvelopeDto =
            [
                {
                    ActionType = "CreateAccount"
                    ActionData = new CreateAccountTxActionDto ()
                } :> obj
                {
                    ActionType = "CreateAccount"
                    ActionData = new CreateAccountTxActionDto ()
                } :> obj
                {
                    ActionType = "CreateAccount"
                    ActionData = new CreateAccountTxActionDto ()
                } :> obj
                {
                    ActionType = "CreateAccount"
                    ActionData = new CreateAccountTxActionDto ()
                } :> obj
                {
                    ActionType = "CreateAccount"
                    ActionData = new CreateAccountTxActionDto ()
                } :> obj
            ]
            |> Helpers.newTx senderWallet nonce actionFee

        let expectedError = sprintf "Max allowed number of actions per transaction is %i." maxActionCountPerTx
        let expectedResult : Result<TxHash, AppErrors> = Error [AppError expectedError]

        // COMPOSE
        let getAvailableChxBalance _ =
            senderBalance

        let getTotalFeeForPendingTxs _ =
            ChxAmount 0m

        let saveTx _ =
            failwith "saveTx shouldn't be called"

        let saveTxToDb _ =
            failwith "saveTxToDb shouldn't be called"

        // ACT
        let result =
            Workflows.submitTx
                Helpers.verifySignature
                Hashing.decode
                Hashing.isValidBlockchainAddress
                Hashing.hash
                getAvailableChxBalance
                getTotalFeeForPendingTxs
                saveTx
                saveTxToDb
                maxActionCountPerTx
                Helpers.minTxActionFee
                false
                txEnvelopeDto

        // ASSERT
        test <@ result = expectedResult @>
