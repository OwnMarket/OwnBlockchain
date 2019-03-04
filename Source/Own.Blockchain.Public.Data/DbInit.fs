namespace Own.Blockchain.Public.Data

open System
open System.IO
open System.Runtime.InteropServices
open FirebirdSql.Data.FirebirdClient
open Own.Common.FSharp
open Own.Blockchain.Common
open Own.Blockchain.Public.Core.DomainTypes

module DbInit =

    [<CLIMutable>]
    type DbVersionInfo = {
        VersionNumber : int
        ExecutionTimestamp : int64
    }

    let private ensureDbExists dbEngineType connectionString =
        if dbEngineType = Firebird then
            let csb = DbTools.prepareFirebirdConnectionString connectionString
            let dbFile = csb.Database
            if not (File.Exists dbFile) then
                Log.notice "Creating database..."
                FbConnection.CreateDatabase(csb.ConnectionString, false)
        else
            dbEngineType
            |> unionCaseName
            |> invalidOp "DB creation is not supported for DbEngineType: %s"

    let private getDbVersion dbEngineType connectionString =
        let sql =
            """
            SELECT MAX(version_number) AS version_number
            FROM db_version;
            """

        try
            DbTools.query<DbVersionInfo> dbEngineType connectionString sql []
            |> List.tryHead
            |> Option.map (fun v -> v.VersionNumber)
            |? 0
        with
        | ex when
            (ex.AllMessages.Contains "Table unknown" && ex.AllMessages.Contains "DB_VERSION") // Firebird
            || ex.AllMessages.Contains """relation "db_version" does not exist""" // Postgres
            -> 0

    let private ensureDbChangeNumberConsistency (dbChanges : DbChange list) =
        let numbers =
            dbChanges
            |> List.sortBy (fun s -> s.Number)
            |> List.mapi (fun i s -> (i + 1, s.Number))

        for (expectedNo, actualNo) in numbers do
            if expectedNo <> actualNo then
                failwithf "Inconsistent DB change number. Expected %i, found %i." expectedNo actualNo

    let private applyDbChange dbEngineType connectionString (dbChange : DbChange) =
        let sql =
            sprintf
                """
                %s
                INSERT INTO db_version (version_number, execution_timestamp)
                VALUES (%i, %i);
                """
                dbChange.Script
                dbChange.Number
                (Utils.getMachineTimestamp ())

        match dbEngineType with
        | Firebird ->
            DbTools.executeFbBatch dbEngineType connectionString sql []
        | Postgres ->
            DbTools.execute dbEngineType connectionString sql [] |> ignore

    let private applyDbChanges dbEngineType connectionString =
        let dbChanges =
            match dbEngineType with
            | Firebird -> DbChanges.firebirdChanges
            | Postgres -> DbChanges.postgresChanges

        ensureDbChangeNumberConsistency dbChanges

        let lastAppliedChangeNumber = getDbVersion dbEngineType connectionString

        let dbChanges =
            dbChanges
            |> List.filter (fun c -> c.Number > lastAppliedChangeNumber)
            |> List.sortBy (fun c -> c.Number)

        for change in dbChanges do
            Log.noticef "Applying DB change %i" change.Number
            applyDbChange dbEngineType connectionString change

    let init dbEngineType connectionString =
        if dbEngineType = Firebird then
            if not (RuntimeInformation.IsOSPlatform OSPlatform.Windows) then
                let firebirdDir = Environment.GetEnvironmentVariable("FIREBIRD")
                if isNull firebirdDir || firebirdDir <> DbTools.appDir then
                    failwith "FIREBIRD environment variable not set to the application directory for the shell session."

            ensureDbExists dbEngineType connectionString

        applyDbChanges dbEngineType connectionString
