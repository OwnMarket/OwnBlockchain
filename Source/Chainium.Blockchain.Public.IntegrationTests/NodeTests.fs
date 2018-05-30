namespace Chainium.Blockchain.Public.IntegrationTests


open System.IO
open System.Text
open System.Net.Http
open Xunit
open Newtonsoft.Json
open Swensen.Unquote
open Chainium.Blockchain.Public.Node
open Chainium.Blockchain.Public.Core.Dtos
open Chainium.Blockchain.Public.Crypto
open Chainium.Blockchain.Public.Data
open Chainium.Blockchain.Public.Core.DomainTypes

module NodeTests =
    let private senderWallet = Chainium.Blockchain.Public.Crypto.Signing.generateWallet None
    let private receiverWallet = Chainium.Blockchain.Public.Crypto.Signing.generateWallet None

    let addressToString (ChainiumAddress a) = a

    let transactionDto = 
        {
            Nonce = 1L
            Fee = 1M
            Actions = 
                [
                    {
                        ActionType = "ChxTransfer"
                        ActionData = 
                            {
                                RecipientAddress = (addressToString receiverWallet.Address)
                                ChxTransferTxActionDto.Amount = 10M
                            }
                        }
                    ]
         }

    let private transactionAsString = 
        transactionDto
        |> JsonConvert.SerializeObject
        |> Encoding.UTF8.GetBytes

    let private expectedTx = 
        let signature = Signing.signMessage senderWallet.PrivateKey transactionAsString
        {
            Tx = System.Convert.ToBase64String(transactionAsString)
            V = signature.V
            S = signature.S
            R = signature.R
        }

    let private submitTransaction txToSubmit (client : HttpClient)=
        let tx = JsonConvert.SerializeObject(txToSubmit)
        let content = new StringContent(tx,System.Text.Encoding.UTF8,"application/json")

        let res = 
            client.PostAsync("/tx",content)
            |> Async.AwaitTask
            |> Async.RunSynchronously

        let httpResult = res.EnsureSuccessStatusCode()
        
        httpResult.Content.ReadAsStringAsync()
            |> Async.AwaitTask
            |> Async.RunSynchronously
            |> JsonConvert.DeserializeObject<SubmitTxResponseDto>

    [<Fact>]
    let ``Api - submit transaction`` () =
        let testServer = Helper.testServer()
        let client = testServer.CreateClient()
        Helper.dataFolderCleanup()
        DbInit.init Config.DbEngineType Config.DbConnectionString

        let txHash = submitTransaction expectedTx client 
        let fileName = sprintf "Tx_%s" txHash.TxHash
        let txFile = Path.Combine(Config.DataDir, fileName)

        test <@  txFile |> File.Exists @>

        let savedData = 
            txFile
                |> File.ReadAllText
                |> JsonConvert.DeserializeObject<TxEnvelopeDto>

        test <@ expectedTx = savedData @>

        let selectStatement = 
            """
            select * from tx
            """

        let transactions = DbTools.query<TxInfoDto> Config.DbConnectionString selectStatement []

        test <@ transactions.Length = 1 @>

        let actual = 
            transactions
            |> List.tryHead
        
        match actual with
        | None -> failwith "Transaction is not stored into database"
        | Some txInfo -> 
            test <@ txHash.TxHash = txInfo.TxHash @>
            test <@ transactionDto.Fee = txInfo.Fee @>
            test <@ transactionDto.Nonce = txInfo.Nonce @>
            test <@ txInfo.Status = byte(0) @>
            test <@ txInfo.SenderAddress = (addressToString senderWallet.Address) @>

    let private insertBalance address (balance : decimal) =
        let insertSQL =
            sprintf  
                """
                insert into chx_balance
                (
                    chainium_address,
                    amount,
                    nonce
                )
                values("%s",%f,0)
                """
                (addressToString address)
                balance

        DbTools.execute Config.DbConnectionString insertSQL []
        |> ignore        

    [<Fact>]
    let ``Node - process transactions`` () =
        let testServer = Helper.testServer()
        let client = testServer.CreateClient()
        Helper.dataFolderCleanup()

        let txHash = submitTransaction expectedTx client
        PaceMaker.start()
        System.Threading.Thread.Sleep(10000)