namespace Chainium.Blockchain.Public.Core

open System
open System.Collections.Concurrent
open Chainium.Common
open Chainium.Blockchain.Public.Core
open Chainium.Blockchain.Public.Core.DomainTypes

module Processing =

    type ProcessingState
        (
        getChxBalanceStateFromStorage : ChainiumAddress -> ChxBalanceState,
        getHoldingStateFromStorage : AccountHash * EquityID -> HoldingState,
        getAccountControllerFromStorage : AccountHash -> ChainiumAddress,
        txResults : ConcurrentDictionary<TxHash, TxProcessedStatus>,
        chxBalances : ConcurrentDictionary<ChainiumAddress, ChxBalanceState>,
        holdings : ConcurrentDictionary<AccountHash * EquityID, HoldingState>,
        accountControllers : ConcurrentDictionary<AccountHash, ChainiumAddress>
        ) =

        new
            (
            getChxBalanceStateFromStorage : ChainiumAddress -> ChxBalanceState,
            getHoldingStateFromStorage : AccountHash * EquityID -> HoldingState,
            getAccountControllerFromStorage : AccountHash -> ChainiumAddress
            ) =
            ProcessingState(
                getChxBalanceStateFromStorage,
                getHoldingStateFromStorage,
                getAccountControllerFromStorage,
                ConcurrentDictionary<TxHash, TxProcessedStatus>(),
                ConcurrentDictionary<ChainiumAddress, ChxBalanceState>(),
                ConcurrentDictionary<AccountHash * EquityID, HoldingState>(),
                ConcurrentDictionary<AccountHash, ChainiumAddress>()
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

        member __.SetTxStatus (txHash : TxHash, txStatus : TxProcessedStatus) =
            txResults.AddOrUpdate(txHash, txStatus, fun _ _ -> txStatus) |> ignore

        member __.GetChxBalance (address : ChainiumAddress) =
            chxBalances.GetOrAdd(address, getChxBalanceStateFromStorage)

        member __.GetHolding (accountHash : AccountHash, equityID : EquityID) =
            holdings.GetOrAdd((accountHash, equityID), getHoldingStateFromStorage)

        member __.GetAccountController (accountHash : AccountHash) =
            accountControllers.GetOrAdd(accountHash, getAccountControllerFromStorage)

        member __.SetChxBalance (address : ChainiumAddress, state : ChxBalanceState) =
            chxBalances.AddOrUpdate(address, state, fun _ _ -> state) |> ignore

        member __.SetHolding (accountHash : AccountHash, equityID : EquityID, state : HoldingState) =
            holdings.AddOrUpdate((accountHash, equityID), state, fun _ _ -> state) |> ignore

        member __.ToProcessingOutput () : ProcessingOutput =
            {
                TxResults = txResults |> Map.ofDict
                ChxBalances = chxBalances |> Map.ofDict
                Holdings = holdings |> Map.ofDict
            }

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Action Processing
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let processChxTransferTxAction
        (state : ProcessingState)
        (senderAddress : ChainiumAddress)
        (action : ChxTransferTxAction)
        : Result<ProcessingState, AppErrors>
        =

        let fromState = state.GetChxBalance(senderAddress)
        let toState = state.GetChxBalance(action.RecipientAddress)

        if fromState.Amount < action.Amount then
            Error [AppError "CHX balance too low."]
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

    let processEquityTransferTxAction
        (state : ProcessingState)
        (senderAddress : ChainiumAddress)
        (action : EquityTransferTxAction)
        : Result<ProcessingState, AppErrors>
        =

        if senderAddress <> state.GetAccountController(action.FromAccountHash) then
            Error [AppError "Tx signer doesn't control the source account."]
        else
            let fromState = state.GetHolding(action.FromAccountHash, action.EquityID)
            let toState = state.GetHolding(action.ToAccountHash, action.EquityID)

            if fromState.Amount < action.Amount then
                Error [AppError "Holding balance too low."]
            else
                state.SetHolding(
                    action.FromAccountHash,
                    action.EquityID,
                    { fromState with Amount = fromState.Amount - action.Amount }
                )
                state.SetHolding(
                    action.ToAccountHash,
                    action.EquityID,
                    { toState with Amount = toState.Amount + action.Amount }
                )
                Ok state

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Tx Processing
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let excludeUnprocessableTxs
        (getChxBalanceState : ChainiumAddress -> ChxBalanceState)
        (txSet : PendingTxInfo list)
        =

        let excludeUnprocessableTxsForAddress senderAddress (txSet : PendingTxInfo list) =
            let stateNonce = (getChxBalanceState senderAddress).Nonce

            let (destinedToFailDueToLowNonce, rest) =
                txSet
                |> List.partition(fun tx -> tx.Nonce <= stateNonce)

            rest
            |> List.sortBy (fun tx -> tx.Nonce)
            |> List.mapi (fun i tx ->
                let expectedNonce = stateNonce + (int64 (i + 1))
                let (Nonce nonceGap) = tx.Nonce - expectedNonce
                (tx, nonceGap)
            )
            |> List.takeWhile (fun (_, nonceGap) -> nonceGap = 0L)
            |> List.map fst
            |> List.append destinedToFailDueToLowNonce

        txSet
        |> List.groupBy (fun tx -> tx.Sender)
        |> List.collect (fun (senderAddress, txs) -> excludeUnprocessableTxsForAddress senderAddress txs)
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

    let getTxBody getTx verifySignature txHash =
        result {
            let! txEnvelopeDto = getTx txHash
            let txEnvelope = Mapping.txEnvelopeFromDto txEnvelopeDto

            let! sender = Validation.verifyTxSignature verifySignature txEnvelope

            let! tx =
                txEnvelope.RawTx
                |> Serialization.deserializeTx
                >>= (Validation.validateTx sender txHash)

            return tx
        }

    let calculateTotalFee (tx : Tx) =
        tx.Fee * (decimal tx.Actions.Length)

    let updateChxBalanceNonce senderAddress txNonce (state : ProcessingState) =
        let senderState = state.GetChxBalance senderAddress

        if txNonce <= senderState.Nonce then
            Error [AppError "Nonce too low."]
        elif txNonce = (senderState.Nonce + 1L) then
            state.SetChxBalance (senderAddress, {senderState with Nonce = txNonce})
            Ok state
        else
            failwith "Nonce too high." // This shouldn't really happen, due to the logic in excludeUnprocessableTxs.

    let processValidatorReward (tx : Tx) validator (state : ProcessingState) =
        {
            ChxTransferTxAction.RecipientAddress = validator
            Amount = calculateTotalFee tx
        }
        |> processChxTransferTxAction state tx.Sender

    let processTxAction (senderAddress : ChainiumAddress) (action : TxAction) (state : ProcessingState) =
        match action with
        | ChxTransfer action -> processChxTransferTxAction state senderAddress action
        | EquityTransfer action -> processEquityTransferTxAction state senderAddress action

    let processTxActions (senderAddress : ChainiumAddress) (actions : TxAction list) (state : ProcessingState) =
        let processAction result action =
            result
            >>= processTxAction senderAddress action

        actions
        |> List.fold processAction (Ok state)

    let processTxSet
        getTx
        verifySignature
        (getChxBalanceStateFromStorage : ChainiumAddress -> ChxBalanceState)
        (getHoldingStateFromStorage : AccountHash * EquityID -> HoldingState)
        (getAccountControllerFromStorage : AccountHash -> ChainiumAddress)
        (validator : ChainiumAddress)
        (txSet : TxHash list)
        =

        let processTx (oldState : ProcessingState) (txHash : TxHash) =
            let newState = oldState.Clone()

            let processingResult =
                result {
                    let! tx = getTxBody getTx verifySignature txHash

                    let! state =
                        updateChxBalanceNonce tx.Sender tx.Nonce newState
                        >>= processValidatorReward tx validator
                        >>= processTxActions tx.Sender tx.Actions

                    return state
                }

            match processingResult with
            | Error errors ->
                oldState.SetTxStatus(txHash, Failure errors)
                oldState
            | Ok _ ->
                newState.SetTxStatus(txHash, Success)
                newState

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
