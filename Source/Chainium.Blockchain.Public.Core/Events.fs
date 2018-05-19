namespace Chainium.Blockchain.Public.Core.Events

open System
open Chainium.Blockchain.Public.Core.DomainTypes

type TxSubmittedEvent = {
    TxHash : TxHash
}

type BlockCreatedEvent = {
    BlockNumber : BlockNumber
}
