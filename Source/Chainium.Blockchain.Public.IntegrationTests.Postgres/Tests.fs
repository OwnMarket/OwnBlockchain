namespace Chainium.Blockchain.Public.IntegrationTests.Postgres

open System
open Xunit
open Chainium.Blockchain.Public.Node
open Chainium.Blockchain.Public.IntegrationTests.Common

module Tests =

    [<Fact>]
    let ``DbInit - Init Postgres database`` () =
        SharedTests.initDatabaseTest Config.DbEngineType Config.DbConnectionString

    [<Fact>]
    let ``Api - submit transaction for Postgres`` () =
        SharedTests.transactionSubmitTest Config.DbEngineType Config.DbConnectionString

    [<Fact>]
    let ``Node - submit and process transactions for Postgres`` () =
        SharedTests.transactionProcessingTest Config.DbEngineType Config.DbConnectionString

    [<Fact>]
    let ``AccountManagement - get account controller for Postgres`` () =
        SharedTests.getAccountControllerTest Config.DbEngineType Config.DbConnectionString

    [<Fact>]
    let ``TransactionProcessing - change account controller`` () =
        SharedTests.changeAccountControllerTest Config.DbEngineType Config.DbConnectionString
