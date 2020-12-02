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
    let ipZero = IPAddress.Parse("0.0.0.0")

    { defaultConfig with
        bindings=[ (HttpBinding.create HTTP ipZero (uint16 8080)) ] }

[<EntryPoint>]
let main argv =
    startWebServer config app
    0