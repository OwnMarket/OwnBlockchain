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

    let private transactionData = 
        receiverWallet.Address
        |> addressToString
        |> sprintf 
            """
            {
                Nonce: 1,
                Fee: 1,
                Actions: [
                {
                        ActionType: "ChxTransfer",
                        ActionData: {
                            RecipientAddress: "%s",
                            Amount: 10
                        }
                }
                ]
            }
            """
        |> Encoding.UTF8.GetBytes

    let private expectedTx = 
        let signature = Signing.signMessage senderWallet.PrivateKey transactionData
        {
            Tx = System.Convert.ToBase64String(transactionData)
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


        let txHash = submitTransaction expectedTx client 
        let fileName = sprintf "Tx_%s" txHash.TxHash
        let txFile = Path.Combine(Config.DataDir, fileName)

        test <@  txFile |> File.Exists @>

        let savedData = 
            txFile
                |> File.ReadAllText
                |> JsonConvert.DeserializeObject<TxEnvelopeDto>

        test <@ expectedTx = savedData @>

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

        Helper.databaseInit()
        insertBalance senderWallet.Address 100m
        insertBalance receiverWallet.Address 100m

        let trans = 
            {
                BlockNumber = 1L
                SenderAddress = addressToString senderWallet.Address
                Nonce = 1L
                Fee = 1M
                Status = int16 0 
                TxHash = txHash.TxHash
            }

        match Db.saveTx Config.DbConnectionString trans with
        | Ok r -> ()
        | Error errs -> 
            errs 
            |> List.fold (fun acc x -> sprintf "%s %A\n " acc x) ""
            |> failwith
            
           

        PaceMaker.start()
        System.Threading.Thread.Sleep(10000)