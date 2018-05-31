namespace Chainium.Blockchain.Public.IntegrationTests.Common

open System
open System.IO
open System.Reflection
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.TestHost
open Microsoft.Extensions.Configuration
open Microsoft.Data.Sqlite
open Chainium.Blockchain.Public.Data
open Chainium.Blockchain.Public.Node

module internal Helper =


    let SQLite = "SQLite"
    let Postgres = "PostgreSQL"

    let testCleanup sqlEngineType connString =
        if Directory.Exists(Config.DataDir) then do
            Directory.Delete(Config.DataDir, true)

        if sqlEngineType = SQLite then
            let conn = new SqliteConnection(connString)
            if File.Exists conn.DataSource then do
                File.Delete conn.DataSource

        if sqlEngineType = Postgres then
            let removeAllTables =
                """
                  DO $$ DECLARE
                    tabname RECORD;
                  BEGIN
                    FOR tabname IN (SELECT tablename
                    FROM pg_tables
                    WHERE schemaname = current_schema())
                  LOOP
                      EXECUTE 'DROP TABLE IF EXISTS ' || quote_ident(tabname.tablename) || ' CASCADE';
                  END LOOP;
                  END $$;
                """
            DbTools.execute connString removeAllTables [] |> ignore


    let testServer () =
        let hostBuilder =
            WebHostBuilder()
                .Configure(Action<IApplicationBuilder> Api.configureApp)
                .ConfigureServices(Api.configureServices)

        new TestServer(hostBuilder)

    let addBalanceAndAccount connectionString (address : string) (amount : decimal) =
        let insertParams =
            [
                "@amount", amount |> box
                "@chainium_address", address |> box
            ]
            |> Seq.ofList

        let insertStatement =
            """
            insert into chx_balance(chainium_address, amount, nonce) values (@chainium_address, @amount, 0);
            insert into account(account_hash, chainium_address) values (@chainium_address, @chainium_address);
            """
        DbTools.execute connectionString insertStatement insertParams
        |> ignore

    let private appDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
    let private config =
        (
            ConfigurationBuilder()
                .SetBasePath(appDir)
                .AddJsonFile("AppSettings.json")
        ).Build()

    let BlockCreationWaitingTime =
        config.["BlockCreationWaitingTimeInSeconds"] |> int

    let DbConnectionString =
        config.["DbConnectionString"]
