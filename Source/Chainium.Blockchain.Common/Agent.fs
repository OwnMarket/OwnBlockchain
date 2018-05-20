namespace Chainium.Blockchain.Common

open System

module Agent =

    let start messageHandler =
        MailboxProcessor.Start <| fun inbox ->
            let rec messageLoop () =
                async {
                    let! message = inbox.Receive()
                    do! messageHandler message
                    return! messageLoop ()
                }

            messageLoop ()

    let startStateful messageHandler initialState =
        MailboxProcessor.Start <| fun inbox ->
            let rec messageLoop oldState =
                async {
                    let! message = inbox.Receive()
                    let! newState = messageHandler oldState message
                    return! messageLoop newState
                }

            messageLoop initialState
