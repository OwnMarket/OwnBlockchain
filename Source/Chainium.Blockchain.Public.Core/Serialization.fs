namespace Chainium.Blockchain.Public.Core

open System
open Chainium.Common
open Chainium.Blockchain.Common.Conversion
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Core.Dtos
open Newtonsoft.Json
open Newtonsoft.Json.Converters
open Newtonsoft.Json.Linq

module Serialization =

    let private tokenToAction<'T> actionType (token : JToken option) =
        if token.IsSome then
            {
                ActionType = actionType
                ActionData = token.Value.ToObject<'T>()
            }
            |> box
        else
            token
            |> box

    let private actionsBasedOnTransactionType =
        let chxTransferAction trType token = tokenToAction<ChxTransferTxActionDto> trType token
        let assetTransferAction trType token = tokenToAction<AssetTransferTxActionDto> trType token
        let controllerChangeAction trType token = tokenToAction<AccountControllerChangeTxActionDto> trType token

        [
            "ChxTransfer", chxTransferAction
            "AssetTransfer", assetTransferAction
            "AccountControllerChange", controllerChangeAction
        ] |> Map.ofList

    let private tokenValue tokenName (jObject : JObject) =
        let token = ref (JValue("") :> JToken)
        let isValid = jObject.TryGetValue(tokenName, StringComparison.OrdinalIgnoreCase, token)
        match isValid with
        | true -> Some token.Value
        | false -> None

    let private actionsConverter = {
        new CustomCreationConverter<TxActionDto>() with

        override this.Create objectType =
            failwith "NotImplemented"

        override this.ReadJson (reader : JsonReader, objectType : Type, existingValue : obj, serializer : JsonSerializer) =
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
        dto
        |> JsonConvert.SerializeObject
        |> stringToBytes
        |> Ok

    let deserialize<'T> (rawData : byte[]) : Result<'T, AppErrors> =
        try
            rawData
            |> bytesToString
            |> fun str -> JsonConvert.DeserializeObject<'T>(str, actionsConverter)
            |> Ok
        with
        | ex ->
            Error [AppError ex.AllMessagesAndStackTraces]

    let serializeTx (txDto : TxDto) =
        serialize<TxDto> txDto

    let deserializeTx (rawTx : byte[]) : Result<TxDto, AppErrors> =
        deserialize<TxDto> rawTx
