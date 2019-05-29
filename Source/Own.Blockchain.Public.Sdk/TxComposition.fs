namespace Own.Blockchain.Public.Sdk

open System
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open Newtonsoft.Json
open Newtonsoft.Json.Serialization
open Own.Common.FSharp
open Own.Blockchain.Common
open Own.Blockchain.Public.Core.Dtos
open Own.Blockchain.Public.Crypto

[<Extension>]
type ObjectExtensions =
    [<Extension>]
    static member ToJson(__ : obj, [<Optional; DefaultParameterValue(false)>] indented) =
        let contractResolver = new DefaultContractResolver()
        contractResolver.NamingStrategy <- new CamelCaseNamingStrategy()

        let settings = new JsonSerializerSettings()
        settings.ContractResolver <- contractResolver
        settings.Formatting <- if indented then Formatting.Indented else Formatting.None

        JsonConvert.SerializeObject(__, settings)

type SignedTx (tx, signature) =
    member val Tx : string = tx
    member val Signature : string = signature

type TxAction (actionType, actionData) =
    member val ActionType : string = actionType
    member val ActionData : obj = actionData

type Tx (senderAddress, nonce) =
    member val SenderAddress : string = senderAddress with get, set
    member val Nonce : int64 = nonce with get, set
    member val ActionFee : decimal = 0.01m with get, set
    member val ExpirationTime : int64 = 0L with get, set
    member val Actions = new ResizeArray<TxAction>()

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Actions
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    member __.AddTransferChxAction(recipientAddress, amount) =
        TxAction(
            "TransferChx",
            {
                TransferChxTxActionDto.RecipientAddress = recipientAddress
                Amount = amount
            }
        )
        |> __.Actions.Add

    member __.AddDelegateStakeAction(validatorAddress, amount) =
        TxAction(
            "DelegateStake",
            {
                DelegateStakeTxActionDto.ValidatorAddress = validatorAddress
                Amount = amount
            }
        )
        |> __.Actions.Add

    member __.AddConfigureValidatorAction(networkAddress, sharedRewardPercent, isEnabled) =
        TxAction(
            "ConfigureValidator",
            {
                ConfigureValidatorTxActionDto.NetworkAddress = networkAddress
                SharedRewardPercent = sharedRewardPercent
                IsEnabled = isEnabled
            }
        )
        |> __.Actions.Add

    member __.AddRemoveValidatorAction() =
        TxAction(
            "RemoveValidator",
            RemoveValidatorTxActionDto()
        )
        |> __.Actions.Add

    member __.AddTransferAssetAction(fromAccountHash, toAccountHash, assetHash, amount) =
        TxAction(
            "TransferAsset",
            {
                TransferAssetTxActionDto.FromAccountHash = fromAccountHash
                ToAccountHash = toAccountHash
                AssetHash = assetHash
                Amount = amount
            }
        )
        |> __.Actions.Add

    member __.AddCreateAssetEmissionAction(emissionAccountHash, assetHash, amount) =
        TxAction(
            "CreateAssetEmission",
            {
                CreateAssetEmissionTxActionDto.EmissionAccountHash = emissionAccountHash
                AssetHash = assetHash
                Amount = amount
            }
        )
        |> __.Actions.Add

    member __.AddCreateAssetAction() =
        TxAction(
            "CreateAsset",
            CreateAssetTxActionDto()
        )
        |> __.Actions.Add

    member __.AddSetAssetCodeAction(assetHash, assetCode) =
        TxAction(
            "SetAssetCode",
            {
                SetAssetCodeTxActionDto.AssetHash = assetHash
                AssetCode = assetCode
            }
        )
        |> __.Actions.Add

    member __.AddSetAssetControllerAction(assetHash, controllerAddress) =
        TxAction(
            "SetAssetController",
            {
                SetAssetControllerTxActionDto.AssetHash = assetHash
                ControllerAddress = controllerAddress
            }
        )
        |> __.Actions.Add

    member __.AddCreateAccountAction() =
        TxAction(
            "CreateAccount",
            CreateAccountTxActionDto()
        )
        |> __.Actions.Add

    member __.AddSetAccountControllerAction(accountHash, controllerAddress) =
        TxAction(
            "SetAccountController",
            {
                SetAccountControllerTxActionDto.AccountHash = accountHash
                ControllerAddress = controllerAddress
            }
        )
        |> __.Actions.Add

    member __.AddSubmitVoteAction(accountHash, assetHash, resolutionHash, voteHash) =
        TxAction(
            "SubmitVote",
            {
                SubmitVoteTxActionDto.AccountHash = accountHash
                AssetHash = assetHash
                ResolutionHash = resolutionHash
                VoteHash = voteHash
            }
        )
        |> __.Actions.Add

    member __.AddSubmitVoteWeightAction(accountHash, assetHash, resolutionHash, voteWeight) =
        TxAction(
            "SubmitVoteWeight",
            {
                SubmitVoteWeightTxActionDto.AccountHash = accountHash
                AssetHash = assetHash
                ResolutionHash = resolutionHash
                VoteWeight = voteWeight
            }
        )
        |> __.Actions.Add

    member __.AddSetAccountEligibilityAction(accountHash, assetHash, isPrimaryEligible, isSecondaryEligible) =
        TxAction(
            "SetAccountEligibility",
            {
                SetAccountEligibilityTxActionDto.AccountHash = accountHash
                AssetHash = assetHash
                IsPrimaryEligible = isPrimaryEligible
                IsSecondaryEligible = isSecondaryEligible
            }
        )
        |> __.Actions.Add

    member __.AddSetAssetEligibilityAction(assetHash, isEligibilityRequired) =
        TxAction(
            "SetAssetEligibility",
            {
                SetAssetEligibilityTxActionDto.AssetHash = assetHash
                IsEligibilityRequired = isEligibilityRequired
            }
        )
        |> __.Actions.Add

    member __.AddChangeKycControllerAddressAction(accountHash, assetHash, kycControllerAddress) =
        TxAction(
            "ChangeKycControllerAddress",
            {
                ChangeKycControllerAddressTxActionDto.AccountHash = accountHash
                AssetHash = assetHash
                KycControllerAddress = kycControllerAddress
            }
        )
        |> __.Actions.Add

    member __.AddAddKycProviderAction(assetHash, providerAddress) =
        TxAction(
            "AddKycProvider",
            {
                AddKycProviderTxActionDto.AssetHash = assetHash
                ProviderAddress = providerAddress
            }
        )
        |> __.Actions.Add

    member __.AddRemoveKycProviderAction(assetHash, providerAddress) =
        TxAction(
            "RemoveKycProvider",
            {
                RemoveKycProviderTxActionDto.AssetHash = assetHash
                ProviderAddress = providerAddress
            }
        )
        |> __.Actions.Add

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Signing
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    member __.Sign(networkCode, privateKey) =
        let getNetworkId () = networkCode |> Hashing.networkId
        let privateKey = privateKey |> Own.Blockchain.Public.Core.DomainTypes.PrivateKey
        let rawTx = __.ToJson() |> Conversion.stringToBytes

        let signature =
            rawTx
            |> Hashing.hash
            |> Signing.signHash getNetworkId privateKey

        let encodedTx = rawTx |> Convert.ToBase64String

        SignedTx(encodedTx, signature.Value)
