namespace Chainium.Blockchain.Public.Faucet

open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
open Giraffe
open Chainium.Common
open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Core.Dtos
open Chainium.Blockchain.Public.Faucet.Dtos

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

    let claimChxHandler : HttpHandler = fun next ctx ->
        task {
            let! requestDto = ctx.BindJsonAsync<ClaimChxRequestDto>()

            let response =
                Composition.claimChx requestDto
                |> toApiResponse

            return! response next ctx
        }

    let claimAssetHandler : HttpHandler = fun next ctx ->
        task {
            let! requestDto = ctx.BindJsonAsync<ClaimAssetRequestDto>()

            let response =
                Composition.claimAsset requestDto
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
            ]
            POST >=> choose [
                route "/chx" >=> claimChxHandler
                route "/asset" >=> claimAssetHandler
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
            .Build()
            .Run()
