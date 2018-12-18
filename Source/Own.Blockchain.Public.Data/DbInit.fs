namespace Own.Blockchain.Public.Data

open System
open System.Data
open System.Data.Common
open System.IO
open Microsoft.Data.Sqlite
open Dapper
open Own.Common
open Own.Blockchain.Common

module DbInit =

    type DbEngineType =
        | SQLite
        | PostgreSQL

    [<CLIMutable>]
    type DbVersionInfo = {
        VersionNumber : int
        ExecutionTimestamp : int64
    }

    let private ensureDbExists dbEngineType connectionString =
        if dbEngineType = SQLite then
            let csb = SqliteConnectionStringBuilder connectionString
            let dbFile = csb.DataSource
            if not (File.Exists dbFile) then
                use fs = File.Create(dbFile)
                fs.Close()
        else
            dbEngineType
            |> unionCaseName
            |> invalidOp "DB creation is not supported for DbEngineType: %s"

    let private getDbVersion connectionString =
        let sql =
            """
            SELECT max(version_number) AS version_number
            FROM db_version;
            """

        try
            DbTools.query<DbVersionInfo> connectionString sql []
            |> List.tryHead
            |> Option.map (fun v -> v.VersionNumber)
            |? 0
        with
        | ex when
            ex.AllMessages.Contains "no such table: db_version"
            || ex.AllMessages.Contains """relation "db_version" does not exist"""
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
                %s;
                INSERT INTO db_version (version_number, execution_timestamp)
                VALUES (@versionNumber, @executionTimestamp);
                """
                dbChange.Script

        let rowsAffected =
            [
                "@versionNumber", dbChange.Number |> box
                "@executionTimestamp", Utils.getUnixTimestamp () |> box
            ]
            |> DbTools.execute connectionString sql

        ()

    let private applyDbChanges dbEngineType connectionString =
        let dbChanges =
            match dbEngineType with
            | SQLite -> DbChanges.sqliteChanges
            | PostgreSQL -> DbChanges.postgresqlChanges

        ensureDbChangeNumberConsistency dbChanges

        let lastAppliedChangeNumber = getDbVersion connectionString

        let dbChanges =
            dbChanges
            |> List.filter (fun c -> c.Number > lastAppliedChangeNumber)
            |> List.sortBy (fun c -> c.Number)

        for change in dbChanges do
            Log.noticef "Applying DB change %i" change.Number
            applyDbChange dbEngineType connectionString change

    let private initDb dbEngineType connectionString =
        if dbEngineType = SQLite then
            ensureDbExists dbEngineType connectionString
        applyDbChanges dbEngineType connectionString

    let init dbEngineTypeCode connectionString =
        match dbEngineTypeCode with
        | "SQLite" -> initDb SQLite connectionString
        | "PostgreSQL" -> initDb PostgreSQL connectionString
        | t -> failwithf "Unknown DB engine type: %s" t
