namespace Own.Blockchain.Public.IntegrationTests.Postgres

open System
open Xunit
open Own.Blockchain.Public.Node
open Own.Blockchain.Public.IntegrationTests.Common

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
    let ``AccountManagement - get account state for Postgres`` () =
        SharedTests.getAccountStateTest Config.DbEngineType Config.DbConnectionString

    [<Fact>]
    let ``TransactionProcessing - set account controller`` () =
        SharedTests.setAccountControllerTest Config.DbEngineType Config.DbConnectionString

    [<Fact>]
    let ``TransactionProcessing - set asset controller`` () =
        SharedTests.setAssetControllerTest Config.DbEngineType Config.DbConnectionString

    [<Fact>]
    let ``TransactionProcessing - set asset code`` () =
        SharedTests.setAssetCodeTest Config.DbEngineType Config.DbConnectionString

    [<Fact>]
    let ``TransactionProcessing - set validator config`` () =
        SharedTests.setValidatorConfigTest Config.DbEngineType Config.DbConnectionString

    [<Fact>]
    let ``TransactionProcessing - delegate stake`` () =
        SharedTests.delegateStakeTest Config.DbEngineType Config.DbConnectionString
