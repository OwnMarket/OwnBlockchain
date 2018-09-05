namespace Chainium.Blockchain.Public.Net.Tests

open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Core.Events
open Chainium.Blockchain.Public.Core.DomainTypes

module WorkflowsMock =

    let processPeerMessage address peerMessage : Result<AppEvent option, AppError list> option =
        match peerMessage with
        | GossipMessage m ->
            RawMock.savePeerData address m.MessageId |> ignore
            None
        | MulticastMessage m ->
            RawMock.savePeerData address m.MessageId |> ignore
            None
        | _ -> None
