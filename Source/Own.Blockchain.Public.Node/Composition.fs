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

    let startTxCacheMonitor () = Raw.startTxCacheMonitor Config.TxCacheExpirationTime
    let startBlockCacheMonitor () = Raw.startBlockCacheMonitor Config.BlockCacheExpirationTime
    let saveTx = Raw.saveTx Config.DbEngineType Config.DbConnectionString
    let getTx = Raw.getTx Config.DbEngineType Config.DbConnectionString Config.MaxTxCacheSize
    let txExists = Raw.txExists Config.DbEngineType Config.DbConnectionString

    let saveTxResult = Raw.saveTxResult Config.DbEngineType Config.DbConnectionString
    let getTxResult = Raw.getTxResult Config.DbEngineType Config.DbConnectionString

    let txResultExists = Raw.txResultExists Config.DbEngineType Config.DbConnectionString
    let deleteTxResult = Raw.deleteTxResult Config.DbEngineType Config.DbConnectionString

    let saveEquivocationProof = Raw.saveEquivocationProof Config.DbEngineType Config.DbConnectionString
    let getEquivocationProof = Raw.getEquivocationProof Config.DbEngineType Config.DbConnectionString
    let equivocationProofExists = Raw.equivocationProofExists Config.DbEngineType Config.DbConnectionString

    let saveEquivocationProofResult = Raw.saveEquivocationProofResult Config.DbEngineType Config.DbConnectionString
    let getEquivocationProofResult = Raw.getEquivocationProofResult Config.DbEngineType Config.DbConnectionString
    let equivocationProofResultExists = Raw.equivocationProofResultExists Config.DbEngineType Config.DbConnectionString
    let deleteEquivocationProofResult = Raw.deleteEquivocationProofResult Config.DbEngineType Config.DbConnectionString

    let saveClosedTradeOrder = Raw.saveClosedTradeOrder Config.DbEngineType Config.DbConnectionString
    let getClosedTradeOrder = Raw.getClosedTradeOrder Config.DbEngineType Config.DbConnectionString
    let closedTradeOrderExists = Raw.closedTradeOrderExists Config.DbEngineType Config.DbConnectionString
    let deleteClosedTradeOrder = Raw.deleteClosedTradeOrder Config.DbEngineType Config.DbConnectionString

    let saveBlock = Raw.saveBlock Config.DbEngineType Config.DbConnectionString
    let getBlock = Raw.getBlock Config.DbEngineType Config.DbConnectionString Config.MaxTxCacheSize
    let blockExists = Raw.blockExists Config.DbEngineType Config.DbConnectionString

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
    let txExistsInDb = Db.txExists Config.DbEngineType Config.DbConnectionString

    let saveEquivocationProofToDb = Db.saveEquivocationProof Config.DbEngineType Config.DbConnectionString
    let getEquivocationInfo = Db.getEquivocationProof Config.DbEngineType Config.DbConnectionString
    let getPendingEquivocationProofs = Db.getPendingEquivocationProofs Config.DbEngineType Config.DbConnectionString
    let getAllPendingEquivocationProofHashes () =
        Db.getAllPendingEquivocationProofHashes Config.DbEngineType Config.DbConnectionString
    let equivocationProofExistsInDb = Db.equivocationProofExists Config.DbEngineType Config.DbConnectionString

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
    let getAccountKycProviders = Db.getAccountKycProviders Config.DbEngineType Config.DbConnectionString
    let getVoteState = Db.getVoteState Config.DbEngineType Config.DbConnectionString

    let getEligibilityState = Db.getEligibilityState Config.DbEngineType Config.DbConnectionString
    let getAssetKycProviders = Db.getAssetKycProviders Config.DbEngineType Config.DbConnectionString

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

    let getTradingPairControllers () =
        Db.getTradingPairControllers Config.DbEngineType Config.DbConnectionString
        // TODO DSX: Remove below upon implementing governance
        |> function
        | [] -> [BlockchainAddress Config.GenesisAddress]
        | cs -> cs

    let getTradingPairState = Db.getTradingPairState Config.DbEngineType Config.DbConnectionString
    let getTradeOrderState = Db.getTradeOrderState Config.DbEngineType Config.DbConnectionString
    let getTradeOrders = Db.getTradeOrders Config.DbEngineType Config.DbConnectionString
    let getExpiredTradeOrders = Db.getExpiredTradeOrders Config.DbEngineType Config.DbConnectionString
    let getExecutableTradeOrders = Db.getExecutableTradeOrders Config.DbEngineType Config.DbConnectionString
    let getAccountTradeOrders = Db.getAccountTradeOrders Config.DbEngineType Config.DbConnectionString
    let getIneligibleTradeOrders = Db.getIneligibleTradeOrders Config.DbEngineType Config.DbConnectionString
    let getHoldingInTradeOrders = Db.getHoldingInTradeOrders Config.DbEngineType Config.DbConnectionString
    let getOpenTradeOrderHashes () = Db.getOpenTradeOrderHashes Config.DbEngineType Config.DbConnectionString

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
            getTx
            saveTxToDb
            txResultExists
            deleteTxResult
            getEquivocationProof
            saveEquivocationProofToDb
            equivocationProofResultExists
            deleteEquivocationProofResult
            Consensus.createConsensusMessageHash
            Hashing.decode
            Hashing.hash
            verifySignature
            Hashing.isValidHash
            Hashing.isValidBlockchainAddress
            Config.MaxActionCountPerTx

    let createBlock =
        Workflows.createBlock
            getTx
            getEquivocationProof
            verifySignature
            Hashing.isValidHash
            Hashing.isValidBlockchainAddress
            getChxAddressState
            getHoldingState
            getVoteState
            getEligibilityState
            getAssetKycProviders
            getAccountState
            getAssetState
            getAssetHashByCode
            getValidatorState
            getStakeState
            getStakers
            getTotalChxStaked
            getTopStakersByStake
            getTradingPairControllers
            getTradingPairState
            getTradeOrderState
            getTradeOrders
            getExpiredTradeOrders
            getIneligibleTradeOrders
            getHoldingInTradeOrders
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
            Config.MaxTxSetFetchIterations
            Config.CreateEmptyBlocks
            Config.MinEmptyBlockTime
            Config.MinValidatorCount
            (PrivateKey Config.ValidatorPrivateKey)

    let storeReceivedBlock =
        Workflows.storeReceivedBlock
            Hashing.isValidHash
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

    let persistClosedTradeOrders =
        Workflows.persistClosedTradeOrders
            saveClosedTradeOrder

    let removeOrphanClosedTradeOrders () =
        Workflows.removeOrphanClosedTradeOrders
            getOpenTradeOrderHashes
            closedTradeOrderExists
            deleteClosedTradeOrder

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
            createNewBlockchainConfiguration
            createBlock
            Config.MinValidatorCount

    let applyBlock =
        Workflows.applyBlock
            getBlock
            applyBlockToCurrentState
            persistTxResults
            persistEquivocationProofResults
            persistClosedTradeOrders
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
            Peers.requestBlocksFromPeer

    let fetchMissingBlocks publishEvent =
        Synchronization.fetchMissingBlocks
            getLastAppliedBlockNumber
            getLastStoredBlockNumber
            getStoredBlockNumbers
            getBlock
            blockExists
            txExists
            equivocationProofExists
            txExistsInDb
            equivocationProofExistsInDb
            Peers.requestBlocksFromPeer
            Peers.requestTxsFromPeer
            Peers.requestEquivocationProofsFromPeer
            publishEvent
            Config.MaxBlockFetchQueue

    let tryApplyNextBlock =
        Synchronization.tryApplyNextBlock
            getLastAppliedBlockNumber
            getBlock
            applyBlock
            txExists
            equivocationProofExists
            txExistsInDb
            equivocationProofExistsInDb
            removeOrphanTxResults
            removeOrphanEquivocationProofResults
            removeOrphanClosedTradeOrders

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
            Peers.requestTxsFromPreferredPeer
            Peers.requestEquivocationProofsFromPreferredPeer
            Hashing.isValidHash
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
            Config.CreateEmptyBlocks
            Config.MinEmptyBlockTime
            Config.StaleConsensusDetectionInterval
            Config.ConsensusMessageRetryingInterval
            Config.ConsensusProposeRetryingInterval
            Config.ConsensusTimeoutPropose
            Config.ConsensusTimeoutVote
            Config.ConsensusTimeoutCommit
            Config.ConsensusTimeoutDelta

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

    let getNodeInfoApi () =
        Workflows.getNodeInfoApi
            addressFromPrivateKey
            Config.VersionNumber
            Config.VersionHash
            Config.NetworkCode
            Config.PublicAddress
            Config.ValidatorPrivateKey
            Config.MinTxActionFee

    let getPeersApi () = Workflows.getPeerListApi Peers.getPeerList

    let submitTx =
        Workflows.submitTx
            verifySignature
            Hashing.isValidHash
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

    let getValidatorsApi = Workflows.getValidatorsApi getCurrentValidators getAllValidators

    let getValidatorStakesApi = Workflows.getValidatorStakesApi getValidatorState getValidatorStakes

    let getAddressStakesApi = Workflows.getAddressStakesApi getAddressStakes

    let getAddressApi = Workflows.getAddressApi getChxAddressState getDetailedChxBalance

    let getAddressAccountsApi = Workflows.getAddressAccountsApi getAddressAccounts

    let getAddressAssetsApi = Workflows.getAddressAssetsApi getAddressAssets

    let getAccountApi = Workflows.getAccountApi getAccountState getAccountHoldings

    let getAccountVotesApi = Workflows.getAccountVotesApi getAccountState getAccountVotes

    let getAccountEligibilitiesApi = Workflows.getAccountEligibilitiesApi getAccountState getAccountEligibilities

    let getAccountKycProvidersApi = Workflows.getAccountKycProvidersApi getAccountState getAccountKycProviders

    let getAssetApi = Workflows.getAssetApi getAssetState

    let getAssetKycProvidersApi = Workflows.getAssetKycProvidersApi getAssetState getAssetKycProviders

    let getTradeOrderBookApi = Workflows.getTradeOrderBookApi getExecutableTradeOrders

    let getAccountTradeOrdersApi = Workflows.getAccountTradeOrdersApi getAccountTradeOrders

    let getTradeOrderApi = Workflows.getTradeOrderApi getTradeOrderState getClosedTradeOrder

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Network
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let propagateTx =
        Workflows.propagateTx
            Config.PublicAddress
            Peers.sendMessage
            getTx
            getNetworkId

    let repropagatePendingTx publishEvent =
        Workflows.repropagatePendingTx
            getTx
            getPendingTxs
            getChxAddressState
            getAvailableChxBalance
            publishEvent
            (ChxAmount Config.MinTxActionFee)
            Config.MaxTxSetFetchIterations
            Config.MaxTxCountPerBlock
            Config.TxRepropagationCount

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

    let startGossip =
        Peers.startGossip
            Config.ListeningAddress
            Config.PublicAddress
            Config.NetworkBootstrapNodes
            Config.AllowPrivateNetworkPeers
            Config.DnsResolverCacheExpirationTime
            Config.MaxConnectedPeers
            Config.GossipFanoutPercentage
            Config.GossipDiscoveryInterval
            Config.GossipInterval
            Config.GossipMaxMissedHeartbeats
            Config.PeerResponseThrottlingTime
            Config.NetworkSendoutRetryTimeout
            Config.PeerMessageMaxSize
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

    let stopGossip () = Peers.stopGossip ()

    let startNetworkAgents () = Peers.startNetworkAgents ()

    let discoverNetwork () = Peers.discoverNetwork Config.NetworkDiscoveryTime

    let updatePeerList = Peers.updatePeerList

    let getNetworkStatsApi () = Peers.getNetworkStats ()
