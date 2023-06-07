#r "nuget: FsHttp"
#r "nuget: System.IdentityModel.Tokens.Jwt"

open FsHttp
open Microsoft.IdentityModel.Tokens


type Token = {
  AccessToken : string
  ExpiresIn : float
}

let     tokenrequest username password =
    http {
        POST "https://weblink.mastracu.it:444/oauth2/token"
        CacheControl "no-cache"
        body
        jsonSerialize
            {|
                UserName = username
                Password = password
                ClientId = "f4ddebe19f06496187e672a50e8dcbb9"
            |}
    }
    |> Request.send
    |> Response.expectStatusCode 200
    |> Result.map (Response.deserializeJson<Token> >> fun t -> t.AccessToken) 

    
let resourcerequest url validToken =
    http {
        POST url
        CacheControl "no-cache"
        header "token" validToken
    }
    |> Request.send
    |> Response.expectStatusCode 200
    |> Result.map Response.toText

let maybeValidToken = tokenrequest "Admin" "Admin"
let Resource1req = Result.bind (resourcerequest "https://weblink.mastracu.it:444/api/rawcmd") maybeValidToken
let Resource2req = Result.bind (resourcerequest "http://localhost:8084/audience1/sample2") maybeValidToken


