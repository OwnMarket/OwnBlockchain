namespace Chainium.Blockchain.Public.Data

open Microsoft.Data.Sqlite
open System.Data.Common
open Dapper
open System.Data
open System
open Chainium.Common

module DbTools =
    let private newConnection dbConnectionString =
        new SqliteConnection(dbConnectionString)
        :> DbConnection


    let private newCommand (sql : string) (conn : DbConnection) : DbCommand =
        match conn with
        | :? SqliteConnection as sqlliteconn -> 
            new SqliteCommand(sql, sqlliteconn)
            :> DbCommand

        | _ -> failwith "Unknon connection type"
    
    let private dbParameter (name : string, value : obj) =
         SqliteParameter(name, value)
        :> DbParameter
       
    let execute (dbConnectionString : string) (sql : string) (parameters : (string * obj) seq) : int =
        use conn = newConnection(dbConnectionString)
        try
            use cmd = newCommand sql conn

            let sqlParam = fun (name, value) -> dbParameter(name, value)

            for p in parameters do
                let queryParam = sqlParam p
                cmd.Parameters.Add queryParam
                |> ignore          

            conn.Open()
            cmd.ExecuteNonQuery()
        finally
            conn.Close()

    let query<'T> (dbConnectionString : string) (sql : string) (parameters : (string * obj) seq) : 'T list =
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores <- true
        use conn = newConnection(dbConnectionString)
        try

            conn.Open()

            if parameters |> Seq.isEmpty then
                conn.Query<'T>(sql)
            else 
                conn.Query<'T>(sql, dict parameters)
            |> List.ofSeq
        finally
            conn.Close()
