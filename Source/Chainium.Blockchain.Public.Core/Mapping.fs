namespace Chainium.Blockchain.Public.Core

open System
open Chainium.Common
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Core.Dtos
open Chainium.Blockchain.Public.Core.Events

module Mapping =

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Tx
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let txStatusNumberToString (txStatusNumber : byte) =
        match txStatusNumber with
        | 0uy -> "Pending"
        | 1uy -> "Success"
        | 2uy -> "Failure"
        | s -> failwithf "Unknown tx status: %i" s

    let txErrorCodeNumberToString (txErrorCodeNumber : Nullable<int16>) : string =
        if not txErrorCodeNumber.HasValue then
            None
        elif Enum.IsDefined(typeof<TxErrorCode>, txErrorCodeNumber.Value) then
            let txErrorCode : TxErrorCode = LanguagePrimitives.EnumOfValue txErrorCodeNumber.Value
            txErrorCode.ToString() |> Some
        else
            failwithf "Unknown tx error code: %s" (txErrorCodeNumber.ToString())
        |> Option.toObj

    let txEnvelopeFromDto (dto : TxEnvelopeDto) : TxEnvelope =
        {
            RawTx = dto.Tx |> Convert.FromBase64String
            Signature =
                {
                    V = dto.V
                    R = dto.R
                    S = dto.S
                }
        }

    let txActionFromDto (action : TxActionDto) =
        match action.ActionData with
        | :? TransferChxTxActionDto as a ->
            {
                TransferChxTxAction.RecipientAddress = ChainiumAddress a.RecipientAddress
                Amount = ChxAmount a.Amount
            }
            |> TransferChx
        | :? TransferAssetTxActionDto as a ->
            {
                FromAccountHash = AccountHash a.FromAccount
                ToAccountHash = AccountHash a.ToAccount
                AssetHash = AssetHash a.AssetHash
                Amount = AssetAmount a.Amount
            }
            |> TransferAsset
        | :? CreateAssetEmissionTxActionDto as a ->
            {
                CreateAssetEmissionTxAction.EmissionAccountHash = AccountHash a.EmissionAccountHash
                AssetHash = AssetHash a.AssetHash
                Amount = AssetAmount a.Amount
            }
            |> CreateAssetEmission
        | :? CreateAccountTxActionDto ->
            CreateAccount
        | :? CreateAssetTxActionDto ->
            CreateAsset
        | :? SetAccountControllerTxActionDto as a ->
            {
                SetAccountControllerTxAction.AccountHash = AccountHash a.AccountHash
                ControllerAddress = ChainiumAddress a.ControllerAddress
            }
            |> SetAccountController
        | :? SetAssetControllerTxActionDto as a ->
            {
                SetAssetControllerTxAction.AssetHash = AssetHash a.AssetHash
                ControllerAddress = ChainiumAddress a.ControllerAddress
            }
            |> SetAssetController
        | :? SetAssetCodeTxActionDto as a ->
            {
                SetAssetCodeTxAction.AssetHash = AssetHash a.AssetHash
                AssetCode = AssetCode a.AssetCode
            }
            |> SetAssetCode
        | :? SetValidatorNetworkAddressTxActionDto as a ->
            {
                SetValidatorNetworkAddressTxAction.NetworkAddress = a.NetworkAddress
            }
            |> SetValidatorNetworkAddress
        | _ ->
            failwith "Invalid action type to map."

    let txFromDto sender hash (dto : TxDto) : Tx =
        {
            TxHash = hash
            Sender = sender
            Nonce = Nonce dto.Nonce
            Fee = ChxAmount dto.Fee
            Actions = dto.Actions |> List.map txActionFromDto
        }

    let txToTxInfoDto (tx : Tx) : TxInfoDto =
        {
            TxHash = tx.TxHash |> (fun (TxHash h) -> h)
            SenderAddress = tx.Sender |> (fun (ChainiumAddress a) -> a)
            Nonce = tx.Nonce |> (fun (Nonce n) -> n)
            Fee = tx.Fee |> (fun (ChxAmount a) -> a)
            ActionCount = Convert.ToInt16 tx.Actions.Length
        }

    let pendingTxInfoFromDto (dto : PendingTxInfoDto) : PendingTxInfo =
        {
            TxHash = TxHash dto.TxHash
            Sender = ChainiumAddress dto.SenderAddress
            Nonce = Nonce dto.Nonce
            Fee = ChxAmount dto.Fee
            ActionCount = dto.ActionCount
            AppearanceOrder = dto.AppearanceOrder
        }

    let txResultToDto (txResult : TxResult) =
        let status, errorCode, failedActionNumber =
            match txResult.Status with
            | Success -> 1uy, Nullable (), Nullable ()
            | Failure txError ->
                let statusNumber = 2uy
                match txError with
                | TxError errorCode ->
                    let errorNumber = errorCode |> LanguagePrimitives.EnumToValue
                    statusNumber, Nullable errorNumber, Nullable ()
                | TxActionError (TxActionNumber actionNumber, errorCode) ->
                    let errorNumber = errorCode |> LanguagePrimitives.EnumToValue
                    statusNumber, Nullable errorNumber, Nullable actionNumber

        {
            Status = status
            ErrorCode = errorCode
            FailedActionNumber = failedActionNumber
            BlockNumber = txResult.BlockNumber |> (fun (BlockNumber b) -> b)
        }

    let txResultFromDto (dto : TxResultDto) : TxResult =
        {
            Status =
                match dto.Status with
                | 1uy -> Success
                | 2uy ->
                    match dto.ErrorCode.HasValue, dto.FailedActionNumber.HasValue with
                    | true, false ->
                        let (errorCode : TxErrorCode) = dto.ErrorCode.Value |> LanguagePrimitives.EnumOfValue
                        TxError errorCode
                    | true, true ->
                        let (errorCode : TxErrorCode) = dto.ErrorCode.Value |> LanguagePrimitives.EnumOfValue
                        TxActionError (TxActionNumber dto.FailedActionNumber.Value, errorCode)
                    | _, _ -> failwith "Invalid error code and action number state in TxResult."
                    |> Failure
                | c -> failwithf "Unknown TxStatus code %i" c
            BlockNumber = BlockNumber dto.BlockNumber
        }

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Block
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let blockHeaderFromDto (dto : BlockHeaderDto) : BlockHeader =
        {
            BlockHeader.Number = BlockNumber dto.Number
            Hash = BlockHash dto.Hash
            PreviousHash = BlockHash dto.PreviousHash
            Timestamp = Timestamp dto.Timestamp
            Validator = ChainiumAddress dto.Validator
            TxSetRoot = MerkleTreeRoot dto.TxSetRoot
            TxResultSetRoot = MerkleTreeRoot dto.TxResultSetRoot
            StateRoot = MerkleTreeRoot dto.StateRoot
        }

    let blockFromDto (dto : BlockDto) : Block =
        {
            Header = blockHeaderFromDto dto.Header
            TxSet = dto.TxSet |> List.map TxHash
        }

    let blockHeaderToDto (block : BlockHeader) : BlockHeaderDto =
        {
            BlockHeaderDto.Number = block.Number |> fun (BlockNumber n) -> n
            Hash = block.Hash |> fun (BlockHash h) -> h
            PreviousHash = block.PreviousHash |> fun (BlockHash h) -> h
            Timestamp = block.Timestamp |> fun (Timestamp t) -> t
            Validator = block.Validator |> fun (ChainiumAddress a) -> a
            TxSetRoot = block.TxSetRoot |> fun (MerkleTreeRoot r) -> r
            TxResultSetRoot = block.TxResultSetRoot |> fun (MerkleTreeRoot r) -> r
            StateRoot = block.StateRoot |> fun (MerkleTreeRoot r) -> r
        }

    let blockToDto (block : Block) : BlockDto =
        {
            Header = blockHeaderToDto block.Header
            TxSet = block.TxSet |> List.map (fun (TxHash h) -> h)
        }

    let blockInfoDtoFromBlockHeaderDto (blockHeaderDto : BlockHeaderDto) : BlockInfoDto =
        {
            BlockNumber = blockHeaderDto.Number
            BlockHash = blockHeaderDto.Hash
            BlockTimestamp = blockHeaderDto.Timestamp
        }

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // State
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let chxBalanceStateFromDto (dto : ChxBalanceStateDto) : ChxBalanceState =
        {
            Amount = ChxAmount dto.Amount
            Nonce = Nonce dto.Nonce
        }

    let chxBalanceStateToDto (state : ChxBalanceState) : ChxBalanceStateDto =
        {
            Amount = state.Amount |> fun (ChxAmount a) -> a
            Nonce = state.Nonce |> fun (Nonce n) -> n
        }

    let holdingStateFromDto (dto : HoldingStateDto) : HoldingState =
        {
            Amount = AssetAmount dto.Amount
        }

    let holdingStateToDto (state : HoldingState) : HoldingStateDto =
        {
            Amount = state.Amount |> fun (AssetAmount a) -> a
        }

    let accountStateFromDto (dto : AccountStateDto) : AccountState =
        {
            ControllerAddress = ChainiumAddress dto.ControllerAddress
        }

    let accountStateToDto (state : AccountState) : AccountStateDto =
        {
            ControllerAddress = state.ControllerAddress |> fun (ChainiumAddress a) -> a
        }

    let assetStateFromDto (dto : AssetStateDto) : AssetState =
        {
            AssetCode =
                if dto.AssetCode.IsNullOrWhiteSpace() then
                    None
                else
                    dto.AssetCode |> AssetCode |> Some
            ControllerAddress = ChainiumAddress dto.ControllerAddress
        }

    let assetStateToDto (state : AssetState) : AssetStateDto =
        {
            AssetCode = state.AssetCode |> Option.map (fun (AssetCode c) -> c) |> Option.toObj
            ControllerAddress = state.ControllerAddress |> fun (ChainiumAddress a) -> a
        }

    let validatorStateFromDto (dto : ValidatorStateDto) : ValidatorState =
        {
            NetworkAddress = dto.NetworkAddress
        }

    let validatorStateToDto (state : ValidatorState) : ValidatorStateDto =
        {
            NetworkAddress = state.NetworkAddress
        }

    let stakeStateFromDto (dto : StakeStateDto) : StakeState =
        {
            Amount = ChxAmount dto.Amount
        }

    let stakeStateToDto (state : StakeState) : StakeStateDto =
        {
            Amount = state.Amount |> fun (ChxAmount a) -> a
        }

    let outputToDto (output : ProcessingOutput) : ProcessingOutputDto =
        let txResults =
            output.TxResults
            |> Map.toList
            |> List.map (fun (TxHash h, s : TxResult) -> h, s |> txResultToDto)
            |> Map.ofList

        let chxBalances =
            output.ChxBalances
            |> Map.toList
            |> List.map (fun (ChainiumAddress a, s : ChxBalanceState) -> a, chxBalanceStateToDto s)
            |> Map.ofList

        let holdings =
            output.Holdings
            |> Map.toList
            |> List.map (fun ((AccountHash ah, AssetHash ac), s : HoldingState) -> (ah, ac), holdingStateToDto s)
            |> Map.ofList

        let accounts =
            output.Accounts
            |> Map.toList
            |> List.map (fun (AccountHash ah, s : AccountState) -> ah, accountStateToDto s)
            |> Map.ofList

        let assets =
            output.Assets
            |> Map.toList
            |> List.map (fun (AssetHash ah, s : AssetState) -> ah, assetStateToDto s)
            |> Map.ofList

        let validators =
            output.Validators
            |> Map.toList
            |> List.map (fun (ChainiumAddress a, s : ValidatorState) -> a, validatorStateToDto s)
            |> Map.ofList

        let stakes =
            output.Stakes
            |> Map.toList
            |> List.map (fun ((ChainiumAddress sa, ChainiumAddress va), s : StakeState) -> (sa, va), stakeStateToDto s)
            |> Map.ofList

        {
            ProcessingOutputDto.TxResults = txResults
            ChxBalances = chxBalances
            Holdings = holdings
            Accounts = accounts
            Assets = assets
            Validators = validators
            Stakes = stakes
        }

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Events
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let txSubmittedEventToSubmitTxResponseDto (event : TxReceivedEventData) =
        let (TxHash hash) = event.TxHash
        { SubmitTxResponseDto.TxHash = hash }

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // API
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let chxBalanceStateDtoToGetAddressApiResponseDto
        (ChainiumAddress chainiumAddress)
        (chxBalanceState : ChxBalanceStateDto)
        =

        {
            GetAddressApiResponseDto.ChainiumAddress = chainiumAddress
            GetAddressApiResponseDto.Balance = chxBalanceState.Amount
            GetAddressApiResponseDto.Nonce = chxBalanceState.Nonce
        }

    let accountHoldingDtosToGetAccoungHoldingsResponseDto
        (AccountHash accountHash)
        (accountState : AccountStateDto)
        (holdings : AccountHoldingDto list)
        =

        let mapFn (holding : AccountHoldingDto) : GetAccountApiHoldingDto =
            {
                AssetHash = holding.AssetHash
                Balance = holding.Amount
            }

        {
            GetAccountApiResponseDto.AccountHash = accountHash
            GetAccountApiResponseDto.ControllerAddress = accountState.ControllerAddress
            GetAccountApiResponseDto.Holdings = List.map mapFn holdings
        }

    let blockTxsToGetBlockApiResponseDto
        (blockInfo : BlockDto)
        =

        {
            GetBlockApiResponseDto.Number = blockInfo.Header.Number
            GetBlockApiResponseDto.Hash = blockInfo.Header.Hash
            GetBlockApiResponseDto.PreviousHash = blockInfo.Header.PreviousHash
            GetBlockApiResponseDto.Timestamp = blockInfo.Header.Timestamp
            GetBlockApiResponseDto.Validator = blockInfo.Header.Validator
            GetBlockApiResponseDto.TxSetRoot = blockInfo.Header.TxSetRoot
            GetBlockApiResponseDto.TxResultSetRoot = blockInfo.Header.TxResultSetRoot
            GetBlockApiResponseDto.StateRoot = blockInfo.Header.StateRoot
            GetBlockApiResponseDto.TxSet = blockInfo.TxSet
        }

    let txToGetTxApiResponseDto
        (TxHash txHash)
        (ChainiumAddress senderAddress)
        (txDto : TxDto)
        (txResult : TxResultDto option)
        =

        let txStatus, txErrorCode, failedActionNumber, blockNumber =
            match txResult with
            | Some r -> r.Status, r.ErrorCode, r.FailedActionNumber, Nullable r.BlockNumber
            | None -> 0uy, Nullable(), Nullable(), Nullable()

        {
            GetTxApiResponseDto.TxHash = txHash
            GetTxApiResponseDto.SenderAddress = senderAddress
            GetTxApiResponseDto.Nonce = txDto.Nonce
            GetTxApiResponseDto.Fee = txDto.Fee
            GetTxApiResponseDto.Actions = txDto.Actions
            GetTxApiResponseDto.Status = txStatus |> txStatusNumberToString
            GetTxApiResponseDto.ErrorCode = txErrorCode |> txErrorCodeNumberToString
            GetTxApiResponseDto.FailedActionNumber = failedActionNumber
            GetTxApiResponseDto.BlockNumber = blockNumber
        }

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Network
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let gossipMemberFromDto (dto: GossipMemberDto) : GossipMember =
        {
            NetworkAddress = NetworkAddress dto.NetworkAddress
            Heartbeat = dto.Heartbeat
        }

    let gossipMemberToDto (gossipMember : GossipMember) : GossipMemberDto =
        {
            NetworkAddress = gossipMember.NetworkAddress |> fun (NetworkAddress a) -> a
            Heartbeat = gossipMember.Heartbeat
        }

    let gossipDiscoveryMessageFromDto (dto : GossipDiscoveryMessageDto) : GossipDiscoveryMessage =
        {
            ActiveMembers = dto.ActiveMembers |> List.map gossipMemberFromDto
        }

    let gossipDiscoveryMessageToDto (gossipDiscoveryMessage: GossipDiscoveryMessage) =
        {
            ActiveMembers = gossipDiscoveryMessage.ActiveMembers |> List.map gossipMemberToDto
        }

    let gossipMessageFromDto (dto : GossipMessageDto) =
        let gossipMessageId =
            match dto.MessageType with
            | "Tx" -> Tx (TxHash dto.MessageId)
            | "Block" -> Block (dto.MessageId |> Convert.ToInt64 |> BlockNumber)
            | _ -> failwithf "Invalid gossip message type %s" dto.MessageType

        {
            MessageId = gossipMessageId
            SenderAddress = NetworkAddress dto.SenderAddress
            Data = dto.Data
        }

    let gossipMessageToDto (gossipMessage : GossipMessage) =
        let messageType, messageId =
            match gossipMessage.MessageId with
            | Tx txHash -> "Tx", txHash |> fun (TxHash tx) -> tx
            | Block blockNr -> "Block", blockNr |> fun (BlockNumber b) -> b |> Convert.ToString

        {
            MessageId = messageId
            MessageType = messageType
            SenderAddress = gossipMessage.SenderAddress |> fun (NetworkAddress a) -> a
            Data = gossipMessage.Data
        }

    let multicastMessageFromDto (dto : MulticastMessageDto) =
        let multicastMssageId =
            match dto.MessageType with
            | "Tx" -> Tx (TxHash dto.MessageId)
            | "Block" -> Block (dto.MessageId |> Convert.ToInt64 |> BlockNumber)
            | _ -> failwithf "Invalid gossip message type %s" dto.MessageType

        {
            MessageId = multicastMssageId
            Data = dto.Data
        }

    let multicastMessageToDto (multicastMessage : MulticastMessage) =
        let messageType, messageId =
            match multicastMessage.MessageId with
            | Tx txHash -> "Tx", txHash |> fun (TxHash tx) -> tx
            | Block blockNr -> "Block", blockNr |> fun (BlockNumber b) -> b |> Convert.ToString

        {
            MessageId = messageId
            MessageType = messageType
            Data = multicastMessage.Data
        }

    let peerMessageFromDto (dto : PeerMessageDto) =
        match dto.MessageData with
        | :? GossipDiscoveryMessageDto as m ->
            gossipDiscoveryMessageFromDto m |> GossipDiscoveryMessage
        | :? GossipMessageDto as m ->
            gossipMessageFromDto m |> GossipMessage
        | :? MulticastMessageDto as m ->
            multicastMessageFromDto m |> MulticastMessage
        | _ -> failwith "Invalid message type to map."

    let peerMessageToDto (serialize : (obj -> string)) (peerMessage : PeerMessage) : PeerMessageDto =
        let messageType, data =
            match peerMessage with
            | GossipDiscoveryMessage m -> "GossipDiscoveryMessage", m |> gossipDiscoveryMessageToDto |> serialize
            | GossipMessage m -> "GossipMessage", m |> gossipMessageToDto |> serialize
            | MulticastMessage m -> "MulticastMessage", m |> multicastMessageToDto |> serialize

        {
            MessageType = messageType
            MessageData = data
        }
