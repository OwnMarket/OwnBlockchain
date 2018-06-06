namespace Chainium.Blockchain.Public.IntegrationTests.Common

open System.IO
open System.Text
open System.Threading
open System.Net.Http
open Newtonsoft.Json
open Swensen.Unquote
open Chainium.Blockchain.Public.Node
open Chainium.Blockchain.Public.Core.Dtos
open Chainium.Blockchain.Public.Crypto
open Chainium.Blockchain.Public.Data
open Chainium.Blockchain.Public.Core.DomainTypes

module SharedTests =

    let addressToString (ChainiumAddress a) = a

    let transactionDto receiver fee =
        {
            Nonce = 1L
            Fee = fee
            Actions =
                [
                    {
                        ActionType = "ChxTransfer"
                        ActionData =
                            {
                                RecipientAddress = (addressToString receiver)
                                ChxTransferTxActionDto.Amount = 10M
                            }
                    }
                ]
        }

    let private transactionBytes transactionDto =
        transactionDto
        |> JsonConvert.SerializeObject
        |> Encoding.UTF8.GetBytes

    let private prepareTransaction sender transactionAsString =
        let signature = Signing.signMessage sender.PrivateKey transactionAsString
        {
            Tx = System.Convert.ToBase64String(transactionAsString)
            V = signature.V
            S = signature.S
            R = signature.R
        }

    let private submitTransaction txToSubmit (client : HttpClient) =
        let tx = JsonConvert.SerializeObject(txToSubmit)
        let content = new StringContent(tx, System.Text.Encoding.UTF8, "application/json")

        let res =
            client.PostAsync("/tx", content)
            |> Async.AwaitTask
            |> Async.RunSynchronously

        let httpResult = res.EnsureSuccessStatusCode()

        httpResult.Content.ReadAsStringAsync()
            |> Async.AwaitTask
            |> Async.RunSynchronously
            |> JsonConvert.DeserializeObject<SubmitTxResponseDto>

    let private testInit engineType connectionString =
        Helper.testCleanup engineType connectionString
        DbInit.init engineType connectionString

        let testServer = Helper.testServer()
        testServer.CreateClient()

    let private prepareAndSubmitTransaction client isValid =
        let senderWallet = Chainium.Blockchain.Public.Crypto.Signing.generateWallet ()
        let receiverWallet = Chainium.Blockchain.Public.Crypto.Signing.generateWallet ()

        let fee =
            match isValid with
            | true -> 10M
            | false -> -10M

        let dto = transactionDto receiverWallet.Address fee

        let expectedTx =
            dto
            |> transactionBytes
            |> prepareTransaction senderWallet

        let txHash = submitTransaction expectedTx client

        (senderWallet, receiverWallet, dto, expectedTx, txHash)

    let submissionChecks
        (
            senderWallet,
            receiverWallet,
            (dto : TxDto),
            expectedTx,
            (txHash : SubmitTxResponseDto)
        )
        shouldExist
        connectionString
        =

        let fileName = sprintf "Tx_%s" txHash.TxHash
        let txFile = Path.Combine(Config.DataDir, fileName)

        test <@ txFile |> File.Exists = shouldExist @>

        if shouldExist then do
            let savedData =
                txFile
                    |> File.ReadAllText
                    |> JsonConvert.DeserializeObject<TxEnvelopeDto>

            test <@ expectedTx = savedData @>

        let selectStatement =
            sprintf
                """
                select * from tx
                where tx_hash = '%s'
                """
                txHash.TxHash

        let transactions = DbTools.query<TxInfoDto> Config.DbConnectionString selectStatement []

        let actual =
            transactions
            |> List.tryHead

        match actual with
        | None ->
            if shouldExist then do
                failwith "Transaction is not stored into database"
        | Some txInfo ->
            test <@ txHash.TxHash = txInfo.TxHash @>
            test <@ dto.Fee = txInfo.Fee @>
            test <@ dto.Nonce = txInfo.Nonce @>
            test <@ txInfo.Status = byte(0) @>
            test <@ txInfo.SenderAddress = (addressToString senderWallet.Address) @>

    let transactionSubmitTest engineType connString isValidTransaction =
        let client = testInit engineType connString
        let data = prepareAndSubmitTransaction client isValidTransaction
        submissionChecks data isValidTransaction connString

    let transactionProcessingTest engineType connString =
        let client = testInit engineType connString
        [1..4]
        |> List.map
            (
                fun i ->
                    let isValid = i % 2 = 0
                    let submissionResult = prepareAndSubmitTransaction client isValid
                    submissionChecks submissionResult isValid connString

                    let setbalances (s, r) =
                        Helper.addBalanceAndAccount connString s 100M
                        Helper.addBalanceAndAccount connString r 0M

                    let storeChxBalances (a, b, _, _, _) =
                        (
                            a.Address |> addressToString,
                            b.Address |> addressToString
                        )
                        |> setbalances

                    storeChxBalances submissionResult
            )
        |> ignore

        let expectedBlockPath = sprintf "%s/Block_1" Config.DataDir
        PaceMaker.start()

        let mutable iter = 0
        let sleepTime = 2

        while File.Exists(expectedBlockPath) |> not && iter < Helper.BlockCreationWaitingTime do
            Thread.Sleep(sleepTime * 1000)
            iter <- iter + sleepTime

        test <@ File.Exists(expectedBlockPath) @>

    let private numOfUpdatesExecuted connectionString =
        let sql =
            """
                SELECT version_number
                FROM db_version;
            """
        DbTools.query<int> connectionString sql []

    let initDatabaseTest engineType connString =
        Helper.testCleanup engineType connString
        DbInit.init engineType connString
        let changes = numOfUpdatesExecuted connString
        test <@ changes.Length > 0 @>
