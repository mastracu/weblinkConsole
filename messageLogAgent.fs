module MessageLogAgent

open System
open System.Runtime.Serialization.Json
open System.Runtime.Serialization

open JsonHelper

[<DataContract>]
type LogEntry =
   { 
      [<field: DataMember(Name = "timestamp")>]
      timestamp : string;
      [<field: DataMember(Name = "txt")>]
      txt : string;
   }

type Log = 
   { msgList : LogEntry list } 
   static member Empty = {msgList = [] }
   member x.AppendToLog (msg:string) = 
      { msgList = {timestamp = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"); txt=msg} :: x.msgList }

type LogAgentMsg = 
    | Exit
    | Reset
    | AppendToLog of string
    | LogDump  of AsyncReplyChannel<string>

type LogAgent(logEvent:Event<String>) =
    let printerMsgMailboxProcessor =
        MailboxProcessor.Start(fun inbox ->
            let rec locationAgentLoop agentLog =
                async { let! msg = inbox.Receive()
                        match msg with
                        | Exit -> return ()
                        | Reset -> return! locationAgentLoop Log.Empty
                        | AppendToLog newMsg ->
                            logEvent.Trigger(newMsg)
                            return! locationAgentLoop (agentLog.AppendToLog newMsg)
                        | LogDump replyChannel ->
                            replyChannel.Reply (json<LogEntry array> (List.toArray agentLog.msgList)) 
                            return! locationAgentLoop agentLog
                      }
            locationAgentLoop Log.Empty
        )
    member this.Exit() = printerMsgMailboxProcessor.Post(Exit)
    member this.Empty() = printerMsgMailboxProcessor.Post(Reset)
    member this.AppendToLog newEntry = printerMsgMailboxProcessor.Post(AppendToLog newEntry)
    member this.LogDump() = printerMsgMailboxProcessor.PostAndReply((fun reply -> LogDump reply), timeout = 2000)
