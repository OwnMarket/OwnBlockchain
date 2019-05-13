namespace Own.Blockchain.Public.Net.Tests

open Own.Blockchain.Common
open Own.Blockchain.Public.Core.Events
open Own.Blockchain.Public.Core.DomainTypes

module WorkflowsMock =

    let processPeerMessage
        address
        respondToPeer
        getNetworkId
        peerMessageEnvelope
        : Result<AppEvent option, AppError list> list option
        =

        let processRequest address messageIds targetAddress =
            let responseResult =
                messageIds
                |> List.map (fun messageId ->
                    match messageId with
                    | Tx txHash ->
                        if RawMock.hasData address messageId then
                            {
                                MessageId = messageId
                                Data = "txEnvelope" |> Conversion.stringToBytes
                            }
                            |> Ok
                        else
                            Result.appError (sprintf "Error Tx %A not found" txHash)
                    | EquivocationProof equivocationProofHash ->
                        if RawMock.hasData address messageId then
                            {
                                MessageId = messageId
                                Data = "equivocationProof" |> Conversion.stringToBytes
                            }
                            |> Ok
                        else
                            Result.appError (sprintf "Error EquivocationProof %A not found" equivocationProofHash)
                    | Block blockNr ->
                        if RawMock.hasData address messageId then
                            {
                                MessageId = messageId
                                Data = "blockEnvelope" |> Conversion.stringToBytes
                            }
                            |> Ok
                        else
                            Result.appError (sprintf "Error Block %A not found" blockNr)
                    | Consensus _ -> Result.appError "Cannot request consensus message from Peer"
                    | ConsensusState _ -> Result.appError "Cannot request consensus state from Peer"
                    | BlockchainHead ->
                        {
                            MessageId = messageId
                            Data = "blockNr" |> Conversion.stringToBytes
                        }
                        |> Ok
                    | PeerList ->
                        {
                            MessageId = messageId
                            Data = "peerList" |> Conversion.stringToBytes
                        }
                        |> Ok
                )

            let responseItems =
                responseResult
                |> List.choose (function
                    | Ok responseItem -> Some responseItem
                    | _ -> None
                )

            let errors =
                responseResult
                |> List.choose (function
                    | Ok _ -> None
                    | Error e -> Some e.Head.Message
                )

            {
                PeerMessageEnvelope.NetworkId = getNetworkId ()
                PeerMessage = ResponseDataMessage {ResponseDataMessage.Items = responseItems}
            }
            |> respondToPeer targetAddress

            match errors with
            | [] -> Ok None
            | _ -> Result.appErrors errors

        match peerMessageEnvelope.PeerMessage with
        | GossipMessage m ->
            RawMock.savePeerData address m.MessageId
            None
        | MulticastMessage m ->
            RawMock.savePeerData address m.MessageId
            None
        | RequestDataMessage m -> [ processRequest address m.Items m.SenderIdentity ] |> Some
        | ResponseDataMessage m ->
            m.Items
            |> List.iter(fun response -> RawMock.savePeerData address response.MessageId)
            None
        | _ ->
            None
