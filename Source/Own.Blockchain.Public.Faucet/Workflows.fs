namespace Own.Blockchain.Public.Faucet

open System
open System.Collections.Concurrent
open Own.Common.FSharp
open Own.Blockchain.Common
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Faucet.Dtos

module Workflows =

    let private chxClaimQueue = new ConcurrentQueue<BlockchainAddress>()
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
        (getAddressNonce : BlockchainAddress -> Nonce)
        (submitTx : string -> string)
        (createHash : byte[] -> string)
        (signHash : PrivateKey -> string -> Signature)
        (faucetSupplyHolderPrivateKey : PrivateKey)
        (faucetSupplyHolderAddress : BlockchainAddress)
        (ChxAmount actionFee)
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
                sprintf """{SenderAddress: "%s", Nonce: %i, ActionFee: %s, Actions: [%s]}"""
                    faucetSupplyHolderAddress.Value
                    (addressNonce + 1L)
                    (actionFee.ToString())
                    (String.Join(", ", actions))
                |> Conversion.stringToBytes

            let txHash = createHash rawTx

            let signature = signHash faucetSupplyHolderPrivateKey txHash

            createTxEnvelopeJson rawTx signature
            |> submitTx
            |> Some

    let claimChx
        isValidBlockchainAddress
        (ChxAmount claimableAmount)
        (requestDto : ClaimChxRequestDto)
        : Result<string, AppErrors>
        =

        let address = BlockchainAddress requestDto.BlockchainAddress

        if requestDto.BlockchainAddress.IsNullOrWhiteSpace() then
            Result.appError "BlockchainAddress is missing"
        elif not (isValidBlockchainAddress address) then
            Result.appError "Invalid blockchain address"
        elif chxClaimQueue |> Seq.contains address then
            Result.appError "Address is already in the queue"
        else
            chxClaimQueue.Enqueue address

            claimableAmount.ToString()
            |> sprintf "Address is added to the queue and will soon receive %s CHX"
            |> Ok

    let claimAsset
        isValidHash
        (AssetAmount claimableAmount)
        (requestDto : ClaimAssetRequestDto)
        : Result<string, AppErrors>
        =

        let account = AccountHash requestDto.AccountHash

        if requestDto.AccountHash.IsNullOrWhiteSpace() then
            Result.appError "AccountHash is missing"
        elif not (isValidHash account.Value) then
            Result.appError "Invalid account hash"
        elif assetClaimQueue |> Seq.contains account then
            Result.appError "Account is already in the queue"
        else
            assetClaimQueue.Enqueue account

            claimableAmount.ToString()
            |> sprintf "Account is added to the queue and will soon receive %s unit(s) of asset"
            |> Ok

    let distributeChx
        getAddressNonce
        submitTx
        createHash
        signHash
        faucetSupplyHolderPrivateKey
        faucetSupplyHolderAddress
        actionFee
        distributionBatchSize
        (ChxAmount chxAmountPerAddress)
        =

        let createAction (BlockchainAddress address) =
            sprintf """{ActionType: "TransferChx", ActionData: {RecipientAddress: "%s", Amount: %s}}"""
                address
                (chxAmountPerAddress.ToString())

        composeAndSubmitTx
            getAddressNonce
            submitTx
            createHash
            signHash
            faucetSupplyHolderPrivateKey
            faucetSupplyHolderAddress
            actionFee
            distributionBatchSize
            createAction
            chxClaimQueue

    let distributeAsset
        getAddressNonce
        submitTx
        createHash
        signHash
        faucetSupplyHolderPrivateKey
        faucetSupplyHolderAddress
        actionFee
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
                        FromAccountHash: "%s",
                        ToAccountHash: "%s",
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
            createHash
            signHash
            faucetSupplyHolderPrivateKey
            faucetSupplyHolderAddress
            actionFee
            distributionBatchSize
            createAction
            assetClaimQueue
