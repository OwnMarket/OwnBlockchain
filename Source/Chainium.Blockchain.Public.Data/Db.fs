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
        |? 0m
        |> ChxAmount

    let getLastAppliedBlockNumber (dbConnectionString : string) : BlockNumber option =
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

    let getLastAppliedBlockTimestamp (dbConnectionString : string) : Timestamp option =
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

    let getAddressAccounts
        (dbConnectionString : string)
        (ChainiumAddress address)
        : AccountHash list
        =

        let sql =
            """
            SELECT account_hash
            FROM account
            WHERE controller_address = @controllerAddress
            ORDER BY account_id
            """

        [
            "@controllerAddress", address |> box
        ]
        |> DbTools.query<string> dbConnectionString sql
        |> List.map AccountHash

    let getAccountState (dbConnectionString : string) (AccountHash accountHash) : AccountStateDto option =
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

        match DbTools.query<AccountStateDto> dbConnectionString sql sqlParams with
        | [] -> None
        | [accountState] -> Some accountState
        | _ -> failwithf "Multiple accounts found for account hash %A" accountHash

    let getAccountHoldings
        (dbConnectionString : string)
        (AccountHash accountHash)
        (assetHash : AssetHash option)
        : AccountHoldingDto list option
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
            | Some (AssetHash hash) ->
                [
                    "@accountHash", accountHash |> box
                    "@assetHash", hash |> box
                ]

        match DbTools.query<AccountHoldingDto> dbConnectionString sql sqlParams with
        | [] -> None
        | holdings -> Some holdings

    let getHoldingState
        (dbConnectionString : string)
        (AccountHash accountHash, AssetHash assetHash)
        : HoldingStateDto option =
        let sql =
            """
            SELECT h.amount
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

    let getAssetState (dbConnectionString : string) (AssetHash assetHash) : AssetStateDto option =
        let sql =
            """
            SELECT asset_code, controller_address
            FROM asset
            WHERE asset_hash = @assetHash
            """

        let sqlParams =
            [
                "@assetHash", assetHash |> box
            ]

        match DbTools.query<AssetStateDto> dbConnectionString sql sqlParams with
        | [] -> None
        | [assetState] -> Some assetState
        | _ -> failwithf "Multiple assets found for asset hash %A" assetHash

    let getAssetHashByCode (dbConnectionString : string) (AssetCode assetCode) : AssetHash option =
        let sql =
            """
            SELECT asset_hash
            FROM asset
            WHERE asset_code = @assetCode
            """

        let sqlParams =
            [
                "@assetCode", assetCode |> box
            ]

        match DbTools.query<string> dbConnectionString sql sqlParams with
        | [] -> None
        | [assetHash] -> assetHash |> AssetHash |> Some
        | _ -> failwithf "Multiple asset hashes found for asset code %A" assetCode

    let getValidatorState (dbConnectionString : string) (ChainiumAddress validatorAddress) : ValidatorStateDto option =
        let sql =
            """
            SELECT network_address
            FROM validator
            WHERE validator_address = @validatorAddress
            """

        let sqlParams =
            [
                "@validatorAddress", validatorAddress |> box
            ]

        match DbTools.query<ValidatorStateDto> dbConnectionString sql sqlParams with
        | [] -> None
        | [validatorState] -> Some validatorState
        | _ -> failwithf "Multiple validators found for validator address %A" validatorAddress

    let getAllValidators (dbConnectionString : string) : ValidatorInfoDto list =
        let sql =
            """
            SELECT validator_address, network_address
            FROM validator
            ORDER BY validator_address
            """

        DbTools.query<ValidatorInfoDto> dbConnectionString sql []

    let getTopValidatorsByStake
        (dbConnectionString : string)
        (topCount : int)
        (ChxAmount threshold)
        : ValidatorSnapshotDto list
        =

        let sql =
            """
            SELECT validator_address, network_address, total_stake
            FROM validator
            JOIN (
                SELECT validator_address, sum(amount) AS total_stake
                FROM stake
                GROUP BY validator_address
                HAVING sum(amount) >= @threshold
                ORDER BY sum(amount) DESC, count(stakeholder_address) DESC, validator_address
                LIMIT @topCount
            ) s USING (validator_address)
            ORDER BY validator_address
            """

        [
            "@topCount", topCount |> box
            "@threshold", threshold |> box
        ]
        |> DbTools.query<ValidatorSnapshotDto> dbConnectionString sql

    let getStakeState
        (dbConnectionString : string)
        (ChainiumAddress stakeholderAddress, ChainiumAddress validatorAddress)
        : StakeStateDto option
        =

        let sql =
            """
            SELECT amount
            FROM stake
            WHERE stakeholder_address = @stakeholderAddress
            AND validator_address = @validatorAddress
            """

        let sqlParams =
            [
                "@stakeholderAddress", stakeholderAddress |> box
                "@validatorAddress", validatorAddress |> box
            ]

        match DbTools.query<StakeStateDto> dbConnectionString sql sqlParams with
        | [] -> None
        | [stakeState] -> Some stakeState
        | _ -> failwithf "Multiple stakes from address %A found for validator %A" stakeholderAddress validatorAddress

    let getTotalChxStaked (dbConnectionString : string) (ChainiumAddress stakeholderAddress) : ChxAmount =
        let sql =
            """
            SELECT sum(amount)
            FROM stake
            WHERE stakeholder_address = @stakeholderAddress
            """

        [
            "@stakeholderAddress", stakeholderAddress |> box
        ]
        |> DbTools.query<Nullable<decimal>> dbConnectionString sql
        |> List.tryHead
        |> Option.bind Option.ofNullable
        |? 0m
        |> ChxAmount

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Network
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let getAllPeerNodes (dbConnectionString : string) : NetworkAddress list =
        let sql =
            """
            SELECT network_address
            FROM peer
            """

        DbTools.query<string> dbConnectionString sql []
        |> List.map (fun a -> NetworkAddress a)

    let removePeerNode (dbConnectionString : string) (NetworkAddress networkAddress) : Result<unit, AppErrors> =
        let sql =
            """
            DELETE FROM peer
            WHERE network_address = @networkAddress
            """
        let sqlParams =
            [
                "@networkAddress", networkAddress |> box
            ]
        try
            match DbTools.execute dbConnectionString sql sqlParams with
            | 1 -> Ok ()
            | _ -> Result.appError "Didn't remove peer."
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to remove peer."

    let private getPeerNode (dbConnectionString : string) (NetworkAddress networkAddress) : NetworkAddress option =
        let sql =
            """
            SELECT network_address
            FROM peer
            WHERE network_address = @networkAddress
            """

        let sqlParams =
            [
                "@networkAddress", networkAddress |> box
            ]

        match DbTools.query<string> dbConnectionString sql sqlParams with
        | [] -> None
        | [a] -> a |> NetworkAddress |> Some
        | _ -> failwithf "Multiple entries found for address %A" networkAddress

    let private insertPeerNode (dbConnectionString : string) (NetworkAddress networkAddress) : Result<unit, AppErrors> =
        let sql =
            """
            INSERT INTO peer (network_address)
            VALUES (@networkAddress)
            """

        let sqlParams =
            [
                "@networkAddress", networkAddress |> box
            ]

        try
            match DbTools.execute dbConnectionString sql sqlParams with
            | 1 -> Ok ()
            | _ -> Result.appError "Didn't insert peer."
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to insert peer."

    let savePeerNode (dbConnectionString : string) (NetworkAddress networkAddress) : Result<unit, AppErrors> =
        match getPeerNode dbConnectionString (NetworkAddress networkAddress) with
        | Some _ -> Ok ()
        | None -> insertPeerNode dbConnectionString (NetworkAddress networkAddress)

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
            | 0 // When applying the block during catch-up, tx might not be in the pool.
            | 1 -> Ok ()
            | _ ->
                sprintf "Didn't remove processed transaction from the pool: %s" txHash
                |> Result.appError
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            sprintf "Failed to remove processed transaction from the pool: %s" txHash
            |> Result.appError

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
            | _ -> Result.appError "Didn't insert block."
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to insert block."

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
            | _ -> Result.appError "Didn't update block number."
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to update block number."

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
            | _ -> Result.appError "Didn't insert CHX balance state."
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to insert CHX balance state."

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
            | _ -> Result.appError "Didn't update CHX balance state."
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to update CHX balance state."

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
            | _ -> Result.appError "Didn't insert holding state."
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to insert holding state."

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
            | _ -> Result.appError "Didn't update holding state."
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to update holding state."

    let private updateHoldings
        conn
        transaction
        (holdings : Map<string * string, HoldingStateDto>)
        : Result<unit, AppErrors>
        =

        let foldFn result ((accountHash, assetHash), holdingState : HoldingStateDto) =
            result
            >>= fun _ ->
                {
                    AccountHash = accountHash
                    AssetHash = assetHash
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
        (accountInfo : AccountInfoDto)
        : Result<unit, AppErrors>
        =

        let sql =
            """
            INSERT INTO account (account_hash, controller_address)
            VALUES (@accountHash, @controllerAddress)
            """

        let sqlParams =
            [
                "@accountHash", accountInfo.AccountHash |> box
                "@controllerAddress", accountInfo.ControllerAddress |> box
            ]

        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 1 -> Ok ()
            | _ -> Result.appError "Didn't insert account state."
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to insert account state."

    let private updateAccount
        conn
        transaction
        (accountInfo : AccountInfoDto)
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
                "@accountHash", accountInfo.AccountHash |> box
                "@accountController", accountInfo.ControllerAddress |> box
            ]

        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 0 -> addAccount conn transaction accountInfo
            | 1 -> Ok ()
            | _ -> Result.appError "Didn't update account state."
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to update account state."

    let private updateAccounts
        (conn : DbConnection)
        (transaction : DbTransaction)
        (accounts : Map<string, AccountStateDto>)
        : Result<unit, AppErrors>
        =

        let foldFn result (accountHash, (state : AccountStateDto)) =
            result
            >>= (fun _ ->
                {
                    AccountHash = accountHash
                    ControllerAddress = state.ControllerAddress
                }
                |> updateAccount conn transaction
            )

        accounts
        |> Map.toList
        |> List.fold foldFn (Ok ())

    let private addAsset
        conn
        transaction
        (assetInfo : AssetInfoDto)
        : Result<unit, AppErrors>
        =

        let sql =
            """
            INSERT INTO asset (asset_hash, asset_code, controller_address)
            VALUES (@assetHash, @assetCode, @controllerAddress)
            """

        let assetCodeParamValue =
            if assetInfo.AssetCode.IsNullOrWhiteSpace() then
                DBNull.Value |> box
            else
                assetInfo.AssetCode |> box

        let sqlParams =
            [
                "@assetHash", assetInfo.AssetHash |> box
                "@assetCode", assetCodeParamValue
                "@controllerAddress", assetInfo.ControllerAddress |> box
            ]

        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 1 -> Ok ()
            | _ -> Result.appError "Didn't insert asset state."
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to insert asset state."

    let private updateAsset
        conn
        transaction
        (assetInfo : AssetInfoDto)
        : Result<unit, AppErrors>
        =

        let sql =
            """
            UPDATE asset
            SET asset_code = @assetCode,
                controller_address = @controllerAddress
            WHERE asset_hash = @assetHash
            """

        let assetCodeParamValue =
            if assetInfo.AssetCode.IsNullOrWhiteSpace() then
                DBNull.Value |> box
            else
                assetInfo.AssetCode |> box

        let sqlParams =
            [
                "@assetHash", assetInfo.AssetHash |> box
                "@assetCode", assetCodeParamValue
                "@controllerAddress", assetInfo.ControllerAddress |> box
            ]

        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 0 -> addAsset conn transaction assetInfo
            | 1 -> Ok ()
            | _ -> Result.appError "Didn't update asset state."
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to update asset state."

    let private updateAssets
        (conn : DbConnection)
        (transaction : DbTransaction)
        (assets : Map<string, AssetStateDto>)
        : Result<unit, AppErrors>
        =

        let foldFn result (assetHash, (state : AssetStateDto)) =
            result
            >>= (fun _ ->
                {
                    AssetHash = assetHash
                    AssetCode = state.AssetCode
                    ControllerAddress = state.ControllerAddress
                }
                |> updateAsset conn transaction
            )

        assets
        |> Map.toList
        |> List.fold foldFn (Ok ())

    let private addValidator
        conn
        transaction
        (validatorInfo : ValidatorInfoDto)
        : Result<unit, AppErrors>
        =

        let sql =
            """
            INSERT INTO validator (validator_address, network_address)
            VALUES (@validatorAddress, @networkAddress)
            """

        let sqlParams =
            [
                "@validatorAddress", validatorInfo.ValidatorAddress |> box
                "@networkAddress", validatorInfo.NetworkAddress |> box
            ]

        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 1 -> Ok ()
            | _ -> Result.appError "Didn't insert validator state."
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to insert validator state."

    let private updateValidator
        conn
        transaction
        (validatorInfo : ValidatorInfoDto)
        : Result<unit, AppErrors>
        =

        let sql =
            """
            UPDATE validator
            SET network_address = @networkAddress
            WHERE validator_address = @validatorAddress
            """

        let sqlParams =
            [
                "@validatorAddress", validatorInfo.ValidatorAddress |> box
                "@networkAddress", validatorInfo.NetworkAddress |> box
            ]

        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 0 -> addValidator conn transaction validatorInfo
            | 1 -> Ok ()
            | _ -> Result.appError "Didn't update validator state."
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to update validator state."

    let private updateValidators
        (conn : DbConnection)
        (transaction : DbTransaction)
        (validators : Map<string, ValidatorStateDto>)
        : Result<unit, AppErrors>
        =

        let foldFn result (validatorAddress, (state : ValidatorStateDto)) =
            result
            >>= (fun _ ->
                {
                    ValidatorAddress = validatorAddress
                    NetworkAddress = state.NetworkAddress
                }
                |> updateValidator conn transaction
            )

        validators
        |> Map.toList
        |> List.fold foldFn (Ok ())

    let private addStake conn transaction (stakeInfo : StakeInfoDto) : Result<unit, AppErrors> =
        let sql =
            """
            INSERT INTO stake (stakeholder_address, validator_address, amount)
            VALUES (@stakeholderAddress, @validatorAddress, @amount)
            """

        let sqlParams =
            [
                "@stakeholderAddress", stakeInfo.StakeholderAddress |> box
                "@validatorAddress", stakeInfo.ValidatorAddress |> box
                "@amount", stakeInfo.StakeState.Amount |> box
            ]

        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 1 -> Ok ()
            | _ -> Result.appError "Didn't insert stake state."
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to insert stake state."

    let private updateStake conn transaction (stakeInfo : StakeInfoDto) : Result<unit, AppErrors> =
        let sql =
            """
            UPDATE stake
            SET amount = @amount
            WHERE stakeholder_address = @stakeholderAddress
            AND validator_address = @validatorAddress
            """

        let sqlParams =
            [
                "@stakeholderAddress", stakeInfo.StakeholderAddress |> box
                "@validatorAddress", stakeInfo.ValidatorAddress |> box
                "@amount", stakeInfo.StakeState.Amount |> box
            ]

        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 0 -> addStake conn transaction stakeInfo
            | 1 -> Ok ()
            | _ -> Result.appError "Didn't update stake state."
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to update stake state."

    let private updateStakes
        conn
        transaction
        (stakes : Map<string * string, StakeStateDto>)
        : Result<unit, AppErrors>
        =

        let foldFn result ((stakeholderAddress, validatorAddress), stakeState : StakeStateDto) =
            result
            >>= fun _ ->
                {
                    StakeholderAddress = stakeholderAddress
                    ValidatorAddress = validatorAddress
                    StakeState =
                        {
                            Amount = stakeState.Amount
                        }
                }
                |> updateStake conn transaction

        stakes
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
            result {
                do! removeProcessedTxs conn transaction state.TxResults
                do! updateChxBalances conn transaction state.ChxBalances
                do! updateHoldings conn transaction state.Holdings
                do! updateAccounts conn transaction state.Accounts
                do! updateAssets conn transaction state.Assets
                do! updateValidators conn transaction state.Validators
                do! updateStakes conn transaction state.Stakes
                do! updateBlock conn transaction blockInfoDto
            }

        match result with
        | Ok () ->
            transaction.Commit()
            conn.Close()
            Ok ()
        | Error errors ->
            transaction.Rollback()
            conn.Close()
            Log.appErrors errors
            Result.appError "Failed to apply new state."
