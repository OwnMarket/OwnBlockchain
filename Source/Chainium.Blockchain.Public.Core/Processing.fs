namespace Chainium.Blockchain.Public.Core

open System
open System.Collections.Concurrent
open Chainium.Common
open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Core
open Chainium.Blockchain.Public.Core.DomainTypes

module Processing =

    type ProcessingState
        (
        getChxBalanceStateFromStorage : ChainiumAddress -> ChxBalanceState option,
        getHoldingStateFromStorage : AccountHash * AssetHash -> HoldingState option,
        getAccountStateFromStorage : AccountHash -> AccountState option,
        getAssetStateFromStorage : AssetHash -> AssetState option,
        txResults : ConcurrentDictionary<TxHash, TxResult>,
        chxBalances : ConcurrentDictionary<ChainiumAddress, ChxBalanceState>,
        holdings : ConcurrentDictionary<AccountHash * AssetHash, HoldingState>,
        accounts : ConcurrentDictionary<AccountHash, AccountState option>,
        assets : ConcurrentDictionary<AssetHash, AssetState option>
        ) =

        let getChxBalanceState address =
            getChxBalanceStateFromStorage address
            |? {Amount = ChxAmount 0M; Nonce = Nonce 0L}
        let getHoldingState (accountHash, assetHash) =
            getHoldingStateFromStorage (accountHash, assetHash)
            |? {Amount = AssetAmount 0M}

        new
            (
            getChxBalanceStateFromStorage : ChainiumAddress -> ChxBalanceState option,
            getHoldingStateFromStorage : AccountHash * AssetHash -> HoldingState option,
            getAccountStateFromStorage : AccountHash -> AccountState option,
            getAssetStateFromStorage : AssetHash -> AssetState option
            ) =
            ProcessingState(
                getChxBalanceStateFromStorage,
                getHoldingStateFromStorage,
                getAccountStateFromStorage,
                getAssetStateFromStorage,
                ConcurrentDictionary<TxHash, TxResult>(),
                ConcurrentDictionary<ChainiumAddress, ChxBalanceState>(),
                ConcurrentDictionary<AccountHash * AssetHash, HoldingState>(),
                ConcurrentDictionary<AccountHash, AccountState option>(),
                ConcurrentDictionary<AssetHash, AssetState option>()
            )

        member __.Clone () =
            ProcessingState(
                getChxBalanceStateFromStorage,
                getHoldingStateFromStorage,
                getAccountStateFromStorage,
                getAssetStateFromStorage,
                ConcurrentDictionary(txResults),
                ConcurrentDictionary(chxBalances),
                ConcurrentDictionary(holdings),
                ConcurrentDictionary(accounts),
                ConcurrentDictionary(assets)
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

        member __.GetChxBalance (address : ChainiumAddress) : ChxBalanceState =
            chxBalances.GetOrAdd(address, getChxBalanceState)

        member __.GetHolding (accountHash : AccountHash, assetHash : AssetHash) : HoldingState =
            holdings.GetOrAdd((accountHash, assetHash), getHoldingState)

        member __.GetAccount (accountHash : AccountHash) =
            accounts.GetOrAdd(accountHash, getAccountStateFromStorage)

        member __.GetAsset (assetHash : AssetHash) =
            assets.GetOrAdd(assetHash, getAssetStateFromStorage)

        member __.SetChxBalance (address : ChainiumAddress, state : ChxBalanceState) =
            chxBalances.AddOrUpdate(address, state, fun _ _ -> state) |> ignore

        member __.SetHolding (accountHash : AccountHash, assetHash : AssetHash, state : HoldingState) =
            holdings.AddOrUpdate((accountHash, assetHash), state, fun _ _ -> state) |> ignore

        member __.SetAccount (accountHash : AccountHash, state : AccountState) =
            let state = Some state
            accounts.AddOrUpdate (accountHash, state, fun _ _ -> state) |> ignore

        member __.SetAsset (assetHash : AssetHash, state : AssetState) =
            let state = Some state
            assets.AddOrUpdate (assetHash, state, fun _ _ -> state) |> ignore

        member __.SetTxResult (txHash : TxHash, txResult : TxResult) =
            txResults.AddOrUpdate(txHash, txResult, fun _ _ -> txResult) |> ignore

        member __.ToProcessingOutput () : ProcessingOutput =
            {
                TxResults = txResults |> Map.ofDict
                ChxBalances = chxBalances |> Map.ofDict
                Holdings = holdings |> Map.ofDict
                Accounts =
                    accounts
                    |> Seq.choose (fun a -> a.Value |> Option.map (fun s -> a.Key, s))
                    |> Map.ofSeq
                Assets =
                    assets
                    |> Seq.choose (fun a -> a.Value |> Option.map (fun s -> a.Key, s))
                    |> Map.ofSeq
            }

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Action Processing
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let processTransferChxTxAction
        (state : ProcessingState)
        (senderAddress : ChainiumAddress)
        (action : TransferChxTxAction)
        : Result<ProcessingState, TxErrorCode>
        =

        let fromState = state.GetChxBalance(senderAddress)
        let toState = state.GetChxBalance(action.RecipientAddress)

        if fromState.Amount < action.Amount then
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
        (senderAddress : ChainiumAddress)
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
        (senderAddress : ChainiumAddress)
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
        decodeHash
        createHash
        (state : ProcessingState)
        (senderAddress : ChainiumAddress)
        (Nonce nonce)
        (TxActionNumber actionNumber)
        : Result<ProcessingState, TxErrorCode>
        =

        let accountHash =
            [
                senderAddress |> fun (ChainiumAddress a) -> decodeHash a
                nonce |> Conversion.int64ToBytes
                actionNumber |> Conversion.int16ToBytes
            ]
            |> Array.concat
            |> createHash
            |> AccountHash

        match state.GetAccount(accountHash) with
        | None ->
            state.SetAccount(accountHash, {ControllerAddress = senderAddress})
            Ok state
        | _ ->
            Error TxErrorCode.AccountAlreadyExists // Hash collision.

    let processCreateAssetTxAction
        decodeHash
        createHash
        (state : ProcessingState)
        (senderAddress : ChainiumAddress)
        (Nonce nonce)
        (TxActionNumber actionNumber)
        : Result<ProcessingState, TxErrorCode>
        =

        let assetHash =
            [
                senderAddress |> fun (ChainiumAddress a) -> decodeHash a
                nonce |> Conversion.int64ToBytes
                actionNumber |> Conversion.int16ToBytes
            ]
            |> Array.concat
            |> createHash
            |> AssetHash

        match state.GetAsset(assetHash) with
        | None ->
            state.SetAsset(assetHash, {AssetCode = None; ControllerAddress = senderAddress})
            Ok state
        | _ ->
            Error TxErrorCode.AssetAlreadyExists // Hash collision.

    let processSetAccountControllerTxAction
        (state : ProcessingState)
        (senderAddress : ChainiumAddress)
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
        (senderAddress : ChainiumAddress)
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
        (senderAddress : ChainiumAddress)
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

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Tx Processing
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let excludeTxsWithNonceGap
        (getChxBalanceState : ChainiumAddress -> ChxBalanceState option)
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
        (getChxBalanceState : ChainiumAddress -> ChxBalanceState option)
        senderAddress
        (txSet : PendingTxInfo list)
        =

        let senderBalance =
            getChxBalanceState senderAddress
            |> Option.map (fun s -> s.Amount)
            |? ChxAmount 0M

        txSet
        |> List.sortBy (fun tx -> tx.Nonce)
        |> List.scan (fun newSet tx -> newSet @ [tx]) []
        |> List.takeWhile (fun newSet ->
            let totalTxSetFee = newSet |> List.sumBy (fun tx -> tx.TotalFee)
            totalTxSetFee <= senderBalance
        )
        |> List.last

    let excludeUnprocessableTxs getChxBalanceState (txSet : PendingTxInfo list) =
        txSet
        |> List.groupBy (fun tx -> tx.Sender)
        |> List.collect (fun (senderAddress, txs) ->
            txs
            |> excludeTxsWithNonceGap getChxBalanceState senderAddress
            |> excludeTxsIfBalanceCannotCoverFees getChxBalanceState senderAddress
        )
        |> List.sortBy (fun tx -> tx.AppearanceOrder)

    let getTxSetForNewBlock getPendingTxs getChxBalanceState maxTxCountPerBlock : PendingTxInfo list =
        let rec getTxSet txHashesToSkip (txSet : PendingTxInfo list) =
            let txCountToFetch = maxTxCountPerBlock - txSet.Length
            let fetchedTxs =
                getPendingTxs txHashesToSkip txCountToFetch
                |> List.map Mapping.pendingTxInfoFromDto
            let txSet = excludeUnprocessableTxs getChxBalanceState (txSet @ fetchedTxs)
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
                    |> List.sortBy (fun tx -> tx.Nonce, tx.Fee |> fun (ChxAmount a) -> -a)
                let orderedSet =
                    orderedSet
                    @ precedingTxsForSameSender
                    @ [head]
                orderSet orderedSet rest

        txSet
        |> List.sortBy (fun tx -> tx.AppearanceOrder)
        |> orderSet []
        |> List.map (fun tx -> tx.TxHash)

    let getTxBody getTx verifySignature isValidAddress minTxActionFee txHash =
        result {
            let! txEnvelopeDto = getTx txHash
            let! txEnvelope = Validation.validateTxEnvelope txEnvelopeDto
            let! sender = Validation.verifyTxSignature verifySignature txEnvelope

            let! tx =
                txEnvelope.RawTx
                |> Serialization.deserializeTx
                >>= (Validation.validateTx isValidAddress minTxActionFee sender txHash)

            return tx
        }

    let updateChxBalanceNonce senderAddress txNonce (state : ProcessingState) =
        let senderState = state.GetChxBalance senderAddress

        if txNonce <= senderState.Nonce then
            Error (TxError TxErrorCode.NonceTooLow)
        elif txNonce = (senderState.Nonce + 1L) then
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
        decodeHash
        createHash
        (senderAddress : ChainiumAddress)
        (nonce : Nonce)
        (actionNumber : TxActionNumber)
        (action : TxAction)
        (state : ProcessingState)
        =

        match action with
        | TransferChx action -> processTransferChxTxAction state senderAddress action
        | TransferAsset action -> processTransferAssetTxAction state senderAddress action
        | CreateAssetEmission action -> processCreateAssetEmissionTxAction state senderAddress action
        | CreateAccount -> processCreateAccountTxAction decodeHash createHash state senderAddress nonce actionNumber
        | CreateAsset -> processCreateAssetTxAction decodeHash createHash state senderAddress nonce actionNumber
        | SetAccountController action -> processSetAccountControllerTxAction state senderAddress action
        | SetAssetController action -> processSetAssetControllerTxAction state senderAddress action
        | SetAssetCode action -> processSetAssetCodeTxAction state senderAddress action

    let processTxActions
        decodeHash
        createHash
        (senderAddress : ChainiumAddress)
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
                processTxAction decodeHash createHash senderAddress nonce actionNumber action state
                |> Result.mapError (fun e -> TxActionError (actionNumber, e))
        ) (Ok state)

    let processTxSet
        getTx
        verifySignature
        isValidAddress
        decodeHash
        createHash
        (getChxBalanceStateFromStorage : ChainiumAddress -> ChxBalanceState option)
        (getHoldingStateFromStorage : AccountHash * AssetHash -> HoldingState option)
        (getAccountStateFromStorage : AccountHash -> AccountState option)
        (getAssetStateFromStorage : AssetHash -> AssetState option)
        minTxActionFee
        (validator : ChainiumAddress)
        (blockNumber : BlockNumber)
        (txSet : TxHash list)
        =

        let processTx (state : ProcessingState) (txHash : TxHash) =
            let rawTxHash = txHash |> fun (TxHash h) -> h

            let tx =
                match getTxBody getTx verifySignature isValidAddress minTxActionFee txHash with
                | Ok tx -> tx
                | Error err ->
                    Log.appErrors err
                    txHash
                    |> fun (TxHash h) -> h
                    |> failwithf "Cannot load tx %s" rawTxHash // TODO: Remove invalid tx from the pool?

            match processValidatorReward tx validator state with
            | Error e ->
                // Logic in excludeTxsIfBalanceCannotCoverFees is supposed to prevent this.
                failwithf "Cannot process validator reward for tx %s (Error: %A)" rawTxHash e
            | Ok state ->
                match updateChxBalanceNonce tx.Sender tx.Nonce state with
                | Error e ->
                    state.SetTxResult(txHash, { Status = Failure e; BlockNumber = blockNumber })
                    state
                | Ok oldState ->
                    let newState = oldState.Clone()
                    match processTxActions decodeHash createHash tx.Sender tx.Nonce tx.Actions newState with
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
                getAccountStateFromStorage,
                getAssetStateFromStorage
            )

        let state =
            txSet
            |> List.fold processTx initialState

        state.ToProcessingOutput()
