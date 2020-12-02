open Suave
open Suave.Filters
open Suave.Operators
open Suave.Successful

let app =
    choose
        [ GET >=> choose
            [ path "/" >=> OK "Index"
              path "/hello" >=> OK "Hello!" ]
          POST >=> choose
            [ path "/hello" >=> OK "Hello POST!" ] ]

[<EntryPoint>]
let main argv =
    startWebServer defaultConfig app
    0