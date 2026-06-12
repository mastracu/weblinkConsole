namespace MinimalPushApp

open Android.App
open Android.Util
open Firebase.Messaging

// --- EVENT BUS: Permette al servizio di parlare con l'Activity ---
module AppEvents =
    let messageReceived = new Event<string * string>() // (Titolo, Body)
    let tokenRefreshed = new Event<string>()

// --- SERVIZIO IN BACKGROUND ---
[<Service(Exported = true)>]
[<IntentFilter([| "com.google.firebase.MESSAGING_EVENT" |])>]
type MyFirebaseService() =
    inherit FirebaseMessagingService()

    override this.OnNewToken(token: string) =
        Log.Debug("FCM_TEST", sprintf "NUOVO TOKEN: %s" token) |> ignore
        // Avvisiamo la UI che c'è un nuovo token
        AppEvents.tokenRefreshed.Trigger(token)

    override this.OnMessageReceived(message: RemoteMessage) =
        let title = if message.GetNotification() <> null then message.GetNotification().Title else "Senza Titolo"
        let body = if message.GetNotification() <> null then message.GetNotification().Body else "Senza Corpo"
        
        Log.Debug("FCM_TEST", sprintf "NOTIFICA: %s - %s" title body) |> ignore
        
        // Avvisiamo la UI che è arrivata una notifica
        AppEvents.messageReceived.Trigger((title, body))
