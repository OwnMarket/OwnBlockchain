namespace Own.Blockchain.Public.Node

open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
open Giraffe
open Own.Common
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

    let submitTxHandler : HttpHandler = fun next ctx ->
        task {
            let! requestDto = ctx.BindJsonAsync<TxEnvelopeDto>()

            let response =
                Composition.submitTx false requestDto
                |> tee (Result.iter (TxSubmitted >> Agents.publishEvent))
                |> Result.map (fun txHash -> { SubmitTxResponseDto.TxHash = txHash.Value })
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

    let getAccountHandler (accountHash : string): HttpHandler = fun next ctx ->
        task {

            let response =
                ctx.TryGetQueryStringValue "asset"
                |> Option.map AssetHash
                |> Composition.getAccountApi (AccountHash accountHash)
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
                routef "/tx/%s" getTxHandler
                routef "/block/%d" getBlockHandler
                routef "/address/%s/accounts" getAddressAccountsHandler
                routef "/address/%s" getAddressHandler
                routef "/account/%s" getAccountHandler
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
