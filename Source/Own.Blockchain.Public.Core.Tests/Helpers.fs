namespace Own.Blockchain.Public.Core.Tests

open System
open Newtonsoft.Json
open Own.Common
open Own.Blockchain.Common
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Core.Dtos
open Own.Blockchain.Public.Crypto

module Helpers =

    let randomString () = Guid.NewGuid().ToString("N")

    let minTxActionFee = ChxAmount 0.001m

    let extractActionData<'T> = function
        | TransferChx action -> box action :?> 'T
        | TransferAsset action -> box action :?> 'T
        | CreateAssetEmission action -> box action :?> 'T
        | CreateAccount -> failwith "CreateAccount TxAction has no data to extract."
        | CreateAsset -> failwith "CreateAsset TxAction has no data to extract."
        | SetAccountController action -> box action :?> 'T
        | SetAssetController action -> box action :?> 'T
        | SetAssetCode action -> box action :?> 'T
        | ConfigureValidator action -> box action :?> 'T
        | DelegateStake action -> box action :?> 'T
        | SubmitVote action -> box action :?> 'T
        | SubmitVoteWeight action -> box action :?> 'T
        | SetAccountEligibility action -> box action :?> 'T
        | ChangeKycControllerAddress action -> box action :?> 'T
        | AddKycController action -> box action :?> 'T
        | RemoveKycController action -> box action :?> 'T

    let newPendingTxInfo
        (txHash : TxHash)
        (senderAddress : BlockchainAddress)
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
        (BlockchainAddress senderAddress)
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

        let (Signature signature) = Signing.signHash sender.PrivateKey txHash.Value

        let txEnvelopeDto =
            {
                Tx = rawTx |> Convert.ToBase64String
                Signature = signature
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
