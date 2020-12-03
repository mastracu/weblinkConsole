module tinyBase64Decoder
  open System
  open System.Text

  // Declare fundamental functions

  // Generate n length Integer List (0 upto n - 1)
  let iota n = [0..n-1]

  // Convert binary string into decimal
  let binToDec binStr = Convert.ToInt32 (binStr, 2)

  // Convert decimal into binary string
  let decToBin dec =  Convert.ToString (dec &&& 0xff, 2)

  // Convert Char Sequence into String
  let charSeqToString charSeq =
    let charList = charSeq |> Seq.toList
    in string (List.fold (fun (sb:StringBuilder) (c:char) -> sb.Append(c))
                         (StringBuilder())
                         charList)

  // Convert int List (corresponding with char) into String
  let intListToString intList =
    intList
    |> List.map (char >> string)
    |> String.concat ""

  // BASE64 Decoder and Subsets

  // Base64 Charactors
  let charset = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/" |> Seq.toList

  // Conversion table, Base64 Char to Binary number string
  let table =
    let tableValues =
      iota(charset.Length)
      |> List.map (fun x ->
        let e = decToBin x
        if e.Length = 6 then e else (String.replicate (6 - e.Length) "0") + e
      )

    Seq.zip charset tableValues
    |> Map.ofSeq

  // Convert Base64 strings into Binary String(by replacing with the above table)
  let convert data =
    data
    |> Seq.choose (fun e -> Map.tryFind e table)
    |> String.Concat

  // Create List by 4 bits List from convted string
  let getQuotients (converted:string) =
    let cLen = converted.Length
    in
      iota (cLen / 4)
      |> List.map (fun i -> converted.[(i * 4)..((i + 1) * 4 - 1)])

  // Create List by two bits List from ```quotients``` (require it from result of the above function)
  let getBuffers quotients =
    let quotients = List.toArray quotients
    let qLen      = quotients.Length
    in
      iota(qLen / 2)
      |> List.map (fun i -> quotients.[(i * 2)..((i + 1) * 2 - 1)])

  // Generating Binaries with buffers from the above function
  let finalize buf =
    buf |> List.map (fun (b:string[]) ->
                      let b0 = b.[0] |> binToDec
                      let b1 = b.[1] |> binToDec
                      ((b0 <<< 4) ^^^ b1) &&& 0xff)

  // Decode base64 String into Unsinged Bytes
  let decode64 (data:string) =
    data
    |> convert
    |> getQuotients
    |> getBuffers
    |> finalize





