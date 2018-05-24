namespace Chainium.Blockchain.Public.Core.Tests

open System
open System.Text
open Newtonsoft.Json
open Chainium.Common
open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Core
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Core.Dtos
open Chainium.Blockchain.Public.Crypto

module Helpers =

    let newPendingTxInfo
        (txHash : TxHash)
        (senderAddress : ChainiumAddress)
        (nonce : Nonce)
        (fee : ChxAmount)
        (appearanceOrder : int64)
        =

        {
            PendingTxInfo.TxHash = txHash
            Sender = senderAddress
            Nonce = nonce
            Fee = fee
            AppearanceOrder = appearanceOrder
        }

    let newTx
        (privateKey : PrivateKey)
        (Nonce nonce)
        (ChxAmount fee)
        (actions : obj list)
        =

        let json =
            sprintf
                """
                {
                    Nonce: %i,
                    Fee: %s,
                    Actions: %s
                }
                """
                nonce
                (fee.ToString())
                (JsonConvert.SerializeObject(actions))

        let rawTx =
            json
            |> Encoding.UTF8.GetBytes

        let txHash =
            rawTx |> Hashing.hash |> TxHash

        let signature =
            Signing.signMessage privateKey rawTx

        let txEnvelopeDto =
            {
                Tx = rawTx |> Convert.ToBase64String
                V = signature.V
                R = signature.R
                S = signature.S
            }

        (txHash, txEnvelopeDto)

    let mockGetChxBalanceState (state : (ChainiumAddress * (ChxAmount * Nonce)) list) =
        let state =
            state
            |> List.map (fun (address, (chxAmount, nonce)) ->
                let chxBalanceState =
                    {
                        ChxBalanceState.Amount = chxAmount
                        Nonce = nonce
                    }
                (address, chxBalanceState)
            )
            |> Map.ofList

        fun address -> state.[address]

    let mockGetHoldingState (state : ((AccountHash * EquityID) * (decimal * int64)) list) =
        let state =
            state
            |> List.map (fun (key, (equityAmount, nonce)) ->
                let holdingState =
                    {
                        HoldingState.Amount = EquityAmount equityAmount
                        Nonce = Nonce nonce
                    }
                (key, holdingState)
            )
            |> Map.ofList

        fun key -> state.[key]
