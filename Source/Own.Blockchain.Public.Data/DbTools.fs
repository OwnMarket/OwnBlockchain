namespace Own.Blockchain.Public.Data

open System.Data.Common
open Microsoft.Data.Sqlite
open Npgsql
open Dapper
open Own.Common

module DbTools =

    type private DatabaseActions =
        {
            CreateCommand : string -> DbConnection -> DbCommand
            CreateParam : (string * obj) -> DbParameter
            CreateTransactionCommand : string -> DbConnection -> DbTransaction -> DbCommand
        }

    let sqlLiteCommand sql (conn : DbConnection) =
        new SqliteCommand(sql, conn :?> SqliteConnection)
        :> DbCommand
    let sqlLiteParam (name : string, value : obj) =
        SqliteParameter(name, value)
        :> DbParameter
    let sqlLiteTransactionCmd (sql : string) (conn : DbConnection) (transaction : DbTransaction) =
        new SqliteCommand(sql, conn :?> SqliteConnection, transaction :?> SqliteTransaction)
        :> DbCommand

    let postgresCommand sql (conn : DbConnection) =
        new NpgsqlCommand(sql, conn :?> NpgsqlConnection)
        :> DbCommand
    let postgresParam (name : string, value : obj) =
        NpgsqlParameter(name, value)
        :> DbParameter
    let postgresTransactionCmd (sql : string) (conn : DbConnection) (transaction : DbTransaction) =
        new NpgsqlCommand(sql, conn :?> NpgsqlConnection, transaction :?> NpgsqlTransaction)
        :> DbCommand

    let private databaseSetup =
        dict [
            typeof<SqliteConnection>,
            {
                CreateCommand = sqlLiteCommand
                CreateParam = sqlLiteParam
                CreateTransactionCommand = sqlLiteTransactionCmd
            }
            typeof<NpgsqlConnection>,
            {
                CreateCommand = postgresCommand
                CreateParam = postgresParam
                CreateTransactionCommand = postgresTransactionCmd
            }
        ]

    let newConnection (dbConnectionString : string) =
        try
            let builder = SqliteConnectionStringBuilder dbConnectionString
            new SqliteConnection(builder.ConnectionString) :> DbConnection
        with
        | ex ->
            let postgresBuilder = NpgsqlConnectionStringBuilder dbConnectionString
            new NpgsqlConnection(postgresBuilder.ConnectionString) :> DbConnection

    let private connectionBasedActions dbConnection =
        let connType = dbConnection.GetType()
        if databaseSetup.ContainsKey(connType) then
            databaseSetup.[connType]
        else failwith "Unknown connection type"

    let execute (dbConnectionString : string) (sql : string) (parameters : (string * obj) seq) : int =
        use conn = newConnection(dbConnectionString)
        try
            let dbActions = connectionBasedActions conn

            use cmd = dbActions.CreateCommand sql conn

            parameters
            |> Seq.iter (dbActions.CreateParam >> cmd.Parameters.Add >> ignore)

            conn.Open()
            cmd.ExecuteNonQuery()
        finally
            conn.Close()

    let executeWithinTransaction
        (conn : DbConnection)
        (transaction : DbTransaction)
        (sql : string)
        (parameters : (string * obj) seq)
        : int
        =

        let dbActions = connectionBasedActions conn
        use cmd = dbActions.CreateTransactionCommand sql conn transaction

        parameters
        |> Seq.iter (dbActions.CreateParam >> cmd.Parameters.Add >> ignore)

        cmd.ExecuteNonQuery()

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
