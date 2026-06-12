module NotificationService

open System.IO
open System.Net.Http
open CorePush.Firebase
open Notifications

// A global in-memory thread-safe storage for the last registered Android token
let lastRegisteredToken = ref ""

/// Crea e inizializza un'istanza di FirebaseSender partendo dal file JSON delle credenziali
let createSender (serviceAccountJsonPath: string) (httpClient: HttpClient) =
    if not (File.Exists(serviceAccountJsonPath)) then
        failwithf "Il file delle credenziali Firebase non è presente al percorso: %s" serviceAccountJsonPath
    
    // Leggiamo il JSON fornito da Firebase
    let jsonSettings = File.ReadAllText(serviceAccountJsonPath)
    
    // Inizializziamo il mittente di CorePush (è thread-safe, quindi puoi registrarlo come Singleton)
    FirebaseSender(jsonSettings, httpClient)

/// Invia una notifica a un dispositivo specifico e restituisce un Result di F#
let sendAndroidPush (sender: FirebaseSender) (deviceToken: string) (title: string) (body: string) =
    async {
        // Creiamo il payload usando i nostri record F#
        let payload = {
            message = {
                token = deviceToken
                notification = { title = title; body = body }
                android = { priority = "high" }
            }
        }

        try
            // Attendiamo il Task restituito da CorePush
            let! result = sender.SendAsync(payload) |> Async.AwaitTask
            
            // CorePush v4.4 restituisce un oggetto che contiene direttamente IsSuccessStatusCode
            if result.IsSuccessStatusCode then
                return Ok "Notifica inviata con successo!"
            else
                // Se IsSuccessStatusCode è false, l'oggetto result contiene le proprietà Error e Message di Firebase
                let errorMessage = sprintf "Errore Firebase: %s - %s" result.Error result.Message
                return Error errorMessage
                
        with ex ->
            return Error (sprintf "Eccezione durante l'invio: %s" ex.Message)
    }
