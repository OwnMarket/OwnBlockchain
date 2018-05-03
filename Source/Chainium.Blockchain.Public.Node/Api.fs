namespace Chainium.Blockchain.Public.Node

open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
open Giraffe

module Api =

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Configuration
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let api =
        choose [
            GET >=> choose [
                route "/" >=> text "TODO: Show link to the help page"
            ]
            POST >=> choose [
                route "/tx" >=> text "TODO: Handle Tx submission"
            ]
        ]

    let configureApp (app : IApplicationBuilder) =
        // Add Giraffe to the ASP.NET Core pipeline
        app.UseGiraffe api

    let configureServices (services : IServiceCollection) =
        // Add Giraffe dependencies
        services.AddGiraffe() |> ignore

    let start () =
        WebHostBuilder()
            .UseKestrel()
            .Configure(Action<IApplicationBuilder> configureApp)
            .ConfigureServices(configureServices)
            .Build()
            .Run()
