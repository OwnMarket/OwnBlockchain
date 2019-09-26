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
            |> List.partition (fun o -> o.Side = TradeOrderSide.Buy)

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
            o.ExecOrderType <> ExecTradeOrderType.Market, // ...MARKET orders (false) on top
            o.LimitPrice, // Lowest price on top
            o.Time // Oldest order on top
        )
        |> List.tryHead
        |> Option.bind (fun (topSellOrderHash, topSellOrder) ->
            buyOrders
            |> List.filter (fun (_, o) -> o.IsExecutable && o.Status = TradeOrderStatus.Open)
            |> List.sortBy (fun (_, o) ->
                o.ExecOrderType <> ExecTradeOrderType.Market, // ...MARKET orders (false) on top
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

    let processTopOrders
        (getHolding : AccountHash * AssetHash -> HoldingState)
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

            {
                Trade.Direction = if buyOrder.Time > sellOrder.Time then TradeOrderSide.Buy else TradeOrderSide.Sell
                BuyOrder = buyOrderHash
                SellOrder = sellOrderHash
                Amount = amountToFill
                Price = price
            }
            |> Some

    let matchTradeOrders
        (getTradeOrders : AssetHash * AssetHash -> (TradeOrderHash * TradeOrderState) list)
        (setTradeOrder : TradeOrderHash * TradeOrderState * TradeOrderChange -> unit)
        (getHolding : AccountHash * AssetHash -> HoldingState)
        (baseAssetHash : AssetHash, quoteAssetHash : AssetHash)
        =

        let processTopOrders =
            processTopOrders
                getHolding
                setTradeOrder

        // Match orders
        let trades =
            Seq.initInfinite (fun _ ->
                getTradeOrders (baseAssetHash, quoteAssetHash)
                |> List.partition (fun (_, o) -> o.Side = TradeOrderSide.Buy)
                |> getTopOrders
            )
            |> Seq.takeWhile Option.isSome
            |> Seq.choose (Option.bind processTopOrders)

        trades |> Seq.iter (Log.noticef "TRADE: %A") // TODO DSX: Include trades in the block

        // Remove IOC orders
        getTradeOrders (baseAssetHash, quoteAssetHash)
        |> List.filter (fun (_, s) ->
            s.TimeInForce = TradeOrderTimeInForce.ImmediateOrCancel
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
