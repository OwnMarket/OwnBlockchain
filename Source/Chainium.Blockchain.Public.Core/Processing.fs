namespace Chainium.Blockchain.Public.Core

open System
open System.Collections.Concurrent
open Chainium.Common
open Chainium.Blockchain.Public.Core
open Chainium.Blockchain.Public.Core.DomainTypes

module Processing =

    type ProcessingState
        (
        getChxBalanceStateFromStorage : ChainiumAddress -> ChxBalanceState option,
        getHoldingStateFromStorage : AccountHash * AssetCode -> HoldingState option,
        getAccountControllerFromStorage : AccountHash -> ChainiumAddress option,
        txResults : ConcurrentDictionary<TxHash, TxResult>,
        chxBalances : ConcurrentDictionary<ChainiumAddress, ChxBalanceState>,
        holdings : ConcurrentDictionary<AccountHash * AssetCode, HoldingState>,
        accountControllers : ConcurrentDictionary<AccountHash, ChainiumAddress option>
        ) =

        let getChxBalanceState address =
            getChxBalanceStateFromStorage address
            |? {Amount = ChxAmount 0M; Nonce = Nonce 0L}
        let getHoldingState (accountHash, assetCode) =
            getHoldingStateFromStorage (accountHash, assetCode)
            |? {Amount = AssetAmount 0M}

        new
            (
            getChxBalanceStateFromStorage : ChainiumAddress -> ChxBalanceState option,
            getHoldingStateFromStorage : AccountHash * AssetCode -> HoldingState option,
            getAccountControllerFromStorage : AccountHash -> ChainiumAddress option
            ) =
            ProcessingState(
                getChxBalanceStateFromStorage,
                getHoldingStateFromStorage,
                getAccountControllerFromStorage,
                ConcurrentDictionary<TxHash, TxResult>(),
                ConcurrentDictionary<ChainiumAddress, ChxBalanceState>(),
                ConcurrentDictionary<AccountHash * AssetCode, HoldingState>(),
                ConcurrentDictionary<AccountHash, ChainiumAddress option>()
            )

        member __.Clone () =
            ProcessingState(
                getChxBalanceStateFromStorage,
                getHoldingStateFromStorage,
                getAccountControllerFromStorage,
                ConcurrentDictionary(txResults),
                ConcurrentDictionary(chxBalances),
                ConcurrentDictionary(holdings),
                ConcurrentDictionary(accountControllers)
            )

        /// Makes sure all involved data is loaded into the state unchanged, except CHX balance nonce which is updated.
        member __.MergeStateAfterFailedTx (otherState : ProcessingState) =
            let otherOutput = otherState.ToProcessingOutput ()
            for other in otherOutput.ChxBalances do
                let current = __.GetChxBalance (other.Key)
                __.SetChxBalance (other.Key, { current with Nonce = other.Value.Nonce })
            for other in otherOutput.Holdings do
                __.GetHolding (other.Key) |> ignore
            for other in otherOutput.AccountControllers do
                __.GetAccountController (other.Key) |> ignore

        member __.GetChxBalance (address : ChainiumAddress) : ChxBalanceState =
            chxBalances.GetOrAdd(address, getChxBalanceState)

        member __.GetHolding (accountHash : AccountHash, assetCode : AssetCode) : HoldingState =
            holdings.GetOrAdd((accountHash, assetCode), getHoldingState)

        member __.GetAccountController (accountHash : AccountHash) =
            accountControllers.GetOrAdd(accountHash, getAccountControllerFromStorage)

        member __.SetChxBalance (address : ChainiumAddress, state : ChxBalanceState) =
            chxBalances.AddOrUpdate(address, state, fun _ _ -> state) |> ignore

        member __.SetHolding (accountHash : AccountHash, assetCode : AssetCode, state : HoldingState) =
            holdings.AddOrUpdate((accountHash, assetCode), state, fun _ _ -> state) |> ignore

        member __.SetAccountController (accountHash : AccountHash, controllerAddress : ChainiumAddress) =
            let controllerAddress = controllerAddress |> Some
            accountControllers.AddOrUpdate (accountHash, controllerAddress, fun _ _ -> controllerAddress) |> ignore

        member __.SetTxResult (txHash : TxHash, txResult : TxResult) =
            txResults.AddOrUpdate(txHash, txResult, fun _ _ -> txResult) |> ignore

        member __.ToProcessingOutput () : ProcessingOutput =
            {
                TxResults = txResults |> Map.ofDict
                ChxBalances = chxBalances |> Map.ofDict
                Holdings = holdings |> Map.ofDict
                AccountControllers = accountControllers |> Map.ofDict
            }

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Action Processing
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let processChxTransferTxAction
        (state : ProcessingState)
        (senderAddress : ChainiumAddress)
        (action : ChxTransferTxAction)
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

    let processAssetTransferTxAction
        (state : ProcessingState)
        (senderAddress : ChainiumAddress)
        (action : AssetTransferTxAction)
        : Result<ProcessingState, TxErrorCode>
        =

        match state.GetAccountController(action.FromAccountHash) with
        | Some accountController when accountController = senderAddress ->
            let fromState = state.GetHolding(action.FromAccountHash, action.AssetCode)
            let toState = state.GetHolding(action.ToAccountHash, action.AssetCode)

            if fromState.Amount < action.Amount then
                Error TxErrorCode.InsufficientAssetHoldingBalance
            else
                state.SetHolding(
                    action.FromAccountHash,
                    action.AssetCode,
                    { fromState with Amount = fromState.Amount - action.Amount }
                )
                state.SetHolding(
                    action.ToAccountHash,
                    action.AssetCode,
                    { toState with Amount = toState.Amount + action.Amount }
                )
                Ok state
        | _ ->
            Error TxErrorCode.SenderIsNotSourceAccountController

    let processAccountControllerChangeTxAction
        (state : ProcessingState)
        (senderAddress : ChainiumAddress)
        (action : AccountControllerChangeTxAction)
        : Result<ProcessingState, TxErrorCode>
        =

        match state.GetAccountController(action.AccountHash) with
        | None -> // New controller entry.
            state.SetAccountController(action.AccountHash, action.ControllerAddress)
            Ok state
        | Some accountController when accountController = senderAddress ->
            state.SetAccountController(action.AccountHash, action.ControllerAddress)
            Ok state
        | _ ->
            Error TxErrorCode.SenderIsNotSourceAccountController

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

    let getTxBody getTx verifySignature isValidAddress txHash =
        result {
            let! txEnvelopeDto = getTx txHash
            let! txEnvelope = Validation.validateTxEnvelope txEnvelopeDto
            let! sender = Validation.verifyTxSignature verifySignature txEnvelope

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
        elif txNonce = (senderState.Nonce + 1L) then
            state.SetChxBalance (senderAddress, {senderState with Nonce = txNonce})
            Ok state
        else
            // Logic in excludeTxsWithNonceGap is supposed to prevent this.
            failwith "Nonce too high."

    let processValidatorReward (tx : Tx) validator (state : ProcessingState) =
        {
            ChxTransferTxAction.RecipientAddress = validator
            Amount = tx.TotalFee
        }
        |> processChxTransferTxAction state tx.Sender
        |> Result.mapError TxError

    let processTxAction (senderAddress : ChainiumAddress) (action : TxAction) (state : ProcessingState) =
        match action with
        | ChxTransfer action -> processChxTransferTxAction state senderAddress action
        | AssetTransfer action -> processAssetTransferTxAction state senderAddress action
        | AccountControllerChange action -> processAccountControllerChangeTxAction state senderAddress action

    let processTxActions (senderAddress : ChainiumAddress) (actions : TxAction list) (state : ProcessingState) =
        actions
        |> List.indexed
        |> List.fold (fun result (index, action) ->
            result
            >>= fun state ->
                processTxAction senderAddress action state
                |> Result.mapError (fun e ->
                    let actionNumber = index + 1 |> Convert.ToInt16 |> TxActionNumber
                    TxActionError (actionNumber, e)
                )
        ) (Ok state)

    let processTxSet
        getTx
        verifySignature
        isValidAddress
        (getChxBalanceStateFromStorage : ChainiumAddress -> ChxBalanceState option)
        (getHoldingStateFromStorage : AccountHash * AssetCode -> HoldingState option)
        (getAccountControllerFromStorage : AccountHash -> ChainiumAddress option)
        (validator : ChainiumAddress)
        (blockNumber : BlockNumber)
        (txSet : TxHash list)
        =

        let processTx (state : ProcessingState) (txHash : TxHash) =
            let rawTxHash = txHash |> fun (TxHash h) -> h

            let tx =
                match getTxBody getTx verifySignature isValidAddress txHash with
                | Ok tx -> tx
                | Error err ->
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
                    match processTxActions tx.Sender tx.Actions newState with
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
                getAccountControllerFromStorage
            )

        let state =
            txSet
            |> List.fold processTx initialState

        state.ToProcessingOutput()
