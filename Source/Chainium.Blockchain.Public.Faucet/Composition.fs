namespace Chainium.Blockchain.Public.Faucet

open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Crypto

module Composition =

    let getAddressNonce = NodeClient.getAddressNonce Config.NodeApiUrl

    let submitTx = NodeClient.submitTx Config.NodeApiUrl

    let claimChx = Workflows.claimChx (ChxAmount Config.MaxClaimableChxAmount)

    let claimAsset = Workflows.claimAsset (AssetAmount Config.MaxClaimableAssetAmount)

    let distributeChx () =
        Workflows.distributeChx
            getAddressNonce
            submitTx
            Signing.signMessage
            (PrivateKey Config.FaucetSupplyHolderPrivateKey)
            (ChainiumAddress Config.FaucetSupplyHolderAddress)
            (ChxAmount Config.TxFee)
            (int Config.DistributionBatchSize)
            (ChxAmount Config.MaxClaimableChxAmount)

    let distributeAsset () =
        Workflows.distributeAsset
            getAddressNonce
            submitTx
            Signing.signMessage
            (PrivateKey Config.FaucetSupplyHolderPrivateKey)
            (ChainiumAddress Config.FaucetSupplyHolderAddress)
            (ChxAmount Config.TxFee)
            (int Config.DistributionBatchSize)
            (AssetAmount Config.MaxClaimableAssetAmount)
            (AssetHash Config.FaucetSupplyAssetHash)
            (AccountHash Config.FaucetSupplyHolderAccountHash)
