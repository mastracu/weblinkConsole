
module identityStore

open System.Collections.Generic
open System.Security.Claims
open System.IO


type IdentityRecord = {
    Username: string
    Password: string
    Company: string
}

let private identityStorage = new Dictionary<string, IdentityRecord>()

let jsonStr =File.ReadAllText(Path.GetFullPath "./json/identity.json")
Array.toList (unjson<PrinterApp array> jsonStr)

let getClaims userName =
    seq {
        yield (ClaimTypes.Name, userName)
        yield (ClaimTypes.Role, identityStorage.[userName].Company)
    } |> Seq.map (fun x -> new Claim(fst x, snd x)) |> async.Return

let isValidCredentials username password =
    match identityStorage.ContainsKey(username) with 
    | false -> false |> async.Return
    | true ->  (identityStorage.[username]).Password = password |> async.Return