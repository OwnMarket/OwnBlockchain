namespace Chainium.Blockchain.Public.Core

open System
open Chainium.Common
open Chainium.Blockchain.Public.Core
open Chainium.Blockchain.Public.Core.DomainTypes
open System.Collections.Concurrent

module Processing =

    type ProcessingState
        (
        getChxBalanceStateFromStorage : ChainiumAddress -> ChxBalanceState,
        getHoldingStateFromStorage : AccountHash * EquityID -> HoldingState,
        txResults : ConcurrentDictionary<TxHash, TxProcessedStatus>,
        chxBalances : ConcurrentDictionary<ChainiumAddress, ChxBalanceState>,
        holdings : ConcurrentDictionary<AccountHash * EquityID, HoldingState>
        ) =

        new
            (
            getChxBalanceStateFromStorage : ChainiumAddress -> ChxBalanceState,
            getHoldingStateFromStorage : AccountHash * EquityID -> HoldingState
            ) =
            ProcessingState(
                getChxBalanceStateFromStorage,
                getHoldingStateFromStorage,
                ConcurrentDictionary<TxHash, TxProcessedStatus>(),
                ConcurrentDictionary<ChainiumAddress, ChxBalanceState>(),
                ConcurrentDictionary<AccountHash * EquityID, HoldingState>()
            )

        member __.Clone () =
            ProcessingState(
                getChxBalanceStateFromStorage,
                getHoldingStateFromStorage,
                ConcurrentDictionary(txResults),
                ConcurrentDictionary(chxBalances),
                ConcurrentDictionary(holdings)
            )

        member __.SetTxStatus (txHash : TxHash, txStatus : TxProcessedStatus) =
            txResults.AddOrUpdate(txHash, txStatus, fun _ _ -> txStatus) |> ignore

        member __.GetChxBalance (address : ChainiumAddress) =
            chxBalances.GetOrAdd(address, getChxBalanceStateFromStorage)

        member __.GetHolding (accountHash : AccountHash, equityID : EquityID) =
            holdings.GetOrAdd((accountHash, equityID), getHoldingStateFromStorage)

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

        let (ChxAmount fromBalance) = fromState.Amount
        let (ChxAmount toBalance) = toState.Amount
        let (ChxAmount amountToTransfer) = action.Amount

        if fromBalance < amountToTransfer then
            Error [AppError "CHX balance too low."]
        else
            let fromState = { fromState with Amount = ChxAmount (fromBalance - amountToTransfer) }
            let toState = { toState with Amount = ChxAmount (toBalance + amountToTransfer) }
            state.SetChxBalance(senderAddress, fromState)
            state.SetChxBalance(action.RecipientAddress, toState)
            Ok state

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Tx Processing
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let excludeUnprocessableTxs (txSet : PendingTxInfo list) =
        // group the txs by sender address and order each group by nonce
        // exclude the txs after nonce gap (e.g. if 5 txs with nonces 1, 2, 3, 5, 6, then discard two last ones)
        txSet

    let getTxSetForNewBlock getPendingTxs maxTxCountPerBlock : PendingTxInfo list =
        let rec getTxSet txHashesToSkip (txSet : PendingTxInfo list) =
            let txCountToFetch = maxTxCountPerBlock - txSet.Length
            let fetchedTxs =
                getPendingTxs txHashesToSkip txCountToFetch
                |> List.map Mapping.pendingTxInfoFromDto
            let txSet = excludeUnprocessableTxs (txSet @ fetchedTxs)
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
        let senders =
            txSet
            |> List.map (fun tx -> tx.Sender)
            |> List.distinct

        let detectTxWithWrongPosition txSet =
            failwith "TODO: detectTxWithWrongPosition"
            // for each distinct sender in the set do
            //     if there are multiple txs in the set coming from the same sender then
            //         for each tx in the set belonging to the sender do
            //             if nonce is smaller then the nonce of some other tx (from same sender of course)
            //             or nonce is equal, but fee is higher, then
            //                 move it before that other tx

        let moveTxIntoCorrectPosition txSet (tx, oldPosition, newPosition) =
            failwith "TODO: moveTxIntoCorrectPosition"

        let rec orderSet txSet =
            // Keep ordering set recursively until everything is ordered
            match detectTxWithWrongPosition txSet with
            | None -> txSet
            | Some (tx, oldPosition, newPosition) ->
                let txSet = moveTxIntoCorrectPosition txSet (tx, oldPosition, newPosition)
                orderSet txSet

        txSet
        |> List.sortBy (fun tx -> tx.AppearanceOrder)
        |> orderSet
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
        let (ChxAmount fee) = tx.Fee
        fee * (decimal tx.Actions.Length) |> ChxAmount

    let processValidatorReward (state : ProcessingState) (tx : Tx) validator =
        {
            ChxTransferTxAction.RecipientAddress = validator
            Amount = calculateTotalFee tx
        }
        |> processChxTransferTxAction state tx.Sender

    let processTxAction (state : ProcessingState) (senderAddress : ChainiumAddress) = function
        | ChxTransfer action -> processChxTransferTxAction state senderAddress action
        | EquityTransfer action -> failwith "TODO: EquityTransfer"

    let processTxActions (state : ProcessingState) (senderAddress : ChainiumAddress) (actions : TxAction list) =
        let processAction result action =
            result
            >>= fun state -> processTxAction state senderAddress action

        actions
        |> List.fold processAction (Ok state)

    let processTxSet
        getTx
        verifySignature
        (getChxBalanceStateFromStorage : ChainiumAddress -> ChxBalanceState)
        (getHoldingStateFromStorage : AccountHash * EquityID -> HoldingState)
        (validator : ChainiumAddress)
        (txSet : TxHash list)
        =

        let processTx (oldState : ProcessingState) (txHash : TxHash) =
            let newState = oldState.Clone()

            let processingResult =
                result {
                    let! tx = getTxBody getTx verifySignature txHash

                    let! state =
                        processValidatorReward newState tx validator
                        >>= fun state -> processTxActions state tx.Sender tx.Actions

                    return state
                }

            match processingResult with
            | Error _ ->
                // TODO: Persist error message as well
                oldState.SetTxStatus(txHash, Failure)
                oldState
            | Ok _ ->
                newState.SetTxStatus(txHash, Success)
                newState

        let initialState = ProcessingState (getChxBalanceStateFromStorage, getHoldingStateFromStorage)

        let state =
            txSet
            |> List.fold processTx initialState

        state.ToProcessingOutput()
