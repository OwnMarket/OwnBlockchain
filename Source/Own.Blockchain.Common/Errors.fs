namespace Own.Blockchain.Common

type AppError = AppError of string with
    member __.Message =
        __ |> fun (AppError m) -> m

type AppErrors = AppError list

type Errors<'T> = 'T list

module Errors =

    let orElse x = function
        | [] -> Ok x
        | errors -> Error errors

    let orElseWith f = function
        | [] -> Ok (f ())
        | errors -> Error errors

module Result =

    let appError errorMessage =
        Error [AppError errorMessage]

    let appErrors errorMessages =
        errorMessages
        |> List.map AppError
        |> Error
