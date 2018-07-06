namespace Chainium.Blockchain.Public.Faucet.Dtos

open System

type ClaimChxRequestDto = {
    ChainiumAddress : string
}

type ClaimAssetRequestDto = {
    AccountHash : string
}
