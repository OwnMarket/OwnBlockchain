namespace Own.Blockchain.Public.Node

open Own.Common
open Own.Blockchain.Public.Core
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Crypto
open Own.Blockchain.Public.Data
open Own.Blockchain.Public.Net

module Composition =

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Raw storage
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let saveTx = Raw.saveTx Config.DataDir

    let getTx = Raw.getTx Config.DataDir

    let saveTxResult = Raw.saveTxResult Config.DataDir

    let getTxResult = Raw.getTxResult Config.DataDir

    let saveBlock = Raw.saveBlock Config.DataDir

    let getBlock = Raw.getBlock Config.DataDir

    let blockExists = Raw.blockExists Config.DataDir

    let txExists = Raw.txExists Config.DataDir

    let txResultExists = Raw.txResultExists Config.DataDir

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Database
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let initDb () = DbInit.init Config.DbEngineType Config.DbConnectionString

    let saveTxToDb = Db.saveTx Config.DbConnectionString

    let getTxInfo = Db.getTx Config.DbConnectionString

    let getPendingTxs = Db.getPendingTxs Config.DbConnectionString

    let getTotalFeeForPendingTxs = Db.getTotalFeeForPendingTxs Config.DbConnectionString

    let saveBlockToDb = Db.saveBlock Config.DbConnectionString

    let tryGetLastAppliedBlockNumber () = Db.getLastAppliedBlockNumber Config.DbConnectionString
    let getLastAppliedBlockNumber () =
        tryGetLastAppliedBlockNumber () |?> fun _ -> failwith "Cannot get last applied block number."

    let getLastStoredBlockNumber () = Db.getLastStoredBlockNumber Config.DbConnectionString

    let getChxBalanceState = Db.getChxBalanceState Config.DbConnectionString

    let getAddressAccounts = Db.getAddressAccounts Config.DbConnectionString

    let getAccountState = Db.getAccountState Config.DbConnectionString

    let getAccountHoldings = Db.getAccountHoldings Config.DbConnectionString

    let getHoldingState = Db.getHoldingState Config.DbConnectionString

    let getAssetState = Db.getAssetState Config.DbConnectionString

    let getValidatorState = Db.getValidatorState Config.DbConnectionString

    let getTopValidatorsByStake = Db.getTopValidatorsByStake Config.DbConnectionString

    let getStakeState = Db.getStakeState Config.DbConnectionString

    let getTotalChxStaked = Db.getTotalChxStaked Config.DbConnectionString

    let getAllPeerNodes () = Db.getAllPeerNodes Config.DbConnectionString

    let savePeerNode = Db.savePeerNode Config.DbConnectionString

    let removePeerNode = Db.removePeerNode Config.DbConnectionString

    let persistStateChanges = Db.persistStateChanges Config.DbConnectionString

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Validators
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let addressFromPrivateKey = memoize Signing.addressFromPrivateKey

    let getTopValidators () =
        Validators.getTopValidators
            getTopValidatorsByStake
            (ChxAmount Config.GenesisChxSupply)
            (ChxAmount Config.ValidatorThreshold)
            Config.MaxValidatorCount

    let getCurrentValidators () =
        Validators.getCurrentValidators
            getLastAppliedBlockNumber
            getBlock

    let isValidator =
        Validators.isValidator
            getCurrentValidators

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Consensus
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let createConsensusMessageHash =
        Consensus.createConsensusMessageHash
            Hashing.decode
            Hashing.hash

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Blockchain Configuration
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let isConfigurationBlock =
        Config.ConfigurationBlockDelta
        |> int64
        |> Blocks.isConfigurationBlock

    let calculateConfigurationBlockNumberForNewBlock =
        Config.ConfigurationBlockDelta
        |> int64
        |> Blocks.calculateConfigurationBlockNumberForNewBlock

    let createNewBlockchainConfiguration () =
        Blocks.createNewBlockchainConfiguration getTopValidators

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Blockchain
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let createGenesisBlock () =
        Workflows.createGenesisBlock
            Hashing.decode
            Hashing.hash
            Hashing.merkleTree
            Hashing.zeroHash
            Hashing.zeroAddress
            (ChxAmount Config.GenesisChxSupply)
            (BlockchainAddress Config.GenesisAddress)
            Config.GenesisValidators

    let signGenesisBlock =
        Workflows.signGenesisBlock
            createGenesisBlock
            createConsensusMessageHash
            Signing.signHash

    let initBlockchainState () =
        Workflows.initBlockchainState
            tryGetLastAppliedBlockNumber
            createGenesisBlock
            getBlock
            saveBlock
            saveBlockToDb
            persistStateChanges
            createConsensusMessageHash
            Signing.verifySignature
            Config.GenesisSignatures

    let createBlock =
        Workflows.createBlock
            getTx
            Signing.verifySignature
            Hashing.isValidBlockchainAddress
            getChxBalanceState
            getHoldingState
            getAccountState
            getAssetState
            getValidatorState
            getStakeState
            getTotalChxStaked
            Hashing.deriveHash
            Hashing.decode
            Hashing.hash
            Hashing.merkleTree
            calculateConfigurationBlockNumberForNewBlock
            (ChxAmount Config.MinTxActionFee)

    let getAvailableChxBalance =
        Workflows.getAvailableChxBalance
            getChxBalanceState
            getTotalChxStaked

    let proposeBlock =
        Workflows.proposeBlock
            getLastAppliedBlockNumber
            createBlock
            isConfigurationBlock
            createNewBlockchainConfiguration
            getBlock
            getPendingTxs
            getChxBalanceState
            getAvailableChxBalance
            Config.MaxTxCountPerBlock
            addressFromPrivateKey
            (PrivateKey Config.ValidatorPrivateKey)

    let storeReceivedBlock =
        Workflows.storeReceivedBlock
            Hashing.isValidBlockchainAddress
            getBlock
            createConsensusMessageHash
            Signing.verifySignature
            blockExists
            saveBlock
            saveBlockToDb
            Config.MinValidatorCount

    let persistTxResults =
        Workflows.persistTxResults
            saveTxResult

    let isValidSuccessorBlock =
        Blocks.isValidSuccessorBlock
            Hashing.decode
            Hashing.hash
            Hashing.merkleTree

    let applyBlockToCurrentState =
        Workflows.applyBlockToCurrentState
            getBlock
            isValidSuccessorBlock
            txResultExists
            createBlock

    let applyBlock =
        Workflows.applyBlock
            getBlock
            applyBlockToCurrentState
            persistTxResults
            persistStateChanges

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Synchronization
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let tryApplyNextBlock publishEvent =
        Synchronization.tryApplyNextBlock
            getLastAppliedBlockNumber
            getBlock
            applyBlock
            txExists
            publishEvent

    let fetchMissingBlocks publishEvent =
        Synchronization.fetchMissingBlocks
            getLastStoredBlockNumber
            getLastAppliedBlockNumber
            getBlock
            blockExists
            txExists
            Peers.requestBlockFromPeer
            Peers.requestTxFromPeer
            publishEvent
            Config.ConfigurationBlockDelta
            Config.MaxNumberOfBlocksToFetchInParallel

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Consensus
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let createConsensusStateInstance publishEvent =
        Consensus.createConsensusStateInstance
            getLastAppliedBlockNumber
            getCurrentValidators
            proposeBlock
            txExists
            Peers.requestTxFromPeer
            applyBlockToCurrentState
            Hashing.decode
            Hashing.hash
            Signing.signHash
            Peers.sendMessage
            publishEvent
            addressFromPrivateKey
            (PrivateKey Config.ValidatorPrivateKey)
            Config.ConsensusMessageRetryingInterval
            Config.ConsensusProposeRetryingInterval
            Config.ConsensusTimeoutPropose
            Config.ConsensusTimeoutVote
            Config.ConsensusTimeoutCommit

    let handleReceivedConsensusMessage =
        Workflows.handleReceivedConsensusMessage
            Hashing.decode
            Hashing.hash
            getCurrentValidators
            Signing.verifySignature

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // API
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let submitTx =
        Workflows.submitTx
            Signing.verifySignature
            Hashing.isValidBlockchainAddress
            Hashing.hash
            getAvailableChxBalance
            getTotalFeeForPendingTxs
            saveTx
            saveTxToDb
            (ChxAmount Config.MinTxActionFee)

    let getTxApi = Workflows.getTxApi getTx getTxInfo getTxResult Hashing.hash Signing.verifySignature

    let getBlockApi = Workflows.getBlockApi getLastAppliedBlockNumber getBlock

    let getAddressApi = Workflows.getAddressApi getChxBalanceState

    let getAddressAccountsApi = Workflows.getAddressAccountsApi getAddressAccounts

    let getAccountApi = Workflows.getAccountApi getAccountState getAccountHoldings

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Network
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let propagateTx =
        Workflows.propagateTx
            Peers.sendMessage
            Config.NetworkAddress
            getTx

    let propagateBlock =
        Workflows.propagateBlock
            Peers.sendMessage
            Config.NetworkAddress
            getBlock

    let requestLastBlockFromPeer () = Peers.requestLastBlockFromPeer ()

    let processPeerMessage (peerMessage : PeerMessage) =
        Workflows.processPeerMessage
            getTx
            getBlock
            getLastAppliedBlockNumber
            handleReceivedConsensusMessage
            Peers.respondToPeer
            peerMessage

    let startGossip publishEvent =
        Peers.startGossip
            getAllPeerNodes
            savePeerNode
            removePeerNode
            Transport.sendGossipDiscoveryMessage
            Transport.sendGossipMessage
            Transport.sendMulticastMessage
            Transport.sendUnicastMessage
            Transport.receiveMessage
            Transport.closeConnection
            Transport.closeAllConnections
            Config.NetworkAddress
            Config.NetworkBootstrapNodes
            getCurrentValidators
            publishEvent

    let stopGossip () = Peers.stopGossip ()

    let discoverNetwork () = Peers.discoverNetwork Config.NetworkDiscoveryTime
