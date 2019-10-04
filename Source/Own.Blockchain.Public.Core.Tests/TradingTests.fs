namespace Own.Blockchain.Public.Core.Tests

open System
open Xunit
open Swensen.Unquote
open Own.Common.FSharp
open Own.Blockchain.Common
open Own.Blockchain.Public.Core
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Core.Dtos
open Own.Blockchain.Public.Crypto

module TradingTests =

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Helpers
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let private createTradeOrderHash senderAddress nonce actionNumber =
        Hashing.deriveHash senderAddress (Nonce nonce) (TxActionNumber actionNumber) |> TradeOrderHash

    let private initHolding
        (controllerAddress : BlockchainAddress)
        (accountHash : AccountHash, assetHash : AssetHash, amount, isSecondaryEligible : bool)
        =

        (accountHash, assetHash),
        {|
            HoldingState =
                {
                    HoldingState.Balance = AssetAmount amount
                    IsEmission = false
                }
            EligibilityState =
                {
                    EligibilityState.Eligibility =
                        {
                            Eligibility.IsPrimaryEligible = true
                            IsSecondaryEligible = isSecondaryEligible
                        }
                    KycControllerAddress = controllerAddress
                }
        |}

    let private createTradeOrderState
        (baseAssetHash, quoteAssetHash)
        (blockNumber, txPosition, actionNumber)
        accountHash
        (side, amount, orderType, limitPrice, stopPrice, trailingDelta, trailingDeltaIsPercentage, timeInForce)
        (isExecutable, amountFilled, orderStatus)
        =

        {
            TradeOrderState.BlockNumber = BlockNumber blockNumber
            TxPosition = txPosition
            ActionNumber = TxActionNumber actionNumber
            AccountHash = accountHash
            BaseAssetHash = baseAssetHash
            QuoteAssetHash = quoteAssetHash
            Side = side
            Amount = AssetAmount amount
            OrderType = orderType
            LimitPrice = AssetAmount limitPrice
            StopPrice = AssetAmount stopPrice
            TrailingDelta = AssetAmount trailingDelta
            TrailingDeltaIsPercentage = trailingDeltaIsPercentage
            TimeInForce = timeInForce
            IsExecutable = isExecutable
            AmountFilled = AssetAmount amountFilled
            Status = orderStatus
        }

    let private placeOrder
        (baseAssetHash, quoteAssetHash)
        accountHash
        (side, amount, orderType, limitPrice, stopPrice, trailingDelta, trailingDeltaIsPercentage, timeInForce)
        =

        {
            PlaceTradeOrderTxActionDto.AccountHash = accountHash
            BaseAssetHash = baseAssetHash
            QuoteAssetHash = quoteAssetHash
            Side = side
            Amount = amount
            OrderType = orderType
            LimitPrice = limitPrice
            StopPrice = stopPrice
            TrailingDelta = trailingDelta
            TrailingDeltaIsPercentage = trailingDeltaIsPercentage
            TimeInForce = timeInForce
        }

    let private matchOrders blockNumber senderWallet holdings existingOrders incomingOrders =
        // INIT STATE
        let validatorWallet = Signing.generateWallet ()

        let initialChxState =
            [
                senderWallet.Address, { ChxAddressState.Nonce = Nonce 0L; Balance = ChxAmount 10m }
                validatorWallet.Address, { ChxAddressState.Nonce = Nonce 4L; Balance = ChxAmount 10000m }
            ]
            |> Map.ofList

        let holdings =
            holdings
            |> List.map (initHolding senderWallet.Address)
            |> Map.ofList

        let existingOrders = existingOrders |> List.map (fun o -> Helpers.randomHash () |> TradeOrderHash, o)
        let existingOrderHashes = existingOrders |> List.map fst
        let existingOrders = existingOrders |> Map.ofList

        let incomingOrderHashes =
            incomingOrders
            |> List.mapi (fun txIndex actions ->
                let nonce = int64 txIndex + 1L
                actions
                |> List.mapi (fun actionIndex _ ->
                    let actionNumber = Convert.ToInt16 actionIndex + 1s
                    createTradeOrderHash senderWallet.Address nonce actionNumber
                )
            )
            |> List.concat

        // PREPARE TX
        let actionFee = ChxAmount 0.01m

        let txs =
            incomingOrders
            |> List.mapi (fun i actions ->
                let nonce = int64 i + 1L |> Nonce
                actions
                |> List.map (fun action -> { ActionType = "PlaceTradeOrder"; ActionData = action })
                |> Helpers.newTx senderWallet nonce (Timestamp 0L) actionFee
            )

        let txSet = txs |> List.map fst

        let txs = txs |> Map.ofList

        // COMPOSE
        let getTx =
            txs
            |> flip Map.find
            >> Ok

        let getChxAddressState =
            initialChxState
            |> flip Map.tryFind

        let getAccountState _ =
            Some { AccountState.ControllerAddress = senderWallet.Address }

        let getAssetState _ =
            Some { AssetState.AssetCode = None; ControllerAddress = senderWallet.Address; IsEligibilityRequired = true }

        let getHoldingState =
            holdings
            |> flip Map.tryFind
            >> Option.map (fun v -> v.HoldingState)

        let getTradingPairState _ =
            Some { TradingPairState.IsEnabled = true }

        let getTradeOrderState =
            existingOrders
            |> flip Map.tryFind

        let getTradeOrdersFromStorage _ =
            existingOrders
            |> Map.toList
            |> List.map Mapping.tradeOrderStateToInfo

        let output =
            { Helpers.processChangesMockedDeps with
                GetTx = getTx
                GetChxAddressStateFromStorage = getChxAddressState
                GetAccountStateFromStorage = getAccountState
                GetAssetStateFromStorage = getAssetState
                GetHoldingStateFromStorage = getHoldingState
                GetTradingPairStateFromStorage = getTradingPairState
                GetTradeOrderStateFromStorage = getTradeOrderState
                GetTradeOrdersFromStorage = getTradeOrdersFromStorage
                ValidatorAddress = validatorWallet.Address
                TxSet = txSet
                BlockNumber = blockNumber
            }
            |> Helpers.processChanges

        existingOrderHashes, incomingOrderHashes, output

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Tests
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Fact>]
    let ``Matching - LIMIT BUY`` () =
        // ARRANGE
        let senderWallet = Signing.generateWallet ()
        let accountHash1 = Helpers.randomHash () |> AccountHash
        let accountHash2 = Helpers.randomHash () |> AccountHash
        let baseAssetHash = Helpers.randomHash () |> AssetHash
        let quoteAssetHash = Helpers.randomHash () |> AssetHash

        let createTradeOrderState = createTradeOrderState (baseAssetHash, quoteAssetHash)
        let placeOrder = placeOrder (baseAssetHash.Value, quoteAssetHash.Value)

        let holdings =
            [
                accountHash1, baseAssetHash, 1000m, true
                accountHash2, quoteAssetHash, 2000m, true
            ]

        let existingOrders =
            [
                createTradeOrderState
                    (1L, 1, 1s)
                    accountHash1
                    (Sell, 100m, TradeOrderType.Limit, 5m, 0m, 0m, false, GoodTilCancelled)
                    (true, 0m, TradeOrderStatus.Open)
            ]

        let incomingOrders =
            [
                [ // TX1
                    placeOrder accountHash2.Value ("BUY", 100m, "LIMIT", 5m, 0m, 0m, false, "GTC")
                ]
            ]

        // ACT
        let existingOrderHashes, incomingOrderHashes, output =
            matchOrders (BlockNumber 2L) senderWallet holdings existingOrders incomingOrders

        // ASSERT
        test <@ output.TxResults.Count = incomingOrders.Length @>
        for txResult in output.TxResults |> Map.values do
            test <@ txResult.Status = Success @>

        test <@ output.TradeOrders.Count = 2 @>

        let tradeOrderState, tradeOrderChange = output.TradeOrders.[existingOrderHashes.[0]]
        test <@ tradeOrderState.IsExecutable = true @>
        test <@ tradeOrderState.AmountFilled = tradeOrderState.Amount @>
        test <@ tradeOrderState.Status = TradeOrderStatus.Filled @>
        test <@ tradeOrderChange = TradeOrderChange.Remove @>

        let tradeOrderState, tradeOrderChange = output.TradeOrders.[incomingOrderHashes.[0]]
        test <@ tradeOrderState.IsExecutable = true @>
        test <@ tradeOrderState.AmountFilled = tradeOrderState.Amount @>
        test <@ tradeOrderState.Status = TradeOrderStatus.Filled @>
        test <@ tradeOrderChange = TradeOrderChange.Remove @>

        (* TODO DSX: Enable upon implementing settlement
        test <@ output.Holdings.[accountHash1, baseAssetHash].Balance.Value = 900m @>
        test <@ output.Holdings.[accountHash2, baseAssetHash].Balance.Value = 100m @>
        test <@ output.Holdings.[accountHash1, quoteAssetHash].Balance.Value = 500m @>
        test <@ output.Holdings.[accountHash2, quoteAssetHash].Balance.Value = 1500m @>
        *)

    [<Fact>]
    let ``Matching - Stop and Limit price in TRAILING orders follows price`` () =
        // ARRANGE
        let senderWallet = Signing.generateWallet ()
        let accountHash1 = Helpers.randomHash () |> AccountHash
        let accountHash2 = Helpers.randomHash () |> AccountHash
        let baseAssetHash = Helpers.randomHash () |> AssetHash
        let quoteAssetHash = Helpers.randomHash () |> AssetHash

        let createTradeOrderState = createTradeOrderState (baseAssetHash, quoteAssetHash)
        let placeOrder = placeOrder (baseAssetHash.Value, quoteAssetHash.Value)

        let holdings =
            [
                accountHash1, baseAssetHash, 1000m, true
                accountHash2, quoteAssetHash, 2000m, true
            ]

        let existingOrders =
            [
                createTradeOrderState
                    (1L, 1, 1s)
                    accountHash1
                    (Sell, 100m, TradeOrderType.TrailingStopMarket, 0m, 3m, 1m, false, ImmediateOrCancel)
                    (false, 0m, TradeOrderStatus.Open)
                createTradeOrderState
                    (1L, 2, 1s)
                    accountHash1
                    (Buy, 100m, TradeOrderType.TrailingStopMarket, 0m, 7m, 1m, false, ImmediateOrCancel)
                    (false, 0m, TradeOrderStatus.Open)
                createTradeOrderState
                    (1L, 1, 1s)
                    accountHash1
                    (Sell, 100m, TradeOrderType.TrailingStopLimit, 2.5m, 3m, 1m, false, ImmediateOrCancel)
                    (false, 0m, TradeOrderStatus.Open)
                createTradeOrderState
                    (1L, 2, 1s)
                    accountHash1
                    (Buy, 100m, TradeOrderType.TrailingStopLimit, 7.5m, 7m, 1m, false, ImmediateOrCancel)
                    (false, 0m, TradeOrderStatus.Open)
                createTradeOrderState
                    (1L, 1, 1s)
                    accountHash1
                    (Sell, 100m, TradeOrderType.TrailingStopLimit, 2.5m, 3m, 20m, true, ImmediateOrCancel)
                    (false, 0m, TradeOrderStatus.Open)
                createTradeOrderState
                    (1L, 2, 1s)
                    accountHash1
                    (Buy, 100m, TradeOrderType.TrailingStopLimit, 7.5m, 7m, 20m, true, ImmediateOrCancel)
                    (false, 0m, TradeOrderStatus.Open)
            ]

        let incomingOrders =
            [
                [ // TX1
                    placeOrder accountHash1.Value ("SELL", 100m, "LIMIT", 5m, 0m, 0m, false, "GTC")
                ]
                [ // TX2
                    placeOrder accountHash2.Value ("BUY", 100m, "LIMIT", 5m, 0m, 0m, false, "GTC")
                ]
            ]

        // ACT
        let existingOrderHashes, incomingOrderHashes, output =
            matchOrders (BlockNumber 2L) senderWallet holdings existingOrders incomingOrders

        // ASSERT
        test <@ output.TxResults.Count = incomingOrders.Length @>
        for txResult in output.TxResults |> Map.values do
            test <@ txResult.Status = Success @>

        test <@ output.TradeOrders.Count = 8 @>

        // Old orders
        let tradeOrderState, tradeOrderChange = output.TradeOrders.[existingOrderHashes.[0]]
        test <@ tradeOrderState.LimitPrice.Value = 0m @>
        test <@ tradeOrderState.StopPrice.Value = 4m @>
        test <@ tradeOrderState.IsExecutable = false @>
        test <@ tradeOrderState.AmountFilled.Value = 0m @>
        test <@ tradeOrderState.Status = TradeOrderStatus.Open @>
        test <@ tradeOrderChange = TradeOrderChange.Update @>

        let tradeOrderState, tradeOrderChange = output.TradeOrders.[existingOrderHashes.[1]]
        test <@ tradeOrderState.LimitPrice.Value = 0m @>
        test <@ tradeOrderState.StopPrice.Value = 6m @>
        test <@ tradeOrderState.IsExecutable = false @>
        test <@ tradeOrderState.AmountFilled.Value = 0m @>
        test <@ tradeOrderState.Status = TradeOrderStatus.Open @>
        test <@ tradeOrderChange = TradeOrderChange.Update @>

        let tradeOrderState, tradeOrderChange = output.TradeOrders.[existingOrderHashes.[2]]
        test <@ tradeOrderState.LimitPrice.Value = 3.5m @>
        test <@ tradeOrderState.StopPrice.Value = 4m @>
        test <@ tradeOrderState.IsExecutable = false @>
        test <@ tradeOrderState.AmountFilled.Value = 0m @>
        test <@ tradeOrderState.Status = TradeOrderStatus.Open @>
        test <@ tradeOrderChange = TradeOrderChange.Update @>

        let tradeOrderState, tradeOrderChange = output.TradeOrders.[existingOrderHashes.[3]]
        test <@ tradeOrderState.LimitPrice.Value = 6.5m @>
        test <@ tradeOrderState.StopPrice.Value = 6m @>
        test <@ tradeOrderState.IsExecutable = false @>
        test <@ tradeOrderState.AmountFilled.Value = 0m @>
        test <@ tradeOrderState.Status = TradeOrderStatus.Open @>
        test <@ tradeOrderChange = TradeOrderChange.Update @>

        let tradeOrderState, tradeOrderChange = output.TradeOrders.[existingOrderHashes.[4]]
        test <@ tradeOrderState.LimitPrice.Value = 3.5m @>
        test <@ tradeOrderState.StopPrice.Value = 4m @>
        test <@ tradeOrderState.IsExecutable = false @>
        test <@ tradeOrderState.AmountFilled.Value = 0m @>
        test <@ tradeOrderState.Status = TradeOrderStatus.Open @>
        test <@ tradeOrderChange = TradeOrderChange.Update @>

        let tradeOrderState, tradeOrderChange = output.TradeOrders.[existingOrderHashes.[5]]
        test <@ tradeOrderState.LimitPrice.Value = 6.5m @>
        test <@ tradeOrderState.StopPrice.Value = 6m @>
        test <@ tradeOrderState.IsExecutable = false @>
        test <@ tradeOrderState.AmountFilled.Value = 0m @>
        test <@ tradeOrderState.Status = TradeOrderStatus.Open @>
        test <@ tradeOrderChange = TradeOrderChange.Update @>

        // New orders
        let tradeOrderState, tradeOrderChange = output.TradeOrders.[incomingOrderHashes.[0]]
        test <@ tradeOrderState.IsExecutable = true @>
        test <@ tradeOrderState.AmountFilled = tradeOrderState.Amount @>
        test <@ tradeOrderState.Status = TradeOrderStatus.Filled @>
        test <@ tradeOrderChange = TradeOrderChange.Remove @>

        let tradeOrderState, tradeOrderChange = output.TradeOrders.[incomingOrderHashes.[1]]
        test <@ tradeOrderState.IsExecutable = true @>
        test <@ tradeOrderState.AmountFilled = tradeOrderState.Amount @>
        test <@ tradeOrderState.Status = TradeOrderStatus.Filled @>
        test <@ tradeOrderChange = TradeOrderChange.Remove @>

        (* TODO DSX: Enable upon implementing settlement
        test <@ output.Holdings.[accountHash1, baseAssetHash].Balance.Value = 900m @>
        test <@ output.Holdings.[accountHash2, baseAssetHash].Balance.Value = 100m @>
        test <@ output.Holdings.[accountHash1, quoteAssetHash].Balance.Value = 500m @>
        test <@ output.Holdings.[accountHash2, quoteAssetHash].Balance.Value = 1500m @>
        *)
