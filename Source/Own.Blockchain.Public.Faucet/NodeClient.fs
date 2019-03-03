namespace Own.Blockchain.Public.Faucet

open System.Text
open Hopac
open HttpFs.Client
open Newtonsoft.Json
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Core.Dtos

module NodeClient =

    let getAddressNonce nodeApiUrl (BlockchainAddress address) =
        sprintf "%s/address/%s" nodeApiUrl address
        |> Request.createUrl Get
        |> Request.responseAsString
        |> run
        |> JsonConvert.DeserializeObject<ChxAddressStateDto>
        |> fun dto -> dto.Nonce |> Nonce

    let submitTx nodeApiUrl tx =
        sprintf "%s/tx" nodeApiUrl
        |> Request.createUrl Post
        |> Request.bodyStringEncoded tx (Encoding.UTF8)
        |> Request.responseAsString
        |> run
