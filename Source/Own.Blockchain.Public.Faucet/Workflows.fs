namespace Chainium.Blockchain.Public.Faucet

open System
open System.Collections.Concurrent
open Giraffe
open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Core
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Crypto
open Chainium.Blockchain.Public.Faucet.Dtos

module Workflows =

    let private chxClaimQueue = new ConcurrentQueue<ChainiumAddress>()
    let private assetClaimQueue = new ConcurrentQueue<AccountHash>()

    let private getEntriesFromQueue distributionBatchSize (queue : ConcurrentQueue<'T>) =
        let rec getEntries (entries : 'T list) =
            if entries.Length < distributionBatchSize then
                match queue.TryDequeue() with
                | true, address ->
                    let entries = address :: entries
                    getEntries entries
                | _ -> entries
            else
                entries
            |> List.rev

        getEntries []

    let private createTxEnvelopeJson rawTx (Signature signature) =
        sprintf
            """
            {
                tx: "%s",
                signature: "%s"
            }
            """
            (Convert.ToBase64String rawTx)
            signature

    let private composeAndSubmitTx
        (getAddressNonce : ChainiumAddress -> Nonce)
        (submitTx : string -> string)
        (signTx : PrivateKey -> byte[] -> Signature)
        (faucetSupplyHolderPrivateKey : PrivateKey)
        (faucetSupplyHolderAddress : ChainiumAddress)
        (ChxAmount txFee)
        distributionBatchSize
        createAction
        queue
        =

        match getEntriesFromQueue distributionBatchSize queue with
        | [] -> None
        | entries ->
            let (Nonce addressNonce) = getAddressNonce faucetSupplyHolderAddress

            let actions =
                entries
                |> List.map createAction

            let rawTx =
                sprintf """{Nonce: %i, Fee: %s, Actions: [%s]}"""
                    (addressNonce + 1L)
                    (txFee.ToString())
                    (String.Join(", ", actions))
                |> Conversion.stringToBytes

            let signature = signTx faucetSupplyHolderPrivateKey rawTx

            createTxEnvelopeJson rawTx signature
            |> submitTx
            |> Some

    let claimChx (ChxAmount claimableAmount) (requestDto : ClaimChxRequestDto) : Result<string, AppErrors> =
        let address = ChainiumAddress requestDto.ChainiumAddress

        if chxClaimQueue |> Seq.contains address then
            Result.appError "Address is already in the queue."
        else
            chxClaimQueue.Enqueue address

            claimableAmount.ToString()
            |> sprintf "Address is added to the queue and will soon receive %s CHX."
            |> Ok

    let claimAsset (AssetAmount claimableAmount) (requestDto : ClaimAssetRequestDto) : Result<string, AppErrors> =
        let account = AccountHash requestDto.AccountHash

        if assetClaimQueue |> Seq.contains account then
            Result.appError "Account is already in the queue."
        else
            assetClaimQueue.Enqueue account

            claimableAmount.ToString()
            |> sprintf "Account is added to the queue and will soon receive %s unit(s) of asset."
            |> Ok

    let distributeChx
        getAddressNonce
        submitTx
        signTx
        faucetSupplyHolderPrivateKey
        faucetSupplyHolderAddress
        txFee
        distributionBatchSize
        (ChxAmount chxAmountPerAddress)
        =

        let createAction (ChainiumAddress address) =
            sprintf """{ActionType: "TransferChx", ActionData: {RecipientAddress: "%s", Amount: %s}}"""
                address
                (chxAmountPerAddress.ToString())

        composeAndSubmitTx
            getAddressNonce
            submitTx
            signTx
            faucetSupplyHolderPrivateKey
            faucetSupplyHolderAddress
            txFee
            distributionBatchSize
            createAction
            chxClaimQueue

    let distributeAsset
        getAddressNonce
        submitTx
        signTx
        faucetSupplyHolderPrivateKey
        faucetSupplyHolderAddress
        txFee
        distributionBatchSize
        (AssetAmount assetAmountPerAddress)
        (AssetHash faucetSupplyHolderAssetHash)
        (AccountHash faucetSupplyHolderAccountHash)
        =

        let createAction (AccountHash accountHash) =
            sprintf
                """
                {
                    ActionType: "TransferAsset",
                    ActionData: {
                        FromAccount: "%s",
                        ToAccount: "%s",
                        AssetHash: "%s",
                        Amount: %s
                    }
                }
                """
                faucetSupplyHolderAccountHash
                accountHash
                faucetSupplyHolderAssetHash
                (assetAmountPerAddress.ToString())

        composeAndSubmitTx
            getAddressNonce
            submitTx
            signTx
            faucetSupplyHolderPrivateKey
            faucetSupplyHolderAddress
            txFee
            distributionBatchSize
            createAction
            assetClaimQueue
