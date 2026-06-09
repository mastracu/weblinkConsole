namespace MinimalPushApp

open Android.App
open Android.Util
open Firebase.Messaging

// Questi attributi auto-generano le righe necessarie nell'AndroidManifest.xml!
[<Service(Exported = true)>]
[<IntentFilter([| "com.google.firebase.MESSAGING_EVENT" |])>]
type MyFirebaseService() =
    inherit FirebaseMessagingService()

    // Scatta quando Firebase assegna un nuovo token al dispositivo
    override this.OnNewToken(token: string) =
        Log.Debug("FCM_TEST", "========================================") |> ignore
        Log.Debug("FCM_TEST", sprintf "NUOVO DEVICE TOKEN: %s" token) |> ignore
        Log.Debug("FCM_TEST", "========================================") |> ignore

    // Scatta quando ricevi una notifica e l'app è IN PRIMO PIANO
    override this.OnMessageReceived(message: RemoteMessage) =
        let title = if message.GetNotification() <> null then message.GetNotification().Title else "Nessun titolo"
        let body = if message.GetNotification() <> null then message.GetNotification().Body else "Nessun corpo"
        
        Log.Debug("FCM_TEST", sprintf "NOTIFICA RICEVUTA! Titolo: %s - Body: %s" title body) |> ignore

