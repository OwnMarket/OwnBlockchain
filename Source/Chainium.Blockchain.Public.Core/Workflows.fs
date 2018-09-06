﻿namespace Chainium.Blockchain.Public.Core

open System
open Chainium.Common
open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Core
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Core.Dtos
open Chainium.Blockchain.Public.Core.Events

module Workflows =

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Consensus
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let getTopValidators
        getTopValidatorsByStake
        totalSupply
        quorumSupplyPercent
        maxValidatorCount
        =

        totalSupply
        |> Consensus.calculateQuorumSupply quorumSupplyPercent
        |> Consensus.calculateValidatorThreshold maxValidatorCount
        |> getTopValidatorsByStake maxValidatorCount
        |> List.map Mapping.validatorSnapshotFromDto

    let getActiveValidators getValidatorSnapshots =
        getValidatorSnapshots ()
        |> List.map Mapping.validatorSnapshotFromDto

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Blockchain
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let getAvailableChxBalance getChxBalanceState getTotalChxStaked senderAddress : ChxAmount =
        let chxBalance =
            senderAddress
            |> getChxBalanceState
            |> Option.map (Mapping.chxBalanceStateFromDto >> fun state -> state.Amount)
            |? ChxAmount 0m

        let chxStaked = getTotalChxStaked senderAddress

        chxBalance - chxStaked

    let initBlockchainState
        (getLastAppliedBlockNumber : unit -> BlockNumber option)
        (getBlock : BlockNumber -> Result<BlockEnvelopeDto, AppErrors>)
        saveBlock
        applyNewState
        decodeHash
        createHash
        createMerkleTree
        zeroHash
        zeroAddress
        genesisChxSupply
        genesisAddress
        genesisValidators
        =

        if getLastAppliedBlockNumber () = None then
            let genesisValidators =
                genesisValidators
                |> List.map (fun (ca, na) -> ChainiumAddress ca, {ValidatorState.NetworkAddress = na})
                |> Map.ofList

            let genesisState = Blocks.createGenesisState genesisChxSupply genesisAddress genesisValidators

            let genesisBlock =
                Blocks.createGenesisBlock
                    decodeHash createHash createMerkleTree zeroHash zeroAddress genesisState

            let genesisBlockExists =
                match getBlock genesisBlock.Header.Number >>= Blocks.extractBlockFromEnvelopeDto with
                | Ok genesisBlockFromDisk ->
                    if genesisBlockFromDisk <> genesisBlock then
                        failwith "Stored genesis block is invalid."
                    true
                | _ ->
                    false

            let genesisBlockDto = genesisBlock |> Mapping.blockToDto

            let blockInfoDto = Mapping.blockInfoDtoFromBlockHeaderDto genesisBlockDto.Header

            let result =
                result {
                    if not genesisBlockExists then
                        let! blockEnvelopeDto =
                            Serialization.serialize<BlockDto> genesisBlockDto
                            >>= fun blockBytes ->
                                {
                                    Block = blockBytes |> Convert.ToBase64String
                                    Signature = "" // TODO: Genesis block should be signed by genesis validators.
                                }
                                |> Ok
                        do! saveBlock genesisBlock.Header.Number blockEnvelopeDto
                    do! genesisState
                        |> Mapping.outputToDto
                        |> applyNewState blockInfoDto
                }

            match result with
            | Ok _ ->
                Log.info "Blockchain state initialized."
            | Error errors ->
                Log.appErrors errors
                failwith "Cannot initialize blockchain state."

    let createBlock
        getTx
        verifySignature
        isValidAddress
        getChxBalanceStateFromStorage
        getHoldingStateFromStorage
        getAccountStateFromStorage
        getAssetStateFromStorage
        getValidatorStateFromStorage
        getStakeStateFromStorage
        getTotalChxStakedFromStorage
        (getTopValidators : unit -> ValidatorSnapshot list)
        (getActiveValidators : unit -> ValidatorSnapshot list)
        getBlock
        decodeHash
        createHash
        createMerkleTree
        checkpointBlockCount
        minTxActionFee
        validatorAddress
        (previousBlockHash : BlockHash)
        blockNumber
        timestamp
        txSet
        =

        let getChxBalanceState = memoize (getChxBalanceStateFromStorage >> Option.map Mapping.chxBalanceStateFromDto)
        let getHoldingState = memoize (getHoldingStateFromStorage >> Option.map Mapping.holdingStateFromDto)
        let getAccountState = memoize (getAccountStateFromStorage >> Option.map Mapping.accountStateFromDto)
        let getAssetState = memoize (getAssetStateFromStorage >> Option.map Mapping.assetStateFromDto)
        let getValidatorState = memoize (getValidatorStateFromStorage >> Option.map Mapping.validatorStateFromDto)
        let getStakeState = memoize (getStakeStateFromStorage >> Option.map Mapping.stakeStateFromDto)
        let getTotalChxStaked = memoize getTotalChxStakedFromStorage

        result {
            let shouldCreateSnapshot = blockNumber |> fun (BlockNumber n) -> n % (int64 checkpointBlockCount) = 0L
            let activeValidators =
                if shouldCreateSnapshot then
                    getTopValidators ()
                else
                    getActiveValidators ()

            let output =
                txSet
                |> Processing.processTxSet
                    getTx
                    verifySignature
                    isValidAddress
                    decodeHash
                    createHash
                    getChxBalanceState
                    getHoldingState
                    getAccountState
                    getAssetState
                    getValidatorState
                    getStakeState
                    getTotalChxStaked
                    minTxActionFee
                    validatorAddress
                    blockNumber

            let output = { output with ValidatorSnapshots = activeValidators }

            let block =
                Blocks.assembleBlock
                    decodeHash
                    createHash
                    createMerkleTree
                    validatorAddress
                    blockNumber
                    timestamp
                    previousBlockHash
                    txSet
                    output

            return (block, output)
        }

    let proposeBlock
        createBlock
        getBlock
        getPendingTxs
        getChxBalanceStateFromStorage
        getAvailableChxBalanceFromStorage
        signBlock
        saveBlock
        applyNewState
        maxTxCountPerBlock
        addressFromPrivateKey
        validatorPrivateKey
        previousBlockNumber
        : Result<BlockCreatedEventData, AppErrors> option
        =

        let timestamp = Utils.getUnixTimestamp () |> Timestamp

        let getChxBalanceState = memoize (getChxBalanceStateFromStorage >> Option.map Mapping.chxBalanceStateFromDto)
        let getAvailableChxBalance = memoize getAvailableChxBalanceFromStorage

        let validatorAddress = validatorPrivateKey |> addressFromPrivateKey
        match
            Processing.getTxSetForNewBlock
                getPendingTxs
                getChxBalanceState
                getAvailableChxBalance
                maxTxCountPerBlock
            with
        | [] -> None // Nothing to process.
        | txSet ->
            result {
                let txSet =
                    txSet
                    |> Processing.orderTxSet

                let! previousBlock = getBlock previousBlockNumber >>= Blocks.extractBlockFromEnvelopeDto

                let! block, output =
                    createBlock
                        validatorAddress
                        previousBlock.Header.Hash
                        (previousBlock.Header.Number + 1L)
                        timestamp
                        txSet

                let outputDto = Mapping.outputToDto output
                let blockDto = Mapping.blockToDto block
                let blockInfoDto = Mapping.blockInfoDtoFromBlockHeaderDto blockDto.Header

                let! blockEnvelopeDto =
                    Serialization.serialize<BlockDto> blockDto
                    >>= fun blockBytes ->
                        let signature : Signature = signBlock validatorPrivateKey blockBytes
                        {
                            Block = blockBytes |> Convert.ToBase64String
                            Signature = signature |> fun (Signature s) -> s
                        }
                        |> Ok

                do! saveBlock block.Header.Number blockEnvelopeDto
                Synchronization.setLastAvailableBlockNumber block.Header.Number

                return { BlockCreatedEventData.BlockNumber = block.Header.Number }
            }
            |> Some

    let persistTxResults saveTxResult txResults =
        result {
            do! txResults
                |> Map.toList
                |> List.fold (fun result (txHash, txResult) ->
                    result
                    >>= fun _ -> saveTxResult (TxHash txHash) txResult
                ) (Ok ())
        }

    let applyBlock
        createBlock
        getBlock
        (getValidators : unit -> ValidatorInfoDto list)
        verifySignature
        persistTxResults
        saveBlock
        applyNewState
        blockNumber
        blockEnvelopeDto
        =
        Log.debugf ">>> applyBlock %A" blockNumber
        result {
            let blockProposer =
                getValidators ()
                |> List.map (fun v -> // TODO: Remove this once we start using validator snapshots
                    {
                        ValidatorSnapshot.ValidatorAddress = ChainiumAddress v.ValidatorAddress
                        NetworkAddress = v.NetworkAddress
                        TotalStake = ChxAmount 0m
                    }
                )
                |> Consensus.getBlockProposer blockNumber

            let! previousBlock =
                getBlock (blockNumber - 1L) >>= Blocks.extractBlockFromEnvelopeDto

            let! block =
                Blocks.getBlockDto
                    verifySignature
                    blockEnvelopeDto
                    blockProposer.ValidatorAddress
                |> Result.map Mapping.blockFromDto

            //if not (Blocks.isValidBlock decodeHash createHash createMerkleTree previousBlockHash block) then
            // TODO:

            let! createdBlock, output =
                createBlock
                    block.Header.Validator
                    previousBlock.Header.Hash
                    block.Header.Number
                    block.Header.Timestamp
                    block.TxSet

            let outputDto = Mapping.outputToDto output
            let createdBlockDto = Mapping.blockToDto createdBlock

            if block = createdBlock then
                let blockInfoDto = Mapping.blockInfoDtoFromBlockHeaderDto createdBlockDto.Header
                do! persistTxResults outputDto.TxResults
                do! applyNewState blockInfoDto outputDto
            else
                return!
                    blockNumber
                    |> fun (BlockNumber n) ->
                        sprintf "Applying of block %i didn't result in expected blockchain state." n
                    |> Result.appError
        }

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Network
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let propagateTx sendMessageToPeers networkAddress getTx (txHash : TxHash) =
        match getTx txHash with
        | Ok (txEnvelopeDto : TxEnvelopeDto) ->
            let peerMessage = GossipMessage {
                MessageId = Tx txHash
                // TODO: move it into network code
                SenderAddress = NetworkAddress networkAddress
                Data = txEnvelopeDto
            }

            peerMessage
            |> sendMessageToPeers
        | _ -> Log.errorf "Tx %s does not exist" (txHash |> fun (TxHash hash) -> hash)

    let propagateBlock
        sendMessageToPeers
        networkAddress
        getBlock
        (blockNumber : BlockNumber)
        =

        match getBlock blockNumber with
        | Ok (blockEnvelopeDto : BlockEnvelopeDto) ->
            let peerMessage = GossipMessage {
                MessageId = Block blockNumber
                // TODO: move it into network code
                SenderAddress = NetworkAddress networkAddress
                Data = blockEnvelopeDto
            }
            peerMessage
            |> sendMessageToPeers
        | _ -> Log.errorf "Block %i does not exist." (blockNumber |> fun (BlockNumber b) -> b)

    let processPeerMessage
        getTx
        getBlock
        getLastBlockNumber
        submitTx
        saveBlock
        respondToPeer
        peerMessage
        =

        let processTxFromPeer txHash data =
            let txEnvelopeDto = Serialization.deserializeJObject data
            match getTx txHash with
            | Ok _ -> None |> Ok
            | _ ->
                submitTx txEnvelopeDto
                |> Result.map (fun _ ->
                    {TxReceivedEventData.TxHash = txHash} |> TxReceived |> Some
                )

        let processBlockFromPeer blockNr data =
            match getBlock blockNr with
            | Ok _ -> None |> Ok
            | _ ->
                let blockEnvelopeDto = Serialization.deserializeJObject data
                match saveBlock blockNr blockEnvelopeDto with // TODO: Validate block
                | Ok () ->
                    Synchronization.setLastAvailableBlockNumber blockNr // TODO: Move into storeReceivedBlock workflow
                    {BlockCreatedEventData.BlockNumber = blockNr} |> BlockReceived |> Some |> Ok
                | _ -> Result.appError "Error saving received block"

        let processData messageId (data : obj) =
            match messageId with
            | Tx txHash -> processTxFromPeer txHash data
            | Block blockNr -> processBlockFromPeer blockNr data

        let processRequest messageId senderAddress =
            match messageId with
            | Tx txHash ->
                match getTx txHash with
                | Ok txEvenvelopeDto ->
                    ResponseDataMessage {
                        MessageId = messageId
                        Data = txEvenvelopeDto
                    }
                    |> respondToPeer senderAddress
                    Ok None
                | _ -> Result.appError (sprintf "Tx %A not found" txHash)

            | Block blockNr ->
                let blockNr =
                    match blockNr with
                    | BlockNumber -1L -> getLastBlockNumber()
                    | _ -> Some blockNr

                match blockNr with
                | Some blockNr ->
                    match getBlock blockNr with
                    | Ok blockEnvelopeDto ->
                        ResponseDataMessage {
                            MessageId = NetworkMessageId.Block blockNr
                            Data = blockEnvelopeDto
                        }
                        |> respondToPeer senderAddress
                        Ok None
                    | _ -> Result.appError (sprintf "Block %A not found" blockNr)
                | None -> Result.appError "Error retrieving last block"

        match peerMessage with
        | GossipDiscoveryMessage _ -> None
        | GossipMessage m -> processData m.MessageId m.Data |> Some
        | MulticastMessage m -> processData m.MessageId m.Data |> Some
        | RequestDataMessage m -> processRequest m.MessageId m.SenderAddress |> Some
        | ResponseDataMessage m -> processData m.MessageId m.Data |> Some

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // API
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let submitTx
        verifySignature
        isValidAddress
        createHash
        getAvailableChxBalance
        getTotalFeeForPendingTxs
        saveTx
        saveTxToDb
        minTxActionFee
        txEnvelopeDto
        : Result<TxReceivedEventData, AppErrors>
        =

        result {
            let! txEnvelope = Validation.validateTxEnvelope txEnvelopeDto
            let! senderAddress = Validation.verifyTxSignature verifySignature txEnvelope
            let txHash = txEnvelope.RawTx |> createHash |> TxHash

            let! txDto = Serialization.deserializeTx txEnvelope.RawTx
            let! tx = Validation.validateTx isValidAddress minTxActionFee senderAddress txHash txDto

            do!
                Validation.checkIfBalanceCanCoverFees
                    getAvailableChxBalance
                    getTotalFeeForPendingTxs
                    senderAddress
                    tx.TotalFee

            do! saveTx txHash txEnvelopeDto
            do! tx
                |> Mapping.txToTxInfoDto
                |> saveTxToDb

            return { TxHash = txHash }
        }

    let getTxApi
        getTx
        verifySignature
        getTxResult
        (txHash : TxHash)
        : Result<GetTxApiResponseDto, AppErrors>
        =

        result {
            let! txEnvelope =
                getTx txHash
                |> Result.map Mapping.txEnvelopeFromDto

            let! txDto = Serialization.deserializeTx txEnvelope.RawTx

            let! senderAddress = Validation.verifyTxSignature verifySignature txEnvelope

            let txResult =
                match getTxResult txHash with
                | Ok result -> Some result
                | _ -> None

            return Mapping.txToGetTxApiResponseDto txHash senderAddress txDto txResult
        }

    let getBlockApi
        getBlock
        (blockNumber : BlockNumber)
        : Result<GetBlockApiResponseDto, AppErrors>
        =

        match getBlock blockNumber >>= Blocks.extractBlockFromEnvelopeDto with
        | Ok block ->
            block
            |> Mapping.blockToDto
            |> Mapping.blockTxsToGetBlockApiResponseDto
            |> Ok
        | _ -> Result.appError (sprintf "Block %i does not exist" (blockNumber |> fun (BlockNumber b) -> b))

    let getAddressApi
        getChxBalanceState
        (chainiumAddress : ChainiumAddress)
        : Result<GetAddressApiResponseDto, AppErrors>
        =

        match getChxBalanceState chainiumAddress with
        | Some addressState ->
            addressState
            |> Mapping.chxBalanceStateDtoToGetAddressApiResponseDto chainiumAddress
            |> Ok
        | None ->
            {
                ChxBalanceStateDto.Amount = 0m
                Nonce = 0L
            }
            |> Mapping.chxBalanceStateDtoToGetAddressApiResponseDto chainiumAddress
            |> Ok

    let getAddressAccountsApi
        (getAddressAccounts : ChainiumAddress -> AccountHash list)
        (address : ChainiumAddress)
        : Result<GetAddressAccountsApiResponseDto, AppErrors>
        =

        let accounts =
            getAddressAccounts address
            |> List.map (fun (AccountHash h) -> h)

        Ok {GetAddressAccountsApiResponseDto.Accounts = accounts}

    let getAccountApi
        (getAccountState : AccountHash -> AccountStateDto option)
        getAccountHoldings
        (accountHash : AccountHash)
        (assetHash : AssetHash option)
        : Result<GetAccountApiResponseDto, AppErrors>
        =

        match getAccountState accountHash with
        | None ->
            accountHash
            |> fun (AccountHash h) -> sprintf "Account %s does not exist." h
            |> Result.appError
        | Some accountState ->
            getAccountHoldings accountHash assetHash
            |? []
            |> Mapping.accountHoldingDtosToGetAccoungHoldingsResponseDto accountHash accountState
            |> Ok
