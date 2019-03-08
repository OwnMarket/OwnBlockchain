namespace Own.Blockchain.Public.Core.Events

open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Core.Dtos

type AppEvent =
    | PeerMessageReceived of PeerMessageEnvelope
    | TxSubmitted of TxHash
    | TxReceived of TxHash * TxEnvelopeDto
    | TxFetched of TxHash * TxEnvelopeDto
    | TxStored of TxHash * isFetched : bool
    | BlockCommitted of BlockNumber * BlockEnvelopeDto
    | BlockReceived of BlockNumber * BlockEnvelopeDto
    | BlockFetched of BlockNumber * BlockEnvelopeDto
    | BlockStored of BlockNumber * isFetched : bool
    | BlockCompleted of BlockNumber // Block is completed (all Txs fetched) and ready to be applied.
    | BlockApplied of BlockNumber
    | ConsensusMessageReceived of ConsensusCommand
    | ConsensusCommandInvoked of ConsensusCommand
    | EquivocationProofDetected of EquivocationProofDto * BlockchainAddress
    | EquivocationProofReceived of EquivocationProofDto
    | EquivocationProofFetched of EquivocationProofDto
    | EquivocationProofStored of EquivocationProofHash * isFetched : bool
    | PeerListReceived of GossipMember list
    | ConsensusStateRequestReceived of ConsensusStateRequestDto * PeerNetworkIdentity
    | ConsensusStateResponseReceived of ConsensusStateResponseDto
