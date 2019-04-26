namespace Own.Blockchain.Public.Node

open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
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

    let getStatsHandler : HttpHandler = fun next ctx ->
        task {
            let response =
                Stats.getCurrent ()
                |> Ok
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

    let submitTxHandler : HttpHandler = fun next ctx ->
        task {
            let response =
                try
                    let requestDto =
                        ctx.BindJsonAsync<TxEnvelopeDto>()
                        |> Async.AwaitTask
                        |> Async.RunSynchronously

                    Composition.submitTx Agents.publishEvent false requestDto
                    |> tee (Result.iter (TxSubmitted >> Agents.publishEvent))
                    |> Result.map (fun txHash -> { SubmitTxResponseDto.TxHash = txHash.Value })
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

    let getBlockHandler blockNumber : HttpHandler = fun next ctx ->
        task {
            let response =
                Composition.getBlockApi (BlockNumber blockNumber)
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

    let getAccountHandler (accountHash : string): HttpHandler = fun next ctx ->
        task {
            let response =
                ctx.TryGetQueryStringValue "asset"
                |> Option.map AssetHash
                |> Composition.getAccountApi (AccountHash accountHash)
                |> toApiResponse

            return! response next ctx
        }

    let getAccountVotesHandler (accountHash : string): HttpHandler = fun next ctx ->
        task {
            let response =
                ctx.TryGetQueryStringValue "asset"
                |> Option.map AssetHash
                |> Composition.getAccountVotesApi (AccountHash accountHash)
                |> toApiResponse

            return! response next ctx
        }

    let getAccountEligibilitiesHandler (accountHash : string): HttpHandler = fun next ctx ->
        task {
            let response =
                Composition.getAccountEligibilitiesApi (AccountHash accountHash)
                |> toApiResponse

            return! response next ctx
        }

    let getAccountKycProvidersHandler (accountHash : string): HttpHandler = fun next ctx ->
        task {
            let response =
                Composition.getAccountKycProvidersApi (AccountHash accountHash)
                |> toApiResponse

            return! response next ctx
        }

    let getAssetHandler (assetHash : string): HttpHandler = fun next ctx ->
        task {
            let response =
                Composition.getAssetApi (AssetHash assetHash)
                |> toApiResponse

            return! response next ctx
        }

    let getAssetKycProvidersHandler (assetHash : string): HttpHandler = fun next ctx ->
        task {
            let response =
                Composition.getAssetKycProvidersApi (AssetHash assetHash)
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

    let getPeersHandler : HttpHandler = fun next ctx ->
        task {
            let response =
                Composition.getPeersApi ()
                |> toApiResponse

            return! response next ctx
        }

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Configuration
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let api =
        choose [
            GET >=> choose [
                route "/" >=> text "TODO: Show link to the help page"
                route "/stats" >=> getStatsHandler
                route "/pool" >=> getTxPoolInfoHandler
                routef "/tx/%s/raw" getRawTxHandler
                routef "/tx/%s" getTxHandler
                routef "/equivocation/%s" getEquivocationProofHandler
                routef "/block/%d" getBlockHandler
                routef "/address/%s/accounts" getAddressAccountsHandler
                routef "/address/%s/assets" getAddressAssetsHandler
                routef "/address/%s/stakes" getAddressStakesHandler
                routef "/address/%s" getAddressHandler
                routef "/account/%s/votes" getAccountVotesHandler
                routef "/account/%s/eligibilities" getAccountEligibilitiesHandler
                routef "/account/%s/kyc-providers" getAccountKycProvidersHandler
                routef "/account/%s" getAccountHandler
                routef "/asset/%s/kyc-providers" getAssetKycProvidersHandler
                routef "/asset/%s" getAssetHandler
                route "/validators" >=> getValidatorsHandler
                routef "/validator/%s/stakes" getValidatorStakesHandler
                route "/peers" >=> getPeersHandler
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
        WebHostBuilder()
            .SuppressStatusMessages(true)
            .UseKestrel()
            .Configure(Action<IApplicationBuilder> configureApp)
            .ConfigureServices(configureServices)
            .UseUrls(Config.ApiListeningAddresses)
            .Build()
            .Run()
