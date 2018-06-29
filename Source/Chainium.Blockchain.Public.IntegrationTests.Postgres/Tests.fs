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
    let ``Genesis block created - Postgres`` () =
        SharedTests.initBlockchainStateTest Config.DbEngineType Config.DbConnectionString

    [<Fact>]
    let ``Genesis block load from storage`` () =
        SharedTests.loadBlockTest Config.DbEngineType Config.DbConnectionString

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
    let ``TransactionProcessing - set account controller`` () =
        SharedTests.setAccountControllerTest Config.DbEngineType Config.DbConnectionString

    [<Fact>]
    let ``TransactionProcessing - set asset controller`` () =
        SharedTests.setAssetControllerTest Config.DbEngineType Config.DbConnectionString

    [<Fact>]
    let ``TransactionProcessing - set asset code`` () =
        SharedTests.setAssetCodeTest Config.DbEngineType Config.DbConnectionString
