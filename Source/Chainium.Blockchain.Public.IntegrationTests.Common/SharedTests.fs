namespace Chainium.Blockchain.Public.IntegrationTests.Common

open System
open System.IO
open System.Text
open System.Threading
open System.Net.Http
open Newtonsoft.Json
open Swensen.Unquote
open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Node
open Chainium.Blockchain.Public.Core.Dtos
open Chainium.Blockchain.Public.Crypto
open Chainium.Blockchain.Public.Data
open Chainium.Blockchain.Public.Core
open Chainium.Blockchain.Public.Core.DomainTypes

module SharedTests =

    let addressToString (ChainiumAddress a) = a

    let newTxDto fee nonce actions =
        {
            Nonce = nonce
            Fee = fee
            Actions = actions
        }

    let private transactionEnvelope sender txDto =
        let txBytes =
            txDto
            |> JsonConvert.SerializeObject
            |> Conversion.stringToBytes

        let signature = Signing.signMessage sender.PrivateKey txBytes
        {
            Tx = System.Convert.ToBase64String(txBytes)
            V = signature.V
            S = signature.S
            R = signature.R
        }

    let private submitTransaction (client : HttpClient) txToSubmit =
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
        Composition.initBlockchainState ()

        let testServer = Helper.testServer()
        testServer.CreateClient()

    let submissionChecks
        connectionString
        shouldExist
        senderWallet
        (dto : TxDto)
        expectedTx
        (responseDto : SubmitTxResponseDto)
        =

        let fileName = sprintf "Tx_%s" responseDto.TxHash
        let txFile = Path.Combine(Config.DataDir, fileName)

        test <@ txFile |> File.Exists = shouldExist @>

        if shouldExist then
            let savedData =
                txFile
                |> File.ReadAllText
                |> JsonConvert.DeserializeObject<TxEnvelopeDto>

            test <@ expectedTx = savedData @>

        let actual = Db.getTx connectionString (TxHash responseDto.TxHash)

        match actual with
        | None ->
            if shouldExist then
                failwith "Transaction is not stored into database"
        | Some txInfo ->
            test <@ responseDto.TxHash = txInfo.TxHash @>
            test <@ dto.Fee = txInfo.Fee @>
            test <@ dto.Nonce = txInfo.Nonce @>
            test <@ txInfo.SenderAddress = (addressToString senderWallet.Address) @>

    let transactionSubmitTest engineType connString isValidTransaction =
        let client = testInit engineType connString
        let senderWallet = Chainium.Blockchain.Public.Crypto.Signing.generateWallet ()
        let receiverWallet = Chainium.Blockchain.Public.Crypto.Signing.generateWallet ()

        let action =
            {
                ActionType = "TransferChx"
                ActionData =
                    {
                        RecipientAddress = receiverWallet.Address |> addressToString
                        TransferChxTxActionDto.Amount = 10M
                    }
            }

        let txDto = newTxDto 1M 1L [action]
        let expectedTx = transactionEnvelope senderWallet txDto

        submitTransaction client expectedTx
        |> submissionChecks connString isValidTransaction senderWallet txDto expectedTx

    let processTransactions expectedBlockPath =
        PaceMaker.start()

        let mutable iter = 0
        let sleepTime = 2

        while File.Exists(expectedBlockPath) |> not && iter < Helper.BlockCreationWaitingTime do
            Thread.Sleep(sleepTime * 1000)
            iter <- iter + sleepTime

        test <@ File.Exists(expectedBlockPath) @>

    let transactionProcessingTest engineType connString =
        let client = testInit engineType connString

        let submittedTxHashes =
            [
                for i in [1 .. 4] do
                    let senderWallet = Chainium.Blockchain.Public.Crypto.Signing.generateWallet ()
                    let receiverWallet = Chainium.Blockchain.Public.Crypto.Signing.generateWallet ()

                    Helper.addBalanceAndAccount connString (addressToString senderWallet.Address) 100M
                    Helper.addBalanceAndAccount connString (addressToString receiverWallet.Address) 0M

                    let isValid = i % 2 = 0
                    let amt = if isValid then 10M else -10M
                    // prepare transaction
                    let action =
                        {
                            ActionType = "TransferChx"
                            ActionData =
                                {
                                    RecipientAddress = receiverWallet.Address |> addressToString
                                    TransferChxTxActionDto.Amount = amt
                                }
                        }

                    let fee = 1M
                    let nonce = 1L
                    let txDto = newTxDto fee nonce [ action ]

                    let expectedTx = transactionEnvelope senderWallet txDto

                    let submitedTransactionDto = submitTransaction client expectedTx
                    submissionChecks connString isValid senderWallet txDto expectedTx submitedTransactionDto

                    if isValid then
                        yield submitedTransactionDto.TxHash
            ]

        test <@ submittedTxHashes.Length = 2 @>

        processTransactions Helper.ExpectedPathForFirstBlock

        for txHash in submittedTxHashes do
            let txResultFileName = sprintf "TxResult_%s" txHash
            let expectedTxResultPath = Path.Combine(Config.DataDir, txResultFileName)
            test <@ File.Exists expectedTxResultPath @>

            test <@ Db.getTx connString (TxHash txHash) = None @>

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

    let initBlockchainStateTest engineType connString =
        // ARRANGE
        Helper.testCleanup engineType connString
        DbInit.init engineType connString
        let expectedChxBalanceState =
            {
                ChxBalanceStateDto.Amount = Config.GenesisChxSupply
                Nonce = 0L
            }

        // ACT
        Composition.initBlockchainState ()

        // ASSERT
        let genesisAddressChxBalanceState =
            Config.GenesisAddress
            |> ChainiumAddress
            |> Db.getChxBalanceState connString

        let lastAppliedBlockNumber =
            Db.getLastBlockNumber connString

        test <@ genesisAddressChxBalanceState = Some expectedChxBalanceState @>
        test <@ lastAppliedBlockNumber = Some (BlockNumber 0L) @>

    let loadBlockTest engineType connString =
        // ARRANGE
        Helper.testCleanup engineType connString
        DbInit.init engineType connString
        Composition.initBlockchainState ()

        let expectedBlockDto =
            Blocks.createGenesisState
                (ChxAmount Config.GenesisChxSupply)
                (ChainiumAddress Config.GenesisAddress)
            |> Blocks.createGenesisBlock
                Hashing.decode Hashing.hash Hashing.merkleTree Hashing.zeroHash Hashing.zeroAddress
            |> Mapping.blockToDto

        // ACT
        let loadedBlockDto = Raw.getBlock Config.DataDir (BlockNumber 0L)

        // ASSERT
        test <@ loadedBlockDto = Ok expectedBlockDto @>

    let getAccountStateTest engineType connectionString =
        Helper.testCleanup engineType connectionString
        DbInit.init engineType connectionString
        let wallet = Chainium.Blockchain.Public.Crypto.Signing.generateWallet ()

        let paramName = "@chainium_address"

        let insertSql =
            String.Format
                (
                    """
                    INSERT INTO account (account_hash, controller_address)
                    VALUES ({0}, {0});
                    """,
                    paramName
                )

        let address = addressToString wallet.Address

        [paramName, address |> box]
        |> Seq.ofList
        |> DbTools.execute connectionString insertSql
        |> ignore

        match Db.getAccountState connectionString (AccountHash address) with // TODO: Use separate account hash.
        | None -> failwith "Unable to get account state."
        | Some accountState -> test <@ ChainiumAddress accountState.ControllerAddress = wallet.Address @>

    let setAccountControllerTest engineType connectionString =
        // ARRANGE
        let client = testInit engineType connectionString

        let sender = Signing.generateWallet()
        let newController = Signing.generateWallet()
        let initialSenderChxBalance = 10M
        let initialValidatorChxBalance = 0M

        Helper.addChxBalance connectionString (sender.Address |> addressToString) initialSenderChxBalance
        Helper.addChxBalance connectionString (Config.ValidatorAddress) initialValidatorChxBalance

        let nonce = 1L
        let accountHash = Helper.createNewHashForSender sender.Address (Nonce nonce) (TxActionNumber 1s)

        let txActions =
            [
                {
                    ActionType = "CreateAccount"
                    ActionData = CreateAccountTxActionDto()
                }
                {
                    ActionType = "SetAccountController"
                    ActionData =
                        {
                            SetAccountControllerTxActionDto.AccountHash = accountHash
                            ControllerAddress = newController.Address |> addressToString
                        }
                }
            ]

        let fee = 1M
        let totalFee = fee * (decimal txActions.Length)

        let txDto = newTxDto fee nonce txActions
        let txEnvelope = transactionEnvelope sender txDto

        // ACT
        submitTransaction client txEnvelope
        |> submissionChecks connectionString true sender txDto txEnvelope

        processTransactions Helper.ExpectedPathForFirstBlock

        // ASSERT
        let accountState =
            Db.getAccountState connectionString (AccountHash accountHash)
            |> Option.map Mapping.accountStateFromDto
        let senderBalance = Db.getChxBalanceState connectionString sender.Address
        let validatorBalance = Db.getChxBalanceState connectionString (Config.ValidatorAddress |> ChainiumAddress)

        test <@ accountState = Some { ControllerAddress = newController.Address } @>
        test <@ senderBalance = Some { Amount = (initialSenderChxBalance - totalFee); Nonce = nonce } @>
        test <@ validatorBalance = Some { Amount = (initialValidatorChxBalance + totalFee); Nonce = 0L } @>

    let setAssetControllerTest engineType connectionString =
        // ARRANGE
        let client = testInit engineType connectionString

        let sender = Signing.generateWallet()
        let newController = Signing.generateWallet()
        let initialSenderChxBalance = 10M
        let initialValidatorChxBalance = 0M

        Helper.addChxBalance connectionString (sender.Address |> addressToString) initialSenderChxBalance
        Helper.addChxBalance connectionString (Config.ValidatorAddress) initialValidatorChxBalance

        let nonce = 1L
        let assetHash = Helper.createNewHashForSender sender.Address (Nonce nonce) (TxActionNumber 1s)

        let txActions =
            [
                {
                    ActionType = "CreateAsset"
                    ActionData = CreateAssetTxActionDto()
                }
                {
                    ActionType = "SetAssetController"
                    ActionData =
                        {
                            SetAssetControllerTxActionDto.AssetHash = assetHash
                            ControllerAddress = newController.Address |> addressToString
                        }
                }
            ]

        let fee = 1M
        let totalFee = fee * (decimal txActions.Length)

        let txDto = newTxDto fee nonce txActions
        let txEnvelope = transactionEnvelope sender txDto

        // ACT
        submitTransaction client txEnvelope
        |> submissionChecks connectionString true sender txDto txEnvelope

        processTransactions Helper.ExpectedPathForFirstBlock

        // ASSERT
        let assetState =
            Db.getAssetState connectionString (AssetHash assetHash)
            |> Option.map Mapping.assetStateFromDto
        let senderBalance = Db.getChxBalanceState connectionString sender.Address
        let validatorBalance = Db.getChxBalanceState connectionString (Config.ValidatorAddress |> ChainiumAddress)

        test <@ assetState = Some { AssetCode = None; ControllerAddress = newController.Address } @>
        test <@ senderBalance = Some { Amount = (initialSenderChxBalance - totalFee); Nonce = nonce } @>
        test <@ validatorBalance = Some { Amount = (initialValidatorChxBalance + totalFee); Nonce = 0L } @>

    let setAssetCodeTest engineType connectionString =
        // ARRANGE
        let client = testInit engineType connectionString

        let assetCode = "Foo"
        let sender = Signing.generateWallet()
        let initialSenderChxBalance = 10M
        let initialValidatorChxBalance = 0M

        Helper.addChxBalance connectionString (sender.Address |> addressToString) initialSenderChxBalance
        Helper.addChxBalance connectionString (Config.ValidatorAddress) initialValidatorChxBalance

        let nonce = 1L
        let assetHash = Helper.createNewHashForSender sender.Address (Nonce nonce) (TxActionNumber 1s)

        let txActions =
            [
                {
                    ActionType = "CreateAsset"
                    ActionData = CreateAssetTxActionDto()
                }
                {
                    ActionType = "SetAssetCode"
                    ActionData =
                        {
                            SetAssetCodeTxActionDto.AssetHash = assetHash
                            AssetCode = assetCode
                        }
                }
            ]

        let fee = 1M
        let totalFee = fee * (decimal txActions.Length)

        let txDto = newTxDto fee nonce txActions
        let txEnvelope = transactionEnvelope sender txDto

        // ACT
        submitTransaction client txEnvelope
        |> submissionChecks connectionString true sender txDto txEnvelope

        processTransactions Helper.ExpectedPathForFirstBlock

        // ASSERT
        let assetCode = AssetCode assetCode
        let assetState =
            Db.getAssetState connectionString (AssetHash assetHash)
            |> Option.map Mapping.assetStateFromDto
        let senderBalance = Db.getChxBalanceState connectionString sender.Address
        let validatorBalance = Db.getChxBalanceState connectionString (Config.ValidatorAddress |> ChainiumAddress)

        test <@ assetState = Some { AssetCode = Some assetCode; ControllerAddress = sender.Address } @>
        test <@ senderBalance = Some { Amount = (initialSenderChxBalance - totalFee); Nonce = nonce } @>
        test <@ validatorBalance = Some { Amount = (initialValidatorChxBalance + totalFee); Nonce = 0L } @>
