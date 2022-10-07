open System.Collections

open System.Collections.Generic
let inventory = Dictionary<string, float>()
inventory.Add("Apples", 0.33)
inventory.Add("Oranges", 0.23)
inventory.Add("Bananas", 0.45)
inventory.Remove "Oranges"
let bananas = inventory.["Bananas"]
