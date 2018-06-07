namespace Chainium.Blockchain.Public.Node

open Chainium.Blockchain.Public.Core
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Crypto
open Chainium.Blockchain.Public.Data
open Chainium.Blockchain.Public.Net

module Composition =

    // Raw storage

    let saveTx = Raw.saveTx Config.DataDir

    let getTx = Raw.getTx Config.DataDir

    let saveBlock = Raw.saveBlock Config.DataDir

    let getBlock = Raw.getBlock Config.DataDir

    // DB

    let initDb () = DbInit.init Config.DbEngineType Config.DbConnectionString

    let saveTxToDb = Db.saveTx Config.DbConnectionString

    let getPendingTxs = Db.getPendingTxs Config.DbConnectionString

    let getLastBlockTimestamp () = Db.getLastBlockTimestamp Config.DbConnectionString

    let getLastBlockNumber () = Db.getLastBlockNumber Config.DbConnectionString

    let getChxBalanceState = Db.getChxBalanceState Config.DbConnectionString

    let getHoldingState = Db.getHoldingState Config.DbConnectionString

    let getAccountController = Db.getAccountController Config.DbConnectionString

    let applyNewState = Db.applyNewState Config.DbConnectionString

    // Workflows

    let submitTx =
        Workflows.submitTx
            Signing.verifySignature
            Hashing.isValidChainiumAddress
            Hashing.hash
            saveTx
            saveTxToDb

    let createNewBlock () =
        Workflows.createNewBlock
            getPendingTxs
            getTx
            Signing.verifySignature
            Hashing.isValidChainiumAddress
            getChxBalanceState
            getHoldingState
            getAccountController
            getLastBlockNumber
            getBlock
            Hashing.decode
            Hashing.hash
            Hashing.merkleTree
            saveBlock
            applyNewState
            Config.MaxTxCountPerBlock
            (ChainiumAddress Config.ValidatorAddress)

    let propagateTx = Workflows.propagateTx Peers.sendMessage

    let propagateBlock = Workflows.propagateBlock Peers.sendMessage
