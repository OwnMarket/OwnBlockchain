namespace Chainium.Blockchain.Public.IntegrationTests

open System
open System.IO
open System.Reflection
open System.ComponentModel
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.TestHost
open Microsoft.Data.Sqlite
open Chainium.Blockchain.Public.Node
open Chainium.Blockchain.Public.Data


module Helper =
    open Microsoft.Extensions.Configuration

    let testCleanup() =
        if Directory.Exists(Config.DataDir) then do
                Directory.Delete(Config.DataDir,true)
        
        if Config.DbEngineType = "SQLite"  then do 
            let conn = new SqliteConnection(Config.DbConnectionString)
            if File.Exists conn.DataSource then do
                File.Delete conn.DataSource

    let testServer() =
        let hostBuilder = 
            WebHostBuilder()
                .Configure(Action<IApplicationBuilder> Api.configureApp)
                .ConfigureServices(Api.configureServices)

        new TestServer(hostBuilder)

    let addBalanceAndAccount (address : string) (amount : decimal) =
        let insertParams =
            [
                "@amount", amount |> box
                "@chainium_address", address |> box
            ]
            |> Seq.ofList 
        
        let insertStatement = 
            """
            insert into chx_balance(chainium_address, amount,nonce) values (@chainium_address, @amount,0);
            insert into account(account_hash,chainium_address) values (@chainium_address,@chainium_address);
            """
        DbTools.execute Config.DbConnectionString insertStatement insertParams
        |> ignore
    
    let private appDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
    let private config =
        (
            ConfigurationBuilder()
                .SetBasePath(appDir)
                .AddJsonFile("AppSettings.json")
        ).Build()
    
    let blockCreationWaitingTime  =
        config.["BlockCreationWaitingTimeInSeconds"] |> int
