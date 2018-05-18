namespace Chainium.Blockchain.Public.Data

open System
open Chainium.Common

module DbTools =

    let execute (dbConnectionString : string) (sql : string) (parameters : (string * obj) seq) : int =
        failwith "Execute SQL and return number of affected rows"

    let query<'T> (dbConnectionString : string) (sql : string) (parameters : (string * obj) seq) : 'T list =
        failwith "Run Dapper query and return mapped DTOs"
