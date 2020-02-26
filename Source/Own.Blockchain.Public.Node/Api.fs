namespace Own.Blockchain.Public.Node

open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
open FSharp.Control.Tasks
open Giraffe
open Own.Common.FSharp
open Own.Blockchain.Common
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Core.Dtos
open Own.Blockchain.Public.Core.Events

module Api =

    let toApiResponse = function
        | Ok data ->
            data
            |> json
        | Error errors ->
            errors
            |> List.map (fun (AppError e) -> e)
            |> (fun es -> { ErrorResponseDto.Errors = es })
            |> json

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Handlers
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let getNodeInfoHandler : HttpHandler = fun next ctx ->
        task {
            let response =
                Composition.getNodeInfoApi ()
                |> Ok
                |> toApiResponse

            return! response next ctx
        }

    let getStatsHandler : HttpHandler = fun next ctx ->
        task {
            let response =
                Stats.getCurrent ()
                |> Ok
                |> toApiResponse

            return! response next ctx
        }

    let getNetworkStatsHandler : HttpHandler = fun next ctx ->
        task {
            let response =
                Composition.getNetworkStatsApi ()
                |> Ok
                |> toApiResponse

            return! response next ctx
        }

    let getPeersHandler : HttpHandler = fun next ctx ->
        task {
            let response =
                Composition.getPeersApi ()
                |> toApiResponse

            return! response next ctx
        }

    let getTxPoolInfoHandler : HttpHandler = fun next ctx ->
        task {
            let response =
                Composition.getTxPoolInfo ()
                |> Ok
                |> toApiResponse

            return! response next ctx
        }

    let getConsensusInfoHandler : HttpHandler = fun next ctx ->
        task {
            let response =
                Composition.getConsensusInfo ()
                |> toApiResponse

            return! response next ctx
        }

    let submitTxHandler : HttpHandler = fun next ctx ->
        task {
            let response =
                try
                    let requestDto =
                        ctx.BindJsonAsync<TxEnvelopeDto>()
                        |> Async.AwaitTask
                        |> Async.RunSynchronously

                    Composition.submitTx Agents.publishEvent false requestDto
                    |> tap (Result.iter (TxSubmitted >> Agents.publishEvent))
                    |> Result.map (fun (txHash, _) -> { SubmitTxResponseDto.TxHash = txHash.Value })
                with
                | _ -> Result.appError "Invalid JSON format"
                |> toApiResponse

            return! response next ctx
        }

    let getTxHandler txHash : HttpHandler = fun next ctx ->
        task {
            let response =
                Composition.getTxApi (TxHash txHash)
                |> toApiResponse

            return! response next ctx
        }

    let getRawTxHandler txHash : HttpHandler = fun next ctx ->
        task {
            let response =
                Composition.getTx (TxHash txHash)
                |> toApiResponse

            return! response next ctx
        }

    let getEquivocationProofHandler equivocationProofHash : HttpHandler = fun next ctx ->
        task {
            let response =
                Composition.getEquivocationProofApi (EquivocationProofHash equivocationProofHash)
                |> toApiResponse

            return! response next ctx
        }

    let getHeadBlockNumberHandler : HttpHandler = fun next ctx ->
        task {
            let response =
                (Composition.getLastAppliedBlockNumber ()).Value
                |> Ok
                |> toApiResponse

            return! response next ctx
        }

    let getHeadBlockHandler : HttpHandler = fun next ctx ->
        task {
            let response =
                Composition.getLastAppliedBlockNumber ()
                |> Composition.getBlockApi
                |> toApiResponse

            return! response next ctx
        }

    let getBlockHandler blockNumber : HttpHandler = fun next ctx ->
        task {
            let response =
                Composition.getBlockApi (BlockNumber blockNumber)
                |> toApiResponse

            return! response next ctx
        }

    let getValidatorsHandler : HttpHandler = fun next ctx ->
        task {
            let response =
                ctx.TryGetQueryStringValue "activeOnly"
                |> Option.map (bool.TryParse >> snd)
                |> Composition.getValidatorsApi
                |> toApiResponse

            return! response next ctx
        }

    let getValidatorStakesHandler blockchainAddress : HttpHandler = fun next ctx ->
        task {
            let response =
                Composition.getValidatorStakesApi (BlockchainAddress blockchainAddress)
                |> toApiResponse

            return! response next ctx
        }

    let getValidatorHandler blockchainAddress : HttpHandler = fun next ctx ->
        task {
            let response =
                Composition.getValidatorApi (BlockchainAddress blockchainAddress)
                |> toApiResponse

            return! response next ctx
        }

    let getAddressHandler blockchainAddress : HttpHandler = fun next ctx ->
        task {
            let response =
                Composition.getAddressApi (BlockchainAddress blockchainAddress)
                |> toApiResponse

            return! response next ctx
        }

    let getAddressAccountsHandler blockchainAddress : HttpHandler = fun next ctx ->
        task {
            let response =
                Composition.getAddressAccountsApi (BlockchainAddress blockchainAddress)
                |> toApiResponse

            return! response next ctx
        }

    let getAddressAssetsHandler blockchainAddress : HttpHandler = fun next ctx ->
        task {
            let response =
                Composition.getAddressAssetsApi (BlockchainAddress blockchainAddress)
                |> toApiResponse

            return! response next ctx
        }

    let getAddressStakesHandler blockchainAddress : HttpHandler = fun next ctx ->
        task {
            let response =
                Composition.getAddressStakesApi (BlockchainAddress blockchainAddress)
                |> toApiResponse

            return! response next ctx
        }

    let getAccountHandler (accountHash : string) : HttpHandler = fun next ctx ->
        task {
            let response =
                ctx.TryGetQueryStringValue "asset"
                |> Option.map AssetHash
                |> Composition.getAccountApi (AccountHash accountHash)
                |> toApiResponse

            return! response next ctx
        }

    let getAccountVotesHandler (accountHash : string) : HttpHandler = fun next ctx ->
        task {
            let response =
                ctx.TryGetQueryStringValue "asset"
                |> Option.map AssetHash
                |> Composition.getAccountVotesApi (AccountHash accountHash)
                |> toApiResponse

            return! response next ctx
        }

    let getAccountEligibilitiesHandler (accountHash : string) : HttpHandler = fun next ctx ->
        task {
            let response =
                Composition.getAccountEligibilitiesApi (AccountHash accountHash)
                |> toApiResponse

            return! response next ctx
        }

    let getAccountKycProvidersHandler (accountHash : string) : HttpHandler = fun next ctx ->
        task {
            let response =
                Composition.getAccountKycProvidersApi (AccountHash accountHash)
                |> toApiResponse

            return! response next ctx
        }

    let getAssetHandler (assetHash : string) : HttpHandler = fun next ctx ->
        task {
            let response =
                Composition.getAssetApi (AssetHash assetHash)
                |> toApiResponse

            return! response next ctx
        }

    let getAssetByCodeHandler (assetCode : string) : HttpHandler = fun next ctx ->
        task {
            let response =
                Composition.getAssetByCodeApi (AssetCode assetCode)
                |> toApiResponse

            return! response next ctx
        }

    let getAssetKycProvidersHandler (assetHash : string) : HttpHandler = fun next ctx ->
        task {
            let response =
                Composition.getAssetKycProvidersApi (AssetHash assetHash)
                |> toApiResponse

            return! response next ctx
        }

    let getRootHandler : HttpHandler = fun next ctx ->
        task {
            let response =
                sprintf """<a href="%s" target="_blank" style="font-family: sans-serif">%s</a>""" // IgnoreCodeStyle
                    "https://github.com/OwnMarket/OwnBlockchain/blob/master/Docs/Nodes/NodeApi.md"
                    "API Documentation"
                |> setBodyFromString

            return! response next ctx
        }

    let getWalletFrontendFile =
        let walletFrontendFile = lazy (
            System.IO.File.ReadAllText(Config.WalletFrontendFile)
                .Replace("""<base href="/">""", """<base href="/wallet">""") // IgnoreCodeStyle
                .Replace("<<NODE_API_URL>>", "")
                .Replace("<<NETWORK_CODE>>", Config.NetworkCode)
        )
        fun () -> walletFrontendFile.Value

    let getWalletHandler : HttpHandler = fun next ctx ->
        task {
            let response = getWalletFrontendFile () |> setBodyFromString
            return! response next ctx
        }

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Configuration
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let api =
        choose [
            GET >=> choose [
                route "/" >=> getRootHandler

                // System
                route "/node" >=> getNodeInfoHandler
                route "/stats" >=> getStatsHandler
                route "/network" >=> getNetworkStatsHandler
                route "/peers" >=> getPeersHandler
                route "/pool" >=> getTxPoolInfoHandler
                route "/consensus" >=> getConsensusInfoHandler

                // Canonical blockchain data
                routef "/tx/%s/raw" getRawTxHandler
                routef "/tx/%s" getTxHandler
                routef "/equivocation/%s" getEquivocationProofHandler
                route "/block/head/number" >=> getHeadBlockNumberHandler
                route "/block/head" >=> getHeadBlockHandler
                routef "/block/%d" getBlockHandler

                // Validators & Staking
                route "/validators" >=> getValidatorsHandler
                routef "/validator/%s/stakes" getValidatorStakesHandler
                routef "/validator/%s" getValidatorHandler
                routef "/address/%s/stakes" getAddressStakesHandler

                // Entity state
                routef "/address/%s/accounts" getAddressAccountsHandler
                routef "/address/%s/assets" getAddressAssetsHandler
                routef "/address/%s" getAddressHandler
                routef "/account/%s/votes" getAccountVotesHandler
                routef "/account/%s/eligibilities" getAccountEligibilitiesHandler
                routef "/account/%s/kyc-providers" getAccountKycProvidersHandler
                routef "/account/%s" getAccountHandler
                routef "/asset/%s/kyc-providers" getAssetKycProvidersHandler
                routef "/asset/%s" getAssetHandler
                routef "/asset-by-code/%s" getAssetByCodeHandler

                // Wallet
                routeStartsWith "/wallet" >=> getWalletHandler
            ]
            POST >=> choose [
                route "/tx" >=> submitTxHandler
            ]
        ]

    let errorHandler (ex : Exception) _ =
        Log.errorf "API request failed: %s" ex.AllMessagesAndStackTraces

        clearResponse
        >=> ServerErrors.INTERNAL_ERROR ex.AllMessages

    let configureApp (app : IApplicationBuilder) =
        // Add Giraffe to the ASP.NET Core pipeline
        app.UseGiraffeErrorHandler(errorHandler)
            .UseCors("Default")
            .UseGiraffe(api)

    let configureServices (services : IServiceCollection) =
        let corsPolicies (options : CorsOptions) =
            options.AddPolicy(
                "Default",
                fun builder ->
                    builder
                        .AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .WithExposedHeaders("Access-Control-Allow-Origin")
                    |> ignore
            )

        // Add Giraffe dependencies
        services
            .AddCors(fun options -> corsPolicies options)
            .AddGiraffe()
        |> ignore

    let start () =
        Log.infof "Exposing API on: %s" Config.ApiListeningAddresses
        WebHostBuilder()
            .SuppressStatusMessages(true)
            .UseKestrel()
            .Configure(Action<IApplicationBuilder> configureApp)
            .ConfigureServices(configureServices)
            .UseUrls(Config.ApiListeningAddresses)
            .Build()
            .Run()
