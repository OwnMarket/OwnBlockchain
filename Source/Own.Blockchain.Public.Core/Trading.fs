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
