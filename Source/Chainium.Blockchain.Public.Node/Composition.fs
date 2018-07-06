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

    let saveTxResult = Raw.saveTxResult Config.DataDir

    let getTxResult = Raw.getTxResult Config.DataDir

    let saveBlock = Raw.saveBlock Config.DataDir

    let getBlock = Raw.getBlock Config.DataDir

    let blockExists = Raw.blockExists Config.DataDir

    // DB

    let initDb () = DbInit.init Config.DbEngineType Config.DbConnectionString

    let saveTxToDb = Db.saveTx Config.DbConnectionString

    let getTxInfo = Db.getTx Config.DbConnectionString

    let getPendingTxs = Db.getPendingTxs Config.DbConnectionString

    let getTotalFeeForPendingTxs = Db.getTotalFeeForPendingTxs Config.DbConnectionString

    let getLastBlockTimestamp () = Db.getLastBlockTimestamp Config.DbConnectionString

    let getLastBlockNumber () = Db.getLastBlockNumber Config.DbConnectionString

    let getChxBalanceState = Db.getChxBalanceState Config.DbConnectionString

    let getAccountHoldings = Db.getAccountHoldings Config.DbConnectionString

    let getHoldingState = Db.getHoldingState Config.DbConnectionString

    let getAccountState = Db.getAccountState Config.DbConnectionString

    let getAssetState = Db.getAssetState Config.DbConnectionString

    let applyNewState = Db.applyNewState Config.DbConnectionString

    // Workflows

    let submitTx =
        Workflows.submitTx
            Signing.verifySignature
            Hashing.isValidChainiumAddress
            Hashing.hash
            getChxBalanceState
            getTotalFeeForPendingTxs
            saveTx
            saveTxToDb
            (ChxAmount Config.MinTxActionFee)

    let createNewBlock () =
        Workflows.createNewBlock
            getPendingTxs
            getTx
            Signing.verifySignature
            Hashing.isValidChainiumAddress
            getChxBalanceState
            getHoldingState
            getAccountState
            getAssetState
            getLastBlockNumber
            getBlock
            Hashing.decode
            Hashing.hash
            Hashing.merkleTree
            saveTxResult
            saveBlock
            applyNewState
            (ChxAmount Config.MinTxActionFee)
            Config.MaxTxCountPerBlock
            (ChainiumAddress Config.ValidatorAddress)

    let initBlockchainState () =
        Workflows.initBlockchainState
            getLastBlockNumber
            getBlock
            saveBlock
            applyNewState
            Hashing.decode
            Hashing.hash
            Hashing.merkleTree
            Hashing.zeroHash
            Hashing.zeroAddress
            (ChxAmount Config.GenesisChxSupply)
            (ChainiumAddress Config.GenesisAddress)

    let advanceToLastKnownBlock () =
        Workflows.advanceToLastKnownBlock
            getTx
            Signing.verifySignature
            Hashing.isValidChainiumAddress
            getChxBalanceState
            getHoldingState
            getAccountState
            getAssetState
            Hashing.decode
            Hashing.hash
            Hashing.merkleTree
            saveTxResult
            saveBlock
            applyNewState
            getLastBlockNumber
            blockExists
            getBlock
            (ChxAmount Config.GenesisChxSupply)

    let propagateTx = Workflows.propagateTx Peers.sendMessage

    let propagateBlock = Workflows.propagateBlock Peers.sendMessage

    let getAddressApi = Workflows.getAddressApi getChxBalanceState

    let getAccountApi = Workflows.getAccountApi getAccountState getAccountHoldings

    let getBlockApi = Workflows.getBlockApi getBlock

    let getTxApi = Workflows.getTxApi getTx Signing.verifySignature getTxResult
