namespace Own.Blockchain.Public.Core.Tests

open System
open Newtonsoft.Json
open Own.Common
open Own.Blockchain.Common
open Own.Blockchain.Public.Core
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Core.Dtos
open Own.Blockchain.Public.Crypto

module Helpers =

    let networkCode = "UNIT_TESTS"

    let getNetworkId () =
        Hashing.networkId networkCode

    let verifySignature = Signing.verifySignature getNetworkId

    let randomString () = Guid.NewGuid().ToString("N")

    let maxActionCountPerTx = 1000

    let minTxActionFee = ChxAmount 0.001m

    let extractActionData<'T> = function
        | TransferChx action -> box action :?> 'T
        | TransferAsset action -> box action :?> 'T
        | CreateAssetEmission action -> box action :?> 'T
        | CreateAccount -> failwith "CreateAccount TxAction has no data to extract."
        | CreateAsset -> failwith "CreateAsset TxAction has no data to extract."
        | SetAccountController action -> box action :?> 'T
        | SetAssetController action -> box action :?> 'T
        | SetAssetCode action -> box action :?> 'T
        | ConfigureValidator action -> box action :?> 'T
        | RemoveValidator -> failwith "RemoveValidator TxAction has no data to extract."
        | DelegateStake action -> box action :?> 'T
        | SubmitVote action -> box action :?> 'T
        | SubmitVoteWeight action -> box action :?> 'T
        | SetAccountEligibility action -> box action :?> 'T
        | SetAssetEligibility action -> box action :?> 'T
        | ChangeKycControllerAddress action -> box action :?> 'T
        | AddKycProvider action -> box action :?> 'T
        | RemoveKycProvider action -> box action :?> 'T

    let newPendingTxInfo
        (txHash : TxHash)
        (senderAddress : BlockchainAddress)
        (nonce : Nonce)
        (actionFee : ChxAmount)
        (actionCount : int16)
        (appearanceOrder : int64)
        =

        {
            PendingTxInfo.TxHash = txHash
            Sender = senderAddress
            Nonce = nonce
            ActionFee = actionFee
            ActionCount = actionCount
            AppearanceOrder = appearanceOrder
        }

    let newRawTxDto
        (BlockchainAddress senderAddress)
        (nonce : int64)
        (actionFee : decimal)
        (actions : obj list)
        =

        let json =
            sprintf
                """
                {
                    SenderAddress: "%s",
                    Nonce: %i,
                    ActionFee: %s,
                    Actions: %s
                }
                """
                senderAddress
                nonce
                (actionFee.ToString())
                (JsonConvert.SerializeObject(actions))

        Conversion.stringToBytes json

    let newTx
        (sender : WalletInfo)
        (Nonce nonce)
        (ChxAmount actionFee)
        (actions : obj list)
        =

        let rawTx = newRawTxDto sender.Address nonce actionFee actions

        let txHash =
            rawTx |> Hashing.hash |> TxHash

        let (Signature signature) = Signing.signHash getNetworkId sender.PrivateKey txHash.Value

        let txEnvelopeDto =
            {
                Tx = rawTx |> Convert.ToBase64String
                Signature = signature
            }

        (txHash, txEnvelopeDto)

    let verifyMerkleProofs (MerkleTreeRoot merkleRoot) leafs =
        let leafs = leafs |> List.map Hashing.decode

        // Performance is not priority in unit tests, so avoid exposing hashBytes out of Crypto assembly.
        let hashBytes = Hashing.hash >> Hashing.decode

        [
            for leaf in leafs ->
                MerkleTree.calculateProof hashBytes leafs leaf
                |> MerkleTree.verifyProof hashBytes (Hashing.decode merkleRoot) leaf
        ]

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Mock for Processing.processChanges
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let validatorDeposit = ChxAmount 5000m
    let validatorDepositLockTime = 2s
    let validatorBlacklistTime = 5s

    type ProcessChangesDependencies =
        {
            GetTx : TxHash -> Result<TxEnvelopeDto, AppErrors>
            GetEquivocationProof : EquivocationProofHash -> Result<EquivocationProofDto, AppErrors>
            VerifySignature : Signature -> string -> BlockchainAddress option
            IsValidAddress : BlockchainAddress -> bool
            DeriveHash : BlockchainAddress -> Nonce -> TxActionNumber -> string
            DecodeHash : string -> byte[]
            CreateHash : byte[] -> string
            CreateConsensusMessageHash :
                (string -> byte[]) -> (byte[] -> string) -> BlockNumber -> ConsensusRound -> ConsensusMessage -> string
            GetChxBalanceStateFromStorage : BlockchainAddress -> ChxBalanceState option
            GetHoldingStateFromStorage : AccountHash * AssetHash -> HoldingState option
            GetVoteStateFromStorage : VoteId -> VoteState option
            GetEligibilityStateFromStorage : AccountHash * AssetHash -> EligibilityState option
            GetKycProvidersFromStorage : AssetHash -> BlockchainAddress list
            GetAccountStateFromStorage : AccountHash -> AccountState option
            GetAssetStateFromStorage : AssetHash -> AssetState option
            GetAssetHashByCodeFromStorage : AssetCode -> AssetHash option
            GetValidatorStateFromStorage : BlockchainAddress -> ValidatorState option
            GetStakeStateFromStorage : BlockchainAddress * BlockchainAddress -> StakeState option
            GetStakersFromStorage : BlockchainAddress -> BlockchainAddress list
            GetTotalChxStakedFromStorage : BlockchainAddress -> ChxAmount
            GetTopStakers : BlockchainAddress -> StakerInfo list
            GetLockedAndBlacklistedValidators : unit -> BlockchainAddress list
            MaxActionCountPerTx : int
            ValidatorDeposit : ChxAmount
            ValidatorDepositLockTime : int16
            ValidatorBlacklistTime : int16
            Validators : BlockchainAddress list
            ValidatorAddress : BlockchainAddress
            SharedRewardPercent : decimal
            BlockNumber : BlockNumber
            BlockchainConfiguration : BlockchainConfiguration option
            EquivocationProofs : EquivocationProofHash list
            TxSet : TxHash list
        }

    let processChangesMockedDeps =
        let unexpectedInvocation functionName =
            failwithf "%s unexpectedly invoked." functionName
        {
            GetTx = fun _ -> unexpectedInvocation "GetTx"
            GetEquivocationProof = fun _ -> unexpectedInvocation "GetEquivocationProof"
            VerifySignature = verifySignature
            IsValidAddress = Hashing.isValidBlockchainAddress
            DeriveHash = Hashing.deriveHash
            DecodeHash = Hashing.decode
            CreateHash = Hashing.hash
            CreateConsensusMessageHash = Consensus.createConsensusMessageHash
            GetChxBalanceStateFromStorage = fun _ -> unexpectedInvocation "GetChxBalanceStateFromStorage"
            GetHoldingStateFromStorage = fun _ -> unexpectedInvocation "GetHoldingStateFromStorage"
            GetVoteStateFromStorage = fun _ -> unexpectedInvocation "GetVoteStateFromStorage"
            GetEligibilityStateFromStorage = fun _ -> unexpectedInvocation "GetEligibilityStateFromStorage"
            GetKycProvidersFromStorage = fun _ -> unexpectedInvocation "GetKycProvidersFromStorage"
            GetAccountStateFromStorage = fun _ -> unexpectedInvocation "GetAccountStateFromStorage"
            GetAssetStateFromStorage = fun _ -> unexpectedInvocation "GetAssetStateFromStorage"
            GetAssetHashByCodeFromStorage = fun _ -> unexpectedInvocation "GetAssetHashByCodeFromStorage"
            GetValidatorStateFromStorage = fun _ -> None
            GetStakeStateFromStorage = fun _ -> unexpectedInvocation "GetStakeStateFromStorage"
            GetStakersFromStorage = fun _ -> unexpectedInvocation "GetStakersFromStorage"
            GetTotalChxStakedFromStorage = fun _ -> ChxAmount 0m
            GetTopStakers = fun _ -> []
            GetLockedAndBlacklistedValidators = fun _ -> []
            MaxActionCountPerTx = maxActionCountPerTx
            ValidatorDeposit = validatorDeposit
            ValidatorDepositLockTime = validatorDepositLockTime
            ValidatorBlacklistTime = validatorBlacklistTime
            Validators = []
            ValidatorAddress = BlockchainAddress ""
            SharedRewardPercent = 0m
            BlockNumber = BlockNumber 1L
            BlockchainConfiguration = None
            EquivocationProofs = []
            TxSet = []
        }

    let processChanges mockedDeps =
        Processing.processChanges
            mockedDeps.GetTx
            mockedDeps.GetEquivocationProof
            mockedDeps.VerifySignature
            mockedDeps.IsValidAddress
            mockedDeps.DeriveHash
            mockedDeps.DecodeHash
            mockedDeps.CreateHash
            mockedDeps.CreateConsensusMessageHash
            mockedDeps.GetChxBalanceStateFromStorage
            mockedDeps.GetHoldingStateFromStorage
            mockedDeps.GetVoteStateFromStorage
            mockedDeps.GetEligibilityStateFromStorage
            mockedDeps.GetKycProvidersFromStorage
            mockedDeps.GetAccountStateFromStorage
            mockedDeps.GetAssetStateFromStorage
            mockedDeps.GetAssetHashByCodeFromStorage
            mockedDeps.GetValidatorStateFromStorage
            mockedDeps.GetStakeStateFromStorage
            mockedDeps.GetStakersFromStorage
            mockedDeps.GetTotalChxStakedFromStorage
            mockedDeps.GetTopStakers
            mockedDeps.GetLockedAndBlacklistedValidators
            mockedDeps.MaxActionCountPerTx
            mockedDeps.ValidatorDeposit
            mockedDeps.ValidatorDepositLockTime
            mockedDeps.ValidatorBlacklistTime
            mockedDeps.Validators
            mockedDeps.ValidatorAddress
            mockedDeps.SharedRewardPercent
            mockedDeps.BlockNumber
            mockedDeps.BlockchainConfiguration
            mockedDeps.EquivocationProofs
            mockedDeps.TxSet
