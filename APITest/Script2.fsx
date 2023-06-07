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
        POST "http://localhost:8083/oauth2/token"
        CacheControl "no-cache"
        body
        jsonSerialize
            {|
                UserName = username
                Password = password
                ClientId = "54d8bfbacb0d485689e7ffd8dab8989c"
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
let Resource1req = Result.bind (resourcerequest "http://localhost:8084/audience1/sample1") maybeValidToken
let Resource2req = Result.bind (resourcerequest "http://localhost:8084/audience1/sample2") maybeValidToken


