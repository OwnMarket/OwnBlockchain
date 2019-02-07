namespace Own.Blockchain.Public.Core

open System
open System.Collections.Concurrent
open Own.Common
open Own.Blockchain.Common
open Own.Blockchain.Public.Core
open Own.Blockchain.Public.Core.DomainTypes

module Processing =

    type ProcessingState
        (
        getChxBalanceStateFromStorage : BlockchainAddress -> ChxBalanceState option,
        getHoldingStateFromStorage : AccountHash * AssetHash -> HoldingState option,
        getVoteStateFromStorage : VoteId -> VoteState option,
        getEligibilityStateFromStorage : AccountHash * AssetHash -> EligibilityState option,
        getKycProvidersFromStorage : AssetHash -> BlockchainAddress list,
        getAccountStateFromStorage : AccountHash -> AccountState option,
        getAssetStateFromStorage : AssetHash -> AssetState option,
        getAssetHashByCodeFromStorage : AssetCode -> AssetHash option,
        getValidatorStateFromStorage : BlockchainAddress -> ValidatorState option,
        getStakeStateFromStorage : BlockchainAddress * BlockchainAddress -> StakeState option,
        getTotalChxStakedFromStorage : BlockchainAddress -> ChxAmount,
        txResults : ConcurrentDictionary<TxHash, TxResult>,
        equivocationProofResults : ConcurrentDictionary<EquivocationProofHash, EquivocationProofResult>,
        chxBalances : ConcurrentDictionary<BlockchainAddress, ChxBalanceState>,
        holdings : ConcurrentDictionary<AccountHash * AssetHash, HoldingState>,
        votes : ConcurrentDictionary<VoteId, VoteState option>,
        eligibilities : ConcurrentDictionary<AccountHash * AssetHash, EligibilityState option>,
        kycProviders :
            ConcurrentDictionary<AssetHash, ConcurrentDictionary<BlockchainAddress, KycProviderChange option>>,
        accounts : ConcurrentDictionary<AccountHash, AccountState option>,
        assets : ConcurrentDictionary<AssetHash, AssetState option>,
        assetHashesByCode : ConcurrentDictionary<AssetCode, AssetHash option>,
        validators : ConcurrentDictionary<BlockchainAddress, ValidatorState option>,
        stakes : ConcurrentDictionary<BlockchainAddress * BlockchainAddress, StakeState option>,
        totalChxStaked : ConcurrentDictionary<BlockchainAddress, ChxAmount>, // Not part of the blockchain state
        stakingRewards : ConcurrentDictionary<BlockchainAddress, ChxAmount>,
        collectedReward : ChxAmount
        ) =

        let getChxBalanceState address =
            getChxBalanceStateFromStorage address
            |? {Amount = ChxAmount 0m; Nonce = Nonce 0L}
        let getHoldingState (accountHash, assetHash) =
            getHoldingStateFromStorage (accountHash, assetHash)
            |? {Amount = AssetAmount 0m; IsEmission = false}

        new
            (
            getChxBalanceStateFromStorage : BlockchainAddress -> ChxBalanceState option,
            getHoldingStateFromStorage : AccountHash * AssetHash -> HoldingState option,
            getVoteStateFromStorage : VoteId -> VoteState option,
            getEligibilityStateFromStorage : AccountHash * AssetHash -> EligibilityState option,
            getKycProvidersFromStorage : AssetHash -> BlockchainAddress list,
            getAccountStateFromStorage : AccountHash -> AccountState option,
            getAssetStateFromStorage : AssetHash -> AssetState option,
            getAssetHashByCodeFromStorage : AssetCode -> AssetHash option,
            getValidatorStateFromStorage : BlockchainAddress -> ValidatorState option,
            getStakeStateFromStorage : BlockchainAddress * BlockchainAddress -> StakeState option,
            getTotalChxStakedFromStorage : BlockchainAddress -> ChxAmount
            ) =
            ProcessingState(
                getChxBalanceStateFromStorage,
                getHoldingStateFromStorage,
                getVoteStateFromStorage,
                getEligibilityStateFromStorage,
                getKycProvidersFromStorage,
                getAccountStateFromStorage,
                getAssetStateFromStorage,
                getAssetHashByCodeFromStorage,
                getValidatorStateFromStorage,
                getStakeStateFromStorage,
                getTotalChxStakedFromStorage,
                ConcurrentDictionary<TxHash, TxResult>(),
                ConcurrentDictionary<EquivocationProofHash, EquivocationProofResult>(),
                ConcurrentDictionary<BlockchainAddress, ChxBalanceState>(),
                ConcurrentDictionary<AccountHash * AssetHash, HoldingState>(),
                ConcurrentDictionary<VoteId, VoteState option>(),
                ConcurrentDictionary<AccountHash * AssetHash, EligibilityState option>(),
                ConcurrentDictionary<AssetHash, ConcurrentDictionary<BlockchainAddress, KycProviderChange option>>(),
                ConcurrentDictionary<AccountHash, AccountState option>(),
                ConcurrentDictionary<AssetHash, AssetState option>(),
                ConcurrentDictionary<AssetCode, AssetHash option>(),
                ConcurrentDictionary<BlockchainAddress, ValidatorState option>(),
                ConcurrentDictionary<BlockchainAddress * BlockchainAddress, StakeState option>(),
                ConcurrentDictionary<BlockchainAddress, ChxAmount>(),
                ConcurrentDictionary<BlockchainAddress, ChxAmount>(),
                ChxAmount 0m
            )

        member val CollectedReward = collectedReward with get, set

        member __.Clone () =
            ProcessingState(
                getChxBalanceStateFromStorage,
                getHoldingStateFromStorage,
                getVoteStateFromStorage,
                getEligibilityStateFromStorage,
                getKycProvidersFromStorage,
                getAccountStateFromStorage,
                getAssetStateFromStorage,
                getAssetHashByCodeFromStorage,
                getValidatorStateFromStorage,
                getStakeStateFromStorage,
                getTotalChxStakedFromStorage,
                ConcurrentDictionary(txResults),
                ConcurrentDictionary(equivocationProofResults),
                ConcurrentDictionary(chxBalances),
                ConcurrentDictionary(holdings),
                ConcurrentDictionary(votes),
                ConcurrentDictionary(eligibilities),
                ConcurrentDictionary(kycProviders),
                ConcurrentDictionary(accounts),
                ConcurrentDictionary(assets),
                ConcurrentDictionary(assetHashesByCode),
                ConcurrentDictionary(validators),
                ConcurrentDictionary(stakes),
                ConcurrentDictionary(totalChxStaked),
                ConcurrentDictionary(stakingRewards),
                __.CollectedReward
            )

        /// Makes sure all involved data is loaded into the state unchanged, except CHX balance nonce which is updated.
        member __.MergeStateAfterFailedTx (otherState : ProcessingState) =
            let otherOutput = otherState.ToProcessingOutput ()
            for other in otherOutput.ChxBalances do
                let current = __.GetChxBalance (other.Key)
                __.SetChxBalance (other.Key, { current with Nonce = other.Value.Nonce })
            for other in otherOutput.Holdings do
                __.GetHolding (other.Key) |> ignore
            for other in otherOutput.Votes do
                __.GetVote (other.Key) |> ignore
            for other in otherOutput.Eligibilities do
                __.GetAccountEligibility (other.Key) |> ignore
            for other in otherOutput.KycProviders do
                __.GetKycProviders (other.Key) |> ignore
            for other in otherOutput.Accounts do
                __.GetAccount (other.Key) |> ignore
            for other in otherOutput.Assets do
                __.GetAsset (other.Key) |> ignore
            for other in otherOutput.Validators do
                __.GetValidator (other.Key) |> ignore
            for other in otherOutput.Stakes do
                __.GetStake (other.Key) |> ignore

        member __.GetChxBalance (address : BlockchainAddress) =
            chxBalances.GetOrAdd(address, getChxBalanceState)

        member __.GetHolding (accountHash : AccountHash, assetHash : AssetHash) =
            holdings.GetOrAdd((accountHash, assetHash), getHoldingState)

        member __.GetVote (voteId : VoteId) =
            votes.GetOrAdd(voteId, getVoteStateFromStorage)

        member __.GetAccountEligibility (accountHash : AccountHash, assetHash : AssetHash) =
            eligibilities.GetOrAdd((accountHash, assetHash), getEligibilityStateFromStorage)

        member __.GetKycProviders (assetHash) =
            kycProviders.GetOrAdd(assetHash, fun _ ->
                getKycProvidersFromStorage assetHash
                |> Seq.map (fun address -> (address, None))
                |> Map.ofSeq
                |> ConcurrentDictionary
            )
            |> List.ofDict
            |> List.filter (fun (_, change) -> change <> Some Remove)
            |> List.map fst

        member __.GetAccount (accountHash : AccountHash) =
            accounts.GetOrAdd(accountHash, getAccountStateFromStorage)

        member __.GetAsset (assetHash : AssetHash) =
            assets.GetOrAdd(assetHash, getAssetStateFromStorage)

        member __.GetAssetHashByCode (assetCode : AssetCode) =
            assetHashesByCode.GetOrAdd(assetCode, getAssetHashByCodeFromStorage)

        member __.GetValidator (address : BlockchainAddress) =
            validators.GetOrAdd(address, getValidatorStateFromStorage)

        member __.GetStake (stakerAddress : BlockchainAddress, validatorAddress : BlockchainAddress) =
            stakes.GetOrAdd((stakerAddress, validatorAddress), getStakeStateFromStorage)

        // Not part of the blockchain state
        member __.GetTotalChxStaked (address) =
            totalChxStaked.GetOrAdd(address, getTotalChxStakedFromStorage)

        member __.SetChxBalance (address, state : ChxBalanceState) =
            chxBalances.AddOrUpdate(address, state, fun _ _ -> state) |> ignore

        member __.SetHolding (accountHash, assetHash, state : HoldingState) =
            holdings.AddOrUpdate((accountHash, assetHash), state, fun _ _ -> state) |> ignore

        member __.SetVote (voteId, state : VoteState) =
            let state = Some state;
            votes.AddOrUpdate(voteId, state, fun _ _ -> state) |> ignore

        member __.SetAccountEligibility (accountHash, assetHash, state : EligibilityState) =
            let state = Some state;
            eligibilities.AddOrUpdate((accountHash, assetHash), state, fun _ _ -> state) |> ignore

        member __.SetKycProvider (assetHash, providerAddress, providerChange) =
            match kycProviders.TryGetValue assetHash with
            | false, _ ->
                let newProvider = new ConcurrentDictionary<BlockchainAddress, KycProviderChange option>()
                newProvider.AddOrUpdate (providerAddress, providerChange, fun _ _ -> providerChange) |> ignore
                kycProviders.AddOrUpdate (assetHash, newProvider, fun _ _ -> newProvider) |> ignore
            | true, existingProvider ->
                match existingProvider.TryGetValue providerAddress with
                | false, _ ->
                    existingProvider.AddOrUpdate (
                        providerAddress,
                        providerChange,
                        fun _ _ -> providerChange)
                    |> ignore
                | true, existingChange ->
                    if existingChange = Some Add && providerChange = Some Remove
                        || existingChange = Some Remove && providerChange = Some Add
                    then
                        existingProvider.AddOrUpdate (providerAddress, None, fun _ _ -> None) |> ignore
                    else
                        existingProvider.AddOrUpdate (
                            providerAddress,
                            providerChange,
                            fun _ _ -> providerChange)
                        |> ignore

        member __.SetAccount (accountHash, state : AccountState) =
            let state = Some state
            accounts.AddOrUpdate (accountHash, state, fun _ _ -> state) |> ignore

        member __.SetAsset (assetHash, state : AssetState) =
            let state = Some state
            assets.AddOrUpdate (assetHash, state, fun _ _ -> state) |> ignore

        member __.SetValidator (address, state : ValidatorState) =
            let state = Some state
            validators.AddOrUpdate(address, state, fun _ _ -> state) |> ignore

        member __.SetStake (stakerAddress, validatorAddress, state : StakeState) =
            let state = Some state
            stakes.AddOrUpdate((stakerAddress, validatorAddress), state, fun _ _ -> state) |> ignore

        // Not part of the blockchain state
        member __.SetTotalChxStaked (address : BlockchainAddress, amount) =
            totalChxStaked.AddOrUpdate(address, amount, fun _ _ -> amount) |> ignore

        member __.SetTxResult (txHash : TxHash, txResult : TxResult) =
            txResults.AddOrUpdate(txHash, txResult, fun _ _ -> txResult) |> ignore

        member __.SetEquivocationProofResult (hash : EquivocationProofHash, result : EquivocationProofResult) =
            equivocationProofResults.AddOrUpdate(hash, result, fun _ _ -> result) |> ignore

        member __.SetStakingReward (stakerAddress : BlockchainAddress, amount : ChxAmount) =
            stakingRewards.AddOrUpdate(
                stakerAddress,
                amount,
                fun _ _ -> failwithf "Staking reward already set for %s" stakerAddress.Value
            ) |> ignore

        member __.ToProcessingOutput () : ProcessingOutput =
            {
                TxResults = txResults |> Map.ofDict
                EquivocationProofResults = equivocationProofResults |> Map.ofDict
                ChxBalances = chxBalances |> Map.ofDict
                Holdings = holdings |> Map.ofDict
                Votes =
                    votes
                    |> Seq.ofDict
                    |> Seq.choose (fun (k, v) -> v |> Option.map (fun s -> k, s))
                    |> Map.ofSeq
                Eligibilities =
                    eligibilities
                    |> Seq.choose (fun a -> a.Value |> Option.map (fun s -> a.Key, s))
                    |> Map.ofSeq
                KycProviders =
                    kycProviders
                    |> Map.ofDict
                    |> Map.map (fun _ provider ->
                        provider
                        |> Seq.ofDict
                        |> Seq.choose (fun (k, v) -> v |> Option.map (fun s -> k, s))
                        |> Map.ofSeq
                    )
                Accounts =
                    accounts
                    |> Seq.ofDict
                    |> Seq.choose (fun (k, v) -> v |> Option.map (fun s -> k, s))
                    |> Map.ofSeq
                Assets =
                    assets
                    |> Seq.ofDict
                    |> Seq.choose (fun (k, v) -> v |> Option.map (fun s -> k, s))
                    |> Map.ofSeq
                Validators =
                    validators
                    |> Seq.ofDict
                    |> Seq.choose (fun (k, v) -> v |> Option.map (fun s -> k, s))
                    |> Map.ofSeq
                Stakes =
                    stakes
                    |> Seq.ofDict
                    |> Seq.choose (fun (k, v) -> v |> Option.map (fun s -> k, s))
                    |> Map.ofSeq
                StakingRewards = stakingRewards |> Map.ofDict
            }

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Action Processing
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let processTransferChxTxAction
        validatorDeposit
        (state : ProcessingState)
        (senderAddress : BlockchainAddress)
        isDepositSlashing
        (action : TransferChxTxAction)
        : Result<ProcessingState, TxErrorCode>
        =

        let fromState = state.GetChxBalance(senderAddress)
        let toState = state.GetChxBalance(action.RecipientAddress)

        let validatorDeposit =
            state.GetValidator(senderAddress)
            |> Option.filter (fun v -> v.TimeToLockDeposit > 0s)
            |> Option.map (fun _ -> validatorDeposit)
            |? ChxAmount 0m

        let availableBalance =
            fromState.Amount
            - state.GetTotalChxStaked(senderAddress)
            - if isDepositSlashing then ChxAmount 0m else validatorDeposit // Deposit must be available to slash it.

        if availableBalance < action.Amount then
            Error TxErrorCode.InsufficientChxBalance
        else
            state.SetChxBalance(
                senderAddress,
                { fromState with Amount = fromState.Amount - action.Amount }
            )
            state.SetChxBalance(
                action.RecipientAddress,
                { toState with Amount = toState.Amount + action.Amount }
            )
            Ok state

    let processTransferAssetTxAction
        (state : ProcessingState)
        (senderAddress : BlockchainAddress)
        (action : TransferAssetTxAction)
        : Result<ProcessingState, TxErrorCode>
        =

        match state.GetAccount(action.FromAccountHash), state.GetAccount(action.ToAccountHash) with
        | None, _ ->
            Error TxErrorCode.SourceAccountNotFound
        | _, None ->
            Error TxErrorCode.DestinationAccountNotFound
        | Some fromAccountState, Some _ when fromAccountState.ControllerAddress = senderAddress ->
            let fromState = state.GetHolding(action.FromAccountHash, action.AssetHash)
            let toState = state.GetHolding(action.ToAccountHash, action.AssetHash)

            let isPrimaryEligible, isSecondaryEligible =
                match state.GetAsset(action.AssetHash) with
                | Some asset when asset.IsEligibilityRequired ->
                    match state.GetAccountEligibility(action.ToAccountHash, action.AssetHash) with
                    | Some eligibilityState ->
                        eligibilityState.Eligibility.IsPrimaryEligible, eligibilityState.Eligibility.IsSecondaryEligible
                    | None ->
                        false, false
                | _ ->
                    true, true

            if fromState.Amount < action.Amount then
                Error TxErrorCode.InsufficientAssetHoldingBalance
            else
                if fromState.IsEmission && not isPrimaryEligible then
                    Error TxErrorCode.NotEligibleInPrimary
                elif not fromState.IsEmission && not isSecondaryEligible then
                    Error TxErrorCode.NotEligibleInSecondary
                else
                    state.SetHolding(
                        action.FromAccountHash,
                        action.AssetHash,
                        { fromState with Amount = fromState.Amount - action.Amount }
                    )
                    state.SetHolding(
                        action.ToAccountHash,
                        action.AssetHash,
                        { toState with Amount = toState.Amount + action.Amount }
                    )
                    Ok state
        | _ ->
            Error TxErrorCode.SenderIsNotSourceAccountController

    let processCreateAssetEmissionTxAction
        (state : ProcessingState)
        (senderAddress : BlockchainAddress)
        (action : CreateAssetEmissionTxAction)
        : Result<ProcessingState, TxErrorCode>
        =

        match state.GetAsset(action.AssetHash), state.GetAccount(action.EmissionAccountHash) with
        | None, _ ->
            Error TxErrorCode.AssetNotFound
        | _, None ->
            Error TxErrorCode.AccountNotFound
        | Some assetState, Some _ when assetState.ControllerAddress = senderAddress ->
            let holdingState = state.GetHolding(action.EmissionAccountHash, action.AssetHash)
            state.SetHolding(
                action.EmissionAccountHash,
                action.AssetHash,
                { holdingState with Amount = holdingState.Amount + action.Amount; IsEmission = true }
            )
            Ok state
        | _ ->
            Error TxErrorCode.SenderIsNotAssetController

    let processCreateAccountTxAction
        deriveHash
        (state : ProcessingState)
        (senderAddress : BlockchainAddress)
        (nonce : Nonce)
        (actionNumber : TxActionNumber)
        : Result<ProcessingState, TxErrorCode>
        =

        let accountHash =
            deriveHash senderAddress nonce actionNumber
            |> AccountHash

        match state.GetAccount(accountHash) with
        | None ->
            state.SetAccount(accountHash, {ControllerAddress = senderAddress})
            Ok state
        | _ ->
            Error TxErrorCode.AccountAlreadyExists // Hash collision.

    let processCreateAssetTxAction
        deriveHash
        (state : ProcessingState)
        (senderAddress : BlockchainAddress)
        (nonce : Nonce)
        (actionNumber : TxActionNumber)
        : Result<ProcessingState, TxErrorCode>
        =

        let assetHash =
            deriveHash senderAddress nonce actionNumber
            |> AssetHash

        match state.GetAsset(assetHash) with
        | None ->
            state.SetAsset(
                assetHash,
                {AssetCode = None; ControllerAddress = senderAddress; IsEligibilityRequired = false}
            )
            Ok state
        | _ ->
            Error TxErrorCode.AssetAlreadyExists // Hash collision.

    let processSetAccountControllerTxAction
        (state : ProcessingState)
        (senderAddress : BlockchainAddress)
        (action : SetAccountControllerTxAction)
        : Result<ProcessingState, TxErrorCode>
        =

        match state.GetAccount(action.AccountHash) with
        | None ->
            Error TxErrorCode.AccountNotFound
        | Some accountState when accountState.ControllerAddress = senderAddress ->
            state.SetAccount(action.AccountHash, {accountState with ControllerAddress = action.ControllerAddress})
            Ok state
        | _ ->
            Error TxErrorCode.SenderIsNotSourceAccountController

    let processSetAssetControllerTxAction
        (state : ProcessingState)
        (senderAddress : BlockchainAddress)
        (action : SetAssetControllerTxAction)
        : Result<ProcessingState, TxErrorCode>
        =

        match state.GetAsset(action.AssetHash) with
        | None ->
            Error TxErrorCode.AssetNotFound
        | Some assetState when assetState.ControllerAddress = senderAddress ->
            state.SetAsset(action.AssetHash, {assetState with ControllerAddress = action.ControllerAddress})
            Ok state
        | _ ->
            Error TxErrorCode.SenderIsNotAssetController

    let processSetAssetCodeTxAction
        (state : ProcessingState)
        (senderAddress : BlockchainAddress)
        (action : SetAssetCodeTxAction)
        : Result<ProcessingState, TxErrorCode>
        =

        match state.GetAsset(action.AssetHash) with
        | None ->
            Error TxErrorCode.AssetNotFound
        | Some assetState when assetState.ControllerAddress = senderAddress ->
            match state.GetAssetHashByCode action.AssetCode with
            | Some _ ->
                Error TxErrorCode.AssetCodeAlreadyExists
            | None ->
                state.SetAsset(action.AssetHash, {assetState with AssetCode = Some action.AssetCode})
                Ok state
        | _ ->
            Error TxErrorCode.SenderIsNotAssetController

    let processConfigureValidatorTxAction
        validatorDeposit
        (state : ProcessingState)
        (senderAddress : BlockchainAddress)
        (action : ConfigureValidatorTxAction)
        : Result<ProcessingState, TxErrorCode>
        =

        let senderState = state.GetChxBalance(senderAddress)
        let totalChxStaked = state.GetTotalChxStaked(senderAddress)

        let availableBalance = senderState.Amount - totalChxStaked

        if availableBalance < validatorDeposit then
            Error TxErrorCode.InsufficientChxBalance
        else
            match state.GetValidator(senderAddress) with
            | None ->
                state.SetValidator(
                    senderAddress,
                    {
                        ValidatorState.NetworkAddress = action.NetworkAddress
                        SharedRewardPercent = action.SharedRewardPercent
                        TimeToLockDeposit = 0s
                        TimeToBlacklist = 0s
                    }
                )
                Ok state
            | Some validatorState ->
                state.SetValidator(
                    senderAddress,
                    { validatorState with
                        NetworkAddress = action.NetworkAddress
                        SharedRewardPercent = action.SharedRewardPercent
                    }
                )
                Ok state

    let processDelegateStakeTxAction
        validatorDeposit
        (state : ProcessingState)
        (senderAddress : BlockchainAddress)
        (action : DelegateStakeTxAction)
        : Result<ProcessingState, TxErrorCode>
        =

        let senderState = state.GetChxBalance(senderAddress)
        let totalChxStaked = state.GetTotalChxStaked(senderAddress)

        let validatorDeposit =
            state.GetValidator(senderAddress)
            |> Option.filter (fun v -> v.TimeToLockDeposit > 0s)
            |> Option.map (fun _ -> validatorDeposit)
            |? ChxAmount 0m

        let availableBalance = senderState.Amount - totalChxStaked - validatorDeposit

        if availableBalance < action.Amount then
            Error TxErrorCode.InsufficientChxBalance
        else
            let stakeState =
                match state.GetStake(senderAddress, action.ValidatorAddress) with
                | None -> {StakeState.Amount = action.Amount}
                | Some s -> {s with Amount = s.Amount + action.Amount}

            if stakeState.Amount < ChxAmount 0m then
                Error TxErrorCode.InsufficientStake
            else
                state.SetStake(senderAddress, action.ValidatorAddress, stakeState)
                state.SetTotalChxStaked(senderAddress, totalChxStaked + action.Amount)
                Ok state

    let processSubmitVoteTxAction
        (state : ProcessingState)
        (senderAddress : BlockchainAddress)
        (action : SubmitVoteTxAction)
        : Result<ProcessingState, TxErrorCode>
        =

        match state.GetAsset(action.VoteId.AssetHash), state.GetAccount(action.VoteId.AccountHash) with
        | None, _ ->
            Error TxErrorCode.AssetNotFound
        | _, None ->
            Error TxErrorCode.AccountNotFound
        | Some _, Some accountState when accountState.ControllerAddress = senderAddress ->
            let holding = state.GetHolding(action.VoteId.AccountHash, action.VoteId.AssetHash)
            if holding.Amount.Value <= 0m then
                Error TxErrorCode.HoldingNotFound
            else
                match state.GetVote(action.VoteId) with
                | None ->
                    state.SetVote(action.VoteId, { VoteHash = action.VoteHash; VoteWeight = None })
                    Ok state
                | Some vote ->
                    match vote.VoteWeight with
                    | None ->
                        state.SetVote(action.VoteId, { vote with VoteHash = action.VoteHash })
                        Ok state
                    | Some _ -> Error TxErrorCode.VoteIsAlreadyWeighted
        | _ ->
            Error TxErrorCode.SenderIsNotSourceAccountController

    let processSubmitVoteWeightTxAction
        (state : ProcessingState)
        (senderAddress : BlockchainAddress)
        (action : SubmitVoteWeightTxAction)
        : Result<ProcessingState, TxErrorCode>
        =

        match state.GetAsset(action.VoteId.AssetHash), state.GetAccount(action.VoteId.AccountHash) with
        | None, _ ->
            Error TxErrorCode.AssetNotFound
        | _, None ->
            Error TxErrorCode.AccountNotFound
        | Some assetState, Some _ when assetState.ControllerAddress = senderAddress ->
            match state.GetVote(action.VoteId) with
            | None -> Error TxErrorCode.VoteNotFound
            | Some vote ->
                state.SetVote(action.VoteId, { vote with VoteWeight = action.VoteWeight |> Some })
                Ok state
        | _ ->
            Error TxErrorCode.SenderIsNotAssetController

    let processSetAccountEligibilityTxAction
        (state : ProcessingState)
        (senderAddress : BlockchainAddress)
        (action : SetAccountEligibilityTxAction)
        : Result<ProcessingState, TxErrorCode>
        =

        match state.GetAsset(action.AssetHash), state.GetAccount(action.AccountHash) with
        | None, _ ->
            Error TxErrorCode.AssetNotFound
        | _, None ->
            Error TxErrorCode.AccountNotFound
        | Some _, Some _ ->
            let isApprovedKycProvider =
                state.GetKycProviders action.AssetHash
                |> List.contains senderAddress

            if isApprovedKycProvider then
                match state.GetAccountEligibility(action.AccountHash, action.AssetHash) with
                | None ->
                    state.SetAccountEligibility(
                        action.AccountHash,
                        action.AssetHash,
                        {EligibilityState.Eligibility = action.Eligibility; KycControllerAddress = senderAddress}
                    )
                    Ok state
                | Some eligibilityState ->
                    if eligibilityState.KycControllerAddress = senderAddress then
                        state.SetAccountEligibility(
                            action.AccountHash,
                            action.AssetHash,
                            {eligibilityState with Eligibility = action.Eligibility}
                        )
                        Ok state
                    else
                        Error TxErrorCode.SenderIsNotCurrentKycController
            else
                Error TxErrorCode.SenderIsNotApprovedKycProvider

    let processSetAssetEligibilityTxAction
        (state : ProcessingState)
        (senderAddress : BlockchainAddress)
        (action : SetAssetEligibilityTxAction)
        : Result<ProcessingState, TxErrorCode>
        =

        match state.GetAsset(action.AssetHash) with
        | None ->
            Error TxErrorCode.AssetNotFound
        | Some assetState when assetState.ControllerAddress = senderAddress ->
            state.SetAsset(action.AssetHash, {assetState with IsEligibilityRequired = action.IsEligibilityRequired})
            Ok state
        | _ ->
            Error TxErrorCode.SenderIsNotAssetController

    let processChangeKycControllerAddressTxAction
        (state : ProcessingState)
        (senderAddress : BlockchainAddress)
        (action : ChangeKycControllerAddressTxAction)
        : Result<ProcessingState, TxErrorCode>
        =

        match state.GetAsset(action.AssetHash), state.GetAccount(action.AccountHash) with
        | None, _ ->
            Error TxErrorCode.AssetNotFound
        | _, None ->
            Error TxErrorCode.AccountNotFound
        | Some assetState, Some _ ->
            match state.GetAccountEligibility(action.AccountHash, action.AssetHash) with
            | None -> Error TxErrorCode.EligibilityNotFound
            | Some eligibilityState ->
                let isApprovedKycProvider =
                    state.GetKycProviders action.AssetHash
                    |> List.contains senderAddress

                if eligibilityState.KycControllerAddress = senderAddress && isApprovedKycProvider
                    || assetState.ControllerAddress = senderAddress
                then
                    state.SetAccountEligibility(
                        action.AccountHash,
                        action.AssetHash,
                        {eligibilityState with KycControllerAddress = action.KycControllerAddress}
                    )
                    Ok state
                else
                    Error TxErrorCode.SenderIsNotAssetControllerOrApprovedKycProvider

    let processAddKycProviderTxAction
        (state : ProcessingState)
        (senderAddress : BlockchainAddress)
        (action : AddKycProviderTxAction)
        : Result<ProcessingState, TxErrorCode>
        =

        match state.GetAsset(action.AssetHash) with
        | None ->
            Error TxErrorCode.AssetNotFound
        | Some assetState when assetState.ControllerAddress = senderAddress ->
            let providerExists =
                state.GetKycProviders action.AssetHash
                |> List.exists (fun provider -> provider = action.ProviderAddress)
            if providerExists then
                Error TxErrorCode.KycProviderAldreadyExists
            else
                state.SetKycProvider(action.AssetHash, action.ProviderAddress, Some Add)
                Ok state
        | _ ->
            Error TxErrorCode.SenderIsNotAssetController

    let processRemoveKycProviderTxAction
        (state : ProcessingState)
        (senderAddress : BlockchainAddress)
        (action : RemoveKycProviderTxAction)
        : Result<ProcessingState, TxErrorCode>
        =

        match state.GetAsset(action.AssetHash) with
        | None ->
            Error TxErrorCode.AssetNotFound
        | Some assetState when assetState.ControllerAddress = senderAddress ->
            state.SetKycProvider(action.AssetHash, action.ProviderAddress, Some Remove)
            Ok state
        | _ ->
            Error TxErrorCode.SenderIsNotAssetController

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Tx Processing
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let excludeTxsWithNonceGap
        (getChxBalanceState : BlockchainAddress -> ChxBalanceState option)
        senderAddress
        (txSet : PendingTxInfo list)
        =

        let stateNonce =
            getChxBalanceState senderAddress
            |> Option.map (fun s -> s.Nonce)
            |? Nonce 0L

        let (destinedToFailDueToLowNonce, rest) =
            txSet
            |> List.partition(fun tx -> tx.Nonce <= stateNonce)

        rest
        |> List.sortBy (fun tx -> tx.Nonce)
        |> List.mapi (fun i tx ->
            let expectedNonce = stateNonce + (Convert.ToInt64 (i + 1))
            let (Nonce nonceGap) = tx.Nonce - expectedNonce
            (tx, nonceGap)
        )
        |> List.takeWhile (fun (_, nonceGap) -> nonceGap = 0L)
        |> List.map fst
        |> List.append destinedToFailDueToLowNonce

    let excludeTxsIfBalanceCannotCoverFees
        (getAvailableChxBalance : BlockchainAddress -> ChxAmount)
        senderAddress
        (txSet : PendingTxInfo list)
        =

        let availableBalance = getAvailableChxBalance senderAddress

        txSet
        |> List.sortBy (fun tx -> tx.Nonce)
        |> List.scan (fun newSet tx -> newSet @ [tx]) []
        |> List.takeWhile (fun newSet ->
            let totalTxSetFee = newSet |> List.sumBy (fun tx -> tx.TotalFee)
            totalTxSetFee <= availableBalance
        )
        |> List.last

    let excludeUnprocessableTxs getChxBalanceState getAvailableChxBalance (txSet : PendingTxInfo list) =
        txSet
        |> List.groupBy (fun tx -> tx.Sender)
        |> List.collect (fun (senderAddress, txs) ->
            txs
            |> excludeTxsWithNonceGap getChxBalanceState senderAddress
            |> excludeTxsIfBalanceCannotCoverFees getAvailableChxBalance senderAddress
        )
        |> List.sortBy (fun tx -> tx.AppearanceOrder)

    let getTxSetForNewBlock
        getPendingTxs
        getChxBalanceState
        getAvailableChxBalance
        maxTxCountPerBlock
        : PendingTxInfo list
        =

        let rec getTxSet txHashesToSkip (txSet : PendingTxInfo list) =
            let txCountToFetch = maxTxCountPerBlock - txSet.Length
            let fetchedTxs =
                getPendingTxs txHashesToSkip txCountToFetch
                |> List.map Mapping.pendingTxInfoFromDto
            let txSet = excludeUnprocessableTxs getChxBalanceState getAvailableChxBalance (txSet @ fetchedTxs)
            if txSet.Length = maxTxCountPerBlock || fetchedTxs.Length = 0 then
                txSet
            else
                let txHashesToSkip =
                    fetchedTxs
                    |> List.map (fun t -> t.TxHash)
                    |> List.append txHashesToSkip

                getTxSet txHashesToSkip txSet

        getTxSet [] []

    let orderTxSet (txSet : PendingTxInfo list) : TxHash list =
        let rec orderSet orderedSet unorderedSet =
            match unorderedSet with
            | [] -> orderedSet
            | head :: tail ->
                let (precedingTxsForSameSender, rest) =
                    tail
                    |> List.partition (fun tx ->
                        tx.Sender = head.Sender
                        && (
                            tx.Nonce < head.Nonce
                            || (tx.Nonce = head.Nonce && tx.Fee > head.Fee)
                        )
                    )
                let precedingTxsForSameSender =
                    precedingTxsForSameSender
                    |> List.sortBy (fun tx -> tx.Nonce, -tx.Fee.Value)
                let orderedSet =
                    orderedSet
                    @ precedingTxsForSameSender
                    @ [head]
                orderSet orderedSet rest

        txSet
        |> List.sortBy (fun tx -> tx.AppearanceOrder)
        |> orderSet []
        |> List.map (fun tx -> tx.TxHash)

    let getTxBody getTx createHash verifySignature isValidAddress txHash =
        result {
            let! txEnvelopeDto = getTx txHash
            let! txEnvelope = Validation.validateTxEnvelope txEnvelopeDto
            let! sender = Validation.verifyTxSignature createHash verifySignature txEnvelope

            let! tx =
                txEnvelope.RawTx
                |> Serialization.deserializeTx
                >>= (Validation.validateTx isValidAddress sender txHash)

            return tx
        }

    let updateChxBalanceNonce senderAddress txNonce (state : ProcessingState) =
        let senderState = state.GetChxBalance senderAddress

        if txNonce <= senderState.Nonce then
            Error (TxError TxErrorCode.NonceTooLow)
        elif txNonce = (senderState.Nonce + 1) then
            state.SetChxBalance (senderAddress, {senderState with Nonce = txNonce})
            Ok state
        else
            // Logic in excludeTxsWithNonceGap is supposed to prevent this.
            failwith "Nonce too high."

    let processValidatorReward validatorDeposit (tx : Tx) validator (state : ProcessingState) =
        {
            TransferChxTxAction.RecipientAddress = validator
            Amount = tx.TotalFee
        }
        |> processTransferChxTxAction validatorDeposit state tx.Sender false
        |> Result.mapError TxError

    let processTxAction
        deriveHash
        validatorDeposit
        (senderAddress : BlockchainAddress)
        (nonce : Nonce)
        (actionNumber : TxActionNumber)
        (action : TxAction)
        (state : ProcessingState)
        =

        match action with
        | TransferChx action -> processTransferChxTxAction validatorDeposit state senderAddress false action
        | TransferAsset action -> processTransferAssetTxAction state senderAddress action
        | CreateAssetEmission action -> processCreateAssetEmissionTxAction state senderAddress action
        | CreateAccount -> processCreateAccountTxAction deriveHash state senderAddress nonce actionNumber
        | CreateAsset -> processCreateAssetTxAction deriveHash state senderAddress nonce actionNumber
        | SetAccountController action -> processSetAccountControllerTxAction state senderAddress action
        | SetAssetController action -> processSetAssetControllerTxAction state senderAddress action
        | SetAssetCode action -> processSetAssetCodeTxAction state senderAddress action
        | ConfigureValidator action -> processConfigureValidatorTxAction validatorDeposit state senderAddress action
        | DelegateStake action -> processDelegateStakeTxAction validatorDeposit state senderAddress action
        | SubmitVote action -> processSubmitVoteTxAction state senderAddress action
        | SubmitVoteWeight action -> processSubmitVoteWeightTxAction state senderAddress action
        | SetAccountEligibility action -> processSetAccountEligibilityTxAction state senderAddress action
        | SetAssetEligibility action -> processSetAssetEligibilityTxAction state senderAddress action
        | ChangeKycControllerAddress action -> processChangeKycControllerAddressTxAction state senderAddress action
        | AddKycProvider action -> processAddKycProviderTxAction state senderAddress action
        | RemoveKycProvider action -> processRemoveKycProviderTxAction state senderAddress action

    let processTxActions
        deriveHash
        validatorDeposit
        (senderAddress : BlockchainAddress)
        (nonce : Nonce)
        (actions : TxAction list)
        (state : ProcessingState)
        =

        actions
        |> List.indexed
        |> List.fold (fun result (index, action) ->
            result
            >>= fun state ->
                let actionNumber = index + 1 |> Convert.ToInt16 |> TxActionNumber
                processTxAction deriveHash validatorDeposit senderAddress nonce actionNumber action state
                |> Result.mapError (fun e -> TxActionError (actionNumber, e))
        ) (Ok state)

    let processEquivocationProofs
        getProofBody
        verifySignature
        createConsensusMessageHash
        decodeHash
        createHash
        processTxActions
        validatorDeposit
        validatorBlacklistTime
        (blockNumber : BlockNumber)
        (validators : BlockchainAddress list)
        (equivocationProofs : EquivocationProofHash list)
        (state : ProcessingState)
        =

        for proofHash in equivocationProofs do
            let proof =
                getProofBody proofHash
                >>= Validation.validateEquivocationProof
                    verifySignature
                    createConsensusMessageHash
                    decodeHash
                    createHash
                |> Result.handle
                    id
                    (fun errors ->
                        Log.appErrors errors
                        // TODO: Remove invalid proof from the pool?
                        failwithf "Cannot load equivocation proof %s" proofHash.Value
                    )

            match state.GetValidator(proof.ValidatorAddress) with
            | Some s ->
                state.SetValidator(proof.ValidatorAddress, {s with TimeToBlacklist = validatorBlacklistTime})
            | None -> failwithf "Cannot get validator state for %s" proof.ValidatorAddress.Value

            let amountToTake =
                state.GetChxBalance(proof.ValidatorAddress).Amount
                |> min validatorDeposit

            if amountToTake > ChxAmount 0m then
                let validators = validators |> List.except [proof.ValidatorAddress]
                let amountPerValidator = (amountToTake / decimal validators.Length).Rounded
                for v in validators do
                    {
                        RecipientAddress = v
                        Amount = amountPerValidator
                    }
                    |> processTransferChxTxAction validatorDeposit state proof.ValidatorAddress true
                    |> Result.iterError
                        (failwithf "Cannot process equivocation proof %s: (%A)." proof.EquivocationProofHash.Value)

            let equivocationProofResult =
                {
                    DepositTaken = amountToTake
                    BlockNumber = blockNumber
                }
            state.SetEquivocationProofResult(proofHash, equivocationProofResult)

        state

    let distributeReward
        processTxActions
        (getTopStakers : BlockchainAddress -> StakerInfo list)
        validatorAddress
        (sharedRewardPercent : decimal)
        (state : ProcessingState)
        =

        if sharedRewardPercent < 0m then
            failwithf "SharedRewardPercent cannot be negative: %A." sharedRewardPercent

        if sharedRewardPercent > 100m then
            failwithf "SharedRewardPercent cannot be greater than 100: %A." sharedRewardPercent

        if sharedRewardPercent > 0m then
            let stakers = getTopStakers validatorAddress
            if not stakers.IsEmpty then
                let sumOfStakes = stakers |> List.sumBy (fun s -> s.Amount)
                let distributableReward = (state.CollectedReward * sharedRewardPercent / 100m).Rounded

                let rewards =
                    stakers
                    |> List.map (fun s ->
                        {
                            StakingReward.StakerAddress = s.StakerAddress
                            Amount = (s.Amount / sumOfStakes * distributableReward).Rounded
                        }
                    )

                let actions =
                    rewards
                    |> List.map (fun r ->
                        TransferChx {
                            RecipientAddress = r.StakerAddress
                            Amount = r.Amount
                        }
                    )

                let nonce = state.GetChxBalance(validatorAddress).Nonce + 1
                match processTxActions validatorAddress nonce actions state with
                | Ok (state : ProcessingState) ->
                    for r in rewards do
                        state.SetStakingReward(r.StakerAddress, r.Amount) |> ignore
                | Error err -> failwithf "Cannot process reward distribution: (%A)." err

    let updateValidatorCounters
        getLockedAndBlacklistedValidators
        (state : ProcessingState)
        =

        for validatorAddress in getLockedAndBlacklistedValidators () do
            match state.GetValidator(validatorAddress) with
            | Some s ->
                state.SetValidator(
                    validatorAddress,
                    {s with
                        TimeToLockDeposit = s.TimeToLockDeposit - 1s |> max 0s
                        TimeToBlacklist = s.TimeToBlacklist - 1s |> max 0s
                    }
                )
            | None -> failwithf "Cannot get validator state for %s" validatorAddress.Value

    let lockValidatorDeposits
        validatorDepositLockTime
        (blockNumber : BlockNumber)
        (blockchainConfiguration : BlockchainConfiguration option)
        (state : ProcessingState)
        =

        blockchainConfiguration
        |> Option.iter (fun c ->
            for v in c.Validators do
                match state.GetValidator(v.ValidatorAddress) with
                | Some s ->
                    state.SetValidator(v.ValidatorAddress, {s with TimeToLockDeposit = validatorDepositLockTime})
                | None -> failwithf "Cannot get validator state for %s" v.ValidatorAddress.Value
        )

    let processChanges
        getTx
        getEquivocationProof
        verifySignature
        isValidAddress
        deriveHash
        decodeHash
        createHash
        createConsensusMessageHash
        (getChxBalanceStateFromStorage : BlockchainAddress -> ChxBalanceState option)
        (getHoldingStateFromStorage : AccountHash * AssetHash -> HoldingState option)
        (getVoteStateFromStorage : VoteId -> VoteState option)
        (getEligibilityStateFromStorage : AccountHash * AssetHash -> EligibilityState option)
        (getKycProvidersFromStorage : AssetHash -> BlockchainAddress list)
        (getAccountStateFromStorage : AccountHash -> AccountState option)
        (getAssetStateFromStorage : AssetHash -> AssetState option)
        (getAssetHashByCodeFromStorage : AssetCode -> AssetHash option)
        (getValidatorStateFromStorage : BlockchainAddress -> ValidatorState option)
        (getStakeStateFromStorage : BlockchainAddress * BlockchainAddress -> StakeState option)
        (getTotalChxStakedFromStorage : BlockchainAddress -> ChxAmount)
        (getTopStakers : BlockchainAddress -> StakerInfo list)
        getLockedAndBlacklistedValidators
        validatorDeposit
        validatorDepositLockTime
        validatorBlacklistTime
        (validators : BlockchainAddress list)
        (validatorAddress : BlockchainAddress)
        (sharedRewardPercent : decimal)
        (blockNumber : BlockNumber)
        (blockchainConfiguration : BlockchainConfiguration option)
        (equivocationProofs : EquivocationProofHash list)
        (txSet : TxHash list)
        =

        let processTxActions = processTxActions deriveHash validatorDeposit

        let processTx (state : ProcessingState) (txHash : TxHash) =
            let tx =
                match getTxBody getTx createHash verifySignature isValidAddress txHash with
                | Ok tx -> tx
                | Error err ->
                    Log.appErrors err
                    failwithf "Cannot load tx %s" txHash.Value // TODO: Remove invalid tx from the pool?

            match processValidatorReward validatorDeposit tx validatorAddress state with
            | Error e ->
                // Logic in excludeTxsIfBalanceCannotCoverFees is supposed to prevent this.
                failwithf "Cannot process validator reward for tx %s (Error: %A)" txHash.Value e
            | Ok state ->
                state.CollectedReward <- state.CollectedReward + tx.TotalFee
                match updateChxBalanceNonce tx.Sender tx.Nonce state with
                | Error e ->
                    state.SetTxResult(txHash, { Status = Failure e; BlockNumber = blockNumber })
                    state
                | Ok oldState ->
                    let newState = oldState.Clone()
                    match processTxActions tx.Sender tx.Nonce tx.Actions newState with
                    | Error e ->
                        oldState.SetTxResult(txHash, { Status = Failure e; BlockNumber = blockNumber })
                        oldState.MergeStateAfterFailedTx(newState)
                        oldState
                    | Ok state ->
                        state.SetTxResult(txHash, { Status = Success; BlockNumber = blockNumber })
                        state

        let initialState =
            ProcessingState (
                getChxBalanceStateFromStorage,
                getHoldingStateFromStorage,
                getVoteStateFromStorage,
                getEligibilityStateFromStorage,
                getKycProvidersFromStorage,
                getAccountStateFromStorage,
                getAssetStateFromStorage,
                getAssetHashByCodeFromStorage,
                getValidatorStateFromStorage,
                getStakeStateFromStorage,
                getTotalChxStakedFromStorage
            )

        let state =
            txSet
            |> List.fold processTx initialState
            |> processEquivocationProofs
                getEquivocationProof
                verifySignature
                createConsensusMessageHash
                decodeHash
                createHash
                processTxActions
                validatorDeposit
                validatorBlacklistTime
                blockNumber
                validators
                equivocationProofs

        distributeReward processTxActions getTopStakers validatorAddress sharedRewardPercent state

        if blockchainConfiguration.IsSome then
            updateValidatorCounters getLockedAndBlacklistedValidators state
            lockValidatorDeposits validatorDepositLockTime blockNumber blockchainConfiguration state

        state.ToProcessingOutput()
