namespace MinimalPushApp

open System
open System.Net.Http
open System.Text
open System.Text.Json
open Android.App
open Android.OS
open Android.Widget
open Android.Views
open Android.Graphics
open Android.Gms.Tasks
open Firebase
open Firebase.Messaging



// Payload contract from the Android client
type RegisterTokenRequest = {
    token: string
}


// Helper to handle the token generation on startup
type TokenListener() =
    inherit Java.Lang.Object()
    interface IOnCompleteListener with
        member this.OnComplete(task) =
            if task.IsSuccessful then
                let token = task.Result.ToString()
                AppEvents.tokenRefreshed.Trigger(token)
            else
                AppEvents.messageReceived.Trigger(("Errore", "Impossibile recuperare il Token all'avvio."))

[<Activity(Label = "Test Push", MainLauncher = true, Exported = true)>]
type MainActivity() =
    inherit Activity()

    // UI elements
    let mutable statusTextView : TextView = null
    let mutable tokenTextView : TextView = null
    let mutable logTextView : TextView = null
    let mutable serverUrlInput : EditText = null
    let mutable sendTokenButton : Button = null

    // 2. Definizione iniziale, standard e pulita di HttpClient
    let httpClient = new HttpClient()
    
    // Function to register the token to your F# backend
    member this.RegisterTokenWithBackend(serverUrl: string, token: string) =
        async {
            try
                this.RunOnUiThread(Action(fun () -> 
                    statusTextView.Text <- "Invio token al backend..."
                    statusTextView.SetTextColor(Color.Orange)
                ))

                // Create a simple payload map
                let payload = Map.ofList [ "token", token ]
                let json = JsonSerializer.Serialize(payload)
                let content = new StringContent(json, Encoding.UTF8, "application/json")

                // Make the POST request
                let! response = httpClient.PostAsync(serverUrl, content) |> Async.AwaitTask

                if response.IsSuccessStatusCode then
                    this.RunOnUiThread(Action(fun () ->
                        statusTextView.Text <- "Stato: Registrato sul backend \u2713"
                        statusTextView.SetTextColor(Color.ParseColor("#2E7D32")) // Dark Green
                    ))
                else
                    let! errorMsg = response.Content.ReadAsStringAsync() |> Async.AwaitTask
                    this.RunOnUiThread(Action(fun () ->
                        statusTextView.Text <- "Errore registrazione backend"
                        statusTextView.SetTextColor(Color.Red)
                        logTextView.Text <- sprintf "[HTTP Errore] %s\n\n" errorMsg + logTextView.Text
                    ))
            with ex ->
                this.RunOnUiThread(Action(fun () ->
                    statusTextView.Text <- "Errore di connessione"
                    statusTextView.SetTextColor(Color.Red)
                    logTextView.Text <- sprintf "[Connessione Fallita] %s\n\n" ex.Message + logTextView.Text
                ))
        }

    override this.OnCreate(bundle: Bundle) =
        base.OnCreate(bundle)

        // 1. Build the UI
        let layout = new LinearLayout(this)
        layout.Orientation <- Orientation.Vertical
        layout.SetPadding(40, 40, 40, 40)

        // Status
        statusTextView <- new TextView(this, TextSize = 18f, Text = "Stato: Inizializzazione...")
        statusTextView.SetTypeface(null, TypefaceStyle.Bold)
        layout.AddView(statusTextView)

        // Backend Server URL Input
        let urlLabel = new TextView(this, TextSize = 14f, Text = "Indirizzo API Backend:")
        urlLabel.SetPadding(0, 20, 0, 5)
        layout.AddView(urlLabel)

        // Note: 10.0.2.2 is how the Android Emulator connects to localhost on your host PC
        serverUrlInput <- new EditText(this, Text = "https://weblink.mastracu.it:446/register-token")
        layout.AddView(serverUrlInput)

        // Button to manually send token
        sendTokenButton <- new Button(this, Text = "Invia Token al Backend")
        sendTokenButton.Enabled <- false
        layout.AddView(sendTokenButton)

        // Token View
        tokenTextView <- new TextView(this, TextSize = 12f, Text = "Token: In attesa...")
        tokenTextView.SetPadding(0, 20, 0, 40)
        tokenTextView.SetTextIsSelectable(true)
        tokenTextView.SetTextColor(Color.DarkBlue)
        layout.AddView(tokenTextView)

        // Log Title
        let logLabel = new TextView(this, TextSize = 16f, Text = "Log Notifiche Ricevute:")
        logLabel.SetTypeface(null, TypefaceStyle.Bold)
        logLabel.SetPadding(0, 0, 0, 10)
        layout.AddView(logLabel)

        // Scrolling log area
        let scrollView = new ScrollView(this)
        logTextView <- new TextView(this, TextSize = 14f, Text = "")
        scrollView.AddView(logTextView)
        layout.AddView(scrollView)

        this.SetContentView(layout)

        // Current retrieved token
        let mutable currentToken = ""

        // 2. Handle events
        AppEvents.tokenRefreshed.Publish.Add(fun token ->
            currentToken <- token
            this.RunOnUiThread(Action(fun () ->
                tokenTextView.Text <- "Token:\n" + token
                sendTokenButton.Enabled <- true
                
                // Automatically register the token when it's generated
                let serverUrl = serverUrlInput.Text
                this.RegisterTokenWithBackend(serverUrl, token) |> Async.Start
            ))
        )

        AppEvents.messageReceived.Publish.Add(fun (title, body) ->
            this.RunOnUiThread(Action(fun () ->
                let time = DateTime.Now.ToString("HH:mm:ss")
                let newLine = sprintf "[%s] %s\n> %s\n\n" time title body
                logTextView.Text <- newLine + logTextView.Text
            ))
        )

        // Register manually on button click
        sendTokenButton.Click.Add(fun _ ->
            if not (String.IsNullOrEmpty(currentToken)) then
                let serverUrl = serverUrlInput.Text
                this.RegisterTokenWithBackend(serverUrl, currentToken) |> Async.Start
        )

        // 3. Initialize Firebase
        try
            FirebaseApp.InitializeApp(this.ApplicationContext) |> ignore
            statusTextView.Text <- "Stato: Firebase OK \u2713"
            statusTextView.SetTextColor(Color.ParseColor("#2E7D32"))
            FirebaseMessaging.Instance.GetToken().AddOnCompleteListener(new TokenListener()) |> ignore
        with ex ->
            statusTextView.Text <- "Stato: Errore Inizializzazione"
            statusTextView.SetTextColor(Color.Red)
            logTextView.Text <- "Errore: " + ex.Message

    override this.OnDestroy() =
        base.OnDestroy()
        httpClient.Dispose() // Clean up HttpClient when Activity is destroyed
