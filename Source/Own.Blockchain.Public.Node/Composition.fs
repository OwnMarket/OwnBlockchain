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

    let createMixedHashKey = Raw.createMixedHashKey Hashing.decode Hashing.encodeHex

    let saveTx = Raw.saveTx Config.DataDir createMixedHashKey
    let getTx = Raw.getTx Config.DataDir createMixedHashKey
    let txExists = Raw.txExists Config.DataDir createMixedHashKey

    let saveTxResult = Raw.saveTxResult Config.DataDir
    let getTxResult = Raw.getTxResult Config.DataDir
    let txResultExists = Raw.txResultExists Config.DataDir
    let deleteTxResult = Raw.deleteTxResult Config.DataDir

    let saveEquivocationProof = Raw.saveEquivocationProof Config.DataDir createMixedHashKey
    let getEquivocationProof = Raw.getEquivocationProof Config.DataDir createMixedHashKey
    let equivocationProofExists = Raw.equivocationProofExists Config.DataDir createMixedHashKey

    let saveEquivocationProofResult = Raw.saveEquivocationProofResult Config.DataDir
    let getEquivocationProofResult = Raw.getEquivocationProofResult Config.DataDir
    let equivocationProofResultExists = Raw.equivocationProofResultExists Config.DataDir
    let deleteEquivocationProofResult = Raw.deleteEquivocationProofResult Config.DataDir

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
        tryGetLastAppliedBlockNumber () |?> fun _ -> failwith "Cannot get last applied block number."
    let getLastStoredBlockNumber () = Db.getLastStoredBlockNumber Config.DbEngineType Config.DbConnectionString
    let getStoredBlockNumbers () = Db.getStoredBlockNumbers Config.DbEngineType Config.DbConnectionString

    let getChxBalanceState = Db.getChxBalanceState Config.DbEngineType Config.DbConnectionString
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

    let createBlock =
        Workflows.createBlock
            getTx
            getEquivocationProof
            verifySignature
            Hashing.isValidBlockchainAddress
            getChxBalanceState
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
            getChxBalanceState
            getTotalChxStaked
            getValidatorState
            (ChxAmount Config.ValidatorDeposit)

    let getDetailedChxBalance =
        Workflows.getDetailedChxBalance
            getChxBalanceState
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
            getChxBalanceState
            getAvailableChxBalance
            addressFromPrivateKey
            (ChxAmount Config.MinTxActionFee)
            Config.MinValidatorCount
            (PrivateKey Config.ValidatorPrivateKey)

    let storeReceivedBlock =
        Workflows.storeReceivedBlock
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

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Consensus
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let createConsensusStateInstance publishEvent =
        Consensus.createConsensusStateInstance
            getLastAppliedBlockNumber
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
            Peers.sendMessage
            publishEvent
            addressFromPrivateKey
            (PrivateKey Config.ValidatorPrivateKey)
            Config.ConsensusMessageRetryingInterval
            Config.ConsensusProposeRetryingInterval
            Config.ConsensusTimeoutPropose
            Config.ConsensusTimeoutVote
            Config.ConsensusTimeoutCommit
            Config.ConsensusTimeoutDelta
            Config.ConsensusTimeoutIncrements

    let handleReceivedConsensusMessage =
        Workflows.handleReceivedConsensusMessage
            Hashing.decode
            Hashing.hash
            getCurrentValidators
            verifySignature

    let storeEquivocationProof =
        Workflows.storeEquivocationProof
            verifySignature
            Consensus.createConsensusMessageHash
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

    let getAddressApi = Workflows.getAddressApi getChxBalanceState getDetailedChxBalance

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

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Network
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let propagateTx =
        Workflows.propagateTx
            Config.PublicAddress
            Peers.sendMessage
            getTx

    let propagateEquivocationProof =
        Workflows.propagateEquivocationProof
            Config.PublicAddress
            Peers.sendMessage
            getEquivocationProof

    let propagateBlock =
        Workflows.propagateBlock
            Config.PublicAddress
            Peers.sendMessage
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
            Config.ListeningAddress
            Config.PublicAddress
            Config.NetworkBootstrapNodes
            getAllPeerNodes
            savePeerNode
            removePeerNode
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
