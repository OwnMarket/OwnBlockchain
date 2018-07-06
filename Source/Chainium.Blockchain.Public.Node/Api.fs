namespace Chainium.Blockchain.Public.Node

open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
open Giraffe
open Chainium.Common
open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Core
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Core.Dtos
open Chainium.Blockchain.Public.Core.Events

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
                Composition.submitTx requestDto
                |> tee (Result.iter (TxSubmitted >> Agents.publishEvent))
                |> Result.map Mapping.txSubmittedEventToSubmitTxResponseDto
                |> toApiResponse

            return! response next ctx
        }

    let getAddressHandler chxAddress : HttpHandler = fun next ctx ->
        task {
            let response =
                Composition.getAddressApi (ChainiumAddress chxAddress)
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

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Configuration
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let api =
        choose [
            GET >=> choose [
                route "/" >=> text "TODO: Show link to the help page"
                routef "/address/%s" (fun chainiumAddress -> getAddressHandler chainiumAddress)
                routef "/account/%s" (fun accountHash -> getAccountHandler accountHash)
                routef "/tx/%s" (fun txHash -> getTxHandler txHash)
                routef "/block/%d" (fun blockNumber -> getBlockHandler blockNumber)
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
            .UseUrls(Config.ListeningAddresses)
            .Build()
            .Run()
