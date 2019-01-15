namespace Own.Blockchain.Public.Data

open System
open System.Data.Common
open Own.Common
open Own.Blockchain.Common
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Core.Dtos

module Db =

    let private createInsertParams (parameters : (string * obj) list) =
        let parameterNames =
            parameters
            |> List.map fst
            |> fun names -> String.Join (", ", names)

        let columnNames = parameterNames.Replace("@", "")

        (parameterNames, columnNames)

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Tx
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let saveTx dbEngineType (dbConnectionString : string) (txInfoDto : TxInfoDto) : Result<unit, AppErrors> =
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

            let result = DbTools.execute dbEngineType dbConnectionString insertSql txParams

            if result < 0 then
                failwith "Unknown DB error"
            else
                Ok ()
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "DB operation error"

    let getPendingTxs
        dbEngineType
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
            match dbEngineType with
            | Firebird ->
                sprintf
                    """
                    SELECT FIRST @txCountToFetch tx_hash, sender_address, nonce, fee, tx_id AS appearance_order
                    FROM tx
                    %s
                    ORDER BY fee DESC, tx_id
                    """
                    skipConditionPattern
            | PostgreSQL ->
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

        DbTools.query dbEngineType dbConnectionString sql sqlParams

    let getTx dbEngineType (dbConnectionString : string) (TxHash txHash) : TxInfoDto option =
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

        match DbTools.query<TxInfoDto> dbEngineType dbConnectionString sql sqlParams with
        | [] -> None
        | [tx] -> Some tx
        | _ -> failwithf "Multiple Txs found for hash %A" txHash

    let getTotalFeeForPendingTxs
        dbEngineType
        (dbConnectionString : string)
        (BlockchainAddress senderAddress)
        : ChxAmount
        =

        let sql =
            """
            SELECT sum(fee * action_count)
            FROM tx
            WHERE sender_address = @senderAddress
            """

        [
            "@senderAddress", senderAddress |> box
        ]
        |> DbTools.query<Nullable<decimal>> dbEngineType dbConnectionString sql
        |> List.tryHead
        |> Option.bind Option.ofNullable
        |? 0m
        |> ChxAmount

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Block
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let saveBlock dbEngineType (dbConnectionString : string) (blockInfo : BlockInfoDto) : Result<unit, AppErrors> =
        let sql =
            """
            INSERT INTO block (block_number, block_hash, block_timestamp, is_config_block, is_applied)
            VALUES (@blockNumber, @blockHash, @blockTimestamp, @isConfigBlock, FALSE)
            """

        let sqlParams =
            [
                "@blockNumber", blockInfo.BlockNumber |> box
                "@blockHash", blockInfo.BlockHash |> box
                "@blockTimestamp", blockInfo.BlockTimestamp |> box
                "@isConfigBlock", blockInfo.IsConfigBlock |> box
            ]

        try
            match DbTools.execute dbEngineType dbConnectionString sql sqlParams with
            | 1 -> Ok ()
            | _ -> Result.appError "Didn't insert block."
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to insert block."

    let getLastAppliedBlockNumber dbEngineType (dbConnectionString : string) : BlockNumber option =
        let sql =
            """
            SELECT block_number
            FROM block
            WHERE is_applied = TRUE
            """

        match DbTools.query<int64> dbEngineType dbConnectionString sql [] with
        | [] -> None
        | [blockNumber] -> blockNumber |> BlockNumber |> Some
        | numbers -> failwithf "Multiple applied block entries found: %A" numbers

    let getLastStoredBlockNumber dbEngineType (dbConnectionString : string) : BlockNumber option =
        let sql =
            match dbEngineType with
            | Firebird ->
                """
                SELECT FIRST 1 block_number
                FROM block
                WHERE is_applied = FALSE
                ORDER BY block_number DESC
                """
            | PostgreSQL ->
                """
                SELECT block_number
                FROM block
                WHERE is_applied = FALSE
                ORDER BY block_number DESC
                LIMIT 1
                """

        match DbTools.query<int64> dbEngineType dbConnectionString sql [] with
        | [] -> None
        | [blockNumber] -> blockNumber |> BlockNumber |> Some
        | _ -> failwith "getLastStoredBlockNumber query retrieved multiple rows."

    let getStoredBlockNumbers dbEngineType (dbConnectionString : string) : BlockNumber list =
        let sql =
            """
            SELECT block_number
            FROM block
            WHERE is_applied = FALSE
            """

        DbTools.query<int64> dbEngineType dbConnectionString sql []
        |> List.map BlockNumber

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // State
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let getChxBalanceState
        dbEngineType
        (dbConnectionString : string)
        (BlockchainAddress address)
        : ChxBalanceStateDto option
        =

        let sql =
            """
            SELECT amount, nonce
            FROM chx_balance
            WHERE blockchain_address = @address
            """

        let sqlParams =
            [
                "@address", address |> box
            ]

        match DbTools.query<ChxBalanceStateDto> dbEngineType dbConnectionString sql sqlParams with
        | [] -> None
        | [state] -> Some state
        | _ -> failwithf "Multiple CHX balance entries found for address %A" address

    let getAddressAccounts
        dbEngineType
        (dbConnectionString : string)
        (BlockchainAddress address)
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
        |> DbTools.query<string> dbEngineType dbConnectionString sql
        |> List.map AccountHash

    let getAccountState dbEngineType (dbConnectionString : string) (AccountHash accountHash) : AccountStateDto option =
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

        match DbTools.query<AccountStateDto> dbEngineType dbConnectionString sql sqlParams with
        | [] -> None
        | [accountState] -> Some accountState
        | _ -> failwithf "Multiple accounts found for account hash %A" accountHash

    let getAccountHoldings
        dbEngineType
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

        match DbTools.query<AccountHoldingDto> dbEngineType dbConnectionString sql sqlParams with
        | [] -> None
        | holdings -> Some holdings

    let getHoldingState
        dbEngineType
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

        match DbTools.query<HoldingStateDto> dbEngineType dbConnectionString sql sqlParams with
        | [] -> None
        | [holdingDetails] -> Some holdingDetails
        | _ -> failwithf "Multiple holdings of asset hash %A found for account hash %A" assetHash accountHash

    let getVoteState
        dbEngineType
        (dbConnectionString : string)
        (voteId : VoteId)
        : VoteStateDto option =
        let sql =
            """
            WITH cte_holding (holding_id)
            AS
            (
                SELECT holding_id FROM holding
                WHERE asset_hash = @assetHash
                AND account_id = (SELECT account_id FROM account WHERE account_hash = @accountHash)
            )
            SELECT vote_hash, vote_weight
            FROM vote AS v
            JOIN cte_holding AS h
            ON v.holding_id = h.holding_id
            WHERE v.resolution_hash = @resolutionHash
            """

        let sqlParams =
            [
                "@accountHash", voteId.AccountHash |> box
                "@assetHash", voteId.AssetHash |> box
                "@resolutionHash", voteId.ResolutionHash |> box
            ]

        match DbTools.query<VoteStateDto> dbEngineType dbConnectionString sql sqlParams with
        | [] -> None
        | [vote] -> Some vote
        | _ ->
            failwithf
                "Multiple votes of resolution hash %A found for account hash %A and asset hash %A"
                voteId.ResolutionHash
                voteId.AccountHash
                voteId.AssetHash

    let getAssetState dbEngineType (dbConnectionString : string) (AssetHash assetHash) : AssetStateDto option =
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

        match DbTools.query<AssetStateDto> dbEngineType dbConnectionString sql sqlParams with
        | [] -> None
        | [assetState] -> Some assetState
        | _ -> failwithf "Multiple assets found for asset hash %A" assetHash

    let getAssetHashByCode dbEngineType (dbConnectionString : string) (AssetCode assetCode) : AssetHash option =
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

        match DbTools.query<string> dbEngineType dbConnectionString sql sqlParams with
        | [] -> None
        | [assetHash] -> assetHash |> AssetHash |> Some
        | _ -> failwithf "Multiple asset hashes found for asset code %A" assetCode

    let getValidatorState
        dbEngineType
        (dbConnectionString : string)
        (BlockchainAddress validatorAddress)
        : ValidatorStateDto option
        =

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

        match DbTools.query<ValidatorStateDto> dbEngineType dbConnectionString sql sqlParams with
        | [] -> None
        | [validatorState] -> Some validatorState
        | _ -> failwithf "Multiple validators found for validator address %A" validatorAddress

    let getTopValidatorsByStake
        dbEngineType
        (dbConnectionString : string)
        (topCount : int)
        (ChxAmount threshold)
        : ValidatorSnapshotDto list
        =

        let sql =
            match dbEngineType with
            | Firebird ->
                """
                SELECT validator_address, network_address, total_stake
                FROM validator
                JOIN (
                    SELECT FIRST @topCount validator_address, sum(amount) AS total_stake
                    FROM stake
                    GROUP BY validator_address
                    HAVING sum(amount) >= @threshold
                    ORDER BY sum(amount) DESC, count(stakeholder_address) DESC, validator_address
                ) s USING (validator_address)
                ORDER BY validator_address
                """
            | PostgreSQL ->
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
        |> DbTools.query<ValidatorSnapshotDto> dbEngineType dbConnectionString sql

    let getStakeState
        dbEngineType
        (dbConnectionString : string)
        (BlockchainAddress stakeholderAddress, BlockchainAddress validatorAddress)
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

        match DbTools.query<StakeStateDto> dbEngineType dbConnectionString sql sqlParams with
        | [] -> None
        | [stakeState] -> Some stakeState
        | _ -> failwithf "Multiple stakes from address %A found for validator %A" stakeholderAddress validatorAddress

    let getTotalChxStaked
        dbEngineType
        (dbConnectionString : string)
        (BlockchainAddress stakeholderAddress)
        : ChxAmount
        =

        let sql =
            """
            SELECT sum(amount)
            FROM stake
            WHERE stakeholder_address = @stakeholderAddress
            """

        [
            "@stakeholderAddress", stakeholderAddress |> box
        ]
        |> DbTools.query<Nullable<decimal>> dbEngineType dbConnectionString sql
        |> List.tryHead
        |> Option.bind Option.ofNullable
        |? 0m
        |> ChxAmount

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Network
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let getAllPeerNodes dbEngineType (dbConnectionString : string) : NetworkAddress list =
        let sql =
            """
            SELECT network_address
            FROM peer
            """

        DbTools.query<string> dbEngineType dbConnectionString sql []
        |> List.map (fun a -> NetworkAddress a)

    let removePeerNode
        dbEngineType
        (dbConnectionString : string)
        (NetworkAddress networkAddress)
        : Result<unit, AppErrors>
        =

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
            match DbTools.execute dbEngineType dbConnectionString sql sqlParams with
            | 1 -> Ok ()
            | _ -> Result.appError "Didn't remove peer."
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to remove peer."

    let private getPeerNode
        dbEngineType
        (dbConnectionString : string)
        (NetworkAddress networkAddress)
        : NetworkAddress option
        =

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

        match DbTools.query<string> dbEngineType dbConnectionString sql sqlParams with
        | [] -> None
        | [a] -> a |> NetworkAddress |> Some
        | _ -> failwithf "Multiple entries found for address %A" networkAddress

    let private insertPeerNode
        dbEngineType
        (dbConnectionString : string)
        (NetworkAddress networkAddress)
        : Result<unit, AppErrors>
        =

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
            match DbTools.execute dbEngineType dbConnectionString sql sqlParams with
            | 1 -> Ok ()
            | _ -> Result.appError "Didn't insert peer."
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to insert peer."

    let savePeerNode
        dbEngineType
        (dbConnectionString : string)
        (NetworkAddress networkAddress)
        : Result<unit, AppErrors>
        =

        match getPeerNode dbEngineType dbConnectionString (NetworkAddress networkAddress) with
        | Some _ -> Ok ()
        | None -> insertPeerNode dbEngineType dbConnectionString (NetworkAddress networkAddress)

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

    let private updateBlock conn transaction (BlockNumber blockNumber) : Result<unit, AppErrors> =
        let sql =
            """
            UPDATE block
            SET is_applied = TRUE
            WHERE block_number = @blockNumber
            AND is_applied = FALSE
            """

        let sqlParams =
            [
                "@blockNumber", blockNumber |> box
            ]

        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 1 -> Ok ()
            | _ -> Result.appError "Didn't update applied block number."
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to update applied block number."

    let private removePreviousBlock conn transaction (BlockNumber currentBlockNumber) : Result<unit, AppErrors> =
        let sql =
            """
            DELETE FROM block
            WHERE block_number < @currentBlockNumber
            """

        let sqlParams =
            [
                "@currentBlockNumber", currentBlockNumber |> box
            ]

        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 0 when currentBlockNumber = 0L -> Ok () // Genesis block doesn't have a predecessor
            | 0 -> Result.appError "Didn't remove previous block number."
            | 1 -> Ok ()
            | c -> failwithf "Removed %i previous block numbers, instead of only one." c
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to remove previous block number."

    let private addChxBalance conn transaction (chxBalanceInfo : ChxBalanceInfoDto) : Result<unit, AppErrors> =
        let sql =
            """
            INSERT INTO chx_balance (blockchain_address, amount, nonce)
            VALUES (@blockchainAddress, @amount, @nonce)
            """

        let sqlParams =
            [
                "@blockchainAddress", chxBalanceInfo.BlockchainAddress |> box
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
            WHERE blockchain_address = @blockchainAddress
            """

        let sqlParams =
            [
                "@blockchainAddress", chxBalanceInfo.BlockchainAddress |> box
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

        let foldFn result (blockchainAddress, chxBalanceState : ChxBalanceStateDto) =
            result
            >>= fun _ ->
                {
                    BlockchainAddress = blockchainAddress
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

    let private addVote conn transaction (voteInfoDto : VoteInfoDto) : Result<unit, AppErrors> =
        let sql =
            """
            INSERT INTO vote (holding_id, resolution_hash, vote_hash, vote_weight)
            SELECT holding_id, @resolutionHash, @voteHash, NULL
            FROM holding
            WHERE asset_hash = @assetHash
            AND account_id = (SELECT account_id FROM account WHERE account_hash = @accountHash)
            """

        let sqlParams =
            [
                "@accountHash", voteInfoDto.AccountHash |> box
                "@assetHash", voteInfoDto.AssetHash |> box
                "@resolutionHash", voteInfoDto.ResolutionHash |> box
                "@voteHash", voteInfoDto.VoteState.VoteHash |> box
            ]

        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 1 -> Ok ()
            | _ -> Result.appError "Didn't insert vote state."
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to insert vote state."

    let private updateVote conn transaction (voteInfo : VoteInfoDto) : Result<unit, AppErrors> =
        let sql =
            """
            WITH cte_holding AS
            (
                SELECT h.holding_id FROM holding AS h
                JOIN account AS a USING (account_id)
                WHERE h.asset_hash = @assetHash
                AND a.account_hash = @accountHash
            )
            UPDATE vote SET vote_hash = @voteHash, vote_weight = @voteWeight
            FROM cte_holding
            WHERE vote.holding_id = cte_holding.holding_id
            AND resolution_hash = @resolutionHash
            """

        let voteWeightParamValue =
            if voteInfo.VoteState.VoteWeight.HasValue then voteInfo.VoteState.VoteWeight.Value |> box
            else DBNull.Value |> box

        let sqlParams =
            [
                "@accountHash", voteInfo.AccountHash |> box
                "@assetHash", voteInfo.AssetHash |> box
                "@resolutionHash", voteInfo.ResolutionHash |> box
                "@voteHash", voteInfo.VoteState.VoteHash |> box
                "@voteWeight", voteWeightParamValue
            ]
        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 0 -> addVote conn transaction voteInfo
            | 1 -> Ok ()
            | _ -> Result.appError "Didn't update holding state."
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to update holding state."

    let private updateVotes
        conn
        transaction
        (votes : Map<string * string * string, VoteStateDto>)
        : Result<unit, AppErrors>
        =

        let foldFn result ((accountHash, assetHash, resolutionHash), voteState : VoteStateDto) =
            result
            >>= fun _ ->
                {
                    AccountHash = accountHash
                    AssetHash = assetHash
                    ResolutionHash = resolutionHash
                    VoteState =
                        {
                            VoteHash = voteState.VoteHash
                            VoteWeight = voteState.VoteWeight
                        }
                }
                |> updateVote conn transaction

        votes
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
            INSERT INTO validator (validator_address, network_address, shared_reward_percent)
            VALUES (@validatorAddress, @networkAddress, @sharedRewardPercent)
            """

        let sqlParams =
            [
                "@validatorAddress", validatorInfo.ValidatorAddress |> box
                "@networkAddress", validatorInfo.NetworkAddress |> box
                "@sharedRewardPercent", validatorInfo.SharedRewardPercent |> box
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
            SET network_address = @networkAddress,
                shared_reward_percent = @sharedRewardPercent
            WHERE validator_address = @validatorAddress
            """

        let sqlParams =
            [
                "@validatorAddress", validatorInfo.ValidatorAddress |> box
                "@networkAddress", validatorInfo.NetworkAddress |> box
                "@sharedRewardPercent", validatorInfo.SharedRewardPercent |> box
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
                    SharedRewardPercent = state.SharedRewardPercent
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

    let persistStateChanges
        dbEngineType
        (dbConnectionString : string)
        (blockNumber : BlockNumber)
        (state : ProcessingOutputDto)
        : Result<unit, AppErrors>
        =

        use conn = DbTools.newConnection dbEngineType dbConnectionString

        conn.Open()
        use transaction = conn.BeginTransaction(Data.IsolationLevel.ReadCommitted)

        let result =
            result {
                do! removeProcessedTxs conn transaction state.TxResults
                do! updateChxBalances conn transaction state.ChxBalances
                do! updateValidators conn transaction state.Validators
                do! updateStakes conn transaction state.Stakes
                do! updateAssets conn transaction state.Assets
                do! updateAccounts conn transaction state.Accounts
                do! updateHoldings conn transaction state.Holdings
                do! updateVotes conn transaction state.Votes
                do! updateBlock conn transaction blockNumber
                do! removePreviousBlock conn transaction blockNumber
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
            Result.appError "Failed to persist state changes."
