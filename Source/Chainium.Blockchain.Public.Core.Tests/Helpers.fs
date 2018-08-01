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

    let minTxActionFee = ChxAmount 0.001M

    let addressToString (ChainiumAddress a) = a

    let extractActionData<'T> = function
        | TransferChx action -> box action :?> 'T
        | TransferAsset action -> box action :?> 'T
        | CreateAssetEmission action -> box action :?> 'T
        | CreateAccount -> failwith "CreateAccount TxAction has no data to extract."
        | CreateAsset -> failwith "CreateAsset TxAction has no data to extract."
        | SetAccountController action -> box action :?> 'T
        | SetAssetController action -> box action :?> 'T
        | SetAssetCode action -> box action :?> 'T
        | SetValidatorNetworkAddress action -> box action :?> 'T
        | SetStake action -> box action :?> 'T

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
        (ChainiumAddress senderAddress)
        (nonce : int64)
        (fee : decimal)
        (actions : obj list)
        =

        let json =
            sprintf
                """
                {
                    SenderAddress: "%s",
                    Nonce: %i,
                    Fee: %s,
                    Actions: %s
                }
                """
                senderAddress
                nonce
                (fee.ToString())
                (JsonConvert.SerializeObject(actions))

        Conversion.stringToBytes json

    let newTx
        (sender : WalletInfo)
        (Nonce nonce)
        (ChxAmount fee)
        (actions : obj list)
        =

        let rawTx = newRawTxDto sender.Address nonce fee actions

        let txHash =
            rawTx |> Hashing.hash |> TxHash

        let signature =
            Signing.signMessage sender.PrivateKey rawTx

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
