namespace Chainium.Blockchain.Public.Core

open Chainium.Common
open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Core.Dtos

module Validation =

    let validateTxEnvelope (txEnvelopeDto : TxEnvelopeDto) : Result<TxEnvelope, AppErrors> =
        [
            if txEnvelopeDto.Tx.IsNullOrWhiteSpace() then
                yield AppError "Tx is missing from the envelope."
            if txEnvelopeDto.V.IsNullOrWhiteSpace() then
                yield AppError "Signature component V is missing from the envelope."
            if txEnvelopeDto.R.IsNullOrWhiteSpace() then
                yield AppError "Signature component R is missing from the envelope."
            if txEnvelopeDto.S.IsNullOrWhiteSpace() then
                yield AppError "Signature component S is missing from the envelope."
        ]
        |> Errors.orElseWith (fun _ -> Mapping.txEnvelopeFromDto txEnvelopeDto)

    let verifyTxSignature verifySignature (txEnvelope : TxEnvelope) : Result<ChainiumAddress, AppErrors> =
        match verifySignature txEnvelope.Signature txEnvelope.RawTx with
        | Some chainiumAddress ->
            Ok chainiumAddress
        | None ->
            Result.appError "Cannot verify signature"

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // TxAction validation
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let private validateTransferChx isValidAddress (action : TransferChxTxActionDto) =
        [
            if action.RecipientAddress.IsNullOrWhiteSpace() then
                yield AppError "Recipient address is not present."

            if action.RecipientAddress |> ChainiumAddress |> isValidAddress |> not then
                yield AppError "Recipient address is not valid."

            if action.Amount <= 0M then
                yield AppError "Chx transfer amount must be larger than zero."
        ]

    let private validateTransferAsset (action : TransferAssetTxActionDto) =
        [
            if action.FromAccount.IsNullOrWhiteSpace() then
                yield AppError "FromAccount value is not valid."

            if action.ToAccount.IsNullOrWhiteSpace() then
                yield AppError "ToAccount value is not valid."

            if action.AssetHash.IsNullOrWhiteSpace() then
                yield AppError "Asset hash is not valid."

            if action.Amount <= 0M then
                yield AppError "Asset amount must be larger than zero."
        ]

    let private validateCreateAssetEmission (action : CreateAssetEmissionTxActionDto) =
        [
            if action.EmissionAccountHash.IsNullOrWhiteSpace() then
                yield AppError "Emission account hash value is not valid."

            if action.AssetHash.IsNullOrWhiteSpace() then
                yield AppError "Asset hash is not valid."

            if action.Amount <= 0M then
                yield AppError "Asset amount must be larger than zero."
        ]

    let private validateSetAccountController isValidAddress (action : SetAccountControllerTxActionDto) =
        [
            if action.AccountHash.IsNullOrWhiteSpace() then
                yield AppError "Account hash is not valid."

            if action.ControllerAddress |> ChainiumAddress |> isValidAddress |> not then
                yield AppError "Controller address is not valid."
        ]

    let private validateSetAssetController isValidAddress (action : SetAssetControllerTxActionDto) =
        [
            if action.AssetHash.IsNullOrWhiteSpace() then
                yield AppError "Asset hash is not valid."

            if action.ControllerAddress |> ChainiumAddress |> isValidAddress |> not then
                yield AppError "Controller address is not valid."
        ]

    let private validateSetAssetCode (action : SetAssetCodeTxActionDto) =
        [
            if action.AssetHash.IsNullOrWhiteSpace() then
                yield AppError "Asset hash is not valid."

            if action.AssetCode.IsNullOrWhiteSpace() then
                yield AppError "Asset code is not valid."
        ]

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Tx validation
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let private validateTxFields (ChxAmount minTxActionFee) (t : TxDto) =
        [
            if t.Nonce <= 0L then
                yield AppError "Nonce must be positive."
            if t.Fee <= 0M then
                yield AppError "Fee must be positive."
            if t.Fee < minTxActionFee then
                yield AppError "Fee is too low."
            if t.Actions |> List.isEmpty then
                yield AppError "There are no actions provided for this transaction."
        ]

    let private validateTxActions isValidAddress (actions : TxActionDto list) =
        let validateTxAction (action : TxActionDto) =
            match action.ActionData with
            | :? TransferChxTxActionDto as a ->
                validateTransferChx isValidAddress a
            | :? TransferAssetTxActionDto as a ->
                validateTransferAsset a
            | :? CreateAssetEmissionTxActionDto as a ->
                validateCreateAssetEmission a
            | :? SetAccountControllerTxActionDto as a ->
                validateSetAccountController isValidAddress a
            | :? SetAssetControllerTxActionDto as a ->
                validateSetAssetController isValidAddress a
            | :? SetAssetCodeTxActionDto as a ->
                validateSetAssetCode a
            | _ ->
                let error = sprintf "Unknown action data type: %s" (action.ActionData.GetType()).FullName
                [AppError error]

        actions
        |> List.collect validateTxAction

    let validateTx isValidAddress minTxActionFee sender hash (txDto : TxDto) : Result<Tx, AppErrors> =
        validateTxFields minTxActionFee txDto @ validateTxActions isValidAddress txDto.Actions
        |> Errors.orElseWith (fun _ -> Mapping.txFromDto sender hash txDto)

    let checkIfBalanceCanCoverFees
        getChxBalanceState
        getTotalFeeForPendingTxs
        senderAddress
        txFee
        : Result<unit, AppErrors>
        =

        let chxBalance =
            senderAddress
            |> getChxBalanceState
            |> Option.map (Mapping.chxBalanceStateFromDto >> fun state -> state.Amount)
            |? ChxAmount 0M

        if txFee > chxBalance then
            Result.appError "CHX balance is insufficient to cover the fee."
        else
            let totalFeeForPendingTxs = getTotalFeeForPendingTxs senderAddress

            if (totalFeeForPendingTxs + txFee) > chxBalance then
                Result.appError "CHX balance is insufficient to cover the fee for all pending transactions."
            else
                Ok ()
