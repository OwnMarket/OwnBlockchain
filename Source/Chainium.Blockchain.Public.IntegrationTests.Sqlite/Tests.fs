namespace Chainium.Blockchain.Public.IntegrationTests.Sqlite

open System
open Xunit
open Chainium.Blockchain.Public.Node
open Chainium.Blockchain.Public.IntegrationTests.Common

module Tests =

    [<Fact>]
    let ``DbInit - Init SQLite database`` () =
        SharedTests.initDatabaseTest Config.DbEngineType Config.DbConnectionString

(*
    TODO: Enable this test once the issue with decimal number rounding in SQLite is resolved.
    [<Fact>]
    let ``Genesis block created - SQLite`` () =
        SharedTests.initBlockchainStateTest Config.DbEngineType Config.DbConnectionString
*)

    [<Fact>]
    let ``Genesis block load from storage`` () =
        SharedTests.loadBlockTest Config.DbEngineType Config.DbConnectionString

    [<Fact>]
    let ``Api - submit transaction for SQLite`` () =
        SharedTests.transactionSubmitTest Config.DbEngineType Config.DbConnectionString

    [<Fact>]
    let ``Node - submit and process transactions for SQLite`` () =
        SharedTests.transactionProcessingTest Config.DbEngineType Config.DbConnectionString

    [<Fact>]
    let ``AccountManagement - get account state for SQLite`` () =
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
    let ``TransactionProcessing - set validator network address`` () =
        SharedTests.setValidatorNetworkAddressTest Config.DbEngineType Config.DbConnectionString

    [<Fact>]
    let ``TransactionProcessing - set stake`` () =
        SharedTests.setStakeTest Config.DbEngineType Config.DbConnectionString
