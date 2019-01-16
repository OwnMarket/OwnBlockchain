namespace Own.Blockchain.Public.Core

open System
open Own.Common
open Own.Blockchain.Common
open Own.Blockchain.Common.Conversion
open Own.Blockchain.Public.Core.Dtos
open Newtonsoft.Json
open Newtonsoft.Json.Converters
open Newtonsoft.Json.Linq

module Serialization =

    let private tokenValue tokenName (jObject : JObject) =
        let token = ref (JValue("") :> JToken)
        let isValid = jObject.TryGetValue(tokenName, StringComparison.OrdinalIgnoreCase, token)
        if isValid then Some token.Value
        else None

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

    let private actionTypeToObjectMapping =
        [
            "TransferChx", tokenToAction<TransferChxTxActionDto>
            "TransferAsset", tokenToAction<TransferAssetTxActionDto>
            "CreateAssetEmission", tokenToAction<CreateAssetEmissionTxActionDto>
            "CreateAccount", tokenToAction<CreateAccountTxActionDto>
            "CreateAsset", tokenToAction<CreateAssetTxActionDto>
            "SetAccountController", tokenToAction<SetAccountControllerTxActionDto>
            "SetAssetController", tokenToAction<SetAssetControllerTxActionDto>
            "SetAssetCode", tokenToAction<SetAssetCodeTxActionDto>
            "ConfigureValidator", tokenToAction<ConfigureValidatorTxActionDto>
            "DelegateStake", tokenToAction<DelegateStakeTxActionDto>
            "SubmitVote", tokenToAction<SubmitVoteTxActionDto>
            "SubmitVoteWeight", tokenToAction<SubmitVoteWeightTxActionDto>
        ] |> Map.ofList

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
                match txType |> actionTypeToObjectMapping.TryFind with
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

    let private tokenToPeerMessage<'T> messageType (token : JToken option) =
        match token with
        | Some _ ->
            {
                MessageType = messageType
                MessageData = JsonConvert.DeserializeObject<'T> (token.Value.ToString())
            }
            |> box
        | None ->
            token |> box

    let private peerMessageTypeToObjectMapping =
        [
            "GossipDiscoveryMessage", tokenToPeerMessage<GossipDiscoveryMessageDto>
            "GossipMessage", tokenToPeerMessage<GossipMessageDto>
            "MulticastMessage", tokenToPeerMessage<MulticastMessageDto>
            "RequestDataMessage", tokenToPeerMessage<RequestDataMessageDto>
            "ResponseDataMessage", tokenToPeerMessage<ResponseDataMessageDto>
        ]
        |> Map.ofList

    let private peerMessageConverter = {
        new CustomCreationConverter<PeerMessageDto>() with

        override __.Create objectType =
            failwith "Not implemented"

        override __.ReadJson
            (reader : JsonReader, objectType : Type, existingValue : obj, serializer : JsonSerializer)
            =

            let jObject = JObject.Load(reader)

            match (tokenValue "MessageType" jObject) with
            | None -> jObject |> box
            | Some messageType ->
                let messageData = tokenValue "MessageData"
                let peerMessageType = messageType.Value<string>()
                match peerMessageType |> peerMessageTypeToObjectMapping.TryFind with
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

    let deserializePeerMessage message =
        JsonConvert.DeserializeObject<PeerMessageDto> (message, peerMessageConverter)

    let deserializeJObject<'T> (data : obj) =
        (data :?> JObject).ToObject<'T>()
