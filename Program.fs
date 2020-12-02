open Suave
open Suave.Filters
open Suave.Operators
open Suave.Successful
open System.Net

let app =
    choose
        [ GET >=> choose
            [ path "/" >=> OK "Index"
              path "/hello" >=> OK "Hello Italy!" ]
          POST >=> choose
            [ path "/hello" >=> OK "Hello POST!" ] ]

let config = 
    let port = "8080"
    let ipZero = IPAddress.Parse("0.0.0.0")

    { defaultConfig with
        bindings=[ (HttpBinding.create HTTP ipZero (uint16 8083)) ] }

[<EntryPoint>]
let main argv =
    startWebServer config app
    0