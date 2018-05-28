namespace Chainium.Blockchain.Public.IntegrationTests

open System
open System.IO
open System.Net.Http
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.TestHost
open Microsoft.AspNetCore.Builder
open Xunit
open Newtonsoft.Json
open Swensen.Unquote
open Chainium.Blockchain.Public.Node
open Chainium.Blockchain.Public.Core.Dtos

module Tests=

    let private buildTestServer() =
        let hostBuilder = 
            WebHostBuilder()
                .Configure(Action<IApplicationBuilder> Api.configureApp)
                .ConfigureServices(Api.configureServices)

        new TestServer(hostBuilder)


    [<Fact>]
    let ``Api - submit transaction`` () =
        ///createDatabase()

        if Directory.Exists(Config.DataDir) 
            then do
                Directory.Delete(Config.DataDir,true)

        let testServer = buildTestServer()
        let client = testServer.CreateClient()

        let expectedTx = 
            {
                Tx = "DQp7DQogICAgTm9uY2U6IDEsDQogICAgRmVlOiAxLA0KICAgIEFjdGlvbnM6IFsNCiAgICAgICAgew0KICAgICAgICAgICAgQWN0aW9uVHlwZTogIkNoeFRyYW5zZmVyIiwNCiAgICAgICAgICAgIEFjdGlvbkRhdGE6IHsNCiAgICAgICAgICAgICAgICBSZWNpcGllbnRBZGRyZXNzOiAiQ0g4OWZ0TFZMSFhuaHF3cmhSbWoxdnUyb0RDb2ZWTlV1UWQiLA0KICAgICAgICAgICAgICAgIEFtb3VudDogMTANCiAgICAgICAgICAgIH0NCiAgICAgICAgfQ0KICAgIF0NCn0NCg=="
                V = "1"
                S = "1E39SCcQGxtZ7Ar8dUX2Y62juVTW9y3yEQP9kddvMQ9dT"
                R = "1AoQ3cWtvrPQHFjdwc9DbcN84g16R9mgpwvqe8FdiVnUD"
            }

        let tx = JsonConvert.SerializeObject(expectedTx)
        
        let content = 
            new StringContent(tx,System.Text.Encoding.UTF8,"application/json")
    
        let res = 
            client.PostAsync("/tx",content)
            |> Async.AwaitTask
            |> Async.RunSynchronously
    
        let httpResult = res.EnsureSuccessStatusCode()
    
        let txHash = 
            httpResult.Content.ReadAsStringAsync()
                |> Async.AwaitTask
                |> Async.RunSynchronously
                |> JsonConvert.DeserializeObject<SubmitTxResponseDto>

        let fileName = sprintf "Tx_%s" txHash.TxHash
        let txFile = Path.Combine(Config.DataDir, fileName)

        test <@  txFile |> File.Exists @>

        let savedData = 
            txFile
                |> File.ReadAllText
                |> JsonConvert.DeserializeObject<TxEnvelopeDto>

        test <@ expectedTx = savedData @>