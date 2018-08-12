namespace Chainium.Blockchain.Public.Core

open System
open Chainium.Common
open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Core
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Core.Dtos
open Chainium.Blockchain.Public.Core.Events

module Workflows =

    let getAvailableChxBalance getChxBalanceState getTotalChxStaked senderAddress : ChxAmount =
        let chxBalance =
            senderAddress
            |> getChxBalanceState
            |> Option.map (Mapping.chxBalanceStateFromDto >> fun state -> state.Amount)
            |? ChxAmount 0M

        let chxStaked = getTotalChxStaked senderAddress

        chxBalance - chxStaked

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

    let isMyTurnToProposeBlock
        (getLastBlockNumber : unit -> BlockNumber option)
        (getValidators : unit -> ValidatorInfoDto list)
        addressFromPrivateKey
        myPrivateKey
        =

        // This is a simple leader based protocol used as a temporary placeholder for real consensus implementation.
        let nextBlockNumber =
            match getLastBlockNumber () with
            | Some bn -> bn + 1L
            | None -> failwith "Blockchain state not initialized."

        let blockProposer =
            getValidators ()
            |> List.sortBy (fun v -> v.ValidatorAddress)
            |> Consensus.getBlockProposer nextBlockNumber

        let (ChainiumAddress myValidatorAddress) = myPrivateKey |> PrivateKey |> addressFromPrivateKey
        blockProposer.ValidatorAddress = myValidatorAddress

    let persistTxResults saveTxResult txResults =
        result {
            do! txResults
                |> Map.toList
                |> List.fold (fun result (txHash, txResult) ->
                    result
                    >>= fun _ -> saveTxResult (TxHash txHash) txResult
                ) (Ok ())
        }

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
        (getLastAppliedBlockNumber : unit -> BlockNumber option)
        getBlock
        decodeHash
        createHash
        createMerkleTree
        minTxActionFee
        validatorAddress
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
            let! previousBlockDto =
                match getLastAppliedBlockNumber () with
                | Some blockNumber -> getBlock blockNumber
                | None -> failwith "Blockchain state is not initialized."
            let previousBlock = Mapping.blockFromDto previousBlockDto
            let blockNumber = previousBlock.Header.Number + 1L

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

            let block =
                Blocks.assembleBlock
                    decodeHash
                    createHash
                    createMerkleTree
                    validatorAddress
                    blockNumber
                    timestamp
                    previousBlock.Header.Hash
                    txSet
                    output

            return (block, output)
        }

    let createNewBlock
        createBlock
        getPendingTxs
        getChxBalanceStateFromStorage
        getAvailableChxBalanceFromStorage
        persistTxResults
        signBlock
        saveBlock
        saveBlockEnvelope
        applyNewState
        maxTxCountPerBlock
        addressFromPrivateKey
        validatorPrivateKey
        : Result<BlockCreatedEventData, AppErrors> option
        =

        let timestamp = Utils.getUnixTimestamp () |> Timestamp

        let getChxBalanceState = memoize (getChxBalanceStateFromStorage >> Option.map Mapping.chxBalanceStateFromDto)
        let getAvailableChxBalance = memoize getAvailableChxBalanceFromStorage

        let validatorAddress = validatorPrivateKey |> PrivateKey |> addressFromPrivateKey
        match
            Processing.getTxSetForNewBlock
                getPendingTxs
                getChxBalanceState
                getAvailableChxBalance
                maxTxCountPerBlock with
        | [] -> None // Nothing to process.
        | txSet ->
            result {
                let txSet =
                    txSet
                    |> Processing.orderTxSet

                let! block, output = createBlock validatorAddress timestamp txSet
                let outputDto = Mapping.outputToDto output
                let blockDto = Mapping.blockToDto block

                let blockInfoDto = Mapping.blockInfoDtoFromBlockHeaderDto blockDto.Header

                do! persistTxResults outputDto.TxResults

                do! saveBlock blockDto

                match Serialization.serialize<BlockDto> blockDto with
                | Ok blockBytes ->
                    let signature : Signature = signBlock (PrivateKey validatorPrivateKey) blockBytes
                    let blockEnvelopeDto = {
                        Block = blockBytes |> Convert.ToBase64String
                        V = signature.V
                        S = signature.S
                        R = signature.R
                    }
                    do! saveBlockEnvelope block.Header.Number blockEnvelopeDto
                | _ -> ()

                do! applyNewState blockInfoDto outputDto

                return { BlockCreatedEventData.BlockNumber = block.Header.Number }
            }
            |> Some

    let applyBlock
        createBlock
        (getValidators : unit -> ValidatorInfoDto list)
        verifySignature
        persistTxResults
        saveBlock
        saveBlockEnvelope
        applyNewState
        blockNumber
        blockEnvelopeDto
        =

        result {
            let blockProposer =
                getValidators ()
                |> List.sortBy (fun v -> v.ValidatorAddress)
                |> Consensus.getBlockProposer blockNumber

            let! blockDto =
                Blocks.getBlockDto
                    verifySignature
                    blockEnvelopeDto
                    (ChainiumAddress blockProposer.ValidatorAddress)

            let txSet = blockDto.TxSet |> List.map(fun hash -> TxHash hash)
            let! createdBlock, output =
                createBlock
                    (ChainiumAddress blockDto.Header.Validator)
                    (Timestamp blockDto.Header.Timestamp)
                    txSet

            let outputDto = Mapping.outputToDto output
            let createdBlockDto = Mapping.blockToDto createdBlock

            if blockDto = createdBlockDto then
                let blockInfoDto = Mapping.blockInfoDtoFromBlockHeaderDto blockDto.Header
                do! persistTxResults outputDto.TxResults
                do! saveBlock blockDto
                do! saveBlockEnvelope blockNumber blockEnvelopeDto
                do! applyNewState blockInfoDto outputDto
            else
                return!
                    blockNumber
                    |> fun (BlockNumber n) ->
                        sprintf "Applying of block %i didn't result in expected blockchain state." n
                    |> Result.appError
        }

    let processBlock
        createBlock
        applyNewState
        (block : Block)
        : Result<BlockProcessedEventData, AppErrors>
        =

        result {
            let! resultingBlock, output = createBlock block.Header.Validator block.Header.Timestamp block.TxSet
            if resultingBlock = block then
                let outputDto = Mapping.outputToDto output
                let blockDto = Mapping.blockToDto block
                let blockInfoDto = Mapping.blockInfoDtoFromBlockHeaderDto blockDto.Header
                do! applyNewState blockInfoDto outputDto
                return { BlockProcessedEventData.BlockNumber = block.Header.Number }
            else
                let message =
                    block.Header.Number
                    |> fun (BlockNumber n) -> n
                    |> sprintf "Processing of block %i didn't result in expected blockchain state."

                Log.error message
                return! Result.appError message
        }

    let advanceToLastKnownBlock
        createBlock
        decodeHash
        createHash
        createMerkleTree
        applyNewState
        (getLastAppliedBlockNumber : unit -> BlockNumber option)
        (blockExists : BlockNumber -> bool)
        (getBlock : BlockNumber -> Result<BlockDto, AppErrors>)
        =

        let rec processNextBlock (previousBlockNumber : BlockNumber, previousBlockHash : BlockHash) =
            let nextBlockNumber = previousBlockNumber + 1L
            if blockExists nextBlockNumber then
                result {
                    let! blockDto = getBlock nextBlockNumber
                    let block = Mapping.blockFromDto blockDto
                    if Blocks.isValidBlock decodeHash createHash createMerkleTree previousBlockHash block then
                        let! event =
                            processBlock
                                createBlock
                                applyNewState
                                block

                        event
                        |> fun ({BlockNumber = (BlockNumber n)}) -> n
                        |> Log.infof "Block %i applied"

                        return! processNextBlock (block.Header.Number, block.Header.Hash)
                    else
                        let message =
                            nextBlockNumber
                            |> fun (BlockNumber n) -> n
                            |> sprintf "Block %i is not valid."

                        Log.error message
                        return! Result.appError message
                }
            else
                Ok previousBlockNumber

        match getLastAppliedBlockNumber () with
        | None -> failwith "Blockchain state is not initialized."
        | Some blockNumber ->
            getBlock blockNumber
            >>= (fun lastBlock ->
                processNextBlock (BlockNumber lastBlock.Header.Number, BlockHash lastBlock.Header.Hash)
            )

    let initBlockchainState
        (getLastAppliedBlockNumber : unit -> BlockNumber option)
        (getBlock : BlockNumber -> Result<BlockDto, AppErrors>)
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

            let genesisBlockDto =
                Blocks.createGenesisBlock
                    decodeHash createHash createMerkleTree zeroHash zeroAddress genesisState
                |> Mapping.blockToDto

            let genesisBlockExists =
                match getBlock (BlockNumber 0L) with
                | Ok genesisBlockDtoFromDisk ->
                    if genesisBlockDtoFromDisk <> genesisBlockDto then
                        failwith "Stored genesis block is invalid."
                    true
                | _ ->
                    false

            let blockInfoDto = Mapping.blockInfoDtoFromBlockHeaderDto genesisBlockDto.Header

            let result =
                result {
                    if not genesisBlockExists then
                        do! saveBlock genesisBlockDto
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
        getBlockEnvelope
        (blockNumber : BlockNumber)
        =

        match getBlockEnvelope blockNumber with
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
        getBlockEnvelope
        submitTx
        applyBlock
        respondToPeer
        peerMessage =
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
            match getBlockEnvelope blockNr with
            | Ok _ -> None |> Ok
            | _ ->
                let blockEnvelopeDto = Serialization.deserializeJObject data
                match applyBlock blockNr blockEnvelopeDto with
                | Ok () -> {BlockCreatedEventData.BlockNumber = blockNr} |> BlockReceived |> Some |> Ok
                | _ -> Result.appError "Error creating block"

        let processData messageId (data : obj) =
            match messageId with
            | Tx txHash -> processTxFromPeer txHash data
            | Block blockNr -> processBlockFromPeer blockNr data

        let processRequest messageId senderAddress =
            match messageId with
            | Tx txHash ->
                match getTx txHash with
                | Ok txEvenvelopeDto ->
                    let peerMessage = ResponseDataMessage {
                        MessageId = messageId
                        Data = txEvenvelopeDto
                    }
                    peerMessage
                    |> respondToPeer senderAddress
                    None |> Ok
                | _ -> Result.appError (sprintf "Error Tx %A not found" txHash)

            | Block blockNr ->
                match getBlockEnvelope blockNr with
                | Ok blockEnvelopeDto ->
                    let peerMessage = ResponseDataMessage {
                        MessageId = messageId
                        Data = blockEnvelopeDto
                    }
                    peerMessage
                    |> respondToPeer senderAddress
                    None |> Ok
                | _ -> Result.appError (sprintf "Error Block %A not found" blockNr)

        match peerMessage with
        | GossipDiscoveryMessage _ -> None
        | GossipMessage m -> processData m.MessageId m.Data |> Some
        | MulticastMessage m -> processData m.MessageId m.Data |> Some
        | RequestDataMessage m -> processRequest m.MessageId m.SenderAddress |> Some
        | ResponseDataMessage m -> processData m.MessageId m.Data |> Some

    let getAddressApi getChxBalanceState (chainiumAddress : ChainiumAddress)
        : Result<GetAddressApiResponseDto, AppErrors> =
        match getChxBalanceState chainiumAddress with
        | Some addressState ->
            addressState
            |> Mapping.chxBalanceStateDtoToGetAddressApiResponseDto chainiumAddress
            |> Ok
        | None ->
            {
                ChxBalanceStateDto.Amount = 0M
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

    let getBlockApi
        getBlock
        (blockNumber : BlockNumber)
        : Result<GetBlockApiResponseDto, AppErrors>
        =

        match getBlock blockNumber with
        | Ok block -> Ok (Mapping.blockTxsToGetBlockApiResponseDto block)
        | _ -> Result.appError (sprintf "Block %i does not exist" (blockNumber |> fun (BlockNumber b) -> b))

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
