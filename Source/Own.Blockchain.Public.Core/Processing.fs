namespace Own.Blockchain.Public.Core

open System
open System.Collections.Concurrent
open Own.Common.FSharp
open Own.Blockchain.Common
open Own.Blockchain.Public.Core
open Own.Blockchain.Public.Core.DomainTypes

module Processing =

    type ProcessingState
        (
        getChxAddressStateFromStorage : BlockchainAddress -> ChxAddressState option,
        getHoldingStateFromStorage : AccountHash * AssetHash -> HoldingState option,
        getVoteStateFromStorage : VoteId -> VoteState option,
        getEligibilityStateFromStorage : AccountHash * AssetHash -> EligibilityState option,
        getKycProvidersFromStorage : AssetHash -> BlockchainAddress list,
        getAccountStateFromStorage : AccountHash -> AccountState option,
        getAssetStateFromStorage : AssetHash -> AssetState option,
        getAssetHashByCodeFromStorage : AssetCode -> AssetHash option,
        getValidatorStateFromStorage : BlockchainAddress -> ValidatorState option,
        getStakeStateFromStorage : BlockchainAddress * BlockchainAddress -> StakeState option,
        getStakersFromStorage : BlockchainAddress -> BlockchainAddress list,
        getTotalChxStakedFromStorage : BlockchainAddress -> ChxAmount,
        getTradingPairControllers : unit -> BlockchainAddress list,
        getTradingPairFromStorage : AssetHash * AssetHash -> TradingPairState option,
        getTradeOrderStateFromStorage : TradeOrderHash -> TradeOrderState option,
        getTradeOrdersFromStorage : AssetHash * AssetHash -> TradeOrderInfo list,
        txResults : ConcurrentDictionary<TxHash, TxResult>,
        equivocationProofResults : ConcurrentDictionary<EquivocationProofHash, EquivocationProofResult>,
        chxAddresses : ConcurrentDictionary<BlockchainAddress, ChxAddressState>,
        holdings : ConcurrentDictionary<AccountHash * AssetHash, HoldingState option>,
        votes : ConcurrentDictionary<VoteId, VoteState option>,
        eligibilities : ConcurrentDictionary<AccountHash * AssetHash, EligibilityState option>,
        kycProviders :
            ConcurrentDictionary<AssetHash, ConcurrentDictionary<BlockchainAddress, KycProviderChange option>>,
        accounts : ConcurrentDictionary<AccountHash, AccountState option>,
        assets : ConcurrentDictionary<AssetHash, AssetState option>,
        assetHashesByCode : ConcurrentDictionary<AssetCode, AssetHash option>,
        validators : ConcurrentDictionary<BlockchainAddress, ValidatorState option * ValidatorChange option>,
        stakes : ConcurrentDictionary<BlockchainAddress * BlockchainAddress, StakeState option>,
        stakers : ConcurrentDictionary<BlockchainAddress, BlockchainAddress list>, // Not part of the blockchain state
        totalChxStaked : ConcurrentDictionary<BlockchainAddress, ChxAmount>, // Not part of the blockchain state
        stakingRewards : ConcurrentDictionary<BlockchainAddress, ChxAmount>,
        collectedReward : ChxAmount,
        tradingPairs : ConcurrentDictionary<AssetHash * AssetHash, TradingPairState option>,
        tradeOrders : ConcurrentDictionary<TradeOrderHash, TradeOrderState option * TradeOrderChange option>
        ) =

        let getChxAddressState address =
            getChxAddressStateFromStorage address
            |? {Nonce = Nonce 0L; Balance = ChxAmount 0m}
        new
            (
            getChxAddressStateFromStorage : BlockchainAddress -> ChxAddressState option,
            getHoldingStateFromStorage : AccountHash * AssetHash -> HoldingState option,
            getVoteStateFromStorage : VoteId -> VoteState option,
            getEligibilityStateFromStorage : AccountHash * AssetHash -> EligibilityState option,
            getKycProvidersFromStorage : AssetHash -> BlockchainAddress list,
            getAccountStateFromStorage : AccountHash -> AccountState option,
            getAssetStateFromStorage : AssetHash -> AssetState option,
            getAssetHashByCodeFromStorage : AssetCode -> AssetHash option,
            getValidatorStateFromStorage : BlockchainAddress -> ValidatorState option,
            getStakeStateFromStorage : BlockchainAddress * BlockchainAddress -> StakeState option,
            getStakersFromStorage : BlockchainAddress -> BlockchainAddress list,
            getTotalChxStakedFromStorage : BlockchainAddress -> ChxAmount,
            getTradingPairControllers : unit -> BlockchainAddress list,
            getTradingPairFromStorage : AssetHash * AssetHash -> TradingPairState option,
            getTradeOrderStateFromStorage : TradeOrderHash -> TradeOrderState option,
            getTradeOrdersFromStorage : AssetHash * AssetHash -> TradeOrderInfo list
            ) =
            ProcessingState(
                getChxAddressStateFromStorage,
                getHoldingStateFromStorage,
                getVoteStateFromStorage,
                getEligibilityStateFromStorage,
                getKycProvidersFromStorage,
                getAccountStateFromStorage,
                getAssetStateFromStorage,
                getAssetHashByCodeFromStorage,
                getValidatorStateFromStorage,
                getStakeStateFromStorage,
                getStakersFromStorage,
                getTotalChxStakedFromStorage,
                getTradingPairControllers,
                getTradingPairFromStorage,
                getTradeOrderStateFromStorage,
                getTradeOrdersFromStorage,
                ConcurrentDictionary<TxHash, TxResult>(),
                ConcurrentDictionary<EquivocationProofHash, EquivocationProofResult>(),
                ConcurrentDictionary<BlockchainAddress, ChxAddressState>(),
                ConcurrentDictionary<AccountHash * AssetHash, HoldingState option>(),
                ConcurrentDictionary<VoteId, VoteState option>(),
                ConcurrentDictionary<AccountHash * AssetHash, EligibilityState option>(),
                ConcurrentDictionary<AssetHash, ConcurrentDictionary<BlockchainAddress, KycProviderChange option>>(),
                ConcurrentDictionary<AccountHash, AccountState option>(),
                ConcurrentDictionary<AssetHash, AssetState option>(),
                ConcurrentDictionary<AssetCode, AssetHash option>(),
                ConcurrentDictionary<BlockchainAddress, ValidatorState option * ValidatorChange option>(),
                ConcurrentDictionary<BlockchainAddress * BlockchainAddress, StakeState option>(),
                ConcurrentDictionary<BlockchainAddress, BlockchainAddress list>(),
                ConcurrentDictionary<BlockchainAddress, ChxAmount>(),
                ConcurrentDictionary<BlockchainAddress, ChxAmount>(),
                ChxAmount 0m,
                ConcurrentDictionary<AssetHash * AssetHash, TradingPairState option>(),
                ConcurrentDictionary<TradeOrderHash, TradeOrderState option * TradeOrderChange option>()
            )

        member val CollectedReward = collectedReward with get, set

        member __.Clone () =
            ProcessingState(
                getChxAddressStateFromStorage,
                getHoldingStateFromStorage,
                getVoteStateFromStorage,
                getEligibilityStateFromStorage,
                getKycProvidersFromStorage,
                getAccountStateFromStorage,
                getAssetStateFromStorage,
                getAssetHashByCodeFromStorage,
                getValidatorStateFromStorage,
                getStakeStateFromStorage,
                getStakersFromStorage,
                getTotalChxStakedFromStorage,
                getTradingPairControllers,
                getTradingPairFromStorage,
                getTradeOrderStateFromStorage,
                getTradeOrdersFromStorage,
                ConcurrentDictionary(txResults),
                ConcurrentDictionary(equivocationProofResults),
                ConcurrentDictionary(chxAddresses),
                ConcurrentDictionary(holdings),
                ConcurrentDictionary(votes),
                ConcurrentDictionary(eligibilities),
                ConcurrentDictionary(kycProviders),
                ConcurrentDictionary(accounts),
                ConcurrentDictionary(assets),
                ConcurrentDictionary(assetHashesByCode),
                ConcurrentDictionary(validators),
                ConcurrentDictionary(stakes),
                ConcurrentDictionary(stakers),
                ConcurrentDictionary(totalChxStaked),
                ConcurrentDictionary(stakingRewards),
                __.CollectedReward,
                ConcurrentDictionary<AssetHash * AssetHash, TradingPairState option>(tradingPairs),
                ConcurrentDictionary<TradeOrderHash, TradeOrderState option * TradeOrderChange option>(tradeOrders)
            )

        /// Makes sure all involved data is loaded into the state unchanged, except CHX balance nonce which is updated.
        member __.MergeStateAfterFailedTx (otherState : ProcessingState) =
            let otherOutput = otherState.ToProcessingOutput ()
            for other in otherOutput.ChxAddresses do
                let current = __.GetChxAddress (other.Key)
                __.SetChxAddress (other.Key, { current with Nonce = other.Value.Nonce })
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
            for other in otherOutput.TradingPairs do
                __.GetTradingPair (other.Key) |> ignore
            for other in otherOutput.TradeOrders do
                __.GetTradeOrder (other.Key) |> ignore

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // State getters
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        member __.GetChxAddress (address : BlockchainAddress) =
            chxAddresses.GetOrAdd(address, getChxAddressState)

        member __.GetHolding (accountHash : AccountHash, assetHash : AssetHash) =
            holdings.GetOrAdd((accountHash, assetHash), getHoldingStateFromStorage)
        member __.GetHoldingOrDefault (accountHash : AccountHash, assetHash : AssetHash) =
            __.GetHolding(accountHash, assetHash) |? {Balance = AssetAmount 0m; IsEmission = false}

        member __.GetVote (voteId : VoteId) =
            votes.GetOrAdd(voteId, getVoteStateFromStorage)

        member __.GetAccountEligibility (accountHash : AccountHash, assetHash : AssetHash) =
            eligibilities.GetOrAdd((accountHash, assetHash), getEligibilityStateFromStorage)

        member __.GetKycProviders assetHash =
            kycProviders.GetOrAdd(assetHash, fun _ ->
                getKycProvidersFromStorage assetHash
                |> Seq.map (fun address -> (address, None))
                |> Map.ofSeq
                |> ConcurrentDictionary
            )
            |> List.ofDict
            |> List.filter (fun (_, change) -> change <> Some KycProviderChange.Remove)
            |> List.map fst

        member __.GetAccount (accountHash : AccountHash) =
            accounts.GetOrAdd(accountHash, getAccountStateFromStorage)

        member __.GetAsset (assetHash : AssetHash) =
            assets.GetOrAdd(assetHash, getAssetStateFromStorage)

        member __.GetAssetHashByCode (assetCode : AssetCode) =
            assetHashesByCode.GetOrAdd(assetCode, getAssetHashByCodeFromStorage)

        member __.GetValidator (address : BlockchainAddress) =
            validators.GetOrAdd(address, fun _ ->
                getValidatorStateFromStorage address
                |> fun state -> state, None
            )
            |> fun (state, change) ->
                match change with
                | Some c when c = ValidatorChange.Remove -> None
                | _ -> state

        member __.GetStake (stakerAddress : BlockchainAddress, validatorAddress : BlockchainAddress) =
            stakes.GetOrAdd((stakerAddress, validatorAddress), getStakeStateFromStorage)

        member __.GetStakers validatorAddress =
            stakers.GetOrAdd(validatorAddress, getStakersFromStorage)

        member __.GetTradingPairControllers () =
            // TODO DSX: Implement as state list for governance actions.
            getTradingPairControllers ()

        member __.GetTradingPair (baseAssetHash : AssetHash, quoteAssetHash : AssetHash) =
            tradingPairs.GetOrAdd((baseAssetHash, quoteAssetHash), getTradingPairFromStorage)

        member __.GetTradeOrder (tradeOrderHash : TradeOrderHash) =
            tradeOrders.GetOrAdd(tradeOrderHash, fun _ ->
                getTradeOrderStateFromStorage tradeOrderHash
                |> fun state -> state, None
            )
            |> fun (state, change) ->
                match change with
                | Some c when c = TradeOrderChange.Remove -> None
                | _ -> state

        // Helpers for trade order matching
        member __.GetLoadedTradingPairs () =
            tradingPairs.Keys
            |> Seq.toList
            |> List.sort

        member __.LoadTradeOrdersForTradingPair (baseAssetHash, quoteAssetHash) =
            for tradeOrderInfo in getTradeOrdersFromStorage (baseAssetHash, quoteAssetHash) do
                if not (tradeOrders.ContainsKey tradeOrderInfo.TradeOrderHash) then
                    let state = tradeOrderInfo |> Mapping.tradeOrderStateFromInfo |> Some
                    if not (tradeOrders.TryAdd(tradeOrderInfo.TradeOrderHash, (state, None))) then
                        failwithf "Cannot add trade order to the state dictionary: %A" tradeOrderInfo

        member __.GetTradeOrdersForTradingPair (baseAssetHash, quoteAssetHash) =
            tradeOrders
            |> Seq.choose (fun o ->
                match o.Value with
                | Some s, _ when s.BaseAssetHash = baseAssetHash && s.QuoteAssetHash = quoteAssetHash -> Some (o.Key, s)
                | _ -> None
            )
            |> Seq.toList

        // Not part of the blockchain state
        member __.GetTotalChxStaked address =
            totalChxStaked.GetOrAdd(address, getTotalChxStakedFromStorage)

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // State setters
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        member __.SetChxAddress (address, state : ChxAddressState) =
            chxAddresses.AddOrUpdate(address, state, fun _ _ -> state) |> ignore

        member __.SetHolding (accountHash, assetHash, state : HoldingState) =
            let state = Some state
            holdings.AddOrUpdate((accountHash, assetHash), state, fun _ _ -> state) |> ignore

        member __.SetVote (voteId, state : VoteState) =
            let state = Some state
            votes.AddOrUpdate(voteId, state, fun _ _ -> state) |> ignore

        member __.SetAccountEligibility (accountHash, assetHash, state : EligibilityState) =
            let state = Some state
            eligibilities.AddOrUpdate((accountHash, assetHash), state, fun _ _ -> state) |> ignore

        member __.SetKycProvider (assetHash, providerAddress, providerChange) =
            match kycProviders.TryGetValue assetHash with
            | false, _ ->
                let newProvider = new ConcurrentDictionary<BlockchainAddress, KycProviderChange option>()
                newProvider.AddOrUpdate (providerAddress, providerChange, fun _ _ -> providerChange) |> ignore
                kycProviders.AddOrUpdate (assetHash, newProvider, fun _ _ -> newProvider) |> ignore
            | true, existingProvider ->
                match existingProvider.TryGetValue providerAddress with
                | true, existingChange ->
                    if existingChange = Some KycProviderChange.Add && providerChange = Some KycProviderChange.Remove
                        || existingChange = Some KycProviderChange.Remove && providerChange = Some KycProviderChange.Add
                    then
                        existingProvider.AddOrUpdate (providerAddress, None, fun _ _ -> None) |> ignore
                    else
                        existingProvider.AddOrUpdate (
                            providerAddress,
                            providerChange,
                            fun _ _ -> providerChange)
                        |> ignore
                | _ ->
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

        member __.SetValidator (address, state, change) =
            let state, change = Some state, Some change
            let processingChange =
                match validators.TryGetValue address with
                | true, (_, existingChange) ->
                    if existingChange = Some ValidatorChange.Add && change = Some ValidatorChange.Remove
                        || existingChange = Some ValidatorChange.Remove && change = Some ValidatorChange.Add
                    then
                        None
                    else
                        change
                | _ -> change
            validators.AddOrUpdate(address, (state, processingChange), fun _ _ -> (state, processingChange)) |> ignore

        member __.SetStake (stakerAddress, validatorAddress, state : StakeState) =
            let state = Some state
            stakes.AddOrUpdate((stakerAddress, validatorAddress), state, fun _ _ -> state) |> ignore

        member __.SetTradingPair (baseAssetHash, quoteAssetHash, state) =
            let state = Some state
            tradingPairs.AddOrUpdate((baseAssetHash, quoteAssetHash), state, fun _ _ -> state) |> ignore

        member __.SetTradeOrder (tradeOrderHash, state, change) =
            let state, change = Some state, Some change
            let processingChange =
                match tradeOrders.TryGetValue tradeOrderHash with
                | true, (_, existingChange) ->
                    match existingChange, change with
                    | Some TradeOrderChange.Add, Some TradeOrderChange.Update ->
                        Some TradeOrderChange.Add
                    | Some TradeOrderChange.Add, Some TradeOrderChange.Remove ->
                        None
                    | Some TradeOrderChange.Update, Some TradeOrderChange.Update
                    | Some TradeOrderChange.Update, Some TradeOrderChange.Remove
                    | None, Some _ ->
                        change
                    | Some _, Some TradeOrderChange.Add
                    | Some TradeOrderChange.Remove, Some _
                    | Some TradeOrderChange.Remove, Some _
                    | _, None ->
                        failwithf "Cannot apply change [%A -> %A] to trade order %s: %A"
                            existingChange change tradeOrderHash.Value state
                | _ -> change
            tradeOrders.AddOrUpdate(
                tradeOrderHash,
                (state, processingChange),
                fun _ _ -> (state, processingChange)
            ) |> ignore

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
                ChxAddresses =
                    chxAddresses
                    |> Map.ofDict
                    |> Map.filter (fun _ a -> a.Nonce.Value <> 0L || a.Balance.Value <> 0m)
                Holdings =
                    holdings
                    |> Seq.ofDict
                    |> Seq.choose (fun (k, v) -> v |> Option.map (fun s -> k, s))
                    |> Map.ofSeq
                Votes =
                    votes
                    |> Seq.ofDict
                    |> Seq.choose (fun (k, v) -> v |> Option.map (fun s -> k, s))
                    |> Map.ofSeq
                Eligibilities =
                    eligibilities
                    |> Seq.ofDict
                    |> Seq.choose (fun (k, v) -> v |> Option.map (fun s -> k, s))
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
                    |> Seq.choose (fun (a, (st, c)) -> st |> Option.map (fun s -> a, (s, c)))
                    |> Seq.choose (fun (a, (s, ch)) -> ch |> Option.map (fun c -> a, (s, c)))
                    |> Map.ofSeq
                Stakes =
                    stakes
                    |> Seq.ofDict
                    |> Seq.choose (fun (k, v) -> v |> Option.map (fun s -> k, s))
                    |> Map.ofSeq
                StakingRewards = stakingRewards |> Map.ofDict
                TradingPairs =
                    tradingPairs
                    |> Seq.ofDict
                    |> Seq.choose (fun (k, v) -> v |> Option.map (fun s -> k, s))
                    |> Map.ofSeq
                TradeOrders =
                    tradeOrders
                    |> Seq.ofDict
                    |> Seq.choose (fun (h, (st, c)) -> st |> Option.map (fun s -> h, (s, c)))
                    |> Seq.choose (fun (h, (s, ch)) -> ch |> Option.map (fun c -> h, (s, c)))
                    |> Map.ofSeq
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

        let fromState = state.GetChxAddress(senderAddress)
        let toState = state.GetChxAddress(action.RecipientAddress)

        let validatorDeposit =
            state.GetValidator(senderAddress)
            |> Option.map (fun _ -> validatorDeposit)
            |? ChxAmount 0m

        let availableBalance =
            fromState.Balance
            - state.GetTotalChxStaked(senderAddress)
            - if isDepositSlashing then ChxAmount 0m else validatorDeposit // Deposit must be available to slash it.

        if availableBalance < action.Amount then
            Error TxErrorCode.InsufficientChxBalance
        else
            let newFromState = { fromState with Balance = fromState.Balance - action.Amount }
            state.SetChxAddress(senderAddress, newFromState)

            let toState = if action.RecipientAddress = senderAddress then newFromState else toState
            let newToState = { toState with Balance = toState.Balance + action.Amount }
            state.SetChxAddress(action.RecipientAddress, newToState)

            if state.GetChxAddress(senderAddress).Balance.Value > Utils.maxBlockchainNumeric
                || state.GetChxAddress(action.RecipientAddress).Balance.Value > Utils.maxBlockchainNumeric
            then
                Error TxErrorCode.ValueTooBig
            else
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
            let fromState = state.GetHoldingOrDefault(action.FromAccountHash, action.AssetHash)
            let toState = state.GetHoldingOrDefault(action.ToAccountHash, action.AssetHash)

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

            if fromState.Balance < action.Amount then
                Error TxErrorCode.InsufficientAssetHoldingBalance
            else
                if fromState.IsEmission && not isPrimaryEligible then
                    Error TxErrorCode.NotEligibleInPrimary
                elif not fromState.IsEmission && not isSecondaryEligible then
                    Error TxErrorCode.NotEligibleInSecondary
                else
                    let newFromState = { fromState with Balance = fromState.Balance - action.Amount }
                    state.SetHolding(action.FromAccountHash, action.AssetHash, newFromState)

                    let toState = if action.ToAccountHash = action.FromAccountHash then newFromState else toState
                    let newToState = { toState with Balance = toState.Balance + action.Amount }
                    state.SetHolding(action.ToAccountHash, action.AssetHash, newToState)

                    let holdingFromAccount = state.GetHoldingOrDefault(action.FromAccountHash, action.AssetHash)
                    let holdingToAccount = state.GetHoldingOrDefault(action.ToAccountHash, action.AssetHash)
                    if holdingFromAccount.Balance.Value > Utils.maxBlockchainNumeric
                        || holdingToAccount.Balance.Value > Utils.maxBlockchainNumeric
                    then
                        Error TxErrorCode.ValueTooBig
                    else
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
            let holdingState = state.GetHoldingOrDefault(action.EmissionAccountHash, action.AssetHash)
            state.SetHolding(
                action.EmissionAccountHash,
                action.AssetHash,
                { holdingState with Balance = holdingState.Balance + action.Amount; IsEmission = true }
            )

            let holdingState = state.GetHoldingOrDefault(action.EmissionAccountHash, action.AssetHash)
            if holdingState.Balance.Value > Utils.maxBlockchainNumeric then
                Error TxErrorCode.ValueTooBig
            else
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

        let senderState = state.GetChxAddress(senderAddress)
        let totalChxStaked = state.GetTotalChxStaked(senderAddress)

        let availableBalance = senderState.Balance - totalChxStaked

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
                        IsEnabled = action.IsEnabled
                    },
                    ValidatorChange.Add
                )
                Ok state
            | Some validatorState ->
                state.SetValidator(
                    senderAddress,
                    { validatorState with
                        NetworkAddress = action.NetworkAddress
                        SharedRewardPercent = action.SharedRewardPercent
                        IsEnabled = action.IsEnabled
                    },
                    ValidatorChange.Update
                )
                Ok state

    let processRemoveValidatorTxAction
        (state : ProcessingState)
        (senderAddress : BlockchainAddress)
        : Result<ProcessingState, TxErrorCode>
        =

        match state.GetValidator(senderAddress) with
        | None -> Error TxErrorCode.ValidatorNotFound
        | Some validatorState ->
            if validatorState.TimeToBlacklist > 0s then
                Error TxErrorCode.ValidatorIsBlacklisted
            elif validatorState.TimeToLockDeposit > 0s then
                Error TxErrorCode.ValidatorDepositLocked
            else
                state.GetStakers senderAddress
                |> List.iter (fun stakerAddress ->
                    state.SetStake (stakerAddress, senderAddress, {StakeState.Amount = ChxAmount 0m})
                )
                state.SetValidator(senderAddress, validatorState, ValidatorChange.Remove)
                Ok state

    let processDelegateStakeTxAction
        validatorDeposit
        (state : ProcessingState)
        (senderAddress : BlockchainAddress)
        (action : DelegateStakeTxAction)
        : Result<ProcessingState, TxErrorCode>
        =

        let senderState = state.GetChxAddress(senderAddress)
        let totalChxStaked = state.GetTotalChxStaked(senderAddress)

        let validatorDeposit =
            state.GetValidator(senderAddress)
            |> Option.map (fun _ -> validatorDeposit)
            |? ChxAmount 0m

        let availableBalance = senderState.Balance - totalChxStaked - validatorDeposit

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

                if state.GetTotalChxStaked(senderAddress).Value > Utils.maxBlockchainNumeric then
                    Error TxErrorCode.ValueTooBig
                else
                    match state.GetStake(senderAddress, action.ValidatorAddress) with
                    | Some stake when stake.Amount.Value > Utils.maxBlockchainNumeric ->
                        Error TxErrorCode.ValueTooBig
                    | _ ->
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
            let holding = state.GetHoldingOrDefault(action.VoteId.AccountHash, action.VoteId.AssetHash)
            if holding.Balance.Value <= 0m then
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
                Error TxErrorCode.KycProviderAlreadyExists
            else
                state.SetKycProvider(action.AssetHash, action.ProviderAddress, Some KycProviderChange.Add)
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
            state.SetKycProvider(action.AssetHash, action.ProviderAddress, Some KycProviderChange.Remove)
            Ok state
        | _ ->
            Error TxErrorCode.SenderIsNotAssetController

    let processConfigureTradingPairTxAction
        (state : ProcessingState)
        (senderAddress : BlockchainAddress)
        (action : ConfigureTradingPairTxAction)
        : Result<ProcessingState, TxErrorCode>
        =

        match state.GetAsset(action.BaseAssetHash), state.GetAsset(action.QuoteAssetHash) with
        | None, _ ->
            Error TxErrorCode.BaseAssetNotFound
        | _, None ->
            Error TxErrorCode.QuoteAssetNotFound
        | Some _, Some _ ->
            let tradingPairControllers = state.GetTradingPairControllers()

            if tradingPairControllers |> List.contains senderAddress then
                match state.GetTradingPair(action.BaseAssetHash, action.QuoteAssetHash) with
                | None ->
                    state.SetTradingPair(
                        action.BaseAssetHash,
                        action.QuoteAssetHash,
                        {
                            IsEnabled = action.IsEnabled
                        }
                    )
                | Some tradingPairState ->
                    state.SetTradingPair(
                        action.BaseAssetHash,
                        action.QuoteAssetHash,
                        { tradingPairState with
                            IsEnabled = action.IsEnabled
                        }
                    )
                Ok state
            else
                Error TxErrorCode.SenderIsNotTradingPairController

    let processPlaceTradeOrderTxAction
        deriveHash
        (blockNumber : BlockNumber)
        (txPosition : int)
        (state : ProcessingState)
        (senderAddress : BlockchainAddress)
        (nonce : Nonce)
        (actionNumber : TxActionNumber)
        (action : PlaceTradeOrderTxAction)
        : Result<ProcessingState, TxErrorCode>
        =

        match state.GetAccount(action.AccountHash) with
        | None ->
            Error TxErrorCode.AccountNotFound
        | Some accountState when accountState.ControllerAddress = senderAddress ->
            match state.GetTradingPair(action.BaseAssetHash, action.QuoteAssetHash) with
            | None ->
                Error TxErrorCode.TradingPairNotFound
            | Some pair ->
                // TODO DSX: Check trading pair conditions
                let tradeOrderHash =
                    deriveHash senderAddress nonce actionNumber
                    |> TradeOrderHash
                match state.GetTradeOrder(tradeOrderHash) with
                | Some state ->
                    failwithf "Trade order %s already exists: %A" tradeOrderHash.Value state
                | None ->
                    let tradeOrderState =
                        {
                            BlockNumber = blockNumber
                            TxPosition = txPosition
                            ActionNumber = actionNumber
                            AccountHash = action.AccountHash
                            BaseAssetHash = action.BaseAssetHash
                            QuoteAssetHash = action.QuoteAssetHash
                            Side = action.Side
                            Amount = action.Amount
                            OrderType = action.OrderType
                            LimitPrice = action.LimitPrice
                            StopPrice = action.StopPrice
                            TrailingDelta = action.TrailingDelta
                            TrailingDeltaIsPercentage = action.TrailingDeltaIsPercentage
                            TimeInForce = action.TimeInForce
                            IsExecutable =
                                match action.OrderType with
                                | TradeOrderType.Market
                                | TradeOrderType.Limit -> true
                                | _ -> false
                            AmountFilled = AssetAmount 0m
                            Status = TradeOrderStatus.Open
                        }
                    state.SetTradeOrder(tradeOrderHash, tradeOrderState, TradeOrderChange.Add)
                    Ok state
        | _ ->
            Error TxErrorCode.SenderIsNotSourceAccountController

    let processCancelTradeOrderTxAction
        (state : ProcessingState)
        (senderAddress : BlockchainAddress)
        (action : CancelTradeOrderTxAction)
        : Result<ProcessingState, TxErrorCode>
        =

        match state.GetTradeOrder(action.TradeOrderHash) with
        | None ->
            Error TxErrorCode.TradeOrderNotFound
        | Some tradeOrderState ->
            match state.GetAccount(tradeOrderState.AccountHash) with
            | None ->
                failwithf "Cannot get state for account %s in trade order %s"
                    tradeOrderState.AccountHash.Value
                    action.TradeOrderHash.Value
            | Some accountState when accountState.ControllerAddress = senderAddress ->
                state.SetTradeOrder(action.TradeOrderHash, tradeOrderState, TradeOrderChange.Remove)
                Ok state
            | _ ->
                Error TxErrorCode.SenderIsNotSourceAccountController

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // TX Processing
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let excludeTxsWithNonceGap
        (getChxAddressState : BlockchainAddress -> ChxAddressState option)
        senderAddress
        (txSet : PendingTxInfo list)
        =

        let stateNonce =
            getChxAddressState senderAddress
            |> Option.map (fun s -> s.Nonce)
            |? Nonce 0L

        let destinedToFailDueToLowNonce, rest =
            txSet
            |> List.partition (fun tx -> tx.Nonce <= stateNonce)

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

    let excludeUnprocessableTxs getChxAddressState getAvailableChxBalance (txSet : PendingTxInfo list) =
        txSet
        |> List.groupBy (fun tx -> tx.Sender)
        |> List.collect (fun (senderAddress, txs) ->
            txs
            |> excludeTxsWithNonceGap getChxAddressState senderAddress
            |> excludeTxsIfBalanceCannotCoverFees getAvailableChxBalance senderAddress
        )
        |> List.sortBy (fun tx -> tx.AppearanceOrder)

    let getTxSetForNewBlock
        getPendingTxs
        getChxAddressState
        getAvailableChxBalance
        minTxActionFee
        maxTxSetFetchIterations
        maxTxCountPerBlock
        : PendingTxInfo list * int
        =

        let rec getTxSet iteration (txHashesToSkip : TxHash list) (txSet : PendingTxInfo list) =
            let txCountToFetch = maxTxCountPerBlock - txSet.Length
            if iteration > maxTxSetFetchIterations then
                Log.warningf "Trying to fetch up to %i TXs, while skipping %i in iteration %i"
                    txCountToFetch
                    txHashesToSkip.Length
                    iteration

            let fetchedTxs =
                getPendingTxs minTxActionFee txHashesToSkip txCountToFetch
                |> List.map Mapping.pendingTxInfoFromDto
            let txSet = excludeUnprocessableTxs getChxAddressState getAvailableChxBalance (txSet @ fetchedTxs)
            if txSet.Length = maxTxCountPerBlock
                || fetchedTxs.Length = 0
                || (iteration >= maxTxSetFetchIterations && not txSet.IsEmpty)
            then
                txSet, iteration
            else
                if iteration >= maxTxSetFetchIterations then
                    Log.warningf "Could not build TxSet in %i iterations" iteration

                let txHashesToSkip =
                    fetchedTxs
                    |> List.map (fun t -> t.TxHash)
                    |> List.append txHashesToSkip

                getTxSet (iteration + 1) txHashesToSkip txSet

        getTxSet 1 [] []

    let orderTxSet (txSet : PendingTxInfo list) : TxHash list =
        let rec orderSet orderedSet unorderedSet =
            match unorderedSet with
            | [] -> orderedSet
            | head :: tail ->
                let precedingTxsForSameSender, rest =
                    tail
                    |> List.partition (fun tx ->
                        tx.Sender = head.Sender
                        && (
                            tx.Nonce < head.Nonce
                            || (tx.Nonce = head.Nonce && tx.ActionFee > head.ActionFee)
                        )
                    )
                let precedingTxsForSameSender =
                    precedingTxsForSameSender
                    |> List.sortBy (fun tx -> tx.Nonce, -tx.ActionFee.Value)
                let orderedSet =
                    orderedSet
                    @ precedingTxsForSameSender
                    @ [head]
                orderSet orderedSet rest

        txSet
        |> List.sortBy (fun tx -> tx.AppearanceOrder)
        |> orderSet []
        |> List.map (fun tx -> tx.TxHash)

    let getTxBody getTx createHash verifySignature isValidHash isValidAddress maxActionCountPerTx txHash =
        result {
            let! txEnvelopeDto = getTx txHash
            let! txEnvelope = Validation.validateTxEnvelope txEnvelopeDto
            let! sender = Validation.verifyTxSignature createHash verifySignature txEnvelope

            let! tx =
                txEnvelope.RawTx
                |> Serialization.deserializeTx
                >>= (Validation.validateTx isValidHash isValidAddress maxActionCountPerTx sender txHash)

            return tx
        }

    let updateChxAddressNonce senderAddress txNonce (state : ProcessingState) =
        let senderState = state.GetChxAddress senderAddress

        if txNonce <= senderState.Nonce then
            Error (TxError TxErrorCode.NonceTooLow)
        elif txNonce = (senderState.Nonce + 1) then
            state.SetChxAddress (senderAddress, {senderState with Nonce = txNonce})
            Ok state
        else
            // Logic in excludeTxsWithNonceGap is supposed to prevent this.
            failwith "Nonce too high"

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
        blockNumber
        txPosition
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
        | RemoveValidator -> processRemoveValidatorTxAction state senderAddress
        | DelegateStake action -> processDelegateStakeTxAction validatorDeposit state senderAddress action
        | SubmitVote action -> processSubmitVoteTxAction state senderAddress action
        | SubmitVoteWeight action -> processSubmitVoteWeightTxAction state senderAddress action
        | SetAccountEligibility action -> processSetAccountEligibilityTxAction state senderAddress action
        | SetAssetEligibility action -> processSetAssetEligibilityTxAction state senderAddress action
        | ChangeKycControllerAddress action -> processChangeKycControllerAddressTxAction state senderAddress action
        | AddKycProvider action -> processAddKycProviderTxAction state senderAddress action
        | RemoveKycProvider action -> processRemoveKycProviderTxAction state senderAddress action
        | ConfigureTradingPair action -> processConfigureTradingPairTxAction state senderAddress action
        | PlaceTradeOrder action ->
            processPlaceTradeOrderTxAction
                deriveHash blockNumber txPosition state senderAddress nonce actionNumber action
        | CancelTradeOrder action -> processCancelTradeOrderTxAction state senderAddress action

    let processTxActions
        deriveHash
        validatorDeposit
        blockNumber
        txPosition
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
                processTxAction
                    deriveHash validatorDeposit blockNumber txPosition senderAddress nonce actionNumber action state
                |> Result.mapError (fun e -> TxActionError (actionNumber, e))
        ) (Ok state)

    let processEquivocationProofs
        getProofBody
        verifySignature
        createConsensusMessageHash
        decodeHash
        createHash
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
                state.SetValidator(
                    proof.ValidatorAddress,
                    {s with TimeToBlacklist = validatorBlacklistTime},
                    ValidatorChange.Update
                )
            | None -> failwithf "Cannot get validator state for %s" proof.ValidatorAddress.Value

            let depositTaken, depositDistribution =
                let amountAvailable =
                    state.GetChxAddress(proof.ValidatorAddress).Balance
                    |> min validatorDeposit

                if amountAvailable > ChxAmount 0m then
                    let validators =
                        validators
                        |> List.except [proof.ValidatorAddress]
                        |> List.filter (fun v ->
                            match state.GetValidator(v) with
                            | Some s -> s.TimeToBlacklist = 0s
                            | None -> failwithf "Cannot get state for validator %s" v.Value
                        )
                    let amountPerValidator = (amountAvailable / decimal validators.Length).Rounded
                    if amountPerValidator.Value > Utils.maxBlockchainNumeric then
                        failwithf "Amount per validator too big: %M" amountPerValidator.Value

                    let depositDistribution =
                        [
                            for v in validators do
                                {
                                    RecipientAddress = v
                                    Amount = amountPerValidator
                                }
                                |> processTransferChxTxAction validatorDeposit state proof.ValidatorAddress true
                                |> Result.iterError
                                    (failwithf "Cannot process equivocation proof %s: (%A)"
                                        proof.EquivocationProofHash.Value)
                                yield
                                    {
                                        DistributedDeposit.ValidatorAddress = v
                                        Amount = amountPerValidator
                                    }
                        ]
                    let depositTaken = depositDistribution |> List.sumBy (fun d -> d.Amount)
                    depositTaken, depositDistribution |> List.filter (fun d -> d.Amount.Value <> 0m)
                else
                    if amountAvailable < ChxAmount 0m then
                        failwithf "Address %s has negative balance: %s"
                            proof.ValidatorAddress.Value
                            (amountAvailable.Value.ToString())
                    amountAvailable, []

            let equivocationProofResult =
                {
                    DepositTaken = depositTaken
                    DepositDistribution = depositDistribution
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
            failwithf "SharedRewardPercent cannot be negative: %A" sharedRewardPercent

        if sharedRewardPercent > 100m then
            failwithf "SharedRewardPercent cannot be greater than 100: %A" sharedRewardPercent

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

                let nonce = state.GetChxAddress(validatorAddress).Nonce + 1
                match processTxActions validatorAddress nonce actions state with
                | Ok (state : ProcessingState) ->
                    for r in rewards do
                        if r.Amount.Value > Utils.maxBlockchainNumeric then
                            failwithf "Reward amount too big: %M" r.Amount.Value
                        else
                            state.SetStakingReward(r.StakerAddress, r.Amount) |> ignore

                | Error err -> failwithf "Cannot process reward distribution: (%A)" err

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
                    },
                    ValidatorChange.Update
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
                    state.SetValidator(
                        v.ValidatorAddress,
                        {s with TimeToLockDeposit = validatorDepositLockTime},
                        ValidatorChange.Update
                    )
                | None -> failwithf "Cannot get validator state for %s" v.ValidatorAddress.Value
        )

    let processChanges
        getTx
        getEquivocationProof
        verifySignature
        isValidHash
        isValidAddress
        deriveHash
        decodeHash
        createHash
        createConsensusMessageHash
        (getChxAddressStateFromStorage : BlockchainAddress -> ChxAddressState option)
        (getHoldingStateFromStorage : AccountHash * AssetHash -> HoldingState option)
        (getVoteStateFromStorage : VoteId -> VoteState option)
        (getEligibilityStateFromStorage : AccountHash * AssetHash -> EligibilityState option)
        (getKycProvidersFromStorage : AssetHash -> BlockchainAddress list)
        (getAccountStateFromStorage : AccountHash -> AccountState option)
        (getAssetStateFromStorage : AssetHash -> AssetState option)
        (getAssetHashByCodeFromStorage : AssetCode -> AssetHash option)
        (getValidatorStateFromStorage : BlockchainAddress -> ValidatorState option)
        (getStakeStateFromStorage : BlockchainAddress * BlockchainAddress -> StakeState option)
        (getStakersFromStorage : BlockchainAddress -> BlockchainAddress list)
        (getTotalChxStakedFromStorage : BlockchainAddress -> ChxAmount)
        (getTopStakers : BlockchainAddress -> StakerInfo list)
        (getTradingPairControllersFromStorage : unit -> BlockchainAddress list)
        (getTradingPairFromStorage : AssetHash * AssetHash -> TradingPairState option)
        (getTradeOrderStateFromStorage : TradeOrderHash -> TradeOrderState option)
        (getTradeOrdersFromStorage : AssetHash * AssetHash -> TradeOrderInfo list)
        getLockedAndBlacklistedValidators
        maxActionCountPerTx
        validatorDeposit
        validatorDepositLockTime
        validatorBlacklistTime
        (validators : BlockchainAddress list)
        (validatorAddress : BlockchainAddress)
        (sharedRewardPercent : decimal)
        (blockNumber : BlockNumber)
        (blockTimestamp : Timestamp)
        (blockchainConfiguration : BlockchainConfiguration option)
        (equivocationProofs : EquivocationProofHash list)
        (txSet : TxHash list)
        =

        let processTxActions = processTxActions deriveHash validatorDeposit blockNumber

        let loadTxs txHashes =
            txHashes
            |> Array.AsyncParallel.map (fun txHash ->
                getTxBody
                    getTx
                    createHash
                    verifySignature
                    isValidHash
                    isValidAddress
                    maxActionCountPerTx
                    txHash
                |> function
                    | Ok tx -> tx
                    | Error err ->
                        Log.appErrors err
                        failwithf "Cannot load TX %s" txHash.Value // TODO: Remove invalid tx from the pool?
            )
            |> Array.toList

        let processValidatorReward (state : ProcessingState) (tx : Tx) =
            match processValidatorReward validatorDeposit tx validatorAddress state with
            | Error e ->
                // Logic in excludeTxsIfBalanceCannotCoverFees is supposed to prevent this.
                failwithf "Cannot process validator reward for TX %s (Error: %A)" tx.TxHash.Value e
            | Ok state ->
                state.CollectedReward <- state.CollectedReward + tx.TotalFee
                state

        let processTx (state : ProcessingState) (index : int, tx : Tx) =
            match updateChxAddressNonce tx.Sender tx.Nonce state with
            | Error e ->
                state.SetTxResult(tx.TxHash, { Status = Failure e; BlockNumber = blockNumber })
                state
            | Ok oldState ->
                if tx.ExpirationTime.Value > 0L && tx.ExpirationTime < blockTimestamp then
                    let txError = TxError TxErrorCode.TxExpired
                    oldState.SetTxResult(tx.TxHash, { Status = Failure txError; BlockNumber = blockNumber })
                    oldState
                else
                    let newState = oldState.Clone()
                    let txPosition = index + 1
                    match processTxActions txPosition tx.Sender tx.Nonce tx.Actions newState with
                    | Error e ->
                        oldState.SetTxResult(tx.TxHash, { Status = Failure e; BlockNumber = blockNumber })
                        oldState.MergeStateAfterFailedTx(newState)
                        oldState
                    | Ok state ->
                        state.SetTxResult(tx.TxHash, { Status = Success; BlockNumber = blockNumber })
                        state

        let state =
            ProcessingState (
                getChxAddressStateFromStorage,
                getHoldingStateFromStorage,
                getVoteStateFromStorage,
                getEligibilityStateFromStorage,
                getKycProvidersFromStorage,
                getAccountStateFromStorage,
                getAssetStateFromStorage,
                getAssetHashByCodeFromStorage,
                getValidatorStateFromStorage,
                getStakeStateFromStorage,
                getStakersFromStorage,
                getTotalChxStakedFromStorage,
                getTradingPairControllersFromStorage,
                getTradingPairFromStorage,
                getTradeOrderStateFromStorage,
                getTradeOrdersFromStorage
            )

        let txs = loadTxs txSet

        let state =
            txs
            |> List.fold processValidatorReward state

        let state =
            txs
            |> List.indexed
            |> List.fold processTx state
            |> processEquivocationProofs
                getEquivocationProof
                verifySignature
                createConsensusMessageHash
                decodeHash
                createHash
                validatorDeposit
                validatorBlacklistTime
                blockNumber
                validators
                equivocationProofs

        distributeReward (processTxActions 0) getTopStakers validatorAddress sharedRewardPercent state

        if blockchainConfiguration.IsSome then
            updateValidatorCounters getLockedAndBlacklistedValidators state
            lockValidatorDeposits validatorDepositLockTime blockNumber blockchainConfiguration state

        for baseAssetHash, quoteAssetHash in state.GetLoadedTradingPairs () do
            state.LoadTradeOrdersForTradingPair (baseAssetHash, quoteAssetHash)
            Trading.matchTradeOrders
                state.GetTradeOrdersForTradingPair
                state.SetTradeOrder
                state.GetHoldingOrDefault
                (baseAssetHash, quoteAssetHash)

        state.ToProcessingOutput()
