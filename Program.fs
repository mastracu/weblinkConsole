
open FSharp.Data

open Suave
open Suave.Http
open Suave.Operators
open Suave.Filters
open Suave.Successful
open Suave.Files
open Suave.RequestErrors
open Suave.Logging
open Suave.Utils
open Suave.Json
open Suave.EventSource

open System
open System.Net
open System.Runtime.Serialization

open Suave.Sockets
open Suave.Sockets.Control
open Suave.ZebraWebSocket
// Websocket handshake function modified for Zebra printers

open System.IO
open System.Xml
open System.Text

open tinyBase64Decoder
open StoreAgent
open MessageLogAgent
open PrintersAgent
open LabelBuilder
open System
open fw

let Crc16b (msg:byte[]) =
    let polynomial      = 0xA001us
    let mutable code    = 0xffffus
    for b in msg do
        code <- code ^^^ uint16 b
        for j in [0..7] do
            if (code &&& 1us <> 0us) then
                code <- (code >>> 1) ^^^ polynomial
            else
                code <- code >>> 1
    code

let sendResetCaptureCmds printerID (printersAgent:PrintersAgent) toLog=
    do printersAgent.SendMsgOverConfigChannel printerID (Opcode.Binary, UTF8.bytes """{}{"capture.channel1.port":"off"} """, true) toLog

let sendBTCaptureCmds printerID (printersAgent:PrintersAgent) toLog=
    do printersAgent.SendMsgOverConfigChannel printerID (Opcode.Binary, UTF8.bytes """{}{"capture.channel1.port":"bt"} """, true) toLog
    do printersAgent.SendMsgOverConfigChannel printerID (Opcode.Binary, UTF8.bytes """{}{"capture.channel1.max_length":"64"} """, true) toLog
    do printersAgent.SendMsgOverConfigChannel printerID (Opcode.Binary, UTF8.bytes """{}{"capture.channel1.delimiter":"\\015\\012"} """, true) toLog

let sendUSBCaptureCmds printerID (printersAgent:PrintersAgent) toLog=
    do printersAgent.SendMsgOverConfigChannel printerID (Opcode.Binary, UTF8.bytes """{}{"capture.channel1.port":"usb"} """, true) toLog
    do printersAgent.SendMsgOverConfigChannel printerID (Opcode.Binary, UTF8.bytes """{}{"capture.channel1.delimiter":"^XZ"} """, true) toLog
    do printersAgent.SendMsgOverConfigChannel printerID (Opcode.Binary, UTF8.bytes """{}{"capture.channel1.max_length":"512"} """, true) toLog

//TODO: https://github.com/SuaveIO/suave/issues/307

let config =
    let port = System.Environment.GetEnvironmentVariable("PORT")
    let ip127  = IPAddress.Parse("127.0.0.1")
    let ipZero = IPAddress.Parse("0.0.0.0")

    { defaultConfig with
        bindings=[ (if port = null then HttpBinding.create HTTP ipZero (uint16 8083)  // 3 Nov - it was ipZero
                    else HttpBinding.create HTTP ipZero (uint16 port)) ]
        homeFolder= Some (Path.GetFullPath "./wwwroot")
    }


let ws allAgents (webSocket : WebSocket) (context: HttpContext) =

  let (storeAgent:StoreAgent, printersAgent:PrintersAgent, logAgent:LogAgent) = allAgents
  let mutable printerUniqueId = ""
  let mutable channelName = ""
  let mutable cbTimeoutEvent:IDisposable = null
  let mutable cbNewMessage2Send:IDisposable = null

  let pongTimer = new System.Timers.Timer(float 20000)
  do pongTimer.AutoReset <- true

  let inbox:ChannelAgent = MailboxProcessor.Start (fun inbox -> async {
        let close = ref false
        while not !close do
            let! (op, pld, fi), isLogged = inbox.Receive()
            if isLogged then
                do logAgent.AppendToLog (sprintf "%s (%s)> %s" (UTF8.toString pld) channelName printerUniqueId)
            else
                ()
            let! successOrError = webSocket.send op (pld|> ByteSegment) fi
            match successOrError with
            | Choice1Of2(con) ->
                close := op = Close
            | Choice2Of2(error) ->
                do System.Console.WriteLine (DateTime.Now.ToString() + sprintf " ### ERROR %A in websocket %s(%s) send operation ###" error printerUniqueId channelName)
                close := true
  })

  let releaseResources() =
        do inbox.Post ((Opcode.Close, [||], true), false)
        if (cbTimeoutEvent <> null) then
            cbTimeoutEvent.Dispose()
        else
            ()
        do pongTimer.Stop()
        do pongTimer.Dispose()
        if (cbNewMessage2Send <> null) then 
            cbNewMessage2Send.Dispose()
        else
            ()
        match channelName with
        | "v1.raw.zebra.com" -> if printerUniqueId.Length>0 then do printersAgent.ClearRawChannel printerUniqueId (Some inbox) else ()
        | "v1.config.zebra.com" -> if printerUniqueId.Length>0 then do printersAgent.ClearConfigChannel printerUniqueId (Some inbox) else ()
        | "v1.main.zebra.com" -> if printerUniqueId.Length>0 then do printersAgent.RemovePrinter printerUniqueId inbox else ()
        | _ -> ()

  // https://github.com/SuaveIO/suave/issues/463
  async {
        let! successOrError = socket {

            // H15 error Heroku - https://devcenter.heroku.com/articles/error-codes#h15-idle-connection
            // A Pong frame MAY be sent unsolicited.  This serves as a unidirectional heartbeat.
            // A response to an unsolicited Pong frame is not expected.
            let pongTimeoutEvent = pongTimer.Elapsed
            do cbTimeoutEvent <- pongTimeoutEvent |> Observable.subscribe (fun _ -> do inbox.Post ((Pong, [||] , true), false))
            do pongTimer.Start()

            // if `loop` is set to false, the server will stop receiving messages
            let mutable loop = true
            while loop do
              // the server will wait for a message to be received without blocking the thread
              let! msg = webSocket.read()

              match msg with
              // the message has type (Opcode * byte [] * bool)
              //
              // Opcode type:
              //   type Opcode = Continuation | Text | Binary | Reserved | Close | Ping | Pong
              //
              // byte [] contains the actual message
              //
              // the last element is the FIN byte, explained later
              //
              // The FIN byte:
              //
              // A single message can be sent separated by fragments. The FIN byte indicates the final fragment. Fragments
              //
              // As an example, this is valid code, and will send only one message to the client:
              //
              // do! webSocket.send Text firstPart false
              // do! webSocket.send Continuation secondPart false
              // do! webSocket.send Continuation thirdPart true
              //
              // More information on the WebSocket protocol can be found at: https://tools.ietf.org/html/rfc6455#page-34
              //

              | (Binary, data, true) ->
                // the message can be converted to a string
                // let str = UTF8.toString data
                let str = Encoding.ASCII.GetString(data)
                let msglen = data.Length
                let response = sprintf "%s <(%s) %s (bytes = %d)" str channelName printerUniqueId msglen
                do logAgent.AppendToLog response
                if (not (channelName="v1.raw.zebra.com") && (msglen < 1024) ) then
                    let jval = JsonValue.Parse str
                    match jval.TryGetProperty "discovery_b64" with
                    | Some jsonval ->
                        let zebraDiscoveryPacket = JsonExtensions.AsString jsonval |> decode64
                        let uniqueID = List.rev (snd (List.fold (fun (pos,acclist) byte -> (pos+1, if (pos > 187 && pos < 202 ) then byte::acclist else acclist))  (0,[]) zebraDiscoveryPacket))
                        do printerUniqueId <- uniqueID |> intListToString
                        do System.Console.WriteLine (DateTime.Now.ToString() + sprintf " discovery_b64 property printerID: %s"  printerUniqueId)
                        do printerUniqueId <- printerUniqueId.Substring (0, (printerUniqueId.IndexOf 'J' + 10))
                        do System.Console.WriteLine (DateTime.Now.ToString() + sprintf " adjusted printerID: %s"  printerUniqueId)
                        do channelName <- "v1.main.zebra.com"
                        do printersAgent.AddPrinter printerUniqueId inbox
                        do printersAgent.SendMsgOverMainChannel printerUniqueId (Opcode.Binary, UTF8.bytes """ { "open" : "v1.raw.zebra.com" } """, true) true
                        do printersAgent.SendMsgOverMainChannel printerUniqueId (Opcode.Binary, UTF8.bytes """ { "open" : "v1.config.zebra.com" } """, true) true
                    | None -> ()

                    match jval.TryGetProperty "alert" with
                    | Some jsonalertval ->
                        match (jsonalertval.GetProperty "condition_id").AsString() with
                        | "SGD SET" ->
                            let sgdFeedback = printersAgent.FetchPrinterInfo printerUniqueId
                            match sgdFeedback with
                            | Some feedback ->
                                do System.Console.WriteLine (DateTime.Now.ToString() + sprintf " Printer application: %s" feedback.sgdSetAlertProcessor )
                                match feedback.sgdSetAlertProcessor with
                                | "priceTag" ->
                                    let barcode = (jsonalertval.GetProperty "setting_value").AsString()
                                    let maybeProd = storeAgent.EanLookup barcode
                                    match maybeProd with
                                    | Some prod ->
                                        let priceString = prod.unitPrice.ToString()
                                        do System.Console.WriteLine (DateTime.Now.ToString() + sprintf " Barcode: %s Price: %s Description: %s" barcode priceString prod.description)
                                        let priceLbl = (buildpricetag false prod)
                                        do printersAgent.SendMsgOverRawChannel printerUniqueId (Opcode.Binary, UTF8.bytes priceLbl, true) true
                                    | None ->
                                        do System.Console.WriteLine (DateTime.Now.ToString() + sprintf " Barcode: %s not found in store" barcode)
                                | "ifadLabelConversion" ->
                                    let label300dpi = (jsonalertval.GetProperty "setting_value").AsString()
                                    let label200dpi = convertIfadLabel label300dpi
                                    do System.Console.WriteLine (DateTime.Now.ToString() + sprintf " Original label: %s" label300dpi)
                                    do System.Console.WriteLine (DateTime.Now.ToString() + sprintf " Converted label: %s" label200dpi)
                                    do printersAgent.SendMsgOverRawChannel printerUniqueId (Opcode.Binary, UTF8.bytes label200dpi, true) true
                                | "dhlRFID" ->
                                    let demoinlabel = (jsonalertval.GetProperty "setting_value").AsString()
                                    let demooutlabel = (encodeDHLLabel demoinlabel)
                                    do System.Console.WriteLine (DateTime.Now.ToString() + sprintf " DHL barcode: %s" demoinlabel)
                                    do System.Console.WriteLine (DateTime.Now.ToString() + sprintf " DHL ZPL format: %s" demooutlabel)
                                    do printersAgent.SendMsgOverRawChannel printerUniqueId (Opcode.Binary, UTF8.bytes demooutlabel, true) true
                                | "labelToGo" -> ()
                                | _ -> ()
                            | None -> ()
                        | _ -> ()
                    | None -> ()

                    match jval.TryGetProperty "channel_name" with
                    | Some jsonval ->
                        do channelName <- JsonExtensions.AsString (jsonval)
                        match jval.TryGetProperty "unique_id" with
                        | Some jsonval ->
                             do printerUniqueId <- JsonExtensions.AsString (jsonval)
                             do System.Console.WriteLine (DateTime.Now.ToString() + sprintf " chan: %s printerID %s" channelName printerUniqueId)
                        | None -> ()
                        match channelName with
                        | "v1.raw.zebra.com" ->
                             // let helloLbl = helloLabel()
                             // do printersAgent.SendMsgOverRawChannel printerUniqueId (Opcode.Binary, Encoding.ASCII.GetBytes helloLbl, true) true
                             do printersAgent.UpdateRawChannel printerUniqueId (Some inbox)
                        | "v1.config.zebra.com" ->
                             do printersAgent.UpdateConfigChannel printerUniqueId (Some inbox)
                             do printersAgent.SendMsgOverConfigChannel printerUniqueId (Opcode.Binary, UTF8.bytes """{}{"alerts.configured":"ALL MESSAGES,SDK,Y,Y,WEBLINK.IP.CONN2,0,N,|SGD SET,SDK,Y,Y,WEBLINK.IP.CONN2,0,N,capture.channel1.data.raw"} """, true) true
                             do printersAgent.SendMsgOverConfigChannel printerUniqueId (Opcode.Binary, UTF8.bytes """{}{"device.product_name":null} """, true) true
                             do printersAgent.SendMsgOverConfigChannel printerUniqueId (Opcode.Binary, UTF8.bytes """{}{"appl.name":null} """, true) true
                             do printersAgent.SendMsgOverConfigChannel printerUniqueId (Opcode.Binary, UTF8.bytes """{}{"device.friendly_name":null} """, true) true
                             do printersAgent.SendMsgOverConfigChannel printerUniqueId (Opcode.Binary, UTF8.bytes """{}{"odometer.user_label_count":"0"} """, true) true
                             match printersAgent.FetchPrinterInfo printerUniqueId with
                                | None -> ()
                                | Some pr -> match pr.sgdSetAlertProcessor with
                                                | "none" -> sendResetCaptureCmds printerUniqueId printersAgent true
                                                | "priceTag" -> sendBTCaptureCmds printerUniqueId printersAgent true
                                                | "labelToGo" -> sendBTCaptureCmds printerUniqueId printersAgent true
                                                | "ifadLabelConversion" -> sendUSBCaptureCmds printerUniqueId printersAgent true
                                                | "dhlRFID" -> sendBTCaptureCmds printerUniqueId printersAgent true
                                                | _ -> ()
                             do printersAgent.SendMsgOverConfigChannel printerUniqueId (Opcode.Binary, UTF8.bytes """{}{"file.cert.expiration":null} """, true) true
                        | _ -> ()
                    | None -> ()

                    match jval.TryGetProperty "device.product_name" with
                    // match jval.TryGetProperty "device.configuration_number" with
                    | Some jsonval ->   let devConfigNumber = JsonExtensions.AsString (jsonval)
                                        do printersAgent.UpdatePartNumber printerUniqueId devConfigNumber
                    | None -> ()

                    match JsonExtensions.TryGetProperty (jval, "file.cert.expiration") with
                    | Some jsonval ->
                        let jsonArray = JsonExtensions.AsArray (jsonval)
                        let maybeWlanIndex = jsonArray |> Seq.tryFindIndex (fun x ->
                                            match x.TryGetProperty "service" with
                                                | Some jsonval -> JsonExtensions.AsString jsonval = "WLAN"
                                                | None -> false
                                        )
                        match maybeWlanIndex with
                            | Some wlanIndex ->
                                match jsonArray.[wlanIndex].TryGetProperty "expires_on" with
                                    | Some jsonval2 -> do printersAgent.UpdateCertExpDate printerUniqueId (JsonExtensions.AsString jsonval2)
                                    | None -> ()
                            | None -> ()

                    | None -> ()

                    match jval.TryGetProperty "appl.name" with
                    | Some jsonval ->   let applName = JsonExtensions.AsString (jsonval)
                                        do printersAgent.UpdateAppVersion printerUniqueId applName
                    | None -> ()

                    match jval.TryGetProperty "device.friendly_name" with
                    | Some jsonval ->   let fName = JsonExtensions.AsString (jsonval)
                                        do printersAgent.UpdateFriendlyName printerUniqueId fName
                    | None -> ()

              | (Ping, data, true) ->
                // Ping message received. Responding with Pong
                // The printer sends a PING message roughly ever 60 seconds. The server needs to respond with a PONG, per RFC6455
                // After three failed PING attempts, the printer disconnects and attempts to reconnect

                do System.Console.WriteLine (DateTime.Now.ToString() + " Ping message from printer " + printerUniqueId + ". Responding with Pong message")
                // A Pong frame sent in response to a Ping frame must have identical "Application data" as found in the message body of the Ping frame being replied to.
                // the `send` function sends a message back to the client
                do inbox.Post ((Opcode.Pong, data, true), false)

              | (Close, _, _) ->
                do System.Console.WriteLine (DateTime.Now.ToString() + sprintf " (%s, %s) Got Close message from printer, releasing resources" printerUniqueId channelName)
                do releaseResources()
                loop <- false

              | (_,_,fi) ->
                do System.Console.WriteLine (DateTime.Now.ToString() + sprintf " Unexpected message from printer of type %A" fi)
        }
        match successOrError with
        | Choice1Of2(con) -> ()
        | Choice2Of2(error) ->
            do System.Console.WriteLine (DateTime.Now.ToString() + sprintf " ### (%s, %s) ERROR in websocket monad, releasing resources ###" printerUniqueId channelName)
            do releaseResources()
        return successOrError
  }



type LogEntryOrTimeout = Timeout | LogEntry of String

let sseContinuation sEvent = (fun out ->
          let errorEvent = new Event<Unit>()
          let timer15sec = new System.Timers.Timer(float 15000)
          do timer15sec.AutoReset <- true
          let timeoutEvent = timer15sec.Elapsed
          do timer15sec.Start()
          let newEvent = (sEvent |> Event.map (fun str -> LogEntry str) ,
                          timeoutEvent |> Event.map (fun _ -> Timeout)) ||> Event.merge

          let inbox = MailboxProcessor.Start (fun inbox ->
                let rec loop n = async {
                        let! newEvent = inbox.Receive()
                        match newEvent with
                        | LogEntry str ->
                            let! _ = string n |> esId out
                            let newLogEntryLines = str.Split '\n'
                            for line in newLogEntryLines do
                                let! _ = line |> data out
                                ()
                            let! _ = string (GC.GetTotalMemory false) |> data out
                            ()
                        | Timeout ->
                            let! _ = "keepAlive" |> comment out
                            ()
                        let! successOrError = dispatch out
                        match successOrError with
                        | Choice1Of2(con) -> ()
                        | Choice2Of2(error) ->
                            errorEvent.Trigger()
                        return! loop (n+1) }
                loop 0)

          let disposableResource = newEvent |> Observable.subscribe (fun arg -> do inbox.Post(arg))


          // https://github.com/SuaveIO/suave/issues/463
          async {
                let! successOrError = socket {
                      System.Console.WriteLine(DateTime.Now.ToString() + " New SSE continuation is setup")
                      let! _ =Control.Async.AwaitEvent(errorEvent.Publish) |>  Suave.Sockets.SocketOp.ofAsync
                      return out
                }
                disposableResource.Dispose()
                timer15sec.Stop()
                timer15sec.Dispose()
                System.Console.WriteLine(DateTime.Now.ToString() + " Exiting SSE - disposed resources in sse handshake continuation function")
                return successOrError
          }
)

[<DataContract>]
type ProductPrinterObj =
   {
      [<field: DataMember(Name = "ProductObj")>]
      ProductObj : Product;
      [<field: DataMember(Name = "id")>]
      id : String;
   }

[<DataContract>]
type Msg2Printer =
   {
      [<field: DataMember(Name = "printerID")>]
      printerID : string;
      [<field: DataMember(Name = "msg")>]
      msg : string;
   }

[<DataContract>]
type File2Printer =
   {
      [<field: DataMember(Name = "printerID")>]
      printerID : string;
      [<field: DataMember(Name = "CISDFCRC16Hdr")>]
      CISDFCRC16Hdr : string;
      [<field: DataMember(Name = "base64Data")>]
      base64Data : string;
   }

let mutable consoleUser = "foo"

let basicAuth =
  let credentialList = [("foo", "bar")]
  Suave.Authentication.authenticateBasic (fun pair ->
                                            consoleUser <- fst pair
                                            List.contains pair credentialList)


let getResourceFromReq<'a> (req : HttpRequest) =
  let getString (rawForm:byte[]) =
    System.Text.Encoding.UTF8.GetString(rawForm)
  req.rawForm |> getString

let app  : WebPart =
  let logEvent = new Event<String>()
  let mLogAgent = LogAgent(logEvent)

  let storeAgent =  StoreAgent()
  let printersAgent = PrintersAgent(mLogAgent)
  let allAgents = (storeAgent, printersAgent, mLogAgent)

  let objectDo func:WebPart =
     mapJson (fun obj ->
                       func obj
                       obj)

  let helperFunction arg =
    let mutable pID = ""
    objectDo (fun (mp:Msg2Printer) ->
                do System.Console.WriteLine (DateTime.Now.ToString() + sprintf " POST /utf82raw - %A" mp)
                let bytes2send = UTF8.bytes (mp.msg)
                do printersAgent.SendMsgOverRawChannel mp.printerID (Opcode.Binary, bytes2send, true ) true
                pID <- mp.printerID
                do System.Console.WriteLine (DateTime.Now.ToString() + sprintf " pID = %s" pID)
             )
    >=> warbler (fun ctx -> OK(if printersAgent.IsKnownID(pID) then "OK\n" else "KO\n")) 
    >=> Writers.setMimeType "text/plain"



  do System.Console.WriteLine (DateTime.Now.ToString() + " WebServer started")

  //let appname = System.Environment.GetEnvironmentVariable("HEROKU_APP_NAME")
  //let releaseAt = System.Environment.GetEnvironmentVariable("HEROKU_RELEASE_CREATED_AT")
  //let releaseVersion = System.Environment.GetEnvironmentVariable("HEROKU_RELEASE_VERSION")

  choose [
    path "/websocketWithSubprotocol" >=> ZebraWebSocket.handShakeWithSubprotocol (chooseSubprotocol "v1.weblink.zebra.com") (ws allAgents)
    path "/sseLog" >=> request (fun _ -> EventSource.handShake (sseContinuation logEvent.Publish ))

    GET >=> choose
        [ path "/hello" >=> OK "Hello GET"
          //stackoverflow.com/questions/4257372/how-to-force-garbage-collector-to-run
          path "/clearlog" >=> warbler (fun ctx -> let _ =  GC.GetTotalMemory true
                                                   OK ( mLogAgent.Empty(); "Log cleared" ))
          path "/logdump.json" >=> warbler (fun ctx -> OK ( mLogAgent.LogDump() ))
          path "/storepricelist.json" >=> warbler (fun ctx -> OK ( storeAgent.StoreInventory() ))
          path "/fwlist.json" >=> warbler (fun ctx -> OK ( fw.fwFileList() ))
          path "/publiconlyprinterslist.json" >=> warbler (fun ctx -> OK ( printersAgent.PrintersInventory(false) ))
          path "/fullprinterslist.json" >=> warbler (fun ctx -> OK ( printersAgent.PrintersInventory(true) ))
          path "/knownprinters.json" >=> warbler (fun ctx -> OK (  printersAgent.PrintersDefault() ))
          basicAuth browseHome
        ]
    POST >=>
      choose
        [ path "/printerupdate" >=>
           objectDo (fun prt -> printersAgent.UpdateApp prt.uniqueID prt.sgdSetAlertProcessor
                                match prt.sgdSetAlertProcessor with
                                | "none" -> sendResetCaptureCmds prt.uniqueID printersAgent true
                                | "priceTag" -> sendBTCaptureCmds prt.uniqueID printersAgent true
                                | "labelToGo" -> sendBTCaptureCmds prt.uniqueID printersAgent true
                                | "ifadLabelConversion" -> sendUSBCaptureCmds prt.uniqueID printersAgent true
                                | "dhlRFID" -> sendBTCaptureCmds prt.uniqueID printersAgent true
                                | _ -> ()
                    )
          path "/productupdate" >=> objectDo (fun prod -> storeAgent.UpdateWith prod)
          path "/productremove" >=> objectDo (fun prod -> storeAgent.RemoveSku prod.sku)

          path "/json2printer" >=> objectDo (fun (mp:Msg2Printer) ->
                                               do System.Console.WriteLine (DateTime.Now.ToString() + sprintf " POST /json2printer - %A" mp)
                                               let bytes2send = UTF8.bytes (mp.msg)
                                               do printersAgent.SendMsgOverConfigChannel mp.printerID (Opcode.Binary, bytes2send, true ) true)
          path "/printproduct" >=> objectDo (fun (prodprint:ProductPrinterObj) ->
                                               do System.Console.WriteLine (DateTime.Now.ToString() + sprintf " POST /printproduct - %A" prodprint)
                                               let bytes2send = prodprint.ProductObj |> buildpricetag false |> UTF8.bytes
                                               do printersAgent.SendMsgOverRawChannel prodprint.id (Opcode.Binary, bytes2send, true) true)
          // http://blog.tamizhvendan.in/blog/2015/06/11/building-rest-api-in-fsharp-using-suave/

          path "/printencproduct" >=> objectDo (fun (prodprint:ProductPrinterObj) ->
                                               do System.Console.WriteLine (DateTime.Now.ToString() + sprintf " POST /printencproduct - %A" prodprint)
                                               let bytes2send = prodprint.ProductObj |> buildpricetag true |> UTF8.bytes
                                               do printersAgent.SendMsgOverRawChannel prodprint.id (Opcode.Binary, bytes2send, true) true)
          path "/upgradeprinter" >=> objectDo (fun (fwjob:FwJobObj) ->
                                               do System.Console.WriteLine (DateTime.Now.ToString() + sprintf " POST /upgradeprinter -- %A" fwjob)
                                               do match printersAgent.FetchPrinterInfo fwjob.id with
                                                  | None -> ()
                                                  | Some pr -> match pr.rawChannelAgent with
                                                               | None -> ()
                                                               | Some agent -> do doFwUpgrade fwjob agent mLogAgent)
          path "/utf82raw" >=> helperFunction ()
          path "/CISDFCRC16" >=> objectDo (fun (fp:File2Printer) ->
                                               do System.Console.WriteLine (DateTime.Now.ToString() + sprintf " POST /CISDFCRC16 - %A" fp.CISDFCRC16Hdr)
                                               let bytes2send = Array.append (ASCII.bytes fp.CISDFCRC16Hdr) (Convert.FromBase64String fp.base64Data)
                                               do printersAgent.SendMsgOverRawChannel fp.printerID (Opcode.Binary, bytes2send, true ) false
                                               mLogAgent.AppendToLog (" POST /CISDFCRC16 - " + fp.CISDFCRC16Hdr) )
          path "/FX9600" >=> request (fun r ->
                                        let bodyAscii = Encoding.ASCII.GetString r.rawForm
                                        mLogAgent.AppendToLog ("POST /FX9600 : " + bodyAscii)
                                        OK ("OK\n"))
        ]
    NOT_FOUND "Found no handlers." ]

//https://help.heroku.com/tickets/560930

[<EntryPoint>]
let main _ =
  startWebServer config app
  0
