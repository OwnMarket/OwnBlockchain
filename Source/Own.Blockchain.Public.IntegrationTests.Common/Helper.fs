namespace Chainium.Blockchain.Public.IntegrationTests.Common

open System
open System.IO
open System.Reflection
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.TestHost
open Microsoft.Extensions.Configuration
open Microsoft.Data.Sqlite
open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Crypto
open Chainium.Blockchain.Public.Data
open Chainium.Blockchain.Public.Node

module internal Helper =

    let SQLite = "SQLite"
    let Postgres = "PostgreSQL"

    let private deleteFile filePath =
        let numberOfDeletionTrys = 20

        let rec tryDeleteFile filePath numOfChecks =
            if File.Exists filePath |> not || numOfChecks >= numberOfDeletionTrys then
                ()
            else
                try
                    File.Delete filePath
                with
                | :? System.IO.IOException as ex ->
                    if numOfChecks = numberOfDeletionTrys then
                        failwithf "%A" ex
                    else
                        System.Threading.Thread.Sleep(1000)
                        tryDeleteFile filePath numOfChecks

        tryDeleteFile filePath 0

    let testCleanup sqlEngineType connString =
        if Directory.Exists(Config.DataDir) then do
            Directory.Delete(Config.DataDir, true)

        if sqlEngineType = SQLite then
            let conn = new SqliteConnection(connString)
            deleteFile conn.DataSource

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

    let generateRandomHash () =
        Signing.generateRandomBytes 64
        |> Hashing.hash

    let createNewHashForSender (ChainiumAddress address) (Nonce nonce) (TxActionNumber actionNumber) =
        [
            address |> Hashing.decode
            nonce |> Conversion.int64ToBytes
            actionNumber |> Conversion.int16ToBytes
        ]
        |> Array.concat
        |> Hashing.hash

    let addChxBalance connectionString (address : string) (amount : decimal) =
        let insertStatement =
            """
            INSERT INTO chx_balance (chainium_address, amount, nonce)
            VALUES (@chainium_address, @amount, 0);
            """
        [
            "@amount", amount |> box
            "@chainium_address", address |> box
        ]
        |> DbTools.execute connectionString insertStatement
        |> ignore

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
