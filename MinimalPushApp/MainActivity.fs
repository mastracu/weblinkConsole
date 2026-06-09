namespace MinimalPushApp

open Android.App
open Android.OS
open Android.Util
open Android.Gms.Tasks
open Firebase.Messaging

// Helper per ascoltare il risultato della richiesta del Token in stile Java/Android
type TokenListener() =
    inherit Java.Lang.Object()
    interface IOnCompleteListener with
        member this.OnComplete(task) =
            if task.IsSuccessful then
                let token = task.Result.ToString()
                Log.Debug("FCM_TEST", "========================================") |> ignore
                Log.Debug("FCM_TEST", sprintf "TOKEN ALL'AVVIO: %s" token) |> ignore
                Log.Debug("FCM_TEST", "========================================") |> ignore
            else
                Log.Error("FCM_TEST", "Recupero token fallito") |> ignore


[<Activity(Label = "@string/app_name", MainLauncher = true, Exported = true)>]
type MainActivity() =
    inherit Activity()

    override this.OnCreate(bundle: Bundle) =
        base.OnCreate(bundle)
        // Impostiamo un layout di base (creato in automatico dal template)
        this.SetContentView(Resource.Layout.Main)

        // All'avvio, chiediamo a Firebase qual è il nostro Token attuale
        FirebaseMessaging.Instance.GetToken().AddOnCompleteListener(new TokenListener()) |> ignore
