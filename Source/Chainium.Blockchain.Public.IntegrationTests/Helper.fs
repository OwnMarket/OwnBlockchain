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
    let dataFolderCleanup() =
        if Directory.Exists(Config.DataDir) 
            then do
                Directory.Delete(Config.DataDir,true)

    let databaseInit() =
        (* TODO: use dbinit to initialize database, for now initialize using script *)
        let baseFolder = 
            Assembly.GetExecutingAssembly().Location
            |> Path.GetDirectoryName

        let dbScript = Path.Combine(baseFolder, "public_chx_database.sql")
        let sql = System.IO.File.ReadAllText(dbScript)

        let conn = new SqliteConnection(Config.DbConnectionString)
        File.Delete(conn.DataSource)

        DbTools.execute Config.DbConnectionString sql []
        |> ignore

    let testServer() =
        let hostBuilder = 
            WebHostBuilder()
                .Configure(Action<IApplicationBuilder> Api.configureApp)
                .ConfigureServices(Api.configureServices)

        new TestServer(hostBuilder)

