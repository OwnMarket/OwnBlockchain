namespace Chainium.Blockchain.Public.Data

open System
open Chainium.Common
open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Core.Dtos

module Db =

    let private dbParams (paramsList : (string * obj) list) =
        let paramsString =
            paramsList
            |> List.fold (fun acc (nm, x) -> sprintf "%s, %s" nm acc) ""

        let paramNames = paramsString.Trim([| ','; ' ' |])
        (paramNames, paramNames.Replace("@", ""))

    let saveTx (dbConnectionString : string) (txInfoDto : TxInfoDto) : Result<unit, AppErrors> =
        try
            let txParams =
                [
                    "@tx_hash", txInfoDto.TxHash |> box
                    "@sender_address", txInfoDto.SenderAddress |> box
                    "@nonce", txInfoDto.Nonce |> box
                    "@fee", txInfoDto.Fee |> box
                    "@action_count", txInfoDto.ActionCount |> box
                    "@status", txInfoDto.Status |> box
                ]

            let paramData = dbParams txParams

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
            Error [AppError "DB operation error"]

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

        let pattern =
            if txsToSkipParamValue <> "" then
                sprintf "AND tx_hash NOT IN (%s)" (txsToSkipParamValue.TrimEnd(','))
            else
                ""

        let sql =
            sprintf
                """
                SELECT tx_hash, sender_address, nonce, fee, tx_id AS appearance_order
                FROM tx
                WHERE status = 0
                %s
                ORDER BY fee DESC, tx_id
                LIMIT @txCountToFetch
                """
                pattern

        let sqlParams =
            [
                "@txCountToFetch", txCountToFetch |> box
            ]

        DbTools.query dbConnectionString sql sqlParams

    let getTotalFeeForPendingTxs (dbConnectionString : string) (ChainiumAddress senderAddress) : ChxAmount =
        let sql =
            """
            SELECT SUM(fee * action_count)
            FROM tx
            WHERE status = 0
            AND sender_address = @senderAddress
            """

        [
            "@senderAddress", senderAddress |> box
        ]
        |> DbTools.query<Nullable<decimal>> dbConnectionString sql
        |> List.tryHead
        |> Option.bind Option.ofNullable
        |? 0M
        |> ChxAmount

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

    let private updateTx conn transaction (txHash : string) (txStatus : byte) : Result<unit, AppErrors> =
        let sql =
            """
            UPDATE tx
            SET status = @txStatus
            WHERE tx_hash = @txHash
            """

        let sqlParams =
            [
                "@txHash", txHash |> box
                "@txStatus", txStatus |> box
            ]

        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 1 -> Ok ()
            | _ -> Error [AppError ("Failed to update transaction")]
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Error [AppError ("Failed to insert update transaction")]

    let private updateTxs conn transaction (txResults : Map<string, TxResultDto>) : Result<unit, AppErrors> =
        let foldFn result (txHash, txResult : TxResultDto) =
            result
            >>= fun _ ->
                // TODO: Remove conversion when StatusCode type is changed to int16
                updateTx conn transaction txHash (txResult.Status |> Convert.ToByte)

        txResults
        |> Map.toList
        |> List.fold foldFn (Ok())

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
            | _ -> Error [AppError ("Failed to insert block")]
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Error [AppError ("Failed to insert block")]

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
            | _ -> Error [AppError ("Failed to update block number")]
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Error [AppError ("Failed to update block number")]

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
            | _ -> Error [AppError ("Failed to insert CHX balance")]
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Error [AppError ("Failed to insert CHX balance")]

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
            | _ -> Error [AppError ("Failed to update CHX balance")]
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Error [AppError ("Failed to update CHX balance")]

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
        |> List.fold foldFn (Ok())

    let private addHolding conn transaction (holdingInfo : HoldingInfoDto) : Result<unit, AppErrors> =
        let sql =
            """
            INSERT INTO holding (account_id, asset_code, amount)
            SELECT account_id, @assetCode, @amount
            FROM account
            WHERE account_hash = @accountHash
            """

        let sqlParams =
            [
                "@accountHash", holdingInfo.AccountHash |> box
                "@assetCode", holdingInfo.AssetCode |> box
                "@amount", holdingInfo.HoldingState.Amount |> box
            ]

        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 1 -> Ok ()
            | _ -> Error [AppError ("Failed to insert holding")]
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Error [AppError ("Failed to insert holding")]

    let private updateHolding conn transaction (holdingInfo : HoldingInfoDto) : Result<unit, AppErrors> =
        let sql =
            """
            UPDATE holding
            SET amount = @amount
            WHERE account_id = (SELECT account_id FROM account WHERE account_hash = @accountHash)
            AND asset_code = @assetCode
            """

        let sqlParams =
            [
                "@accountHash", holdingInfo.AccountHash |> box
                "@assetCode", holdingInfo.AssetCode |> box
                "@amount", holdingInfo.HoldingState.Amount |> box
            ]

        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 0 -> addHolding conn transaction holdingInfo
            | 1 -> Ok ()
            | _ -> Error [AppError ("Failed to update holding")]
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Error [AppError ("Failed to update holding")]

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
                    AccountHash = fst(accountAsset)
                    AssetCode = snd(accountAsset)
                    HoldingState =
                        {
                            Amount = holdingState.Amount
                        }
                }
                |> updateHolding conn transaction

        holdings
        |> Map.toList
        |> List.fold foldFn (Ok())

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
            updateTxs conn transaction state.TxResults
            >>= fun _ -> updateChxBalances conn transaction state.ChxBalances
            >>= fun _ -> updateHoldings conn transaction state.Holdings
            >>= fun _ -> updateBlock conn transaction blockInfoDto

        match result with
        | Ok() ->
            transaction.Commit()
            conn.Close()
            Ok ()
        | _ ->
            transaction.Rollback()
            conn.Close()
            Error [AppError ("Failed to apply new state")]

    let getChxBalanceState (dbConnectionString : string) (ChainiumAddress address) =
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

    let getHoldingState (dbConnectionString : string) (AccountHash accountHash, AssetCode assetCode) =
        let sql =
            """
            SELECT h.amount, h.nonce
            FROM holding AS h
            JOIN account AS a USING (account_id)
            WHERE a.account_hash = @accountHash
            AND h.asset_code = @assetCode
            """

        let sqlParams =
            [
                "@accountHash", accountHash |> box
                "@assetCode", assetCode |> box
            ]

        match DbTools.query<HoldingStateDto> dbConnectionString sql sqlParams with
        | [] -> None
        | [holdingDetails] -> Some holdingDetails
        | _ -> failwithf "Multiple holdings of asset code %A found for account hash %A" assetCode accountHash

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

    let getAssetController (dbConnectionString : string) (AssetCode assetCode) : ChainiumAddress option =
        let sql =
            """
            SELECT controller_address
            FROM asset
            WHERE asset_code = @assetCode
            """

        let sqlParams =
            [
                "@assetCode", assetCode |> box
            ]

        match DbTools.query<AssetControllerDto> dbConnectionString sql sqlParams with
        | [] -> None
        | [assetDetails] -> assetDetails.ControllerAddress |> ChainiumAddress |> Some
        | _ -> failwithf "Multiple controllers found for asset code %A" assetCode
