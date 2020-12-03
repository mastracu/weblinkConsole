module JsonHelper

open System
open System.Runtime.Serialization.Json
open System.Runtime.Serialization

open System.IO
open System.Xml
open System.Text


/// Object to Json 
let internal json<'t> (myObj:'t) =   
        use ms = new MemoryStream() 
        (new DataContractJsonSerializer(typeof<'t>)).WriteObject(ms, myObj) 
        Encoding.Default.GetString(ms.ToArray()) 

/// Object from Json 
let internal unjson<'t> (jsonString:string)  : 't =  
        use ms = new MemoryStream(ASCIIEncoding.Default.GetBytes(jsonString)) 
        let obj = (new DataContractJsonSerializer(typeof<'t>)).ReadObject(ms) 
        obj :?> 't