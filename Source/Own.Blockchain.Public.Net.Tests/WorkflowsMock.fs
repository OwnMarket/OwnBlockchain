namespace Own.Blockchain.Public.Net.Tests

open Own.Blockchain.Common
open Own.Blockchain.Public.Core.Events
open Own.Blockchain.Public.Core.DomainTypes

module WorkflowsMock =

    let processPeerMessage address respondToPeer peerMessage : Result<AppEvent option, AppError list> option =
        let processRequest address messageId targetAddress =
            match messageId with
            | Tx txHash ->
                if RawMock.hasData address messageId then
                    let peerMessage = ResponseDataMessage {
                        MessageId = messageId
                        Data = "txEnvelope" |> Conversion.stringToBytes //TODO: fix this
                    }
                    peerMessage
                    |> respondToPeer targetAddress
                    None
                else
                    Result.appError (sprintf "Error Tx %A not found" txHash) |> Some
            | EquivocationProof equivocationProofHash ->
                if RawMock.hasData address messageId then
                    let peerMessage = ResponseDataMessage {
                        MessageId = messageId
                        Data = "equivocationProof" |> Conversion.stringToBytes //TODO: fix this
                    }
                    peerMessage
                    |> respondToPeer targetAddress
                    None
                else
                    Result.appError (sprintf "Error EquivocationProof %A not found" equivocationProofHash) |> Some
            | Block blockNr ->
                if RawMock.hasData address messageId then
                    let peerMessage = ResponseDataMessage {
                        MessageId = messageId
                        Data = "blockEnvelope" |> Conversion.stringToBytes //TODO: fix this
                    }
                    peerMessage
                    |> respondToPeer targetAddress
                    None
                else
                    Result.appError (sprintf "Error Block %A not found" blockNr) |> Some
            | Consensus _ -> Result.appError ("Cannot request consensus message from Peer") |> Some

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
