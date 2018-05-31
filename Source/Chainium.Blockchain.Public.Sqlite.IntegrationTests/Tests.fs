namespace Chainium.Blockchain.Public.Sqlite.IntegrationTests

open System
open Xunit
open Chainium.Blockchain.Public.Node
open Chainium.Blockchain.Public.IntegrationTests.Common

module Tests =

    [<Fact>]
    let ``DbInit - Init SQLite database`` () =
         SharedTests.initDatabaseTest Config.DbEngineType Config.DbConnectionString

    [<Fact>]
    let ``Api - submit transaction for SQLite`` () =
        SharedTests.transactionSubmitTest Config.DbEngineType Config.DbConnectionString

    [<Fact>]
    let ``Node - submit and process transactions for SQLite`` () =
        SharedTests.transactionProcessingTest Config.DbEngineType Config.DbConnectionString
