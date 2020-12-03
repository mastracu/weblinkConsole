module StoreAgent


open System
open System.Runtime.Serialization.Json
open System.Runtime.Serialization

open System.IO
open System.Xml
open System.Text

open FSharp.Data

open JsonHelper


[<DataContract>]
type Product =
   { 
      [<field: DataMember(Name = "sku")>]
      sku : string;
      [<field: DataMember(Name = "description")>]
      description : string;
      [<field: DataMember(Name = "unitPrice")>]
      unitPrice : float;
      [<field: DataMember(Name = "eanCode")>]
      eanCode : string;
   }

let rec productUpdate prod list =
      match list with
      | [] -> []
      | prodHead :: xs -> if prodHead.sku = prod.sku then prod :: xs else (prodHead :: productUpdate prod xs)

let rec removeSku sku list =
      match list with
      | [] -> []
      | prodHead :: xs -> if prodHead.sku = sku then xs else (prodHead :: removeSku sku xs)

let rec eanLookup barcode list =
      match list with
      | [] -> None
      | prodHead :: xs -> if prodHead.eanCode = barcode then Some prodHead else (eanLookup barcode xs)

type StoreAgentMsg = 
    | Exit
    | Clear
    | ProductUpdate of Product
    | RemoveSku of string 
    | EanLookup of string * AsyncReplyChannel<Product option>
    | IsKnownSKU of string * AsyncReplyChannel<Boolean>
    | StoreInventory  of AsyncReplyChannel<String>

[<DataContract>]
type Store = 
   { [<field: DataMember(Name = "productInStore")>] ProductList : Product list } 
   static member Empty = {ProductList = [] }
   member x.IsKnownSKU address = 
      List.exists (fun prod -> prod.sku = address) x.ProductList
   member x.ProductUpdate prod =  
      { ProductList = 
          if x.IsKnownSKU prod.sku then
              productUpdate prod x.ProductList
          else
              prod :: x.ProductList}
   member x.RemoveSku sku =  
      { ProductList = removeSku sku x.ProductList }
   member x.EanLookup barcode = eanLookup barcode x.ProductList

type StoreAgent() =
    let storeAgentMailboxProcessor =
        MailboxProcessor.Start(fun inbox ->
            let rec storeAgentLoop store =
                async { let! msg = inbox.Receive()
                        match msg with
                        | Exit -> return ()
                        | Clear -> return! storeAgentLoop Store.Empty
                        | ProductUpdate prod -> return! storeAgentLoop (store.ProductUpdate prod)
                        | EanLookup (barcode, replyChannel) -> 
                            replyChannel.Reply (store.EanLookup barcode)
                            return! storeAgentLoop store
                        | IsKnownSKU (sku, replyChannel) -> 
                            replyChannel.Reply (store.IsKnownSKU sku)
                            return! storeAgentLoop store
                        | RemoveSku sku -> return! storeAgentLoop (store.RemoveSku sku)
                        | StoreInventory replyChannel -> 
                            replyChannel.Reply (json<Product array> (List.toArray store.ProductList))
                            return! storeAgentLoop store
                      }
            // http://fsharp.github.io/FSharp.Data/library/Http.html
            // let defaultjson = Http.RequestString("http://weblinkendpoint.mastracu.it/defaultinventory.json")
            let defaultjson =File.ReadAllText("defaultinventory.json")
            let newStore = { ProductList = Array.toList (unjson<Product array> defaultjson) } 
            storeAgentLoop newStore

        )
    member this.Exit() = storeAgentMailboxProcessor.Post(Exit)
    member this.Empty() = storeAgentMailboxProcessor.Post(Clear)
    member this.UpdateWith prod = storeAgentMailboxProcessor.Post(ProductUpdate prod)
    member this.RemoveSku sku = storeAgentMailboxProcessor.Post(RemoveSku sku)
    member this.EanLookup barcode = storeAgentMailboxProcessor.PostAndReply((fun reply -> EanLookup(barcode,reply)), timeout = 2000)
    member this.IsKnownSKU sku = storeAgentMailboxProcessor.PostAndReply((fun reply -> IsKnownSKU(sku,reply)), timeout = 2000)
    member this.StoreInventory() = storeAgentMailboxProcessor.PostAndReply((fun reply -> StoreInventory reply), timeout = 2000)

