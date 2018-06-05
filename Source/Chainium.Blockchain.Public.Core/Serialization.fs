namespace Chainium.Blockchain.Public.Core

open System
open System.Text
open Chainium.Common
open Chainium.Blockchain.Common
open Chainium.Blockchain.Common.Conversion
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Core.Dtos
open Newtonsoft.Json
open Newtonsoft.Json.Converters
open Newtonsoft.Json.Linq

module Serialization =

    let objToString (data : obj) =
        match data with
        | :? string as s -> s
        | _ -> ""

    let private actionFromToken<'T> actionType (token : JToken) =
        {
            ActionType = actionType
            ActionData = token.ToObject<'T>()
        } |> box

    let tokenValue tokenName (jObject : JObject) =
        let token = ref (JValue("") :> JToken)
        let isValid = jObject.TryGetValue(tokenName, StringComparison.OrdinalIgnoreCase, token)
        match isValid with
        | true -> Some token.Value
        | false -> None

    let private actionsMap =
        [
            "ChxTransfer", fun trType token -> actionFromToken<ChxTransferTxActionDto> trType token
            "AssetTransfer", fun trType token -> actionFromToken<AssetTransferTxActionDto> trType token
        ] |> Map.ofList

    let private actionsConverter = {
        new CustomCreationConverter<TxActionDto>() with

        override this.Create objectType =
            failwith "NotImplemented"

        override this.ReadJson (reader : JsonReader, objectType : Type, existingValue : obj, serializer : JsonSerializer) =
            let jObject = JObject.Load(reader)

            let actionType = tokenValue "ActionType" jObject

            match actionType with
            | None -> null
            | Some actionType ->
                if actionType.HasValues then
                    null
                else
                    let realType = actionType.Value<string>()
                    let map = actionsMap.TryFind(realType)
                    let actionData = tokenValue "ActionData" jObject
                    match map with
                    | Some expr -> expr realType actionData.Value
                    | None ->
                        {
                            ActionType = realType
                            ActionData =
                                match actionData with
                                | None -> null
                                | Some x -> x.ToString()
                        } |> box
    }

    let deserializeTx (rawTx : byte[]) : Result<TxDto, AppErrors> =
        let deserialize str = JsonConvert.DeserializeObject<TxDto>(str, actionsConverter)

        try
            rawTx
            |> bytesToString
            |> deserialize
            |> Ok
        with
        | ex ->
            Error [AppError ex.AllMessagesAndStackTraces]

    let serializeTx (txDto : TxDto) =
        txDto
        |> JsonConvert.SerializeObject
        |> stringToBytes
        |> Ok
