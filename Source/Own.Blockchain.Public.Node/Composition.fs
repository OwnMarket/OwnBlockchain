namespace Own.Blockchain.Public.Node

open Own.Common.FSharp
open Own.Blockchain.Public.Core
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Crypto
open Own.Blockchain.Public.Data
open Own.Blockchain.Public.Net

module Composition =

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Raw storage
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let createMixedHashKey = Raw.createMixedHashKey Hashing.decode Hashing.encodeHex
    let startTxCacheMonitor () = Raw.startTxCacheMonitor Config.TxCacheExpirationTime
    let saveTx = Raw.saveTx Config.DataDir createMixedHashKey
    let getTx = Raw.getTx Config.DataDir Config.MaxTxCacheSize createMixedHashKey
    let txExists = Raw.txExists Config.DataDir createMixedHashKey

    let saveTxResult = Raw.saveTxResult Config.DataDir createMixedHashKey
    let getTxResult = Raw.getTxResult Config.DataDir createMixedHashKey
    let txResultExists = Raw.txResultExists Config.DataDir createMixedHashKey
    let deleteTxResult = Raw.deleteTxResult Config.DataDir createMixedHashKey

    let saveEquivocationProof = Raw.saveEquivocationProof Config.DataDir createMixedHashKey
    let getEquivocationProof = Raw.getEquivocationProof Config.DataDir createMixedHashKey
    let equivocationProofExists = Raw.equivocationProofExists Config.DataDir createMixedHashKey

    let saveEquivocationProofResult = Raw.saveEquivocationProofResult Config.DataDir createMixedHashKey
    let getEquivocationProofResult = Raw.getEquivocationProofResult Config.DataDir createMixedHashKey
    let equivocationProofResultExists = Raw.equivocationProofResultExists Config.DataDir createMixedHashKey
    let deleteEquivocationProofResult = Raw.deleteEquivocationProofResult Config.DataDir createMixedHashKey

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
    let getTxPoolInfo () = Db.getTxPoolInfo Config.DbEngineType Config.DbConnectionString

    let saveEquivocationProofToDb = Db.saveEquivocationProof Config.DbEngineType Config.DbConnectionString
    let getEquivocationInfo = Db.getEquivocationProof Config.DbEngineType Config.DbConnectionString
    let getPendingEquivocationProofs = Db.getPendingEquivocationProofs Config.DbEngineType Config.DbConnectionString
    let getAllPendingEquivocationProofHashes () =
        Db.getAllPendingEquivocationProofHashes Config.DbEngineType Config.DbConnectionString

    let saveBlockToDb = Db.saveBlock Config.DbEngineType Config.DbConnectionString
    let tryGetLastAppliedBlockNumber () = Db.getLastAppliedBlockNumber Config.DbEngineType Config.DbConnectionString
    let getLastAppliedBlockNumber () =
        tryGetLastAppliedBlockNumber () |?> fun _ -> failwith "Cannot get last applied block number"
    let getLastAppliedBlockTimestamp () =
        Db.getLastAppliedBlockTimestamp Config.DbEngineType Config.DbConnectionString
        |?> fun _ -> failwith "Cannot get last applied block timestamp"
    let getLastStoredBlockNumber () = Db.getLastStoredBlockNumber Config.DbEngineType Config.DbConnectionString
    let getStoredBlockNumbers () = Db.getStoredBlockNumbers Config.DbEngineType Config.DbConnectionString

    let getChxAddressState = Db.getChxAddressState Config.DbEngineType Config.DbConnectionString
    let getAddressAccounts = Db.getAddressAccounts Config.DbEngineType Config.DbConnectionString
    let getAddressAssets = Db.getAddressAssets Config.DbEngineType Config.DbConnectionString
    let getAddressStakes = Db.getAddressStakes Config.DbEngineType Config.DbConnectionString
    let getValidatorStakes = Db.getValidatorStakes Config.DbEngineType Config.DbConnectionString

    let getAccountState = Db.getAccountState Config.DbEngineType Config.DbConnectionString
    let getAccountHoldings = Db.getAccountHoldings Config.DbEngineType Config.DbConnectionString
    let getHoldingState = Db.getHoldingState Config.DbEngineType Config.DbConnectionString

    let getAccountVotes = Db.getAccountVotes Config.DbEngineType Config.DbConnectionString
    let getAccountEligibilities = Db.getAccountEligibilities Config.DbEngineType Config.DbConnectionString
    let getVoteState = Db.getVoteState Config.DbEngineType Config.DbConnectionString

    let getEligibilityState = Db.getEligibilityState Config.DbEngineType Config.DbConnectionString
    let getKycProvidersState = Db.getKycProvidersState Config.DbEngineType Config.DbConnectionString

    let getAssetState = Db.getAssetState Config.DbEngineType Config.DbConnectionString
    let getAssetHashByCode = Db.getAssetHashByCode Config.DbEngineType Config.DbConnectionString

    let getAllValidators () = Db.getAllValidators Config.DbEngineType Config.DbConnectionString
    let getValidatorState = Db.getValidatorState Config.DbEngineType Config.DbConnectionString
    let getTopValidatorsByStake = Db.getTopValidatorsByStake Config.DbEngineType Config.DbConnectionString
    let getBlacklistedValidators () = Db.getBlacklistedValidators Config.DbEngineType Config.DbConnectionString
    let getLockedAndBlacklistedValidators () =
        Db.getLockedAndBlacklistedValidators Config.DbEngineType Config.DbConnectionString

    let getTopStakersByStake =
        Db.getTopStakersByStake Config.DbEngineType Config.DbConnectionString Config.MaxRewardedStakesCount
    let getStakeState = Db.getStakeState Config.DbEngineType Config.DbConnectionString
    let getStakers = Db.getStakers Config.DbEngineType Config.DbConnectionString
    let getTotalChxStaked = Db.getTotalChxStaked Config.DbEngineType Config.DbConnectionString

    let getAllPeerNodes () = Db.getAllPeerNodes Config.DbEngineType Config.DbConnectionString
    let savePeerNode = Db.savePeerNode Config.DbEngineType Config.DbConnectionString
    let removePeerNode = Db.removePeerNode Config.DbEngineType Config.DbConnectionString

    let persistStateChanges = Db.persistStateChanges Config.DbEngineType Config.DbConnectionString

    let saveConsensusMessage = Db.saveConsensusMessage Config.DbEngineType Config.DbConnectionString
    let getConsensusMessages () = Db.getConsensusMessages Config.DbEngineType Config.DbConnectionString
    let saveConsensusState = Db.saveConsensusState Config.DbEngineType Config.DbConnectionString
    let getConsensusState () = Db.getConsensusState Config.DbEngineType Config.DbConnectionString

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Crypto
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let getNetworkId =
        let networkId = lazy (Hashing.networkId Config.NetworkCode) // Avoid repeated hashing.
        fun () -> networkId.Value

    let signHash =
        Signing.signHash getNetworkId

    let verifySignature =
        Signing.verifySignature getNetworkId

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Validators
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let addressFromPrivateKey = memoize Signing.addressFromPrivateKey

    let getTopValidators () =
        Validators.getTopValidators
            getTopValidatorsByStake
            Config.MaxValidatorCount
            (ChxAmount Config.ValidatorThreshold)
            (ChxAmount Config.ValidatorDeposit)

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

    let createNewBlockchainConfiguration =
        Blocks.createNewBlockchainConfiguration
            getTopValidators
            getBlacklistedValidators

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
            Config.ConfigurationBlockDelta
            Config.ValidatorDepositLockTime
            Config.ValidatorBlacklistTime
            Config.MaxTxCountPerBlock

    let signGenesisBlock =
        Workflows.signGenesisBlock
            createGenesisBlock
            createConsensusMessageHash
            signHash

    let initBlockchainState () =
        Workflows.initBlockchainState
            tryGetLastAppliedBlockNumber
            createGenesisBlock
            getBlock
            saveBlock
            saveBlockToDb
            persistStateChanges
            createConsensusMessageHash
            verifySignature
            Config.GenesisSignatures

    let rebuildBlockchainState () =
        Workflows.rebuildBlockchainState
            getLastAppliedBlockNumber
            getLastStoredBlockNumber
            getBlock
            saveBlockToDb

    let createBlock =
        Workflows.createBlock
            getTx
            getEquivocationProof
            verifySignature
            Hashing.isValidBlockchainAddress
            getChxAddressState
            getHoldingState
            getVoteState
            getEligibilityState
            getKycProvidersState
            getAccountState
            getAssetState
            getAssetHashByCode
            getValidatorState
            getStakeState
            getStakers
            getTotalChxStaked
            getTopStakersByStake
            getValidatorsAtHeight
            getLockedAndBlacklistedValidators
            Hashing.deriveHash
            Hashing.decode
            Hashing.hash
            Consensus.createConsensusMessageHash
            Hashing.merkleTree
            Config.MaxActionCountPerTx
            (ChxAmount Config.ValidatorDeposit)

    let getAvailableChxBalance =
        Workflows.getAvailableChxBalance
            getChxAddressState
            getTotalChxStaked
            getValidatorState
            (ChxAmount Config.ValidatorDeposit)

    let getDetailedChxBalance =
        Workflows.getDetailedChxBalance
            getChxAddressState
            getTotalChxStaked
            getValidatorState
            (ChxAmount Config.ValidatorDeposit)

    let proposeBlock =
        Workflows.proposeBlock
            getLastAppliedBlockNumber
            createBlock
            createNewBlockchainConfiguration
            getBlock
            getPendingTxs
            getPendingEquivocationProofs
            getChxAddressState
            getAvailableChxBalance
            addressFromPrivateKey
            (ChxAmount Config.MinTxActionFee)
            Config.CreateEmptyBlocks
            Config.MinEmptyBlockTime
            Config.MinValidatorCount
            (PrivateKey Config.ValidatorPrivateKey)

    let storeReceivedBlock =
        Workflows.storeReceivedBlock
            Hashing.decode
            Hashing.isValidBlockchainAddress
            getBlock
            createConsensusMessageHash
            verifySignature
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

    let persistEquivocationProofResults =
        Workflows.persistEquivocationProofResults
            saveEquivocationProofResult

    let removeOrphanEquivocationProofResults () =
        Workflows.removeOrphanEquivocationProofResults
            getAllPendingEquivocationProofHashes
            equivocationProofResultExists
            deleteEquivocationProofResult

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
            equivocationProofResultExists
            createBlock
            Config.MinValidatorCount

    let applyBlock =
        Workflows.applyBlock
            getBlock
            applyBlockToCurrentState
            persistTxResults
            persistEquivocationProofResults
            persistStateChanges

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Synchronization
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let updateNetworkTimeOffset () =
        Synchronization.updateNetworkTimeOffset
            Ntp.getNetworkTimeOffset

    let synchronizeBlockchainHead () =
        Synchronization.synchronizeBlockchainHead
            getLastStoredBlockNumber
            getLastAppliedBlockNumber
            getBlock
            Peers.requestBlockchainHeadFromPeer
            Config.BlockchainHeadPollInterval

    let handleReceivedBlockchainHead =
        Synchronization.handleReceivedBlockchainHead
            blockExists
            getLastAppliedBlockNumber
            Peers.requestBlockFromPeer

    let fetchMissingBlocks publishEvent =
        Synchronization.fetchMissingBlocks
            getLastAppliedBlockNumber
            getLastStoredBlockNumber
            getStoredBlockNumbers
            getBlock
            blockExists
            txExists
            equivocationProofExists
            Peers.requestBlockFromPeer
            Peers.requestTxFromPeer
            Peers.requestEquivocationProofFromPeer
            publishEvent
            Config.MaxNumberOfBlocksToFetchInParallel

    let tryApplyNextBlock publishEvent =
        Synchronization.tryApplyNextBlock
            getLastAppliedBlockNumber
            getBlock
            applyBlock
            txExists
            equivocationProofExists
            removeOrphanTxResults
            removeOrphanEquivocationProofResults
            publishEvent

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Consensus
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let persistConsensusMessage =
        Workflows.persistConsensusMessage
            saveConsensusMessage

    let restoreConsensusMessages () =
        Workflows.restoreConsensusMessages
            getConsensusMessages

    let persistConsensusState =
        Workflows.persistConsensusState
            saveConsensusState

    let restoreConsensusState () =
        Workflows.restoreConsensusState
            getConsensusState

    let requestConsensusState =
        Workflows.requestConsensusState
            (PrivateKey Config.ValidatorPrivateKey)
            getNetworkId
            Peers.getIdentity
            Peers.sendMessage
            isValidator
            addressFromPrivateKey

    let sendConsensusState =
        Workflows.sendConsensusState
            getNetworkId
            Peers.respondToPeer

    let verifyConsensusMessage =
        Workflows.verifyConsensusMessage
            Hashing.decode
            Hashing.hash
            getCurrentValidators
            verifySignature

    let createConsensusStateInstance publishEvent =
        Consensus.createConsensusStateInstance
            getLastAppliedBlockNumber
            getLastAppliedBlockTimestamp
            getValidatorsAtHeight
            getValidatorState
            proposeBlock
            txExists
            equivocationProofExists
            Peers.requestTxFromPeer
            Peers.requestEquivocationProofFromPeer
            Hashing.isValidBlockchainAddress
            applyBlockToCurrentState
            Hashing.decode
            Hashing.hash
            signHash
            verifyConsensusMessage
            persistConsensusState
            restoreConsensusState
            persistConsensusMessage
            restoreConsensusMessages
            requestConsensusState
            sendConsensusState
            Peers.sendMessage
            getNetworkId
            publishEvent
            addressFromPrivateKey
            (PrivateKey Config.ValidatorPrivateKey)
            Config.MinEmptyBlockTime
            Config.ConsensusMessageRetryingInterval
            Config.ConsensusProposeRetryingInterval
            Config.ConsensusTimeoutPropose
            Config.ConsensusTimeoutVote
            Config.ConsensusTimeoutCommit
            Config.ConsensusTimeoutDelta
            Config.ConsensusTimeoutIncrements
            Config.StaleRoundDetectionInterval

    let storeEquivocationProof =
        Workflows.storeEquivocationProof
            verifySignature
            Hashing.decode
            Hashing.hash
            saveEquivocationProof
            saveEquivocationProofToDb

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // API
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let submitTx =
        Workflows.submitTx
            verifySignature
            Hashing.decode
            Hashing.isValidBlockchainAddress
            Hashing.hash
            getAvailableChxBalance
            getTotalFeeForPendingTxs
            saveTx
            saveTxToDb
            Config.MaxActionCountPerTx
            (ChxAmount Config.MinTxActionFee)

    let getTxApi = Workflows.getTxApi getTx getTxInfo getTxResult Hashing.hash verifySignature

    let getEquivocationProofApi =
        Workflows.getEquivocationProofApi
            getEquivocationProof
            getEquivocationInfo
            getEquivocationProofResult
            Hashing.decode
            Hashing.hash
            verifySignature

    let getBlockApi = Workflows.getBlockApi getLastAppliedBlockNumber getBlock

    let getAddressApi = Workflows.getAddressApi getChxAddressState getDetailedChxBalance

    let getAddressAccountsApi = Workflows.getAddressAccountsApi getAddressAccounts

    let getAddressAssetsApi = Workflows.getAddressAssetsApi getAddressAssets

    let getAddressStakesApi = Workflows.getAddressStakesApi getAddressStakes

    let getAccountApi = Workflows.getAccountApi getAccountState getAccountHoldings

    let getAccountVotesApi = Workflows.getAccountVotesApi getAccountState getAccountVotes

    let getAccountEligibilitiesApi = Workflows.getAccountEligibilitiesApi getAccountState getAccountEligibilities

    let getAssetApi = Workflows.getAssetApi getAssetState

    let getAssetKycProvidersApi = Workflows.getAssetKycProvidersApi getAssetState getKycProvidersState

    let getValidatorsApi = Workflows.getValidatorsApi getCurrentValidators getAllValidators

    let getValidatorStakesApi = Workflows.getValidatorStakesApi getValidatorState getValidatorStakes

    let getPeersApi () = Workflows.getPeerListApi Peers.getPeerList

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Network
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let propagateTx =
        Workflows.propagateTx
            Config.PublicAddress
            Peers.sendMessage
            getTx
            getNetworkId

    let propagateEquivocationProof =
        Workflows.propagateEquivocationProof
            Config.PublicAddress
            Peers.sendMessage
            getEquivocationProof
            getNetworkId

    let propagateBlock =
        Workflows.propagateBlock
            Config.PublicAddress
            Peers.sendMessage
            getBlock
            getNetworkId

    let processPeerMessage peerMessage =
        Workflows.processPeerMessage
            getTx
            getEquivocationProof
            getBlock
            getLastAppliedBlockNumber
            verifyConsensusMessage
            Peers.respondToPeer
            Peers.getPeerList
            getNetworkId
            peerMessage

    let startGossip publishEvent =
        Peers.startGossip
            Config.ListeningAddress
            Config.PublicAddress
            Config.NetworkBootstrapNodes
            Config.AllowPrivateNetworkPeers
            Config.DnsResolverCacheExpirationTime
            Config.MaxConnectedPeers
            Config.GossipFanout
            Config.GossipDiscoveryInterval
            Config.GossipInterval
            Config.GossipMaxMissedHeartbeats
            Config.PeerResponseThrottlingTime
            getNetworkId
            getAllPeerNodes
            savePeerNode
            removePeerNode
            Transport.init
            Transport.sendGossipDiscoveryMessage
            Transport.sendGossipMessage
            Transport.sendMulticastMessage
            Transport.sendRequestMessage
            Transport.sendResponseMessage
            Transport.receiveMessage
            Transport.closeConnection
            Transport.closeAllConnections
            getCurrentValidators
            publishEvent

    let stopGossip () = Peers.stopGossip ()

    let startNetworkAgents () = Peers.startNetworkAgents ()

    let discoverNetwork () = Peers.discoverNetwork Config.NetworkDiscoveryTime

    let updatePeerList = Peers.updatePeerList
