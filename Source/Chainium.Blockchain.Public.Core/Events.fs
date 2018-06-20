namespace Chainium.Blockchain.Public.Core.Events

open System
open Chainium.Blockchain.Public.Core.DomainTypes

type TxSubmittedEvent = {
    TxHash : TxHash
}

type BlockCreatedEvent = {
    BlockNumber : BlockNumber
}

type BlockProcessedEvent = {
    BlockNumber : BlockNumber
}

type AppEvent =
    | TxSubmitted of TxSubmittedEvent
    | BlockCreated of BlockCreatedEvent
