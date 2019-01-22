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

    let txExists = Raw.txExists Config.DataDir

    let saveTxResult = Raw.saveTxResult Config.DataDir

    let getTxResult = Raw.getTxResult Config.DataDir

    let txResultExists = Raw.txResultExists Config.DataDir

    let deleteTxResult = Raw.deleteTxResult Config.DataDir

    let saveEquivocationProof = Raw.saveEquivocationProof Config.DataDir

    let getEquivocationProof = Raw.getEquivocationProof Config.DataDir

    let saveBlock = Raw.saveBlock Config.DataDir

    let getBlock = Raw.getBlock Config.DataDir

    let blockExists = Raw.blockExists Config.DataDir

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Database
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let initDb () = DbInit.init Config.DbEngineType Config.DbConnectionString

    let saveTxToDb = Db.saveTx Config.DbEngineType Config.DbConnectionString

    let getTxInfo = Db.getTx Config.DbEngineType Config.DbConnectionString

    let getPendingTxs = Db.getPendingTxs Config.DbEngineType Config.DbConnectionString

    let getAllPendingTxHashes () = Db.getAllPendingTxHashes Config.DbEngineType Config.DbConnectionString

    let getTotalFeeForPendingTxs = Db.getTotalFeeForPendingTxs Config.DbEngineType Config.DbConnectionString

    let saveEquivocationProofToDb = Db.saveEquivocationProof Config.DbEngineType Config.DbConnectionString

    let saveBlockToDb = Db.saveBlock Config.DbEngineType Config.DbConnectionString

    let tryGetLastAppliedBlockNumber () = Db.getLastAppliedBlockNumber Config.DbEngineType Config.DbConnectionString
    let getLastAppliedBlockNumber () =
        tryGetLastAppliedBlockNumber () |?> fun _ -> failwith "Cannot get last applied block number."

    let getLastStoredBlockNumber () = Db.getLastStoredBlockNumber Config.DbEngineType Config.DbConnectionString

    let getStoredBlockNumbers () = Db.getStoredBlockNumbers Config.DbEngineType Config.DbConnectionString

    let getChxBalanceState = Db.getChxBalanceState Config.DbEngineType Config.DbConnectionString

    let getAddressAccounts = Db.getAddressAccounts Config.DbEngineType Config.DbConnectionString

    let getAccountState = Db.getAccountState Config.DbEngineType Config.DbConnectionString

    let getAccountHoldings = Db.getAccountHoldings Config.DbEngineType Config.DbConnectionString

    let getHoldingState = Db.getHoldingState Config.DbEngineType Config.DbConnectionString

    let getVoteState = Db.getVoteState Config.DbEngineType Config.DbConnectionString

    let getEligibilityState = Db.getEligibilityState Config.DbEngineType Config.DbConnectionString

    let getAssetState = Db.getAssetState Config.DbEngineType Config.DbConnectionString

    let getValidatorState = Db.getValidatorState Config.DbEngineType Config.DbConnectionString

    let getTopValidatorsByStake =
        Db.getTopValidatorsByStake Config.DbEngineType Config.DbConnectionString Config.MaxValidatorCount

    let getTopStakersByStake =
        Db.getTopStakersByStake Config.DbEngineType Config.DbConnectionString Config.MaxRewardedStakersCount

    let getStakeState = Db.getStakeState Config.DbEngineType Config.DbConnectionString

    let getTotalChxStaked = Db.getTotalChxStaked Config.DbEngineType Config.DbConnectionString

    let getAllPeerNodes () = Db.getAllPeerNodes Config.DbEngineType Config.DbConnectionString

    let savePeerNode = Db.savePeerNode Config.DbEngineType Config.DbConnectionString

    let removePeerNode = Db.removePeerNode Config.DbEngineType Config.DbConnectionString

    let persistStateChanges = Db.persistStateChanges Config.DbEngineType Config.DbConnectionString

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Validators
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let addressFromPrivateKey = memoize Signing.addressFromPrivateKey

    let getTopValidators () =
        Validators.getTopValidators
            getTopValidatorsByStake
            (ChxAmount Config.ValidatorThreshold)

    let getValidatorsAtHeight =
        Validators.getValidatorsAtHeight
            getBlock

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
            getVoteState
            getEligibilityState
            getAccountState
            getAssetState
            getValidatorState
            getStakeState
            getTotalChxStaked
            getTopStakersByStake
            getValidatorsAtHeight
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

    let removeOrphanTxResults () =
        Workflows.removeOrphanTxResults
            getAllPendingTxHashes
            txResultExists
            deleteTxResult

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
            removeOrphanTxResults
            publishEvent

    let fetchMissingBlocks publishEvent =
        Synchronization.fetchMissingBlocks
            getLastAppliedBlockNumber
            getLastStoredBlockNumber
            getStoredBlockNumbers
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
            getValidatorsAtHeight
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

    let storeEquivocationProof =
        Workflows.storeEquivocationProof
            Signing.verifySignature
            createConsensusMessageHash
            Hashing.decode
            Hashing.hash
            saveEquivocationProof
            saveEquivocationProofToDb

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

    let propagateEquivocationProof =
        Workflows.propagateEquivocationProof
            Peers.sendMessage
            Config.NetworkAddress
            getEquivocationProof

    let propagateBlock =
        Workflows.propagateBlock
            Peers.sendMessage
            Config.NetworkAddress
            getBlock

    let requestLastBlockFromPeer () = Peers.requestLastBlockFromPeer ()

    let processPeerMessage (peerMessage : PeerMessage) =
        Workflows.processPeerMessage
            getTx
            getEquivocationProof
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
