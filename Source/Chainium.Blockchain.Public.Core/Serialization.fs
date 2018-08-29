namespace Chainium.Blockchain.Public.Core

open System
open Chainium.Common
open Chainium.Blockchain.Common
open Chainium.Blockchain.Common.Conversion
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Core.Dtos
open Newtonsoft.Json
open Newtonsoft.Json.Converters
open Newtonsoft.Json.Linq

module Serialization =

    let private tokenToAction<'T> actionType (token : JToken option) =
        match token with
        | Some _ ->
            {
                ActionType = actionType
                ActionData = token.Value.ToObject<'T>()
            }
            |> box
        | None ->
            token |> box

    let private actionsBasedOnTransactionType =
        [
            "TransferChx", tokenToAction<TransferChxTxActionDto>
            "TransferAsset", tokenToAction<TransferAssetTxActionDto>
            "CreateAssetEmission", tokenToAction<CreateAssetEmissionTxActionDto>
            "CreateAccount", tokenToAction<CreateAccountTxActionDto>
            "CreateAsset", tokenToAction<CreateAssetTxActionDto>
            "SetAccountController", tokenToAction<SetAccountControllerTxActionDto>
            "SetAssetController", tokenToAction<SetAssetControllerTxActionDto>
            "SetAssetCode", tokenToAction<SetAssetCodeTxActionDto>
            "SetValidatorNetworkAddress", tokenToAction<SetValidatorNetworkAddressTxActionDto>
            "DelegateStake", tokenToAction<DelegateStakeTxActionDto>
        ] |> Map.ofList

    let private tokenValue tokenName (jObject : JObject) =
        let token = ref (JValue("") :> JToken)
        let isValid = jObject.TryGetValue(tokenName, StringComparison.OrdinalIgnoreCase, token)
        match isValid with
        | true -> Some token.Value
        | false -> None

    let private actionsConverter = {
        new CustomCreationConverter<TxActionDto>() with

        override __.Create objectType =
            failwith "NotImplemented"

        override __.ReadJson
            (reader : JsonReader, objectType : Type, existingValue : obj, serializer : JsonSerializer)
            =

            let jObject = JObject.Load(reader)

            let actionData = tokenValue "ActionData"

            match (tokenValue "ActionType" jObject) with
            | None -> jObject |> box
            | Some actionType ->
                let txType = actionType.Value<string>()
                match txType |> actionsBasedOnTransactionType.TryFind with
                | Some create ->
                    actionData jObject
                    |> create txType
                | None ->
                    {
                        ActionType = txType
                        ActionData =
                            match actionData jObject with
                            | None -> null
                            | Some x -> x.ToString()
                    } |> box
    }

    let serialize<'T> (dto : 'T) =
        try
            dto
            |> JsonConvert.SerializeObject
            |> stringToBytes
            |> Ok
        with
        | ex ->
            Result.appError ex.AllMessagesAndStackTraces

    let deserialize<'T> (rawData : byte[]) : Result<'T, AppErrors> =
        try
            rawData
            |> bytesToString
            |> fun str -> JsonConvert.DeserializeObject<'T>(str, actionsConverter)
            |> Ok
        with
        | ex ->
            Result.appError ex.AllMessagesAndStackTraces

    let serializeTx (txDto : TxDto) =
        serialize<TxDto> txDto

    let deserializeTx (rawTx : byte[]) : Result<TxDto, AppErrors> =
        deserialize<TxDto> rawTx

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Network
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let private tokenToMessage<'T> messageType (token : JToken option) =
        match token with
        | Some _ ->
            {
                MessageType = messageType
                MessageData = JsonConvert.DeserializeObject<'T> (token.Value.ToString())
            }
            |> box
        | None ->
            token |> box

    let private messagesBasedOnMessageType =
        [
            "GossipDiscoveryMessage", tokenToMessage<GossipDiscoveryMessageDto>
            "GossipMessage", tokenToMessage<GossipMessageDto>
            "MulticastMessage", tokenToMessage<MulticastMessageDto>
            "RequestDataMessage", tokenToMessage<RequestDataMessageDto>
            "ResponseDataMessage", tokenToMessage<ResponseDataMessageDto>
        ]
        |> Map.ofList

    let private peerMessageConverter = {
        new CustomCreationConverter<PeerMessageDto>() with

        override __.Create objectType =
            failwith "NotImplemented"

        override __.ReadJson
            (reader : JsonReader, objectType : Type, existingValue : obj, serializer : JsonSerializer)
            =

            let jObject = JObject.Load(reader)
            let messageData = tokenValue "MessageData"

            match (tokenValue "MessageType" jObject) with
            | None -> jObject |> box
            | Some messageType ->
                let peerMessageType = messageType.Value<string>()
                match peerMessageType |> messagesBasedOnMessageType.TryFind with
                | Some create ->
                    messageData jObject |> create peerMessageType
                | None ->
                    {
                        MessageType = peerMessageType
                        MessageData =
                            match messageData jObject with
                            | None -> null
                            | Some x -> x.ToString()
                    }
                    |> box
    }

    let serializePeerMessage dto =
        JsonConvert.SerializeObject dto

    let deserializeMessage<'TDto> message =
        JsonConvert.DeserializeObject<'TDto> (message, peerMessageConverter)

    let deserializePeerMessage message =
        deserializeMessage<PeerMessageDto> message

    let deserializeJObject<'T> (data : obj) =
        (data :?> JObject).ToObject<'T>()
