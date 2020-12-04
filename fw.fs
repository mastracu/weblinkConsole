module fw

open System
open System.Runtime.Serialization

open System.IO
open JsonHelper
open Suave

open ZebraWebSocket
open MessageLogAgent
open PrintersAgent
open System.Diagnostics
open Suave.Utils
open System.Text

[<DataContract>]
type FwFile =
   { 
      [<field: DataMember(Name = "fwFileName")>]
      fwFileName : string;
   }

let fwFolderFullPath = Path.GetFullPath "./firmware/"

let fwFileList() = json<FwFile array> (
                      [|"*.zpl";"*.ZPL";"*.NRD";"*.nrd"|]
                      |> (Array.map (fun filter -> Directory.GetFiles(fwFolderFullPath, filter)) >> Array.concat)
                      |> (Array.map (fun s -> {fwFileName = Path.GetFileName s}) )
                    )

[<DataContract>]
type FwJobObj =
   { 
      [<field: DataMember(Name = "fwFile")>]
      fwFile : String;
      [<field: DataMember(Name = "id")>]
      id : String;
   }

let doFwUpgrade (fwJob:FwJobObj) (agent: ChannelAgent) (mLogAgent:LogAgent) =
    // I don't use websocket continuation frames for firmware download
    async {
        let chunckSize = 4096  // it is 4096
        let buffer = Array.zeroCreate chunckSize
        // let copyOfBuffer = Array.zeroCreate chunckSize
        let finished = ref false
        let acc = ref 0L

        use stream = new FileStream (fwFolderFullPath + fwJob.fwFile, FileMode.Open)
        do mLogAgent.AppendToLog (sprintf "Starting fw upgrade %s > %s " fwJob.fwFile fwJob.id )

        while not finished.Value do
           let! count = stream.AsyncRead(buffer, 0, chunckSize)
           finished := count <= 0
           if (not finished.Value) then
              acc := acc.Value + 1L
              do agent.Post ((Opcode.Binary, Array.truncate count buffer, true), false)
              // modifica effettuata il 24 Ottobre
              // do agent.Post ((Opcode.Binary, Array.copy buffer, true), false)              
              if count < chunckSize then
                 do mLogAgent.AppendToLog (sprintf "Frame #%u has size %d" acc.Value count)
              else 
                 ()
           else
              ()

        do mLogAgent.AppendToLog (sprintf "Fw Download started (%u frames of %d bytes queued-up)  %s > %s" acc.Value chunckSize fwJob.fwFile fwJob.id )
        do mLogAgent.AppendToLog (sprintf "Printer %s will not respond until fw upgrade process is complete" fwJob.id )

    } |> Async.Start



