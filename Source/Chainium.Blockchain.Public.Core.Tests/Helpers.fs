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

    let addressToString (ChainiumAddress a) = a

    let newPendingTxInfo
        (txHash : TxHash)
        (senderAddress : ChainiumAddress)
        (nonce : Nonce)
        (fee : ChxAmount)
        (actionCount : int16)
        (appearanceOrder : int64)
        =

        {
            PendingTxInfo.TxHash = txHash
            Sender = senderAddress
            Nonce = nonce
            Fee = fee
            ActionCount = actionCount
            AppearanceOrder = appearanceOrder
        }

    let newRawTxDto
        (nonce : int64)
        (fee : decimal)
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

        Encoding.UTF8.GetBytes json

    let newTx
        (privateKey : PrivateKey)
        (Nonce nonce)
        (ChxAmount fee)
        (actions : obj list)
        =

        let rawTx = newRawTxDto nonce fee actions

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

    let verifyMerkleProofs (MerkleTreeRoot merkleRoot) leafs =
        let leafs = leafs |> List.map Hashing.decode

        // Performance is not priority in unit tests, so avoid exposing hashBytes out of Crypto assembly.
        let hashBytes = Hashing.hash >> Hashing.decode

        [
            for leaf in leafs ->
                MerkleTree.calculateProof hashBytes leafs leaf
                |> MerkleTree.verifyProof hashBytes (Hashing.decode merkleRoot) leaf
        ]
