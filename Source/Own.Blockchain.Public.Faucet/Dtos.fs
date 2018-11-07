namespace Own.Blockchain.Public.Faucet.Dtos

open System

type ClaimChxRequestDto = {
    BlockchainAddress : string
}

type ClaimAssetRequestDto = {
    AccountHash : string
}
