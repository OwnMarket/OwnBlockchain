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
            |? ChxAmount 0m

        let chxStaked = getTotalChxStaked senderAddress

        chxBalance - chxStaked

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Consensus
    ////////////////////////////////////////////////////////////////////////////////////////////////////

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
            |> List.map (fun v -> // TODO: Remove this once we start using validator snapshots
                {
                    ValidatorSnapshot.ValidatorAddress = ChainiumAddress v.ValidatorAddress
                    NetworkAddress = v.NetworkAddress
                    TotalStake = ChxAmount 0m
                }
            )
            |> Consensus.getBlockProposer nextBlockNumber

        let myValidatorAddress = myPrivateKey |> addressFromPrivateKey
        blockProposer.ValidatorAddress = myValidatorAddress

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
        (getTopValidators : unit -> ValidatorSnapshot list)
        (getActiveValidators : unit -> ValidatorSnapshot list)
        (getLastAppliedBlockNumber : unit -> BlockNumber option)
        getBlock
        decodeHash
        createHash
        createMerkleTree
        checkpointBlockCount
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
            let! previousBlock =
                match getLastAppliedBlockNumber () with
                | None -> failwith "Blockchain state is not initialized."
                | Some blockNumber -> getBlock blockNumber >>= Blocks.extractBlockFromEnvelopeDto

            let blockNumber = previousBlock.Header.Number + 1L

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
        applyNewState
        maxTxCountPerBlock
        addressFromPrivateKey
        validatorPrivateKey
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

                let! block, output = createBlock validatorAddress timestamp txSet
                let outputDto = Mapping.outputToDto output
                let blockDto = Mapping.blockToDto block
                let blockInfoDto = Mapping.blockInfoDtoFromBlockHeaderDto blockDto.Header

                let! blockEnvelopeDto =
                    Serialization.serialize<BlockDto> blockDto
                    >>= fun blockBytes ->
                        let signature : Signature = signBlock validatorPrivateKey blockBytes
                        {
                            Block = blockBytes |> Convert.ToBase64String
                            V = signature.V
                            S = signature.S
                            R = signature.R
                        }
                        |> Ok

                do! persistTxResults outputDto.TxResults
                do! saveBlock block.Header.Number blockEnvelopeDto
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
        applyNewState
        blockNumber
        blockEnvelopeDto
        =

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

            let! blockDto =
                Blocks.getBlockDto
                    verifySignature
                    blockEnvelopeDto
                    blockProposer.ValidatorAddress

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
        (getBlock : BlockNumber -> Result<BlockEnvelopeDto, AppErrors>)
        =

        let rec processNextBlock (previousBlockNumber : BlockNumber, previousBlockHash : BlockHash) =
            let nextBlockNumber = previousBlockNumber + 1L
            if blockExists nextBlockNumber then
                result {
                    let! block = getBlock nextBlockNumber >>= Blocks.extractBlockFromEnvelopeDto

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
            >>= Blocks.extractBlockFromEnvelopeDto
            >>= fun lastBlock ->
                processNextBlock (lastBlock.Header.Number, lastBlock.Header.Hash)

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
                                    V = ""
                                    S = ""
                                    R = ""
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
            match getBlock blockNr with
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
                match getBlock blockNr with
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
