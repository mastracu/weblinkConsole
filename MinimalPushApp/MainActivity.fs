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

// 1. Modello Dati con mappatura esplicita al JSON reale
type Printer = {
    [<System.Text.Json.Serialization.JsonPropertyName("uniqueID")>]
    serialNumber : string
    
    [<System.Text.Json.Serialization.JsonPropertyName("productName")>]
    model : string
    
    [<System.Text.Json.Serialization.JsonPropertyName("friendlyName")>]
    friendlyName : string
    
    [<System.Text.Json.Serialization.JsonPropertyName("connectedSince")>]
    connectionTime : string
}

type RegisterTokenRequest = {
    deviceId: string
    token: string
}


// 2. Listener per la ricezione del token Firebase all'avvio
type TokenListener() =
    inherit Java.Lang.Object()
    interface IOnCompleteListener with
        member this.OnComplete(task) =
            if task.IsSuccessful then
                let token = task.Result.ToString()
                AppEvents.tokenRefreshed.Trigger(token)
            else
                AppEvents.messageReceived.Trigger(("Errore", "Impossibile recuperare il Token all'avvio."))

[<Activity(Label = "Zebra Monitor", MainLauncher = true, Exported = true)>]
type MainActivity() =
    inherit Activity()

    // Elementi dell'interfaccia utente
    let mutable statusTextView : TextView = null
    let mutable tokenTextView : TextView = null
    let mutable tableLayout : TableLayout = null
    let mutable refreshButton : Button = null
    let mutable logTextView : TextView = null

    // Utilizziamo un unico HttpClient per tutte le chiamate (POST e GET)
    let httpClient = new HttpClient()

    // 3. Funzione per registrare il token Firebase sul backend via POST
    member this.RegisterTokenWithBackend(serverUrl: string, token: string) =
        async {
            try
                // Recuperiamo l'ID univoco del dispositivo Android
                let androidId = Android.Provider.Settings.Secure.GetString(this.ContentResolver, Android.Provider.Settings.Secure.AndroidId)

                // Creiamo il payload strutturato con i due campi
                let payload = { deviceId = androidId; token = token }
                let json = System.Text.Json.JsonSerializer.Serialize(payload)
                let content = new StringContent(json, Encoding.UTF8, "application/json")

                this.RunOnUiThread(Action(fun () -> 
                    statusTextView.Text <- "Invio token al backend..."
                    statusTextView.SetTextColor(Color.Orange)
                ))

                let! response = httpClient.PostAsync(serverUrl, content) |> Async.AwaitTask

                if response.IsSuccessStatusCode then
                    this.RunOnUiThread(Action(fun () ->
                        statusTextView.Text <- "Stato: Registrato sul backend \u2713"
                        statusTextView.SetTextColor(Color.ParseColor("#2E7D32"))
                    ))
                else
                    let! errorMsg = response.Content.ReadAsStringAsync() |> Async.AwaitTask
                    this.RunOnUiThread(Action(fun () ->
                        statusTextView.Text <- "Errore registrazione backend"
                        statusTextView.SetTextColor(Color.Red)
                        logTextView.Text <- sprintf "[HTTP Errore POST] %s\n\n" errorMsg + logTextView.Text
                    ))
            with ex ->
                this.RunOnUiThread(Action(fun () ->
                    statusTextView.Text <- "Errore connessione token"
                    statusTextView.SetTextColor(Color.Red)
                    logTextView.Text <- sprintf "[Errore Connessione POST] %s\n\n" ex.Message + logTextView.Text
                ))
        }

    // 4. Funzione per recuperare la lista delle stampanti via HTTPS GET
    member this.FetchPrinterList() =
        async {
            let url = "https://weblink.mastracu.it:446//fullprinterslist.json"
            try
                let! response = httpClient.GetAsync(url) |> Async.AwaitTask
                
                if response.IsSuccessStatusCode then
                    let! jsonString = response.Content.ReadAsStringAsync() |> Async.AwaitTask
                    
                    let options = JsonSerializerOptions(PropertyNamingPolicy = JsonNamingPolicy.CamelCase)
                    let printers = JsonSerializer.Deserialize<list<Printer>>(jsonString, options)
                    
                    this.RunOnUiThread(Action(fun () ->
                        this.PopulatePrinterTable(printers)
                    ))
                else
                    this.RunOnUiThread(Action(fun () ->
                        logTextView.Text <- sprintf "[HTTP Errore GET] Stato %d\n" (int response.StatusCode) + logTextView.Text
                    ))
            with ex ->
                this.RunOnUiThread(Action(fun () ->
                    logTextView.Text <- sprintf "[Errore Connessione GET] %s\n" ex.Message + logTextView.Text
                ))
        }

    // Helper per creare una cella di testo formattata
    member this.CreateTableCell(text: string, isHeader: bool) =
        let textView = new TextView(this)
        textView.Text <- text
        textView.SetPadding(15, 20, 15, 20)
        textView.Gravity <- GravityFlags.CenterVertical
        
        if isHeader then
            textView.SetTypeface(null, TypefaceStyle.Bold)
            textView.SetTextColor(Color.White)
            textView.TextSize <- 15f
        else
            textView.SetTextColor(Color.Black)
            textView.TextSize <- 14f
            
        textView

    // 5. Funzione per ricostruire graficamente la tabella delle stampanti
    member this.PopulatePrinterTable(printers: list<Printer>) =
        tableLayout.RemoveAllViews()

        // --- CREAZIONE INTESTAZIONE (HEADER) ---
        let headerRow = new TableRow(this)
        headerRow.SetBackgroundColor(Color.ParseColor("#1976D2")) // Blu Zebra

        let h1 = this.CreateTableCell("N. Serie", true)
        let h2 = this.CreateTableCell("Modello", true)
        let h3 = this.CreateTableCell("Friendly Name", true)
        let h4 = this.CreateTableCell("Connessione", true)

        headerRow.AddView(h1)
        headerRow.AddView(h2)
        headerRow.AddView(h3)
        headerRow.AddView(h4)
        tableLayout.AddView(headerRow)

        // Se l'elenco è vuoto
        if printers.IsEmpty then
            let emptyRow = new TableRow(this)
            emptyRow.SetBackgroundColor(Color.White)
            let noDataCell = this.CreateTableCell("Nessuna stampante connessa.", false)
            let layoutParams = new TableRow.LayoutParams()
            layoutParams.Span <- 4
            noDataCell.LayoutParameters <- layoutParams
            emptyRow.AddView(noDataCell)
            tableLayout.AddView(emptyRow)
        else
            // --- CREAZIONE RIGHE DATI ---
            printers 
            |> List.iteri (fun index printer ->
                let row = new TableRow(this)
                if index % 2 = 0 then row.SetBackgroundColor(Color.ParseColor("#F5F5F5"))
                else row.SetBackgroundColor(Color.White)

                let serialCell = this.CreateTableCell(printer.serialNumber, false)
                serialCell.SetTypeface(null, TypefaceStyle.Bold)
                
                let modelCell = this.CreateTableCell(printer.model, false)
                let nameCell = this.CreateTableCell(printer.friendlyName, false)
                
                let connTimeFormatted = 
                    match DateTime.TryParse(printer.connectionTime) with
                    | true, parsedDate -> parsedDate.ToString("dd/MM HH:mm:ss")
                    | false, _ -> printer.connectionTime

                let timeCell = this.CreateTableCell(connTimeFormatted, false)

                row.AddView(serialCell)
                row.AddView(modelCell)
                row.AddView(nameCell)
                row.AddView(timeCell)

                tableLayout.AddView(row)
            )

    override this.OnCreate(bundle: Bundle) =
        base.OnCreate(bundle)

        // ==========================================
        // COSTRUZIONE INTERFACCIA GRAFICA (VERTICAL LAYOUT)
        // ==========================================
        let mainLayout = new LinearLayout(this)
        mainLayout.Orientation <- Orientation.Vertical
        mainLayout.SetPadding(30, 30, 30, 30)

        // 1. Titolo e Stato di connessione
        statusTextView <- new TextView(this, TextSize = 18f, Text = "Stato: Inizializzazione...")
        statusTextView.SetTypeface(null, TypefaceStyle.Bold)
        statusTextView.SetPadding(0, 0, 0, 10)
        mainLayout.AddView(statusTextView)

        // 2. Token View (Piccola in alto, utile per debug)
        tokenTextView <- new TextView(this, TextSize = 11f, Text = "Token: In attesa...")
        tokenTextView.SetTextColor(Color.Gray)
        tokenTextView.SetTextIsSelectable(true)
        tokenTextView.SetPadding(0, 0, 0, 15)
        mainLayout.AddView(tokenTextView)

        // 3. Pulsante di Aggiornamento Manuale per le Stampanti
        refreshButton <- new Button(this, Text = "Aggiorna Elenco Stampanti")
        refreshButton.SetBackgroundColor(Color.ParseColor("#1976D2"))
        refreshButton.SetTextColor(Color.White)
        mainLayout.AddView(refreshButton)

        // Spaziatore
        let spacer = new View(this)
        spacer.LayoutParameters <- new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 20)
        mainLayout.AddView(spacer)

        // 4. Tabella Dinamica (dentro uno ScrollView orizzontale)
        let horizontalScrollView = new HorizontalScrollView(this)
        tableLayout <- new TableLayout(this)
        
        let tableParams = new TableLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
        tableLayout.LayoutParameters <- tableParams
        tableLayout.SetColumnStretchable(0, true)
        tableLayout.SetColumnStretchable(1, true)
        tableLayout.SetColumnStretchable(2, true)
        tableLayout.SetColumnStretchable(3, true)

        horizontalScrollView.AddView(tableLayout)
        mainLayout.AddView(horizontalScrollView)

        // Spaziatore inferiore
        let spacer2 = new View(this)
        spacer2.LayoutParameters <- new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 20)
        mainLayout.AddView(spacer2)

        // 5. Area inferiore dei log per le notifiche e la diagnostica
        let logLabel = new TextView(this, TextSize = 14f, Text = "Log Notifiche / Diagnostica:")
        logLabel.SetTypeface(null, TypefaceStyle.Bold)
        mainLayout.AddView(logLabel)

        let logScrollView = new ScrollView(this)
        logTextView <- new TextView(this, TextSize = 12f, Text = "App avviata.\n")
        logScrollView.AddView(logTextView)
        mainLayout.AddView(logScrollView)

        this.SetContentView(mainLayout)

        // ==========================================
        // GESTIONE DEGLI EVENTI FIREBASE (NOTIFICHE PUSH)
        // ==========================================
        AppEvents.tokenRefreshed.Publish.Add(fun token ->
            this.RunOnUiThread(Action(fun () ->
                tokenTextView.Text <- "Token: " + (if token.Length > 30 then token.Substring(0, 30) + "..." else token)
                
                // Registra in automatico il token sul backend via POST
                let serverUrl = "https://weblink.mastracu.it:446/register-token"
                this.RegisterTokenWithBackend(serverUrl, token) |> Async.Start
            ))
        )

        AppEvents.messageReceived.Publish.Add(fun (title, body) ->
            this.RunOnUiThread(Action(fun () ->
                let time = DateTime.Now.ToString("HH:mm:ss")
                let newLine = sprintf "[%s] Notifica: %s - %s\n" time title body
                logTextView.Text <- newLine + logTextView.Text
            ))
        )

        // ==========================================
        // OPERAZIONI DI AVVIO
        // ==========================================
        
        // 1. Recupera subito l'elenco delle stampanti via GET
        this.FetchPrinterList() |> Async.Start

        // 2. Inizializzazione Firebase
        try
            FirebaseApp.InitializeApp(this.ApplicationContext) |> ignore
            statusTextView.Text <- "Stato: Firebase OK \u2713"
            statusTextView.SetTextColor(Color.ParseColor("#2E7D32"))
            
            // Richiesta asincrona del Token
            FirebaseMessaging.Instance.GetToken().AddOnCompleteListener(new TokenListener()) |> ignore
        with ex ->
            statusTextView.Text <- "Stato: Errore Inizializzazione Firebase"
            statusTextView.SetTextColor(Color.Red)
            logTextView.Text <- "Errore Firebase: " + ex.Message + "\n" + logTextView.Text

        // 3. Gestore click pulsante aggiorna
        refreshButton.Click.Add(fun _ -> 
            this.FetchPrinterList() |> Async.Start
        )

    override this.OnDestroy() =
        base.OnDestroy()
        httpClient.Dispose()
