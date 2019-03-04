namespace Own.Blockchain.Public.Data

open System.IO
open System.Data.Common
open System.Runtime.InteropServices
open FirebirdSql.Data.FirebirdClient
open FirebirdSql.Data.Isql
open Npgsql
open Dapper
open Own.Common.FSharp
open Own.Blockchain.Common
open Own.Blockchain.Public.Core.DomainTypes

module DbTools =

    type private DatabaseActions =
        {
            CreateCommand : string -> DbConnection -> DbCommand
            CreateParam : (string * obj) -> DbParameter
            CreateTransactionCommand : string -> DbConnection -> DbTransaction -> DbCommand
        }

    let appDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) // TODO: Use Config!

    let firebirdCommand sql (conn : DbConnection) =
        new FbCommand(sql, conn :?> FbConnection)
        :> DbCommand
    let firebirdParam (name : string, value : obj) =
        FbParameter(name, value)
        :> DbParameter
    let firebirdTransactionCmd (sql : string) (conn : DbConnection) (transaction : DbTransaction) =
        new FbCommand(sql, conn :?> FbConnection, transaction :?> FbTransaction)
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
            typeof<FbConnection>,
            {
                CreateCommand = firebirdCommand
                CreateParam = firebirdParam
                CreateTransactionCommand = firebirdTransactionCmd
            }
            typeof<NpgsqlConnection>,
            {
                CreateCommand = postgresCommand
                CreateParam = postgresParam
                CreateTransactionCommand = postgresTransactionCmd
            }
        ]

    let prepareFirebirdConnectionString connectionString =
        let csb = FbConnectionStringBuilder connectionString
        let clientLibraryFileName =
            if connectionString.ToLowerInvariant().Contains("clientlibrary") then
                Path.GetFileName(csb.ClientLibrary)
            else
                if RuntimeInformation.IsOSPlatform OSPlatform.Windows then
                    "fbclient.dll"
                elif RuntimeInformation.IsOSPlatform OSPlatform.OSX then
                    "libfbclient.dylib"
                elif RuntimeInformation.IsOSPlatform OSPlatform.Linux then
                    "libfbclient.so"
                else
                    failwith "Unknown OS"

        csb.ClientLibrary <- Path.Combine(appDir, clientLibraryFileName)

        if not (connectionString.ToLowerInvariant().Contains("servertype")) then
            csb.ServerType <- FbServerType.Embedded

        if csb.UserID.IsNullOrWhiteSpace() then
            csb.UserID <- "SYSDBA"

        if csb.Charset.IsNullOrWhiteSpace() then
            csb.Charset <- "UTF8"

        csb

    let newConnection dbEngineType (dbConnectionString : string) =
        match dbEngineType with
        | Firebird ->
            let csb = prepareFirebirdConnectionString dbConnectionString
            new FbConnection(csb.ConnectionString) :> DbConnection
        | Postgres ->
            new NpgsqlConnection(dbConnectionString) :> DbConnection

    let private connectionBasedActions dbConnection =
        let connType = dbConnection.GetType()
        if databaseSetup.ContainsKey(connType) then
            databaseSetup.[connType]
        else failwith "Unknown connection type"

    let execute dbEngineType (dbConnectionString : string) (sql : string) (parameters : (string * obj) seq) : int =
        use conn = newConnection dbEngineType dbConnectionString
        try
            let dbActions = connectionBasedActions conn

            use cmd = dbActions.CreateCommand sql conn

            parameters
            |> Seq.iter (dbActions.CreateParam >> cmd.Parameters.Add >> ignore)

            conn.Open()
            cmd.ExecuteNonQuery()
        finally
            conn.Close()

    let executeFbBatch dbEngineType (dbConnectionString : string) (sql : string) (parameters : (string * obj) seq) =
        use conn = newConnection dbEngineType dbConnectionString
        try
            let dbActions = connectionBasedActions conn

            let script = new FbScript(sql)
            if script.Parse() <= 0 then
                failwithf "No statements parsed from Firebird SQL script:\n%s" sql

            conn.Open()

            for line in script.Results do
                try
                    use cmd = dbActions.CreateCommand line.Text conn

                    parameters
                    |> Seq.iter (dbActions.CreateParam >> cmd.Parameters.Add >> ignore)

                    cmd.ExecuteNonQuery() |> ignore
                with
                | ex ->
                    Log.errorf "Failed to execute Firebird SQL script statement: %s\nWITH ERROR: %s"
                        line.Text ex.AllMessages
                    reraise ()

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

    let query<'T>
        dbEngineType
        (dbConnectionString : string)
        (sql : string)
        (parameters : (string * obj) seq)
        : 'T list
        =

        Dapper.DefaultTypeMap.MatchNamesWithUnderscores <- true
        use conn = newConnection dbEngineType dbConnectionString

        try
            conn.Open()

            if parameters |> Seq.isEmpty then
                conn.Query<'T>(sql)
            else
                conn.Query<'T>(sql, dict parameters)
            |> List.ofSeq
        finally
            conn.Close()
