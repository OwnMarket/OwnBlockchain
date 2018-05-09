namespace Chainium.Blockchain.Public.Core

open System
open System.Text
open Chainium.Common
open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Core.Dtos
open Newtonsoft.Json
open Newtonsoft.Json.Converters
open Newtonsoft.Json.Linq

module Serialization =
    let stringToBytes (str : string) =
        Encoding.UTF8.GetBytes(str)

    let bytesToString (bytes : byte[]) =
        Encoding.UTF8.GetString(bytes)



    let objToString (data : obj) =
        match data with
        | :? string as s -> s
        | _ -> ""

    let private transactionFromToken<'T> transactionType (token : JToken) =
        {
            ActionType = transactionType
            ActionData = token.ToObject<'T>()
        } |> box
    
    let tokenValue tokenName (jObject:JObject) = 
        let token = ref (JValue("") :> JToken)
        let isValid = jObject.TryGetValue(tokenName, StringComparison.OrdinalIgnoreCase, token)
        match isValid with
        | true -> 
           token.Value
           |> Some
        | false -> None
    
    let private actionsMap = 
        [
            "ChxTransfer",fun trType token -> transactionFromToken<ChxTransferTxActionDto> trType token;
            "EquityTransfer",fun trType token ->transactionFromToken<EquityTransferTxActionDto> trType token
        ] |> Map.ofList
    
    let private transactionsConverter = { 
        new CustomCreationConverter<TxActionDto>() with
        override this.Create objectType =
            failwith "NotImplemented"
        
        override this.ReadJson ((reader : JsonReader), (objectType : Type), (existingValue : obj), (serializer : JsonSerializer)) =
            let jObject = JObject.Load(reader)
            
            let actionType = tokenValue "ActionType" jObject

            match actionType with
            | None -> null
            | Some transactionType ->
                if transactionType.HasValues then
                    null
                else
                    let realType = transactionType.Value<string>()
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
        let deserialize str = JsonConvert.DeserializeObject<TxDto>(str, transactionsConverter)

        try
            rawTx
                |> bytesToString
                |> deserialize
                |> Ok
        with
        | ex ->  
          let appError = 
            ex.AllMessagesAndStackTraces
            |> AppError

          [appError]
          |> Error

    let serializeTx (txDto : TxDto) =
        txDto
        |> JsonConvert.SerializeObject
        |> stringToBytes
        |> Ok
