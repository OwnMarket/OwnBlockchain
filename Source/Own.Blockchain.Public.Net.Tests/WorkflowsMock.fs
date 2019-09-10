namespace Own.Blockchain.Public.Net.Tests

open Own.Blockchain.Common
open Own.Blockchain.Public.Core
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Core.Dtos
open Own.Blockchain.Public.Core.Events

module WorkflowsMock =

    let processPeerMessage
        address
        respondToPeer
        getPeerList
        getNetworkId
        (peerMessageEnvelope : PeerMessageEnvelope)
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
                            Result.appError (sprintf "Error TX %A not found" txHash)
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
                            Data =
                                {
                                    GossipDiscoveryMessageDto.ActiveMembers =
                                        getPeerList ()
                                        |> List.map Mapping.gossipPeerToDto
                                }
                                |> Serialization.serializeBinary
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

        let processResponse address (responseItems : ResponseItemMessage list) =
            responseItems
            |> List.map (fun response ->
                RawMock.savePeerData address response.MessageId
                match response.MessageId with
                | PeerList ->
                    response.Data
                    |> Serialization.deserializeBinary<GossipDiscoveryMessageDto>
                    |> fun m ->
                        m.ActiveMembers
                        |> List.map Mapping.gossipPeerFromDto
                        |> PeerListReceived
                        |> Some
                        |> Ok
                | _ -> Ok None
            )

        match peerMessageEnvelope.PeerMessage with
        | GossipMessage m ->
            RawMock.savePeerData address m.MessageId
            None
        | MulticastMessage m ->
            RawMock.savePeerData address m.MessageId
            None
        | RequestDataMessage m -> [ processRequest address m.Items m.SenderIdentity ] |> Some
        | ResponseDataMessage m ->
            processResponse address m.Items |> Some
        | _ ->
            None
