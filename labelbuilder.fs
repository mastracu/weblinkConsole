module LabelBuilder

open StoreAgent
open Suave.Utils

let helloLabel () =
// let createdAt = System.Environment.GetEnvironmentVariable("HEROKU_RELEASE_CREATED_AT")
// let releaseVersion = System.Environment.GetEnvironmentVariable("HEROKU_RELEASE_VERSION")

    let helloLabel = "
    CT~~CD,~CC^~CT~
    ^XA
    ^MMT
    ^PW200
    ^LL0240
    ^LS0
    ^FT20,50^A0N,28,28^FH\^FDCONNECTED TO WEBLINK^FS
    ^PQ1,1,1,Y^XZ"
    helloLabel

let buildpricetag withEncoding (prod:Product)  =
    let skuEncoding = "^RFW,A^FDBBBBBBBBBBBBB^FS"

    let label0 = "
        ^XA
        ^MMT
        ^CI28
        ^PW400
        ^LL0240
        ^LS0
        ^FT76,49^A0N,28,50^FB236,1,0,C^FH\^FDZebra Store^FS
        ^BY3,3,41^FT56,210^BEN,,Y,N
        ^FDBBBBBBBBBBBBB^FS
        ^FT174,148^A0N,28,28^FH\^FDPPPPPP\15 a piece^FS
        ^FT89,148^A0N,28,28^FH\^FDPrice^FS
        ^FT120,111^A0N,28,28^FH\^FDXXXXXXXXXXXX^FS
        ^FT10,111^A0N,28,28^FH\^FDProduct^FS
        "
    let labelX = label0 + (if withEncoding then skuEncoding else "") + "^PQ1,0,1,Y^XZ"

    labelX |> String.replace "PPPPPP" (prod.unitPrice.ToString())
           |> String.replace "BBBBBBBBBBBBB" prod.eanCode
           |> String.replace "XXXXXXXXXXXX" prod.description
           |> String.replace "SSSSSSSSSS" prod.sku


let convertIfadLabel (label:string) =
    let index1 = label.IndexOf("^FO20,10^ADN80,50^FD") + "^FO20,10^ADN80,50^FD".Length   // IFAD INVENTORY
    let field1 = label.Substring (index1, label.IndexOf("^FS", index1) - index1)
    let index2 = label.IndexOf("^FO20,80^ADN60,30^FD") + "^FO20,80^ADN60,30^FD".Length   // NOTEBOOK
    let field2 = label.Substring (index2, label.IndexOf("^FS", index2) - index2)
    let index3 = label.IndexOf("^FO20,120^ADN30,10^FD") + "^FO20,120^ADN30,10^FD".Length // S/N:
    let field3 = label.Substring (index3, label.IndexOf("^FS", index3) - index3)
    let index4 = label.IndexOf("^FO80,120^ADN30,35^FD") + "^FO80,120^ADN30,35^FD".Length  // 026442374753
    let field4 = label.Substring (index4, label.IndexOf("^FS", index4) - index4)
    let index5 = label.IndexOf("^FO80,160^B3N,N,130,N,N^FD") + "^FO80,160^B3N,N,130,N,N^FD".Length  // 000000054986
    let field5 = label.Substring (index5, label.IndexOf("^FS", index5) - index5)
    let index6 = label.IndexOf("^FO150,305^ADN30,35^FD") + "^FO150,305^ADN30,35^FD".Length // 000000054986
    let field6 = label.Substring (index6, label.IndexOf("^FS", index6) - index6)

    "^XA^BY2,2,10^LH10,20^FO13,7^ADN53,33^FD" + field1 + "^FS" +
    // "^XA^PW700^BY2,2,10^LH20,20^FO13,7^ADN53,33^FD" + "CONVERTED LABEL" + "^FS" +
    "^FO08,53^A0N22,25^FD" + field2 + "^FS" + 
    "^FO08,80^ADN20,7^FD" + field3 + "^FS" +
    "^FO53,80^A0N22,25^FD" + field4 + "^FS" + 
    "^FO08,107^B3N,N,87,N,N^FD" + field5 + "^FS" +
    "^FO80,202^A0N,40,40^FD" + field6 + "^FS^XZ"


let convertWikipediaLabel (label:string) =
(*
^XA
^LH30,30
^FO20,10
^ADN,90,50
^AD^FDWikipedia^FS
^XZ
*)
    let index1 = label.IndexOf("^AD^FD") + "^AD^FD".Length   // IFAD INVENTORY
    let field1 = label.Substring (index1, label.IndexOf("^FS", index1) - index1)
    "^XA^LH30,30^FO20,10^ADN,90,50^AD^FD" + "Converted" + "^FS^XZ"
