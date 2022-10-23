#r "nuget: FsHttp"

open FsHttp


type Token = {
  AccessToken : string
  ExpiresIn : float
}

let sendPrintRequest weblinkEndpointUrl (printer:string) (zplcode:string) =
    http {
        POST weblinkEndpointUrl
        CacheControl "no-cache"
        body
        jsonSerialize
            {|
                printerID = printer
                msg = zplcode
            |}
    }
    |> Request.send
    |> Response.

    
sendPrintRequest "https://weblink.mastracu.it/utf82raw" "40J135000563" "^XA^FO40,40^A0,40^FDHELLO ZEBRA^FS^XZ"
