open System
open JwtToken
open Suave
open AuthServer

[<EntryPoint>]
let main _ =

    let authorizationServerConfig = {
        AddAudienceUrlPath = "/api/audience"
        CreateTokenUrlPath = "/oauth2/token"
        SaveAudience = AudienceStorage.saveAudience
        GetAudience = AudienceStorage.getAudience
        Issuer = "suave"
        TokenTimeSpan = TimeSpan.FromMinutes(1.)
    }

    let identityStore = {
        getClaims = IdentityStore.getClaims
        isValidCredentials = IdentityStore.isValidCredentials
        getSecurityKey = KeyStore.securityKey
        getSigningCredentials = KeyStore.hmacSha256
    }

    let audienceWebPart' = audienceWebPart authorizationServerConfig identityStore   

    let myConfig = { defaultConfig with bindings = [HttpBinding.createSimple HTTP "127.0.0.1" 8083]}
      
    startWebServer myConfig audienceWebPart'

    0 // return an integer exit code
