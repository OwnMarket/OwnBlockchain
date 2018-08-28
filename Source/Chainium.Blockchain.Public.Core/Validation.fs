namespace Chainium.Blockchain.Public.Core

open Chainium.Common
open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Core.Dtos

module Validation =

    let validateSignature (signature : Signature) =
        [
            if signature.V.IsNullOrWhiteSpace() then
                yield AppError "Signature component V is missing from the envelope."
            if signature.R.IsNullOrWhiteSpace() then
                yield AppError "Signature component R is missing from the envelope."
            if signature.S.IsNullOrWhiteSpace() then
                yield AppError "Signature component S is missing from the envelope."
        ]

    let validateTxEnvelope (txEnvelopeDto : TxEnvelopeDto) : Result<TxEnvelope, AppErrors> =
        let signature =
            {
                V = txEnvelopeDto.V
                R = txEnvelopeDto.R
                S = txEnvelopeDto.S
            }
        [
            if txEnvelopeDto.Tx.IsNullOrWhiteSpace() then
                yield AppError "Tx is missing from the envelope."

            yield! validateSignature signature
        ]
        |> Errors.orElseWith (fun _ -> Mapping.txEnvelopeFromDto txEnvelopeDto)

    let validateBlockEnvelope (blockEnvelopeDto : BlockEnvelopeDto) : Result<BlockEnvelope, AppErrors> =
        let signature =
            {
                V = blockEnvelopeDto.V
                R = blockEnvelopeDto.R
                S = blockEnvelopeDto.S
            }
        [
            if blockEnvelopeDto.Block.IsNullOrWhiteSpace() then
                yield AppError "Block is missing from the envelope."

            yield! validateSignature signature
        ]
        |> Errors.orElseWith (fun _ -> Mapping.blockEnvelopeFromDto blockEnvelopeDto)

    let verifyTxSignature verifySignature (txEnvelope : TxEnvelope) : Result<ChainiumAddress, AppErrors> =
        match verifySignature txEnvelope.Signature txEnvelope.RawTx with
        | Some chainiumAddress ->
            Ok chainiumAddress
        | None ->
            Result.appError "Cannot verify signature"

    let verifyBlockSignature verifySignature (blockEnvelope : BlockEnvelope) : Result<ChainiumAddress, AppErrors> =
        match verifySignature blockEnvelope.Signature blockEnvelope.RawBlock with
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
                yield AppError "RecipientAddress is not provided."
            elif action.RecipientAddress |> ChainiumAddress |> isValidAddress |> not then
                yield AppError "RecipientAddress is not valid."

            if action.Amount <= 0m then
                yield AppError "CHX amount must be larger than zero."
        ]

    let private validateTransferAsset (action : TransferAssetTxActionDto) =
        [
            if action.FromAccount.IsNullOrWhiteSpace() then
                yield AppError "FromAccount value is not provided."

            if action.ToAccount.IsNullOrWhiteSpace() then
                yield AppError "ToAccount value is not provided."

            if action.AssetHash.IsNullOrWhiteSpace() then
                yield AppError "AssetHash is not provided."

            if action.Amount <= 0m then
                yield AppError "Asset amount must be larger than zero."
        ]

    let private validateCreateAssetEmission (action : CreateAssetEmissionTxActionDto) =
        [
            if action.EmissionAccountHash.IsNullOrWhiteSpace() then
                yield AppError "EmissionAccountHash value is not provided."

            if action.AssetHash.IsNullOrWhiteSpace() then
                yield AppError "AssetHash is not provided."

            if action.Amount <= 0m then
                yield AppError "Asset amount must be larger than zero."
        ]

    let private validateSetAccountController isValidAddress (action : SetAccountControllerTxActionDto) =
        [
            if action.AccountHash.IsNullOrWhiteSpace() then
                yield AppError "AccountHash is not provided."

            if action.ControllerAddress.IsNullOrWhiteSpace() then
                yield AppError "ControllerAddress is not provided."
            elif action.ControllerAddress |> ChainiumAddress |> isValidAddress |> not then
                yield AppError "ControllerAddress is not valid."
        ]

    let private validateSetAssetController isValidAddress (action : SetAssetControllerTxActionDto) =
        [
            if action.AssetHash.IsNullOrWhiteSpace() then
                yield AppError "AssetHash is not provided."

            if action.ControllerAddress.IsNullOrWhiteSpace() then
                yield AppError "ControllerAddress is not provided."
            elif action.ControllerAddress |> ChainiumAddress |> isValidAddress |> not then
                yield AppError "ControllerAddress is not valid."
        ]

    let private validateSetAssetCode (action : SetAssetCodeTxActionDto) =
        [
            if action.AssetHash.IsNullOrWhiteSpace() then
                yield AppError "AssetHash is not provided."

            if action.AssetCode.IsNullOrWhiteSpace() then
                yield AppError "AssetCode is not provided."
        ]

    let private validateSetValidatorNetworkAddress (action : SetValidatorNetworkAddressTxActionDto) =
        [
            if action.NetworkAddress.IsNullOrWhiteSpace() then
                yield AppError "NetworkAddress is not provided."
        ]

    let private validateDelegateStake isValidAddress (action : DelegateStakeTxActionDto) =
        [
            if action.ValidatorAddress.IsNullOrWhiteSpace() then
                yield AppError "ValidatorAddress is not provided."
            elif action.ValidatorAddress |> ChainiumAddress |> isValidAddress |> not then
                yield AppError "ValidatorAddress is not valid."

            if action.Amount < 0m then
                yield AppError "CHX amount must not be negative."
        ]

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Tx validation
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let private validateTxFields (ChxAmount minTxActionFee) (ChainiumAddress signerAddress) (t : TxDto) =
        [
            if t.SenderAddress <> signerAddress then
                yield AppError "Sender address doesn't match the signature."
            if t.Nonce <= 0L then
                yield AppError "Nonce must be positive."
            if t.Fee <= 0m then
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
            | :? CreateAccountTxActionDto ->
                [] // Nothing to validate.
            | :? CreateAssetTxActionDto ->
                [] // Nothing to validate.
            | :? SetAccountControllerTxActionDto as a ->
                validateSetAccountController isValidAddress a
            | :? SetAssetControllerTxActionDto as a ->
                validateSetAssetController isValidAddress a
            | :? SetAssetCodeTxActionDto as a ->
                validateSetAssetCode a
            | :? SetValidatorNetworkAddressTxActionDto as a ->
                validateSetValidatorNetworkAddress a
            | :? DelegateStakeTxActionDto as a ->
                validateDelegateStake isValidAddress a
            | _ ->
                let error = sprintf "Unknown action data type: %s" (action.ActionData.GetType()).FullName
                [AppError error]

        actions
        |> List.collect validateTxAction

    let validateTx isValidAddress minTxActionFee sender hash (txDto : TxDto) : Result<Tx, AppErrors> =
        validateTxFields minTxActionFee sender txDto
        @ validateTxActions isValidAddress txDto.Actions
        |> Errors.orElseWith (fun _ -> Mapping.txFromDto sender hash txDto)

    let checkIfBalanceCanCoverFees
        (getAvailableBalance : ChainiumAddress -> ChxAmount)
        getTotalFeeForPendingTxs
        senderAddress
        txFee
        : Result<unit, AppErrors>
        =

        let availableBalance = getAvailableBalance senderAddress

        if txFee > availableBalance then
            Result.appError "Available CHX balance is insufficient to cover the fee."
        else
            let totalFeeForPendingTxs = getTotalFeeForPendingTxs senderAddress

            if (totalFeeForPendingTxs + txFee) > availableBalance then
                Result.appError "Available CHX balance is insufficient to cover the fee for all pending transactions."
            else
                Ok ()
