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

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Database
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let initDb () = DbInit.init Config.DbEngineType Config.DbConnectionString

    let saveTxToDb = Db.saveTx Config.DbConnectionString

    let getTxInfo = Db.getTx Config.DbConnectionString

    let getPendingTxs = Db.getPendingTxs Config.DbConnectionString

    let getTotalFeeForPendingTxs = Db.getTotalFeeForPendingTxs Config.DbConnectionString

    let getLastAppliedBlockNumber () = Db.getLastAppliedBlockNumber Config.DbConnectionString

    let getLastAppliedBlockTimestamp () = Db.getLastAppliedBlockTimestamp Config.DbConnectionString

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
            Config.QuorumSupplyPercent
            Config.MaxValidatorCount

    let getFallbackValidators () =
        // TODO: Remove the workaround once the fallback logic is implemented.
        Config.GenesisValidators
        |> List.map (fun (validatorAddress, networkAddress) ->
            {
                ValidatorSnapshot.ValidatorAddress = BlockchainAddress validatorAddress
                NetworkAddress = networkAddress
                TotalStake = ChxAmount 0m
            }
        )

    let getCurrentValidators () =
        Validators.getCurrentValidators
            getLastAppliedBlockNumber
            getBlock

    let isValidator =
        Validators.isValidator
            getCurrentValidators

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
        Blocks.createNewBlockchainConfiguration getTopValidators getFallbackValidators Config.MinValidatorCount

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
            Hashing.decode
            Signing.signMessage

    let initBlockchainState () =
        Workflows.initBlockchainState
            getLastAppliedBlockNumber
            createGenesisBlock
            getBlock
            saveBlock
            persistStateChanges
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
            Signing.signMessage
            saveBlock
            Config.MaxTxCountPerBlock
            addressFromPrivateKey
            (PrivateKey Config.ValidatorPrivateKey)

    let storeReceivedBlock =
        Workflows.storeReceivedBlock
            Hashing.isValidBlockchainAddress
            getBlock
            Signing.verifySignature
            blockExists
            saveBlock
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

    let initSynchronizationState () =
        Synchronization.initSynchronizationState
            getLastAppliedBlockNumber
            blockExists
            getBlock

    let acquireAndApplyMissingBlocks () =
        Synchronization.acquireAndApplyMissingBlocks
            getLastAppliedBlockNumber
            getBlock
            blockExists
            txExists
            Peers.requestBlockFromPeer
            Peers.requestTxFromPeer
            applyBlock
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
            applyBlockToCurrentState
            saveBlock
            applyBlock
            Hashing.decode
            Hashing.hash
            Hashing.zeroHash
            Signing.signMessage
            Peers.sendMessage
            publishEvent
            addressFromPrivateKey
            (PrivateKey Config.ValidatorPrivateKey)
            Config.ConsensusTimeoutPropose
            Config.ConsensusTimeoutVote
            Config.ConsensusTimeoutCommit

    let handleReceivedConsensusMessage =
        Workflows.handleReceivedConsensusMessage
            Hashing.decode
            Hashing.hash
            Hashing.zeroHash
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

    let getTxApi = Workflows.getTxApi getTx Signing.verifySignature getTxResult

    let getBlockApi = Workflows.getBlockApi getBlock

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
            submitTx
            storeReceivedBlock
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
            processPeerMessage
            publishEvent

    let stopGossip () = Peers.stopGossip ()

    let discoverNetwork () = Peers.discoverNetwork Config.NetworkDiscoveryTime
