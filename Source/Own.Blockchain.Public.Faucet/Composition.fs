namespace Own.Blockchain.Public.Faucet

open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Crypto

module Composition =

    let getAddressNonce = NodeClient.getAddressNonce Config.NodeApiUrl

    let submitTx = NodeClient.submitTx Config.NodeApiUrl

    let claimChx = Workflows.claimChx (ChxAmount Config.MaxClaimableChxAmount)

    let claimAsset = Workflows.claimAsset (AssetAmount Config.MaxClaimableAssetAmount)

    let distributeChx () =
        Workflows.distributeChx
            getAddressNonce
            submitTx
            Hashing.hash
            (Signing.signHash Config.NetworkCode)
            (PrivateKey Config.FaucetSupplyHolderPrivateKey)
            (BlockchainAddress Config.FaucetSupplyHolderAddress)
            (ChxAmount Config.TxFee)
            (int Config.DistributionBatchSize)
            (ChxAmount Config.MaxClaimableChxAmount)

    let distributeAsset () =
        Workflows.distributeAsset
            getAddressNonce
            submitTx
            Hashing.hash
            (Signing.signHash Config.NetworkCode)
            (PrivateKey Config.FaucetSupplyHolderPrivateKey)
            (BlockchainAddress Config.FaucetSupplyHolderAddress)
            (ChxAmount Config.TxFee)
            (int Config.DistributionBatchSize)
            (AssetAmount Config.MaxClaimableAssetAmount)
            (AssetHash Config.FaucetSupplyAssetHash)
            (AccountHash Config.FaucetSupplyHolderAccountHash)
