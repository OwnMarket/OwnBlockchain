namespace Own.Blockchain.Public.IntegrationTests.Common

open System
open System.IO
open System.Threading
open System.Net.Http
open Own.Common
open Own.Blockchain.Common
open Own.Blockchain.Public.Node
open Own.Blockchain.Public.Core.Dtos
open Own.Blockchain.Public.Crypto
open Own.Blockchain.Public.Data
open Own.Blockchain.Public.Core
open Own.Blockchain.Public.Core.DomainTypes
open Newtonsoft.Json
open MessagePack
open Swensen.Unquote

module SharedTests =

    let private addressFromPrivateKey = memoize Signing.addressFromPrivateKey

    let newTxDto (BlockchainAddress senderAddress) nonce fee actions =
        {
            SenderAddress = senderAddress
            Nonce = nonce
            Fee = fee
            Actions = actions
        }

    let private transactionEnvelope sender txDto =
        let txBytes =
            txDto
            |> JsonConvert.SerializeObject
            |> Conversion.stringToBytes

        let (Signature signature) =
            txBytes
            |> Hashing.hash
            |> Signing.signHash sender.PrivateKey

        {
            Tx = System.Convert.ToBase64String(txBytes)
            Signature = signature
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
                |> File.ReadAllBytes
                |> LZ4MessagePackSerializer.Deserialize<TxEnvelopeDto>

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
            test <@ txInfo.SenderAddress = senderWallet.Address.Value @>

    let transactionSubmitTest engineType connString isValidTransaction =
        let client = testInit engineType connString
        let senderWallet = Signing.generateWallet ()
        let receiverWallet = Signing.generateWallet ()

        let action =
            {
                ActionType = "TransferChx"
                ActionData =
                    {
                        RecipientAddress = receiverWallet.Address.Value
                        TransferChxTxActionDto.Amount = 10m
                    }
            }

        let txDto = newTxDto senderWallet.Address 1L 1m [action]
        let expectedTx = transactionEnvelope senderWallet txDto

        submitTransaction client expectedTx
        |> submissionChecks connString isValidTransaction senderWallet txDto expectedTx

    let processTransactions expectedBlockPath =
        Workers.startFetcher ()

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
                    let senderWallet = Signing.generateWallet ()
                    let receiverWallet = Signing.generateWallet ()

                    Helper.addBalanceAndAccount connString senderWallet.Address.Value 100m
                    Helper.addBalanceAndAccount connString receiverWallet.Address.Value 0m

                    let isValid = i % 2 = 0
                    let amt = if isValid then 10m else -10m
                    // prepare transaction
                    let action =
                        {
                            ActionType = "TransferChx"
                            ActionData =
                                {
                                    RecipientAddress = receiverWallet.Address.Value
                                    TransferChxTxActionDto.Amount = amt
                                }
                        }

                    let fee = 1m
                    let nonce = 1L
                    let txDto = newTxDto senderWallet.Address nonce fee [action]

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
            |> BlockchainAddress
            |> Db.getChxBalanceState connString

        let lastAppliedBlockNumber =
            Db.getLastAppliedBlockNumber connString

        test <@ genesisAddressChxBalanceState = Some expectedChxBalanceState @>
        test <@ lastAppliedBlockNumber = Some (BlockNumber 0L) @>

    let loadBlockTest engineType connString =
        // ARRANGE
        Helper.testCleanup engineType connString
        DbInit.init engineType connString
        Composition.initBlockchainState ()

        let genesisValidators =
            Config.GenesisValidators
            |> List.map (fun (ba, na) -> BlockchainAddress ba, {ValidatorState.NetworkAddress = na})
            |> Map.ofList

        let expectedBlockDto =
            Blocks.createGenesisState
                (ChxAmount Config.GenesisChxSupply)
                (BlockchainAddress Config.GenesisAddress)
                genesisValidators
            |> Blocks.assembleGenesisBlock
                Hashing.decode Hashing.hash Hashing.merkleTree Hashing.zeroHash Hashing.zeroAddress

        // ACT
        let loadedBlockDto =
            Raw.getBlock Config.DataDir (BlockNumber 0L)
            >>= Blocks.extractBlockFromEnvelopeDto

        // ASSERT
        test <@ loadedBlockDto = Ok expectedBlockDto @>

    let getAccountStateTest engineType connectionString =
        Helper.testCleanup engineType connectionString
        DbInit.init engineType connectionString
        let wallet = Signing.generateWallet ()

        let paramName = "@blockchain_address"

        let insertSql =
            String.Format
                (
                    """
                    INSERT INTO account (account_hash, controller_address)
                    VALUES ({0}, {0});
                    """,
                    paramName
                )

        let address = wallet.Address.Value

        [paramName, address |> box]
        |> Seq.ofList
        |> DbTools.execute connectionString insertSql
        |> ignore

        match Db.getAccountState connectionString (AccountHash address) with // TODO: Use separate account hash.
        | None -> failwith "Unable to get account state."
        | Some accountState -> test <@ BlockchainAddress accountState.ControllerAddress = wallet.Address @>

    let setAccountControllerTest engineType connectionString =
        // ARRANGE
        let client = testInit engineType connectionString

        let sender = Signing.generateWallet()
        let newController = Signing.generateWallet()
        let initialSenderChxBalance = 10m
        let initialValidatorChxBalance = 0m
        let (BlockchainAddress validatorAddress) =
            Config.ValidatorPrivateKey
            |> PrivateKey
            |> addressFromPrivateKey

        Helper.addChxBalance connectionString sender.Address.Value initialSenderChxBalance
        Helper.addChxBalance connectionString validatorAddress initialValidatorChxBalance

        let nonce = 1L
        let accountHash = Hashing.deriveHash sender.Address (Nonce nonce) (TxActionNumber 1s)

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
                            ControllerAddress = newController.Address.Value
                        }
                }
            ]

        let fee = 1m
        let totalFee = fee * (decimal txActions.Length)

        let txDto = newTxDto sender.Address nonce fee txActions
        let txEnvelope = transactionEnvelope sender txDto

        // ACT
        submitTransaction client txEnvelope
        |> submissionChecks connectionString true sender txDto txEnvelope

        processTransactions Helper.ExpectedPathForFirstBlock

        // ASSERT
        let accountState =
            Db.getAccountState connectionString (AccountHash accountHash)
            |> Option.map Mapping.accountStateFromDto

        let validatorAddress = Config.ValidatorPrivateKey |> PrivateKey |> addressFromPrivateKey
        let senderBalance = Db.getChxBalanceState connectionString sender.Address
        let validatorBalance = Db.getChxBalanceState connectionString validatorAddress

        test <@ accountState = Some { ControllerAddress = newController.Address } @>
        test <@ senderBalance = Some { Amount = (initialSenderChxBalance - totalFee); Nonce = nonce } @>
        test <@ validatorBalance = Some { Amount = (initialValidatorChxBalance + totalFee); Nonce = 0L } @>

    let setAssetControllerTest engineType connectionString =
        // ARRANGE
        let client = testInit engineType connectionString

        let sender = Signing.generateWallet()
        let newController = Signing.generateWallet()
        let initialSenderChxBalance = 10m
        let initialValidatorChxBalance = 0m
        let (BlockchainAddress validatorAddress) =
            Config.ValidatorPrivateKey
            |> PrivateKey
            |> addressFromPrivateKey

        Helper.addChxBalance connectionString (sender.Address.Value) initialSenderChxBalance
        Helper.addChxBalance connectionString validatorAddress initialValidatorChxBalance

        let nonce = 1L
        let assetHash = Hashing.deriveHash sender.Address (Nonce nonce) (TxActionNumber 1s)

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
                            ControllerAddress = newController.Address.Value
                        }
                }
            ]

        let fee = 1m
        let totalFee = fee * (decimal txActions.Length)

        let txDto = newTxDto sender.Address nonce fee txActions
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
        let validatorAddress = Config.ValidatorPrivateKey |> PrivateKey |> addressFromPrivateKey
        let validatorBalance = Db.getChxBalanceState connectionString validatorAddress

        test <@ assetState = Some { AssetCode = None; ControllerAddress = newController.Address } @>
        test <@ senderBalance = Some { Amount = (initialSenderChxBalance - totalFee); Nonce = nonce } @>
        test <@ validatorBalance = Some { Amount = (initialValidatorChxBalance + totalFee); Nonce = 0L } @>

    let setAssetCodeTest engineType connectionString =
        // ARRANGE
        let client = testInit engineType connectionString

        let assetCode = "Foo"
        let sender = Signing.generateWallet()
        let initialSenderChxBalance = 10m
        let initialValidatorChxBalance = 0m
        let (BlockchainAddress validatorAddress) =
            Config.ValidatorPrivateKey
            |> PrivateKey
            |> addressFromPrivateKey

        Helper.addChxBalance connectionString sender.Address.Value initialSenderChxBalance
        Helper.addChxBalance connectionString validatorAddress initialValidatorChxBalance

        let nonce = 1L
        let assetHash = Hashing.deriveHash sender.Address (Nonce nonce) (TxActionNumber 1s)

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

        let fee = 1m
        let totalFee = fee * (decimal txActions.Length)

        let txDto = newTxDto sender.Address nonce fee txActions
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
        let validatorAddress = Config.ValidatorPrivateKey |> PrivateKey |> addressFromPrivateKey
        let validatorBalance = Db.getChxBalanceState connectionString validatorAddress

        test <@ assetState = Some { AssetCode = Some assetCode; ControllerAddress = sender.Address } @>
        test <@ senderBalance = Some { Amount = (initialSenderChxBalance - totalFee); Nonce = nonce } @>
        test <@ validatorBalance = Some { Amount = (initialValidatorChxBalance + totalFee); Nonce = 0L } @>

    let setValidatorNetworkAddressTest engineType connectionString =
        // ARRANGE
        let client = testInit engineType connectionString

        let networkAddress = "localhost:5000"
        let sender = Signing.generateWallet()
        let initialSenderChxBalance = 10m
        let initialValidatorChxBalance = 0m
        let (BlockchainAddress validatorAddress) =
            Config.ValidatorPrivateKey
            |> PrivateKey
            |> addressFromPrivateKey

        Helper.addChxBalance connectionString sender.Address.Value initialSenderChxBalance
        Helper.addChxBalance connectionString validatorAddress initialValidatorChxBalance

        let nonce = 1L

        let txActions =
            [
                {
                    ActionType = "SetValidatorNetworkAddress"
                    ActionData =
                        {
                            SetValidatorNetworkAddressTxActionDto.NetworkAddress = networkAddress
                        }
                }
            ]

        let fee = 1m
        let totalFee = fee * (decimal txActions.Length)

        let txDto = newTxDto sender.Address nonce fee txActions
        let txEnvelope = transactionEnvelope sender txDto

        // ACT
        submitTransaction client txEnvelope
        |> submissionChecks connectionString true sender txDto txEnvelope

        processTransactions Helper.ExpectedPathForFirstBlock

        // ASSERT
        let validatorState =
            Db.getValidatorState connectionString sender.Address
            |> Option.map Mapping.validatorStateFromDto
        let senderBalance = Db.getChxBalanceState connectionString sender.Address
        let validatorAddress =
            Config.ValidatorPrivateKey
            |> PrivateKey
            |> addressFromPrivateKey

        let validatorBalance = Db.getChxBalanceState connectionString validatorAddress

        test <@ validatorState = Some { NetworkAddress = networkAddress } @>
        test <@ senderBalance = Some { Amount = (initialSenderChxBalance - totalFee); Nonce = nonce } @>
        test <@ validatorBalance = Some { Amount = (initialValidatorChxBalance + totalFee); Nonce = 0L } @>

    let delegateStakeTest engineType connectionString =
        // ARRANGE
        let client = testInit engineType connectionString

        let stakeValidatorAddress = (Signing.generateWallet ()).Address
        let stakeAmount = 5m
        let sender = Signing.generateWallet()
        let initialSenderChxBalance = 10m
        let initialValidatorChxBalance = 0m

        Helper.addChxBalance connectionString sender.Address.Value initialSenderChxBalance
        let (BlockchainAddress validatorAddress) = Config.ValidatorPrivateKey |> PrivateKey |> addressFromPrivateKey
        Helper.addChxBalance connectionString validatorAddress initialValidatorChxBalance

        let nonce = 1L

        let txActions =
            [
                {
                    ActionType = "DelegateStake"
                    ActionData =
                        {
                            DelegateStakeTxActionDto.ValidatorAddress = stakeValidatorAddress.Value
                            Amount = stakeAmount
                        }
                }
            ]

        let fee = 1m
        let totalFee = fee * (decimal txActions.Length)

        let txDto = newTxDto sender.Address nonce fee txActions
        let txEnvelope = transactionEnvelope sender txDto

        // ACT
        submitTransaction client txEnvelope
        |> submissionChecks connectionString true sender txDto txEnvelope

        processTransactions Helper.ExpectedPathForFirstBlock

        // ASSERT
        let stakeState =
            Db.getStakeState connectionString (sender.Address, stakeValidatorAddress)
            |> Option.map Mapping.stakeStateFromDto
        let senderBalance = Db.getChxBalanceState connectionString sender.Address
        let validatorAddress = Config.ValidatorPrivateKey |> PrivateKey |> addressFromPrivateKey
        let validatorBalance = Db.getChxBalanceState connectionString validatorAddress

        test <@ stakeState = Some { StakeState.Amount = ChxAmount stakeAmount } @>
        test <@ senderBalance = Some { Amount = (initialSenderChxBalance - totalFee); Nonce = nonce } @>
        test <@ validatorBalance = Some { Amount = (initialValidatorChxBalance + totalFee); Nonce = 0L } @>
