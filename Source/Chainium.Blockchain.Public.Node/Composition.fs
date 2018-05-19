namespace Chainium.Blockchain.Public.Node

open System
open Chainium.Blockchain.Public.Core
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Crypto
open Chainium.Blockchain.Public.Data

module Composition =

    // Raw storage

    let saveTx = Raw.saveTx Config.DataDir

    let getTx = Raw.getTx Config.DataDir

    let saveBlock = Raw.saveBlock Config.DataDir

    let getBlock = Raw.getBlock Config.DataDir

    // DB

    let getPendingTxs = Db.getPendingTxs Config.DbConnectionString

    let getLastBlockNumber () = Db.getLastBlockNumber Config.DbConnectionString

    let getChxBalanceState = Db.getChxBalanceState Config.DbConnectionString

    let getHoldingState = Db.getHoldingState Config.DbConnectionString

    let applyNewState = Db.applyNewState Config.DbConnectionString

    // Workflows

    let submitTx = Workflows.submitTx Signing.verifySignature Hashing.hash saveTx

    let createNewBlock () =
        Workflows.createNewBlock
            getPendingTxs
            getTx
            Signing.verifySignature
            getChxBalanceState
            getHoldingState
            getLastBlockNumber
            getBlock
            Hashing.hash
            saveBlock
            applyNewState
            Config.MaxTxCountPerBlock
            (ChainiumAddress Config.ValidatorAddress)
