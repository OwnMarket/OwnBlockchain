namespace Chainium.Blockchain.Public.Core.Tests

open System
open Chainium.Common
open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Core
open Chainium.Blockchain.Public.Core.DomainTypes

module Helpers =

    let newPendingTxInfo
        (TxHash txHash)
        (ChainiumAddress senderAddress)
        (Nonce nonce)
        (ChxAmount fee)
        (appearanceOrder : int64)
        =

        {
            PendingTxInfo.TxHash = TxHash txHash
            Sender = ChainiumAddress senderAddress
            Nonce = Nonce nonce
            Fee = ChxAmount fee
            AppearanceOrder = appearanceOrder
        }
