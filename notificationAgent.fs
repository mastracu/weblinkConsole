module NotificationAgent

// Il payload ricevuto dal client Android
type RegisterTokenRequest = {
    deviceId: string
    token: string
}

// Messaggi dell'agente: ora accettiamo la coppia (DeviceID, Token)
type TokenMessage =
    | Register of string * string * AsyncReplyChannel<unit> // (deviceId, token, reply)
    | GetAll of AsyncReplyChannel<string list>

let tokenManager = MailboxProcessor.Start(fun inbox ->
    // Usiamo una mappa immutabile F# (Map<string, string>) dove la chiave è il deviceId
    let rec loop (deviceMap: Map<string, string>) =
        async {
            let! msg = inbox.Receive()
            match msg with
            | Register(deviceId, newToken, replyChannel) ->
                // F# Map.add sovrascrive automaticamente il valore se la chiave esiste già!
                // Questo risolve nativamente la sostituzione del vecchio token.
                let updatedMap = deviceMap |> Map.add deviceId newToken
                printfn "Token registrato per il dispositivo %s. Dispositivi unici attivi: %d" deviceId updatedMap.Count
                replyChannel.Reply(())
                return! loop updatedMap
                
            | GetAll(replyChannel) ->
                // Estraiamo solo i token validi correnti (i valori della mappa)
                let tokenList = deviceMap |> Map.toSeq |> Seq.map snd |> Seq.toList
                replyChannel.Reply(tokenList)
                return! loop deviceMap
        }
    loop Map.empty
)

