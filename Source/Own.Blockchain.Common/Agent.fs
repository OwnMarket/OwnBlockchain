namespace Own.Blockchain.Common

open Own.Common.FSharp

module Agent =

    let start messageHandler =
        let agent = MailboxProcessor.Start <| fun inbox ->
            let rec messageLoop () =
                async {
                    let! message = inbox.Receive()
                    do! messageHandler message
                    return! messageLoop ()
                }

            messageLoop ()

        agent.Error.Add (fun ex -> Log.errorf "AGENT: %s" ex.AllMessagesAndStackTraces)

        agent

    let startStateful initialState messageHandler =
        let agent = MailboxProcessor.Start <| fun inbox ->
            let rec messageLoop oldState =
                async {
                    let! message = inbox.Receive()
                    let! newState = messageHandler oldState message
                    return! messageLoop newState
                }

            messageLoop initialState

        agent.Error.Add (fun ex -> Log.errorf "AGENT: %s" ex.AllMessagesAndStackTraces)

        agent
