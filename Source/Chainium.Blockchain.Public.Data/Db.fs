namespace Chainium.Blockchain.Public.Data

open System
open System.Data.Common
open Chainium.Common
open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Core.Dtos

module Db =

    let private createInsertParams (parameters : (string * obj) list) =
        let parameterNames =
            parameters
            |> List.map fst
            |> fun names -> String.Join (", ", names)

        let columnNames = parameterNames.Replace("@", "")

        (parameterNames, columnNames)

    let saveTx (dbConnectionString : string) (txInfoDto : TxInfoDto) : Result<unit, AppErrors> =
        try
            let txParams =
                [
                    "@tx_hash", txInfoDto.TxHash |> box
                    "@sender_address", txInfoDto.SenderAddress |> box
                    "@nonce", txInfoDto.Nonce |> box
                    "@fee", txInfoDto.Fee |> box
                    "@action_count", txInfoDto.ActionCount |> box
                ]

            let paramData = createInsertParams txParams

            let insertSql =
                sprintf
                    """
                    INSERT INTO tx (%s)
                    VALUES (%s)
                    """
                    (snd paramData)
                    (fst paramData)

            let result = DbTools.execute dbConnectionString insertSql txParams

            if result < 0 then
                failwith "Unknown DB error"
            else
                Ok ()
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "DB operation error"

    let getPendingTxs
        (dbConnectionString : string)
        (txsToSkip : TxHash list)
        (txCountToFetch : int)
        : PendingTxInfoDto list
        =

        let txsToSkipParamValue =
            (
                txsToSkip
                |> List.fold (fun acc (TxHash t) -> acc + sprintf "'%s'," t) ""
            )

        let skipConditionPattern =
            if txsToSkipParamValue <> "" then
                sprintf "WHERE tx_hash NOT IN (%s)" (txsToSkipParamValue.TrimEnd(','))
            else
                ""

        let sql =
            sprintf
                """
                SELECT tx_hash, sender_address, nonce, fee, tx_id AS appearance_order
                FROM tx
                %s
                ORDER BY fee DESC, tx_id
                LIMIT @txCountToFetch
                """
                skipConditionPattern

        let sqlParams =
            [
                "@txCountToFetch", txCountToFetch |> box
            ]

        DbTools.query dbConnectionString sql sqlParams

    let getTx (dbConnectionString : string) (TxHash txHash) : TxInfoDto option =
        let sql =
            """
            SELECT tx_hash, sender_address, nonce, fee, action_count
            FROM tx
            WHERE tx_hash = @txHash
            """

        let sqlParams =
            [
                "@txHash", txHash |> box
            ]

        match DbTools.query<TxInfoDto> dbConnectionString sql sqlParams with
        | [] -> None
        | [tx] -> Some tx
        | _ -> failwithf "Multiple Txs found for hash %A" txHash

    let getTotalFeeForPendingTxs (dbConnectionString : string) (ChainiumAddress senderAddress) : ChxAmount =
        let sql =
            """
            SELECT SUM(fee * action_count)
            FROM tx
            WHERE sender_address = @senderAddress
            """

        [
            "@senderAddress", senderAddress |> box
        ]
        |> DbTools.query<Nullable<decimal>> dbConnectionString sql
        |> List.tryHead
        |> Option.bind Option.ofNullable
        |? 0M
        |> ChxAmount

    let getLastBlockNumber (dbConnectionString : string) : BlockNumber option =
        let sql =
            """
            SELECT block_number
            FROM block
            ORDER BY block_id DESC
            LIMIT 1
            """

        DbTools.query<BlockInfoDto> dbConnectionString sql []
        |> List.tryHead
        |> Option.map (fun item -> BlockNumber item.BlockNumber)

    let getLastBlockTimestamp (dbConnectionString : string) : Timestamp option =
        let sql =
            """
            SELECT block_timestamp
            FROM block
            ORDER BY block_id DESC
            LIMIT 1
            """

        DbTools.query<BlockInfoDto> dbConnectionString sql []
        |> List.tryHead
        |> Option.map (fun item -> Timestamp item.BlockTimestamp)

    let getChxBalanceState (dbConnectionString : string) (ChainiumAddress address) : ChxBalanceStateDto option =
        let sql =
            """
            SELECT amount, nonce
            FROM chx_balance
            WHERE chainium_address = @address
            """

        let sqlParams =
            [
                "@address", address |> box
            ]

        match DbTools.query<ChxBalanceStateDto> dbConnectionString sql sqlParams with
        | [] -> None
        | [chxAddressDetails] -> Some chxAddressDetails
        | _ -> failwithf "Multiple CHX balance entries found for address %A" address

    let getHoldingState
        (dbConnectionString : string)
        (AccountHash accountHash, AssetHash assetHash)
        : HoldingStateDto option =
        let sql =
            """
            SELECT h.amount, h.nonce
            FROM holding AS h
            JOIN account AS a USING (account_id)
            WHERE a.account_hash = @accountHash
            AND h.asset_hash = @assetHash
            """

        let sqlParams =
            [
                "@accountHash", accountHash |> box
                "@assetHash", assetHash |> box
            ]

        match DbTools.query<HoldingStateDto> dbConnectionString sql sqlParams with
        | [] -> None
        | [holdingDetails] -> Some holdingDetails
        | _ -> failwithf "Multiple holdings of asset hash %A found for account hash %A" assetHash accountHash

    let getAccountHoldings
        (dbConnectionString : string)
        (AccountHash accountHash)
        (assetHash : string option)
        : AccountHoldingsDto list option
        =

        let filter =
            if assetHash.IsNone then
                ""
            else
                "AND h.asset_hash = @assetHash"

        let sql =
            sprintf
                """
                SELECT h.asset_hash, h.amount
                FROM account AS a
                JOIN holding AS h USING (account_id)
                WHERE a.account_hash = @accountHash
                %s
                """
                    filter

        let sqlParams =
            match assetHash with
            | None ->
                [
                    "@accountHash", accountHash |> box
                ]
            | Some hash ->
                [
                    "@accountHash", accountHash |> box
                    "@assetHash", hash |> box
                ]

        match DbTools.query<AccountHoldingsDto> dbConnectionString sql sqlParams with
        | [] -> None
        | holdingDetails -> Some holdingDetails

    let getAccountController (dbConnectionString : string) (AccountHash accountHash) : ChainiumAddress option =
        let sql =
            """
            SELECT controller_address
            FROM account
            WHERE account_hash = @accountHash
            """

        let sqlParams =
            [
                "@accountHash", accountHash |> box
            ]

        match DbTools.query<AccountControllerDto> dbConnectionString sql sqlParams with
        | [] -> None
        | [accountDetails] -> accountDetails.ControllerAddress |> ChainiumAddress |> Some
        | _ -> failwithf "Multiple controllers found for account hash %A" accountHash

    let getAssetController (dbConnectionString : string) (AssetHash assetHash) : ChainiumAddress option =
        let sql =
            """
            SELECT controller_address
            FROM asset
            WHERE asset_hash = @assetHash
            """

        let sqlParams =
            [
                "@assetHash", assetHash |> box
            ]

        match DbTools.query<AssetControllerDto> dbConnectionString sql sqlParams with
        | [] -> None
        | [assetDetails] -> assetDetails.ControllerAddress |> ChainiumAddress |> Some
        | _ -> failwithf "Multiple controllers found for asset hash %A" assetHash

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Apply New State
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let private removeProcessedTx conn transaction (txHash : string) : Result<unit, AppErrors> =
        let sql =
            """
            DELETE FROM tx
            WHERE tx_hash = @txHash
            """

        let sqlParams =
            [
                "@txHash", txHash |> box
            ]

        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 1 -> Ok ()
            | _ -> Result.appError "Failed to remove processed transaction from the pool."
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to remove processed transaction from the pool."

    let private removeProcessedTxs conn transaction (txResults : Map<string, TxResultDto>) : Result<unit, AppErrors> =
        let foldFn result (txHash, txResult : TxResultDto) =
            result
            >>= fun _ -> removeProcessedTx conn transaction txHash

        txResults
        |> Map.toList
        |> List.fold foldFn (Ok ())

    let private addBlock conn transaction (blockInfo : BlockInfoDto) : Result<unit, AppErrors> =
        let sql =
            """
            INSERT INTO block (block_number, block_hash, block_timestamp)
            VALUES (@blockNumber, @blockHash, @blockTimestamp)
            """

        let sqlParams =
            [
                "@blockNumber", blockInfo.BlockNumber |> box
                "@blockHash", blockInfo.BlockHash |> box
                "@blockTimestamp", blockInfo.BlockTimestamp |> box
            ]

        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 1 -> Ok ()
            | _ -> Result.appError "Failed to insert block"
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to insert block"

    let private updateBlock conn transaction (blockInfo : BlockInfoDto) : Result<unit, AppErrors> =
        let sql =
            """
            UPDATE block
            SET block_number = @blockNumber, block_hash = @blockHash, block_timestamp = @blockTimestamp
            """

        let sqlParams =
            [
                "@blockNumber", blockInfo.BlockNumber |> box
                "@blockHash", blockInfo.BlockHash |> box
                "@blockTimestamp", blockInfo.BlockTimestamp |> box
            ]

        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 0 -> addBlock conn transaction blockInfo
            | 1 -> Ok ()
            | _ -> Result.appError "Failed to update block number"
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to update block number"

    let private addChxBalance conn transaction (chxBalanceInfo : ChxBalanceInfoDto) : Result<unit, AppErrors> =
        let sql =
            """
            INSERT INTO chx_balance (chainium_address, amount, nonce)
            VALUES (@chainiumAddress, @amount, @nonce)
            """

        let sqlParams =
            [
                "@chainiumAddress", chxBalanceInfo.ChainiumAddress |> box
                "@amount", chxBalanceInfo.ChxBalanceState.Amount |> box
                "@nonce", chxBalanceInfo.ChxBalanceState.Nonce |> box
            ]

        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 1 -> Ok ()
            | _ -> Result.appError "Failed to insert CHX balance"
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to insert CHX balance"

    let private updateChxBalance conn transaction (chxBalanceInfo : ChxBalanceInfoDto) : Result<unit, AppErrors> =
        let sql =
            """
            UPDATE chx_balance
            SET amount = @amount, nonce = @nonce
            WHERE chainium_address = @chainiumAddress
            """

        let sqlParams =
            [
                "@chainiumAddress", chxBalanceInfo.ChainiumAddress |> box
                "@amount", chxBalanceInfo.ChxBalanceState.Amount |> box
                "@nonce", chxBalanceInfo.ChxBalanceState.Nonce |> box
            ]

        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 0 -> addChxBalance conn transaction chxBalanceInfo
            | 1 -> Ok ()
            | _ -> Result.appError "Failed to update CHX balance"
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to update CHX balance"

    let private updateChxBalances
        conn
        transaction
        (chxBalances : Map<string, ChxBalanceStateDto>)
        : Result<unit, AppErrors>
        =

        let foldFn result (chainiumAddress, chxBalanceState : ChxBalanceStateDto) =
            result
            >>= fun _ ->
                {
                    ChainiumAddress = chainiumAddress
                    ChxBalanceState =
                        {
                            Amount = chxBalanceState.Amount
                            Nonce = chxBalanceState.Nonce
                        }
                }
                |> updateChxBalance conn transaction

        chxBalances
        |> Map.toList
        |> List.fold foldFn (Ok ())

    let private addHolding conn transaction (holdingInfo : HoldingInfoDto) : Result<unit, AppErrors> =
        let sql =
            """
            INSERT INTO holding (account_id, asset_hash, amount)
            SELECT account_id, @assetHash, @amount
            FROM account
            WHERE account_hash = @accountHash
            """

        let sqlParams =
            [
                "@accountHash", holdingInfo.AccountHash |> box
                "@assetHash", holdingInfo.AssetHash |> box
                "@amount", holdingInfo.HoldingState.Amount |> box
            ]

        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 1 -> Ok ()
            | _ -> Result.appError "Failed to insert holding"
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to insert holding"

    let private updateHolding conn transaction (holdingInfo : HoldingInfoDto) : Result<unit, AppErrors> =
        let sql =
            """
            UPDATE holding
            SET amount = @amount
            WHERE account_id = (SELECT account_id FROM account WHERE account_hash = @accountHash)
            AND asset_hash = @assetHash
            """

        let sqlParams =
            [
                "@accountHash", holdingInfo.AccountHash |> box
                "@assetHash", holdingInfo.AssetHash |> box
                "@amount", holdingInfo.HoldingState.Amount |> box
            ]

        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 0 -> addHolding conn transaction holdingInfo
            | 1 -> Ok ()
            | _ -> Result.appError "Failed to update holding"
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to update holding"

    let private updateHoldings
        conn
        transaction
        (holdings : Map<string * string, HoldingStateDto>)
        : Result<unit, AppErrors>
        =

        let foldFn result (accountAsset, holdingState : HoldingStateDto) =
            result
            >>= fun _ ->
                {
                    AccountHash = fst accountAsset
                    AssetHash = snd accountAsset
                    HoldingState =
                        {
                            Amount = holdingState.Amount
                        }
                }
                |> updateHolding conn transaction

        holdings
        |> Map.toList
        |> List.fold foldFn (Ok ())

    let private addAccount
        conn
        transaction
        (accountController : AccountControllerDto)
        : Result<unit, AppErrors>
        =

        let sql =
            """
            INSERT INTO account (account_hash, controller_address)
            VALUES (@accountHash, @controllerAddress)
            """

        let sqlParams =
            [
                "@accountHash", accountController.AccountHash |> box
                "@controllerAddress", accountController.ControllerAddress |> box
            ]

        let error = Result.appError "Failed to insert account"
        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 1 -> Ok ()
            | _ -> error
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            error

    let private updateAccount
        conn
        transaction
        (accountController : AccountControllerDto)
        : Result<unit, AppErrors>
        =

        let sql =
            """
            UPDATE account
            SET controller_address = @accountController
            WHERE account_hash = @accountHash
            """

        let sqlParams =
            [
                "@accountHash", accountController.AccountHash |> box
                "@accountController", accountController.ControllerAddress |> box
            ]

        let error = Result.appError "Failed to update account controller address"
        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 0 -> addAccount conn transaction accountController
            | 1 -> Ok ()
            | _ -> error
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            error

    let private updateAccounts
        (conn : DbConnection)
        (transaction : DbTransaction)
        (accountControllers : Map<string, AccountControllerStateDto>)
        : Result<unit, AppErrors>
        =

        let foldFn result (accountHash, (state : AccountControllerStateDto)) =
            result
            >>= (fun _ ->
                {
                    AccountHash = accountHash
                    ControllerAddress = state.ControllerAddress
                }
                |> updateAccount conn transaction
            )

        accountControllers
        |> Map.toList
        |> List.fold foldFn (Ok ())

    let private addAsset
        conn
        transaction
        (assetController : AssetControllerDto)
        : Result<unit, AppErrors>
        =

        let sql =
            """
            INSERT INTO asset (asset_hash, controller_address)
            VALUES (@assetHash, @controllerAddress)
            """

        let sqlParams =
            [
                "@assetHash", assetController.AssetHash |> box
                "@controllerAddress", assetController.ControllerAddress |> box
            ]

        let error = Result.appError "Failed to insert asset"
        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 1 -> Ok ()
            | _ -> error
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            error

    let private updateAsset
        conn
        transaction
        (assetController : AssetControllerDto)
        : Result<unit, AppErrors>
        =

        let sql =
            """
            UPDATE asset
            SET controller_address = @assetController
            WHERE asset_hash = @assetHash
            """

        let sqlParams =
            [
                "@assetHash", assetController.AssetHash |> box
                "@assetController", assetController.ControllerAddress |> box
            ]

        let error = Result.appError "Failed to update asset controller address"
        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 0 -> addAsset conn transaction assetController
            | 1 -> Ok ()
            | _ -> error
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            error

    let private updateAssets
        (conn : DbConnection)
        (transaction : DbTransaction)
        (assetControllers : Map<string, AssetControllerStateDto>)
        : Result<unit, AppErrors>
        =

        let foldFn result (assetHash, (state : AssetControllerStateDto)) =
            result
            >>= (fun _ ->
                {
                    AssetHash = assetHash
                    ControllerAddress = state.ControllerAddress
                }
                |> updateAsset conn transaction
            )

        assetControllers
        |> Map.toList
        |> List.fold foldFn (Ok ())

    let applyNewState
        (dbConnectionString : string)
        (blockInfoDto : BlockInfoDto)
        (state : ProcessingOutputDto)
        : Result<unit, AppErrors>
        =

        use conn = DbTools.newConnection dbConnectionString

        conn.Open()
        let transaction = conn.BeginTransaction(Data.IsolationLevel.ReadCommitted)

        let result =
            removeProcessedTxs conn transaction state.TxResults
            >>= fun _ -> updateChxBalances conn transaction state.ChxBalances
            >>= fun _ -> updateHoldings conn transaction state.Holdings
            >>= fun _ -> updateAccounts conn transaction state.AccountControllers
            >>= fun _ -> updateAssets conn transaction state.AssetControllers
            >>= fun _ -> updateBlock conn transaction blockInfoDto

        match result with
        | Ok () ->
            transaction.Commit()
            conn.Close()
            Ok ()
        | Error errors ->
            transaction.Rollback()
            conn.Close()
            for e in errors do
                Log.error e
            Result.appError "Failed to apply new state"
