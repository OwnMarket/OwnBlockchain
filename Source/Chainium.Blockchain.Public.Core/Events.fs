namespace Chainium.Blockchain.Public.Core.Events

open System
open Chainium.Blockchain.Public.Core.DomainTypes

type TxReceivedEventData = {
    TxHash : TxHash
}

type BlockCreatedEventData = {
    BlockNumber : BlockNumber
}

type AppEvent =
    | TxSubmitted of TxReceivedEventData
    | TxReceived of TxReceivedEventData
    | BlockCreated of BlockCreatedEventData
    | BlockReceived of BlockCreatedEventData
