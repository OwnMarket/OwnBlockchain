namespace Chainium.Blockchain.Common

type Errors<'T> = 'T list

module Errors =

    let orElse x = function
        | [] -> Ok x
        | errors -> Error errors

    let orElseWith f = function
        | [] -> Ok (f ())
        | errors -> Error errors
