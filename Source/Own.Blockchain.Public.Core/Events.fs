namespace Own.Blockchain.Public.Core.Events

open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Core.Dtos

type AppEvent =
    | PeerMessageReceived of PeerMessageEnvelope
    | TxSubmitted of TxHash
    | TxReceived of TxHash * TxEnvelopeDto
    | TxVerified of TxHash
    | TxFetched of TxHash * TxEnvelopeDto
    | TxStored of TxHash * isFetched : bool
    | BlockCommitted of BlockNumber * BlockEnvelopeDto
    | BlockReceived of BlockNumber * BlockEnvelopeDto
    | BlockFetched of BlockNumber * BlockEnvelopeDto
    | BlockStored of BlockNumber * isFetched : bool
    | BlockReady of BlockNumber // Block is complete (all TXs fetched) and ready to be applied.
    | BlockApplied of BlockNumber
    | ConsensusMessageReceived of ConsensusCommand
    | ConsensusCommandInvoked of ConsensusCommand
    | ConsensusStateRequestReceived of ConsensusStateRequest * PeerNetworkIdentity
    | ConsensusStateResponseReceived of ConsensusStateResponse
    | EquivocationProofDetected of EquivocationProofDto * BlockchainAddress
    | EquivocationProofReceived of EquivocationProofDto
    | EquivocationProofFetched of EquivocationProofDto
    | EquivocationProofStored of EquivocationProofHash * isFetched : bool
    | BlockchainHeadReceived of BlockNumber
    | PeerListReceived of GossipPeer list

type AppEvent with
    member __.CaseName =
        match __ with
        | PeerMessageReceived _ -> "PeerMessageReceived"
        | TxSubmitted _ -> "TxSubmitted"
        | TxReceived _ -> "TxReceived"
        | TxVerified _ -> "TxVerified"
        | TxFetched _ -> "TxFetched"
        | TxStored _ -> "TxStored"
        | BlockCommitted _ -> "BlockCommitted"
        | BlockReceived _ -> "BlockReceived"
        | BlockFetched _ -> "BlockFetched"
        | BlockStored _ -> "BlockStored"
        | BlockReady _ -> "BlockReady"
        | BlockApplied _ -> "BlockApplied"
        | ConsensusMessageReceived _ -> "ConsensusMessageReceived"
        | ConsensusCommandInvoked _ -> "ConsensusCommandInvoked"
        | ConsensusStateRequestReceived _ -> "ConsensusStateRequestReceived"
        | ConsensusStateResponseReceived _ -> "ConsensusStateResponseReceived"
        | EquivocationProofDetected _ -> "EquivocationProofDetected"
        | EquivocationProofReceived _ -> "EquivocationProofReceived"
        | EquivocationProofFetched _ -> "EquivocationProofFetched"
        | EquivocationProofStored _ -> "EquivocationProofStored"
        | BlockchainHeadReceived _ -> "BlockchainHeadReceived"
        | PeerListReceived _ -> "PeerListReceived"
