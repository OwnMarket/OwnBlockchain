namespace Chainium.Blockchain.Common

open System
open System.Text

module Conversion =

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
        Decimal.GetBits x
        |> Array.map int32ToBytes
        |> Array.concat

    let stringToBytes (str : string) =
        Encoding.UTF8.GetBytes str

    let bytesToString (bytes : byte[]) =
        Encoding.UTF8.GetString bytes
