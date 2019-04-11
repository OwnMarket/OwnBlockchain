namespace Own.Blockchain.Common

open System
open System.Text

module Conversion =

    let boolToBytes (x : bool) =
        [| (if x then 1uy else 0uy) |]

    let int16ToBytes (x : int16) =
        let bytes = BitConverter.GetBytes x
        if BitConverter.IsLittleEndian then
            bytes |> Array.rev
        else
            bytes

    let int32ToBytes (x : int32) =
        let bytes = BitConverter.GetBytes x
        if BitConverter.IsLittleEndian then
            bytes |> Array.rev
        else
            bytes

    let int64ToBytes (x : int64) =
        let bytes = BitConverter.GetBytes x
        if BitConverter.IsLittleEndian then
            bytes |> Array.rev
        else
            bytes

    let decimalToBytes (x : decimal) =
        if x = 0m then
            0m // Because Decimal.GetBits(1.0000000m - 1m) = [|0; 0; 0; -2147024896|] and we want [|0; 0; 0; 0|]
        else
            x / 1.0000000000000000000000000000m // Remove trailing zeroes by changing the scaling factor.
        |> Decimal.GetBits
        |> Array.collect int32ToBytes

    let stringToBytes (str : string) =
        Encoding.UTF8.GetBytes str

    let bytesToString (bytes : byte[]) =
        Encoding.UTF8.GetString bytes
