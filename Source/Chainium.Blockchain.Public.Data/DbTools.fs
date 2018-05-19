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

    type private DbParams () =
        let parameters = System.Collections.Generic.List<IDbDataParameter>()

        interface System.Collections.Generic.IEnumerable<IDbDataParameter> with
            member this.GetEnumerator () 
                : System.Collections.Generic.IEnumerator<IDbDataParameter> 
                =
                    parameters.GetEnumerator() 
                    :> System.Collections.Generic.IEnumerator<IDbDataParameter>
            
            member this.GetEnumerator () 
                : System.Collections.IEnumerator 
                =
                    parameters.GetEnumerator()
                    :> System.Collections.IEnumerator
        


        interface SqlMapper.IDynamicParameters with 
            member this.AddParameters 
                (
                    (command  : IDbCommand),
                    (identity : SqlMapper.Identity)
                )
                : unit
                =
                    for p in parameters do
                        command.Parameters.Add p

                        |> ignore
        

        member this.Add value =
            parameters.Add(value)



    let query<'T> (dbConnectionString : string) (sql : string) (parameters : (string * obj) seq) : 'T list =
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores <- true
        use conn = newConnection(dbConnectionString)
        try
            let dbParams = DbParams()
            let addSqlParam = 
                fun (name, value) -> 
                    dbParameter(name, value)
                    |> dbParams.Add 

            parameters
            |> Seq.iter addSqlParam

            conn.Open()
            conn.Query<'T>(sql, dbParams)
            |> List.ofSeq
        finally
            conn.Close()
