namespace Chainium.Blockchain.Public.Net.Tests

open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Core.Events
open Chainium.Blockchain.Public.Core.DomainTypes

module WorkflowsMock =

    let processPeerMessage address respondToPeer peerMessage : Result<AppEvent option, AppError list> option =
        let processRequest address messageId targetAddress =
            match messageId with
            | Tx txHash ->
                if RawMock.hasData address messageId then
                    let peerMessage = ResponseDataMessage {
                        MessageId = messageId
                        Data = "txEnvelope" |> box
                    }
                    peerMessage
                    |> respondToPeer targetAddress
                    None
                else
                    Result.appError (sprintf "Error Tx %A not found" txHash) |> Some
            | Block blockNr ->
                if RawMock.hasData address messageId then
                    let peerMessage = ResponseDataMessage {
                        MessageId = messageId
                        Data = "blockEnvelope" |> box
                    }
                    peerMessage
                    |> respondToPeer targetAddress
                    None
                else
                    Result.appError (sprintf "Error Block %A not found" blockNr) |> Some

        match peerMessage with
        | GossipMessage m ->
            RawMock.savePeerData address m.MessageId
            None
        | MulticastMessage m ->
            RawMock.savePeerData address m.MessageId
            None
        | RequestDataMessage m -> processRequest address m.MessageId m.SenderAddress
        | ResponseDataMessage m ->
            RawMock.savePeerData address m.MessageId
            None
        | _ -> None
