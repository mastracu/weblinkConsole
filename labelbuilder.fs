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
        ^BY3,3,41^FT56,142^BEN,,Y,N^FDBBBBBBBBBBBBB^FS
        ^FT174,85^A0N,28,28^FH\^FDPPPPPP\15 a piece^FS
        ^FT89,85^A0N,28,28^FH\^FDPrice^FS
        ^FT120,50^A0N,28,28^FH\^FDXXXXXXXXXXXX^FS
        ^FT10,50^A0N,28,28^FH\^FDProduct^FS
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


let encodeDHLLabel (dHLregistrationPlate:string) =

    // https://www.rapidtables.com/convert/number/decimal-to-hex.html
    // https://stu.dev/bigint-to-string-in-any-base-fsharp/
    let bigintToDigits b source =
        let rec loop (b : int) num digits =
            let (quotient, remainder) = bigint.DivRem(num, bigint b)
            match quotient with
            | zero when zero = 0I -> int remainder :: digits
            | _ -> loop b quotient (int remainder :: digits)
        loop b source []
    let digitsToString source =
        source
        |> List.map (fun (x : int) -> x.ToString("X").ToLowerInvariant())
        |> String.concat ""
    let printBigintAsHex source =
        let bigintToHex = bigintToDigits 16
        source |> bigintToHex |>  digitsToString |> sprintf "%s"

    let keepNumericOnly str = Seq.fold (fun finstr c  -> if (c < '0' || c > '9') then finstr else finstr + c.ToString()) ""  str
    let hexEncode rp = rp |> keepNumericOnly |> bigint.Parse |> printBigintAsHex

// https://www.zebra.com/us/en/support-downloads/knowledge-articles/defining-the-size-of-the-epc-data-that-can-be-encoded-for-gen2-rfid-tags.html
// I need 30 hex digits to encode 36 decimal digits so I need to encode 128 bits epc
    let label0 = "
^XA
^FO90,50
^A0N,65
^FN7
^FS
^FX Bar code.
^BY3,2,200
^FO90,300^BCN,,,,,A^FDyyyyyyyyyyyyyyyyy^FS
^RFW,H,1,2,1^FD4000^FS
^RFW,H,2,16,1^FDxxxxxxx^FS
^FN7
^RFR,H
^FS
^HV7
^XZ
"
    label0 |> String.replace "xxxxxxx" (hexEncode dHLregistrationPlate) |> String.replace "yyyyyyyyyyyyyyyyy" dHLregistrationPlate
