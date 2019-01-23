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
        getAccountStateFromStorage : AccountHash -> AccountState option,
        getAssetStateFromStorage : AssetHash -> AssetState option,
        getValidatorStateFromStorage : BlockchainAddress -> ValidatorState option,
        getStakeStateFromStorage : BlockchainAddress * BlockchainAddress -> StakeState option,
        getTotalChxStakedFromStorage : BlockchainAddress -> ChxAmount,
        txResults : ConcurrentDictionary<TxHash, TxResult>,
        chxBalances : ConcurrentDictionary<BlockchainAddress, ChxBalanceState>,
        holdings : ConcurrentDictionary<AccountHash * AssetHash, HoldingState>,
        votes: ConcurrentDictionary<VoteId, VoteState option>,
        eligibilities: ConcurrentDictionary<AccountHash * AssetHash, EligibilityState option>,
        kycControllers: ConcurrentDictionary<KycControllerState, KycControllerChange>,
        accounts : ConcurrentDictionary<AccountHash, AccountState option>,
        assets : ConcurrentDictionary<AssetHash, AssetState option>,
        validators : ConcurrentDictionary<BlockchainAddress, ValidatorState option>,
        stakes : ConcurrentDictionary<BlockchainAddress * BlockchainAddress, StakeState option>,
        totalChxStaked : ConcurrentDictionary<BlockchainAddress, ChxAmount>, // Not part of the blockchain state
        stakerRewards : ConcurrentDictionary<BlockchainAddress, ChxAmount>,
        collectedReward : ChxAmount
        ) =

        let getChxBalanceState address =
            getChxBalanceStateFromStorage address
            |? {Amount = ChxAmount 0m; Nonce = Nonce 0L}
        let getHoldingState (accountHash, assetHash) =
            getHoldingStateFromStorage (accountHash, assetHash)
            |? {Amount = AssetAmount 0m}

        new
            (
            getChxBalanceStateFromStorage : BlockchainAddress -> ChxBalanceState option,
            getHoldingStateFromStorage : AccountHash * AssetHash -> HoldingState option,
            getVoteStateFromStorage : VoteId -> VoteState option,
            getEligibilityStateFromStorage : AccountHash * AssetHash -> EligibilityState option,
            getAccountStateFromStorage : AccountHash -> AccountState option,
            getAssetStateFromStorage : AssetHash -> AssetState option,
            getValidatorStateFromStorage : BlockchainAddress -> ValidatorState option,
            getStakeStateFromStorage : BlockchainAddress * BlockchainAddress -> StakeState option,
            getTotalChxStakedFromStorage : BlockchainAddress -> ChxAmount
            ) =
            ProcessingState(
                getChxBalanceStateFromStorage,
                getHoldingStateFromStorage,
                getVoteStateFromStorage,
                getEligibilityStateFromStorage,
                getAccountStateFromStorage,
                getAssetStateFromStorage,
                getValidatorStateFromStorage,
                getStakeStateFromStorage,
                getTotalChxStakedFromStorage,
                ConcurrentDictionary<TxHash, TxResult>(),
                ConcurrentDictionary<BlockchainAddress, ChxBalanceState>(),
                ConcurrentDictionary<AccountHash * AssetHash, HoldingState>(),
                ConcurrentDictionary<VoteId, VoteState option>(),
                ConcurrentDictionary<AccountHash * AssetHash, EligibilityState option>(),
                ConcurrentDictionary<KycControllerState, KycControllerChange>(),
                ConcurrentDictionary<AccountHash, AccountState option>(),
                ConcurrentDictionary<AssetHash, AssetState option>(),
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
                getAccountStateFromStorage,
                getAssetStateFromStorage,
                getValidatorStateFromStorage,
                getStakeStateFromStorage,
                getTotalChxStakedFromStorage,
                ConcurrentDictionary(txResults),
                ConcurrentDictionary(chxBalances),
                ConcurrentDictionary(holdings),
                ConcurrentDictionary(votes),
                ConcurrentDictionary(eligibilities),
                ConcurrentDictionary(kycControllers),
                ConcurrentDictionary(accounts),
                ConcurrentDictionary(assets),
                ConcurrentDictionary(validators),
                ConcurrentDictionary(stakes),
                ConcurrentDictionary(totalChxStaked),
                ConcurrentDictionary(stakerRewards),
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

        member __.GetEligibility (accountHash : AccountHash, assetHash : AssetHash) =
            eligibilities.GetOrAdd((accountHash, assetHash), getEligibilityStateFromStorage)

        member __.GetAccount (accountHash : AccountHash) =
            accounts.GetOrAdd(accountHash, getAccountStateFromStorage)

        member __.GetAsset (assetHash : AssetHash) =
            assets.GetOrAdd(assetHash, getAssetStateFromStorage)

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

        member __.SetEligibility (accountHash, assetHash, state : EligibilityState) =
            let state = Some state;
            eligibilities.AddOrUpdate((accountHash, assetHash), state, fun _ _ -> state) |> ignore

        member __.SetKycController (state : KycControllerState, change : KycControllerChange) =
            kycControllers.AddOrUpdate(state, change, fun _ _ -> change) |> ignore

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

        member __.SetStakerReward (stakerAddress : BlockchainAddress, amount : ChxAmount) =
            stakerRewards.AddOrUpdate(
                stakerAddress,
                amount,
                fun _ _ -> failwithf "Staker reward already set for %s" stakerAddress.Value
            ) |> ignore

        member __.ToProcessingOutput () : ProcessingOutput =
            {
                TxResults = txResults |> Map.ofDict
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
                KycControllers =
                    kycControllers
                    |> Map.ofDict
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
                StakerRewards = stakerRewards |> Map.ofDict
            }

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Action Processing
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let processTransferChxTxAction
        (state : ProcessingState)
        (senderAddress : BlockchainAddress)
        (action : TransferChxTxAction)
        : Result<ProcessingState, TxErrorCode>
        =

        let fromState = state.GetChxBalance(senderAddress)
        let toState = state.GetChxBalance(action.RecipientAddress)

        let availableBalance = fromState.Amount - state.GetTotalChxStaked(senderAddress)

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

            if fromState.Amount < action.Amount then
                Error TxErrorCode.InsufficientAssetHoldingBalance
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
                { holdingState with Amount = holdingState.Amount + action.Amount }
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
            state.SetAsset(assetHash, {AssetCode = None; ControllerAddress = senderAddress})
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
            state.SetAsset(action.AssetHash, {assetState with AssetCode = Some action.AssetCode})
            Ok state
        | _ ->
            Error TxErrorCode.SenderIsNotAssetController

    let processConfigureValidatorTxAction
        (state : ProcessingState)
        (senderAddress : BlockchainAddress)
        (action : ConfigureValidatorTxAction)
        : Result<ProcessingState, TxErrorCode>
        =

        match state.GetValidator(senderAddress) with
        | None ->
            // TODO: Prevent filling the validator table with junk.
            state.SetValidator(
                senderAddress,
                {
                    ValidatorState.NetworkAddress = action.NetworkAddress
                    SharedRewardPercent = action.SharedRewardPercent
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
        (state : ProcessingState)
        (senderAddress : BlockchainAddress)
        (action : DelegateStakeTxAction)
        : Result<ProcessingState, TxErrorCode>
        =

        let senderState = state.GetChxBalance(senderAddress)
        let totalChxStaked = state.GetTotalChxStaked(senderAddress)

        let availableBalance = senderState.Amount - totalChxStaked

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

    let processSetEligibilityTxAction
        (state : ProcessingState)
        (senderAddress : BlockchainAddress)
        (action : SetEligibilityTxAction)
        : Result<ProcessingState, TxErrorCode>
        =

        match state.GetAsset(action.AssetHash), state.GetAccount(action.AccountHash) with
        | None, _ ->
            Error TxErrorCode.AssetNotFound
        | _, None ->
            Error TxErrorCode.AccountNotFound
        | Some assetState, Some _ ->
            match state.GetEligibility(action.AccountHash, action.AssetHash) with
            | None ->
                if assetState.ControllerAddress = senderAddress then
                    state.SetEligibility(
                        action.AccountHash,
                        action.AssetHash,
                        {EligibilityState.Eligibility = action.Eligibility; KycControllerAddress = senderAddress}
                    )
                    Ok state
                else
                    Error TxErrorCode.SenderIsNotAssetController
            | Some eligibilityState when eligibilityState.KycControllerAddress = senderAddress ->
                state.SetEligibility(
                    action.AccountHash,
                    action.AssetHash,
                    {eligibilityState with Eligibility = action.Eligibility}
                )
                Ok state
            | _ ->
                Error TxErrorCode.SenderIsNotCurrentKycController

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
            match state.GetEligibility(action.AccountHash, action.AssetHash) with
            | None -> Error TxErrorCode.EligibilityNotFound
            | Some eligibilityState when eligibilityState.KycControllerAddress = senderAddress ->
                state.SetEligibility(
                    action.AccountHash,
                    action.AssetHash,
                    {eligibilityState with KycControllerAddress = action.KycControllerAddress}
                )
                Ok state
            | _ ->
                Error TxErrorCode.SenderIsNotCurrentKycController

    let processAddKycControllerTxAction
        (state : ProcessingState)
        (senderAddress : BlockchainAddress)
        (action : AddKycControllerTxAction)
        : Result<ProcessingState, TxErrorCode>
        =

        match state.GetAsset(action.AssetHash) with
        | None ->
            Error TxErrorCode.AssetNotFound
        | Some assetState when assetState.ControllerAddress = senderAddress ->
            state.SetKycController(
                {KycControllerState.AssetHash = action.AssetHash; ControllerAddress = action.ControllerAddress},
                KycControllerChange.Add
            )
            Ok state
        | _ ->
            Error TxErrorCode.SenderIsNotAssetController

    let processRemoveKycControllerTxAction
        (state : ProcessingState)
        (senderAddress : BlockchainAddress)
        (action : RemoveKycControllerTxAction)
        : Result<ProcessingState, TxErrorCode>
        =

        match state.GetAsset(action.AssetHash) with
        | None ->
            Error TxErrorCode.AssetNotFound
        | Some assetState when assetState.ControllerAddress = senderAddress ->
            state.SetKycController(
                {KycControllerState.AssetHash = action.AssetHash; ControllerAddress = action.ControllerAddress},
                KycControllerChange.Remove
            )
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

    let processValidatorReward (tx : Tx) validator (state : ProcessingState) =
        {
            TransferChxTxAction.RecipientAddress = validator
            Amount = tx.TotalFee
        }
        |> processTransferChxTxAction state tx.Sender
        |> Result.mapError TxError

    let processTxAction
        deriveHash
        (senderAddress : BlockchainAddress)
        (nonce : Nonce)
        (actionNumber : TxActionNumber)
        (action : TxAction)
        (state : ProcessingState)
        =

        match action with
        | TransferChx action -> processTransferChxTxAction state senderAddress action
        | TransferAsset action -> processTransferAssetTxAction state senderAddress action
        | CreateAssetEmission action -> processCreateAssetEmissionTxAction state senderAddress action
        | CreateAccount -> processCreateAccountTxAction deriveHash state senderAddress nonce actionNumber
        | CreateAsset -> processCreateAssetTxAction deriveHash state senderAddress nonce actionNumber
        | SetAccountController action -> processSetAccountControllerTxAction state senderAddress action
        | SetAssetController action -> processSetAssetControllerTxAction state senderAddress action
        | SetAssetCode action -> processSetAssetCodeTxAction state senderAddress action
        | ConfigureValidator action -> processConfigureValidatorTxAction state senderAddress action
        | DelegateStake action -> processDelegateStakeTxAction state senderAddress action
        | SubmitVote action -> processSubmitVoteTxAction state senderAddress action
        | SubmitVoteWeight action -> processSubmitVoteWeightTxAction state senderAddress action
        | SetEligibility action -> processSetEligibilityTxAction state senderAddress action
        | ChangeKycControllerAddress action -> processChangeKycControllerAddressTxAction state senderAddress action
        | AddKycController action -> processAddKycControllerTxAction state senderAddress action
        | RemoveKycController action -> processRemoveKycControllerTxAction state senderAddress action

    let processTxActions
        deriveHash
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
                processTxAction deriveHash senderAddress nonce actionNumber action state
                |> Result.mapError (fun e -> TxActionError (actionNumber, e))
        ) (Ok state)

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

        if sharedRewardPercent = 0m then
            state
        else
            let stakers = getTopStakers validatorAddress
            if stakers.IsEmpty then
                state
            else
                let sumOfStakes = stakers |> List.sumBy (fun s -> s.Amount)
                let distributableReward = (state.CollectedReward * sharedRewardPercent / 100m).Rounded

                let rewards =
                    stakers
                    |> List.map (fun s ->
                        {
                            StakerReward.StakerAddress = s.StakerAddress
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
                        state.SetStakerReward(r.StakerAddress, r.Amount) |> ignore
                    state
                | Error err -> failwithf "Cannot process reward distribution (%A)." err

    let processTxSet
        getTx
        verifySignature
        isValidAddress
        deriveHash
        createHash
        (getChxBalanceStateFromStorage : BlockchainAddress -> ChxBalanceState option)
        (getHoldingStateFromStorage : AccountHash * AssetHash -> HoldingState option)
        (getVoteStateFromStorage : VoteId -> VoteState option)
        (getEligibilityStateFromStorage : AccountHash * AssetHash -> EligibilityState option)
        (getAccountStateFromStorage : AccountHash -> AccountState option)
        (getAssetStateFromStorage : AssetHash -> AssetState option)
        (getValidatorStateFromStorage : BlockchainAddress -> ValidatorState option)
        (getStakeStateFromStorage : BlockchainAddress * BlockchainAddress -> StakeState option)
        (getTotalChxStakedFromStorage : BlockchainAddress -> ChxAmount)
        (getTopStakers : BlockchainAddress -> StakerInfo list)
        (validatorAddress : BlockchainAddress)
        (sharedRewardPercent : decimal)
        (blockNumber : BlockNumber)
        (txSet : TxHash list)
        =

        let processTx (state : ProcessingState) (txHash : TxHash) =
            let tx =
                match getTxBody getTx createHash verifySignature isValidAddress txHash with
                | Ok tx -> tx
                | Error err ->
                    Log.appErrors err
                    failwithf "Cannot load tx %s" txHash.Value // TODO: Remove invalid tx from the pool?

            match processValidatorReward tx validatorAddress state with
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
                    match processTxActions deriveHash tx.Sender tx.Nonce tx.Actions newState with
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
                getAccountStateFromStorage,
                getAssetStateFromStorage,
                getValidatorStateFromStorage,
                getStakeStateFromStorage,
                getTotalChxStakedFromStorage
            )

        let state =
            txSet
            |> List.fold processTx initialState
            |> distributeReward (processTxActions deriveHash) getTopStakers validatorAddress sharedRewardPercent

        state.ToProcessingOutput()
