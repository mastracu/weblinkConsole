#r "nuget: FsHttp"
#r "nuget: System.IdentityModel.Tokens.Jwt"

open FsHttp
open Microsoft.IdentityModel.Tokens

type AudienceResponse = {
    clientID: string
    base64Secret: string
    name: string
}

let audienceresponse =
    http {
        POST "http://localhost:8083/api/audience"
        CacheControl "no-cache"
        body
        jsonSerialize
            {|
                Name = "audience1"
            |}
    }
    |> Request.send |> Response.deserializeJson<AudienceResponse>
