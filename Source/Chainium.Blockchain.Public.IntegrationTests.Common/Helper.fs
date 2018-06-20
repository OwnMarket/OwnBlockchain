namespace Chainium.Blockchain.Public.IntegrationTests.Common

open System
open System.IO
open System.Reflection
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.TestHost
open Microsoft.Extensions.Configuration
open Microsoft.Data.Sqlite
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Core.Dtos
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
            let schemaName = (Npgsql.NpgsqlConnectionStringBuilder connString).SearchPath
            let removeAllTables =
                sprintf
                    """
                    DO $$ DECLARE
                        v_table_name TEXT;
                    BEGIN
                        FOR v_table_name IN
                            SELECT tablename
                            FROM pg_tables
                            WHERE schemaname = '%s'
                        LOOP
                            EXECUTE 'DROP TABLE IF EXISTS ' || quote_ident(v_table_name) || ' CASCADE';
                        END LOOP;
                    END $$;
                    """
                    schemaName

            DbTools.execute connString removeAllTables [] |> ignore

    let testServer () =
        let hostBuilder =
            WebHostBuilder()
                .Configure(Action<IApplicationBuilder> Api.configureApp)
                .ConfigureServices(Api.configureServices)

        new TestServer(hostBuilder)

    let addBalanceAndAccount connectionString (address : string) (amount : decimal) =
        let insertStatement =
            """
            INSERT INTO chx_balance (chainium_address, amount, nonce) VALUES (@chainium_address, @amount, 0);
            INSERT INTO account (account_hash, controller_address) VALUES (@chainium_address, @chainium_address);
            """
        [
            "@amount", amount |> box
            "@chainium_address", address |> box
        ]
        |> DbTools.execute connectionString insertStatement
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

    let ExpectedPathForFirstBlock = Path.Combine(Config.DataDir,"Block_1")

    let getTxs connectionString (TxHash txHash) =
        let sql =
            """
            SELECT *
            FROM tx
            WHERE tx_hash = @txHash
            """
        [
            "@txHash", txHash |> box
        ]
        |> DbTools.query<TxInfoDto> connectionString sql

