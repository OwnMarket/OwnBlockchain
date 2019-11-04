namespace Own.Blockchain.Public.Core

open Own.Common.FSharp
open Own.Blockchain.Common
open Own.Blockchain.Public.Core
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Core.Dtos

module Trading =

    let getTradeOrderBook
        (getExecutableTradeOrders : AssetHash * AssetHash -> TradeOrderInfoDto list)
        (baseAssetHash : AssetHash, quoteAssetHash : AssetHash)
        : TradeOrderBook
        =

        let buyOrders, sellOrders =
            getExecutableTradeOrders (baseAssetHash, quoteAssetHash)
            |> List.map Mapping.tradeOrderInfoFromDto
            |> List.partition (fun o -> o.Side = Buy)

        {
            BuyOrders = buyOrders |> List.sortBy (fun o -> -o.LimitPrice.Value, o.BlockNumber, o.TradeOrderHash)
            SellOrders = sellOrders |> List.sortBy (fun o -> o.LimitPrice.Value, o.BlockNumber, o.TradeOrderHash)
        }

    let getTopOrders
        (buyOrders : (TradeOrderHash * TradeOrderState) list, sellOrders : (TradeOrderHash * TradeOrderState) list)
        =

        sellOrders
        |> List.filter (fun (_, o) -> o.IsExecutable && o.Status = TradeOrderStatus.Open)
        |> List.sortBy (fun (_, o) ->
            o.ExecOrderType <> ExecTradeOrderType.Market, // false: MARKET orders on top
            o.LimitPrice, // Lowest price on top
            o.Time // Oldest order on top
        )
        |> List.tryHead
        |> Option.bind (fun (topSellOrderHash, topSellOrder) ->
            buyOrders
            |> List.filter (fun (_, o) -> o.IsExecutable && o.Status = TradeOrderStatus.Open)
            |> List.sortBy (fun (_, o) ->
                o.ExecOrderType <> ExecTradeOrderType.Market, // false: MARKET orders on top
                -o.LimitPrice.Value, // Highest price on top
                o.Time // Oldest order on top
            )
            |> List.skipWhile (fun (_, o) ->
                // Don't match MARKET SELL with MARKET BUY
                topSellOrder.ExecOrderType = ExecTradeOrderType.Market && o.ExecOrderType = ExecTradeOrderType.Market
            )
            |> List.tryHead
            |> Option.bind (fun (topBuyOrderHash, topBuyOrder) ->
                if topBuyOrder.ExecOrderType = ExecTradeOrderType.Market
                    || topSellOrder.ExecOrderType = ExecTradeOrderType.Market
                    || topBuyOrder.LimitPrice >= topSellOrder.LimitPrice
                then
                    Some ((topBuyOrderHash, topBuyOrder), (topSellOrderHash, topSellOrder))
                else
                    None
            )
        )

    let updateFilledOrder
        (setTradeOrder : TradeOrderHash * TradeOrderState * TradeOrderChange -> unit)
        (tradeOrderHash : TradeOrderHash, tradeOrderState : TradeOrderState, amountToFill : AssetAmount)
        =

        let tradeOrderStatus =
            if tradeOrderState.AmountRemaining > amountToFill then
                tradeOrderState.Status
            else
                TradeOrderStatus.Filled

        let tradeOrderChange =
            if tradeOrderStatus = TradeOrderStatus.Open then
                TradeOrderChange.Update
            else
                TradeOrderChange.Remove

        let tradeOrderState =
            { tradeOrderState with
                AmountFilled = tradeOrderState.AmountFilled + amountToFill
                Status = tradeOrderStatus
            }

        setTradeOrder (tradeOrderHash, tradeOrderState, tradeOrderChange)

        tradeOrderState

    let settleTrade
        (getHolding : AccountHash * AssetHash -> HoldingState)
        (setHolding : AccountHash * AssetHash * HoldingState -> unit)
        (buyerAccountHash, sellerAccountHash, baseAssetHash, quoteAssetHash, amount : AssetAmount, price : AssetAmount)
        =

        let baseAssetAmount = amount
        let quoteAssetAmount = (amount * price).Rounded

        [
            sellerAccountHash, baseAssetHash, baseAssetAmount * -1m
            buyerAccountHash, baseAssetHash, baseAssetAmount
            buyerAccountHash, quoteAssetHash, quoteAssetAmount * -1m
            sellerAccountHash, quoteAssetHash, quoteAssetAmount
        ]
        |> List.iter (fun (accountHash, assetHash, balanceChange) ->
            let holding = getHolding (accountHash, assetHash)
            let newBalance = holding.Balance + balanceChange

            if newBalance.Value < 0m
                || newBalance.Value > Utils.maxBlockchainNumeric
                || not (Utils.isRounded7 newBalance.Value)
            then
                failwithf "Invalid asset holding balance (%A / %A): %A" accountHash assetHash newBalance

            setHolding (accountHash, assetHash, { holding with Balance = newBalance })
        )

    let processTopOrders
        (getHolding : AccountHash * AssetHash -> HoldingState)
        (setHolding : AccountHash * AssetHash * HoldingState -> unit)
        (setTradeOrder : TradeOrderHash * TradeOrderState * TradeOrderChange -> unit)
        ((buyOrderHash, buyOrder : TradeOrderState), (sellOrderHash, sellOrder : TradeOrderState))
        =

        if buyOrder.AmountRemaining.Value <= 0m || sellOrder.AmountRemaining.Value <= 0m then
            failwithf "Remaining trade order amount must be greater than zero: %A" [buyOrder; sellOrder]

        let price =
            match buyOrder.ExecOrderType, sellOrder.ExecOrderType with
            | ExecTradeOrderType.Market, ExecTradeOrderType.Market ->
                // TODO DSX: Use the last trade price?
                failwithf "Matching two MARKET orders not implemented yet: %A" [buyOrder; sellOrder]
            | ExecTradeOrderType.Market, ExecTradeOrderType.Limit ->
                sellOrder.LimitPrice
            | ExecTradeOrderType.Limit, ExecTradeOrderType.Market ->
                buyOrder.LimitPrice
            | ExecTradeOrderType.Limit, ExecTradeOrderType.Limit ->
                if buyOrder.Time > sellOrder.Time then
                    sellOrder.LimitPrice
                elif buyOrder.Time < sellOrder.Time then
                    buyOrder.LimitPrice
                else
                    failwithf "Orders have same time: %A" [buyOrder; sellOrder] // O_o

        if price.Value <= 0m then
            failwithf "Trade price must be a positive number: %M" price.Value

        let buyerQuoteAssetHolding = getHolding (buyOrder.AccountHash, buyOrder.QuoteAssetHash)

        if buyerQuoteAssetHolding.Balance.Value < 0m then
            failwithf "Buyer's (%s) quote asset (%s) balance is negative: %M"
                buyOrder.AccountHash.Value
                buyOrder.QuoteAssetHash.Value
                buyerQuoteAssetHolding.Balance.Value

        let amountToFill =
            [
                sellOrder.AmountRemaining
                buyOrder.AmountRemaining
                (buyerQuoteAssetHolding.Balance / price).Rounded
            ]
            |> List.min

        if buyerQuoteAssetHolding.Balance < amountToFill * price then
            failwithf "Amount to fill (%M) is greater than buyer's (%s) quote asset (%s) balance: %M"
                amountToFill.Value
                buyOrder.AccountHash.Value
                buyOrder.QuoteAssetHash.Value
                buyerQuoteAssetHolding.Balance.Value

        if amountToFill.Value = 0m then
            setTradeOrder (
                buyOrderHash,
                { buyOrder with
                    Status = TradeOrderStatus.Cancelled TradeOrderCancelReason.InsufficientQuoteAssetBalance
                },
                TradeOrderChange.Remove
            )

            None
        else
            let buyOrder = updateFilledOrder setTradeOrder (buyOrderHash, buyOrder, amountToFill)
            let sellOrder = updateFilledOrder setTradeOrder (sellOrderHash, sellOrder, amountToFill)

            // Settle trade
            settleTrade
                getHolding
                setHolding
                (
                    buyOrder.AccountHash,
                    sellOrder.AccountHash,
                    sellOrder.BaseAssetHash,
                    sellOrder.QuoteAssetHash,
                    amountToFill,
                    price
                )

            {
                Trade.Direction = if buyOrder.Time > sellOrder.Time then Buy else Sell
                BuyOrderHash = buyOrderHash
                SellOrderHash = sellOrderHash
                Amount = amountToFill
                Price = price
            }
            |> Some

    let updateStopOrders
        (getTradeOrders : AssetHash * AssetHash -> (TradeOrderHash * TradeOrderState) list)
        (setTradeOrder : TradeOrderHash * TradeOrderState * TradeOrderChange -> unit)
        (baseAssetHash : AssetHash, quoteAssetHash : AssetHash)
        price
        =

        getTradeOrders (baseAssetHash, quoteAssetHash)
        |> List.toArray
        |> Array.Parallel.iter (fun (h, s) ->
            if not s.IsExecutable && s.IsStopOrder then
                if s.Side = Buy && price >= s.StopPrice || s.Side = Sell && price <= s.StopPrice then
                    setTradeOrder (h, { s with IsExecutable = true }, TradeOrderChange.Update)
                elif s.IsTrailingStopOrder then
                    let trailingOffset =
                        if s.TrailingOffsetIsPercentage then
                            s.TrailingOffset * price / 100m
                        else
                            s.TrailingOffset

                    let expectedStopPrice =
                        match s.Side with
                        | Buy -> price + trailingOffset
                        | Sell -> price + trailingOffset * -1m
                    if expectedStopPrice.Value <= 0m then
                        failwithf "expectedStopPrice must be greater than zero: %M" expectedStopPrice.Value

                    let expectedLimitPrice =
                        match s.ExecOrderType with
                        | ExecTradeOrderType.Market -> AssetAmount 0m
                        | ExecTradeOrderType.Limit ->
                            let limitOffset = s.LimitPrice - s.StopPrice
                            expectedStopPrice + limitOffset
                    if expectedLimitPrice.Value <= 0m && s.ExecOrderType = ExecTradeOrderType.Limit then
                        failwithf "expectedLimitPrice must be greater than zero: %M" expectedLimitPrice.Value

                    if s.Side = Buy && (expectedStopPrice < s.StopPrice || expectedLimitPrice < s.LimitPrice)
                        || s.Side = Sell && (expectedStopPrice > s.StopPrice || expectedLimitPrice > s.LimitPrice)
                    then
                        setTradeOrder (
                            h,
                            { s with StopPrice = expectedStopPrice; LimitPrice = expectedLimitPrice },
                            TradeOrderChange.Update
                        )
        )

    let matchTradeOrders
        (getTradeOrders : AssetHash * AssetHash -> (TradeOrderHash * TradeOrderState) list)
        (setTradeOrder : TradeOrderHash * TradeOrderState * TradeOrderChange -> unit)
        (getHolding : AccountHash * AssetHash -> HoldingState)
        (setHolding : AccountHash * AssetHash * HoldingState -> unit)
        (baseAssetHash : AssetHash, quoteAssetHash : AssetHash)
        =

        let processTopOrders = processTopOrders getHolding setHolding setTradeOrder
        let updateStopOrders = updateStopOrders getTradeOrders setTradeOrder (baseAssetHash, quoteAssetHash)

        // Match orders
        let trades =
            Seq.initInfinite (fun _ ->
                getTradeOrders (baseAssetHash, quoteAssetHash)
                |> List.partition (fun (_, o) -> o.Side = Buy)
                |> getTopOrders
            )
            |> Seq.takeWhile Option.isSome
            |> Seq.choose (Option.bind processTopOrders)
            |> Seq.tap (fun trade ->
                Log.successf "TRADE: %s %M @ %M (%s/%s)\n\tBuy order: %s\n\tSell order: %s"
                    trade.Direction.CaseName
                    trade.Amount.Value
                    trade.Price.Value
                    baseAssetHash.Value
                    quoteAssetHash.Value
                    trade.BuyOrderHash.Value
                    trade.SellOrderHash.Value

                updateStopOrders trade.Price
            )
            |> Seq.toList

        // Remove IOC orders
        getTradeOrders (baseAssetHash, quoteAssetHash)
        |> List.filter (fun (_, s) ->
            s.TimeInForce = ImmediateOrCancel
            && s.IsExecutable
            && s.Status = TradeOrderStatus.Open
        )
        |> List.iter (fun (h, s) ->
            setTradeOrder (
                h,
                { s with Status = TradeOrderStatus.Cancelled TradeOrderCancelReason.TriggeredByTimeInForce },
                TradeOrderChange.Remove
            )
        )

        trades

    let cancelExpiredTradeOrders
        (getExpiredTradeOrders : Timestamp -> (TradeOrderHash * TradeOrderState) list)
        (setTradeOrder : TradeOrderHash * TradeOrderState * TradeOrderChange -> unit)
        (blockTimestamp : Timestamp)
        =

        getExpiredTradeOrders blockTimestamp
        |> List.filter (fun (_, s) -> s.TimeInForce = GoodTilCancelled && s.Status = TradeOrderStatus.Open)
        |> List.iter (fun (h, s) ->
            if s.ExpirationTimestamp > blockTimestamp then
                failwithf "Trade order %s didn't expire yet (%i): %A" h.Value blockTimestamp.Value s
            setTradeOrder (
                h,
                { s with Status = TradeOrderStatus.Cancelled TradeOrderCancelReason.Expired },
                TradeOrderChange.Remove
            )
        )
