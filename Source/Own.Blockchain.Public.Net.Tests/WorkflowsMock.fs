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
        : Result<AppEvent option, AppError list> option
        =

        let processRequest address messageId targetAddress =
            match messageId with
            | Tx txHash ->
                if RawMock.hasData address messageId then
                    {
                        PeerMessageEnvelope.NetworkId = getNetworkId ()
                        PeerMessage =
                            {
                                MessageId = messageId
                                Data = "txEnvelope" |> Conversion.stringToBytes
                            }
                            |> ResponseDataMessage
                    }
                    |> respondToPeer targetAddress
                    None
                else
                    Result.appError (sprintf "Error Tx %A not found" txHash) |> Some
            | EquivocationProof equivocationProofHash ->
                if RawMock.hasData address messageId then
                    {
                        PeerMessageEnvelope.NetworkId = getNetworkId ()
                        PeerMessage =
                            {
                                MessageId = messageId
                                Data = "equivocationProof" |> Conversion.stringToBytes
                            }
                            |> ResponseDataMessage
                    }
                    |> respondToPeer targetAddress
                    None
                else
                    Result.appError (sprintf "Error EquivocationProof %A not found" equivocationProofHash) |> Some
            | Block blockNr ->
                if RawMock.hasData address messageId then
                    {
                        PeerMessageEnvelope.NetworkId = getNetworkId ()
                        PeerMessage =
                            {
                                MessageId = messageId
                                Data = "blockEnvelope" |> Conversion.stringToBytes
                            }
                            |> ResponseDataMessage
                    }
                    |> respondToPeer targetAddress
                    None
                else
                    Result.appError (sprintf "Error Block %A not found" blockNr) |> Some
            | Consensus _ -> Result.appError "Cannot request consensus message from Peer" |> Some
            | ConsensusState _ -> Result.appError "Cannot request consensus state from Peer" |> Some
            | PeerList -> None

        match peerMessageEnvelope.PeerMessage with
        | GossipMessage m ->
            RawMock.savePeerData address m.MessageId
            None
        | MulticastMessage m ->
            RawMock.savePeerData address m.MessageId
            None
        | RequestDataMessage m -> processRequest address m.MessageId m.SenderIdentity
        | ResponseDataMessage m ->
            RawMock.savePeerData address m.MessageId
            None
        | _ ->
            None
