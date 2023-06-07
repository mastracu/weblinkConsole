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
        header "token" validToken
        body
        jsonSerialize
            {|
                printerID = printer
                msg = zplcode
            |}
    }
    |> Request.send


    
sendPrintRequest "https://weblink.mastracu.it:444/api/rawcmd" "40J135000563" "^XA^FO40,40^A0,40^FDHELLO ZEBRA^FS^XZ"
