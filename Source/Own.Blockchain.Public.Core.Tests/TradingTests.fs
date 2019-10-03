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

    let private matchOrders blockNumber senderWallet holdings oldOrders newOrders =
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

        let oldOrders = oldOrders |> Map.ofList

        // PREPARE TX
        let actionFee = ChxAmount 0.01m

        let txs =
            newOrders
            |> List.mapi (fun i actions ->
                let nonce = int64 i + 1L |> Nonce
                actions
                |> List.map (fun (_, action) -> { ActionType = "PlaceTradeOrder"; ActionData = action })
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
            oldOrders
            |> flip Map.tryFind

        let getTradeOrdersFromStorage _ =
            oldOrders
            |> Map.toList
            |> List.map Mapping.tradeOrderStateToInfo

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


    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Tests
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Fact>]
    let ``Trading.matchTradeOrders - LIMIT BUY`` () =
        // ARRANGE
        let senderWallet = Signing.generateWallet ()
        let accountHash1 = Helpers.randomHash () |> AccountHash
        let accountHash2 = Helpers.randomHash () |> AccountHash
        let baseAssetHash = Helpers.randomHash () |> AssetHash
        let quoteAssetHash = Helpers.randomHash () |> AssetHash

        let createTradeOrderHash = createTradeOrderHash senderWallet.Address
        let createTradeOrderState = createTradeOrderState (baseAssetHash, quoteAssetHash)
        let placeOrder = placeOrder (baseAssetHash.Value, quoteAssetHash.Value)

        let holdings =
            [
                accountHash1, baseAssetHash, 1000m, true
                accountHash2, quoteAssetHash, 2000m, true
            ]

        let oldOrders =
            [
                Helpers.randomHash () |> TradeOrderHash,
                    createTradeOrderState
                        (1L, 1, 1s)
                        accountHash1
                        (TradeOrderSide.Sell, 100m, TradeOrderType.Limit, 5m, 0m, 0m, false,
                            TradeOrderTimeInForce.GoodTilCancelled)
                        (true, 0m, TradeOrderStatus.Open)
            ]

        let newOrders =
            [
                [ // TX1
                    createTradeOrderHash 1L 1s,
                        placeOrder accountHash2.Value ("BUY", 100m, "LIMIT", 5m, 0m, 0m, false, "GTC")
                ]
            ]

        let oldOrderHashes = oldOrders |> List.map fst
        let newOrderHashes = newOrders |> List.concat |> List.map fst

        // ACT
        let output = matchOrders (BlockNumber 2L) senderWallet holdings oldOrders newOrders

        // ASSERT
        test <@ output.TxResults.Count = newOrders.Length @>
        for txResult in output.TxResults |> Map.values do
            test <@ txResult.Status = Success @>

        test <@ output.TradeOrders.Count = 2 @>

        let tradeOrderState, tradeOrderChange = output.TradeOrders.[oldOrderHashes.[0]]
        test <@ tradeOrderState.Status = TradeOrderStatus.Filled @>
        test <@ tradeOrderChange = TradeOrderChange.Remove @>

        let tradeOrderState, tradeOrderChange = output.TradeOrders.[newOrderHashes.[0]]
        test <@ tradeOrderState.Status = TradeOrderStatus.Filled @>
        test <@ tradeOrderChange = TradeOrderChange.Remove @>
