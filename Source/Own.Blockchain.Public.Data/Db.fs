namespace Own.Blockchain.Public.Data

open System
open System.Data.Common
open Own.Common.FSharp
open Own.Blockchain.Common
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Core.Dtos

module Db =

    let private boxNullable (v : Nullable<_>) =
        if v.HasValue then
            v.Value |> box
        else
            DBNull.Value |> box

    let private createInsertParams (parameters : (string * obj) list) =
        let parameterNames =
            parameters
            |> List.map fst
            |> fun names -> String.Join (", ", names)

        let columnNames = parameterNames.Replace("@", "")

        (parameterNames, columnNames)

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // TX
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let saveTx dbEngineType dbConnectionString (txInfoDto : TxInfoDto) : Result<unit, AppErrors> =
        try
            let txParams =
                [
                    "@tx_hash", txInfoDto.TxHash |> box
                    "@sender_address", txInfoDto.SenderAddress |> box
                    "@nonce", txInfoDto.Nonce |> box
                    "@action_fee", txInfoDto.ActionFee |> box
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
        dbConnectionString
        (ChxAmount minActionFee)
        (txsToSkip : TxHash list)
        (txCountToFetch : int)
        : PendingTxInfoDto list
        =

        let skipCondition =
            if txsToSkip.IsEmpty then
                ""
            else
                txsToSkip
                |> List.map (fun h -> sprintf "'%s'" h.Value)
                |> fun vs -> String.Join(", ", vs)
                |> sprintf "AND tx_hash NOT IN (%s)"

        let sql =
            match dbEngineType with
            | Firebird ->
                sprintf
                    """
                    SELECT FIRST @txCountToFetch
                        tx_hash, sender_address, nonce, action_fee, action_count, tx_id AS appearance_order
                    FROM tx
                    WHERE action_fee >= @minActionFee
                    %s
                    ORDER BY action_fee DESC, tx_id
                    """
                    skipCondition
            | Postgres ->
                sprintf
                    """
                    SELECT tx_hash, sender_address, nonce, action_fee, action_count, tx_id AS appearance_order
                    FROM tx
                    WHERE action_fee >= @minActionFee
                    %s
                    ORDER BY action_fee DESC, tx_id
                    LIMIT @txCountToFetch
                    """
                    skipCondition

        let sqlParams =
            [
                "@minActionFee", minActionFee |> box
                "@txCountToFetch", txCountToFetch |> box
            ]

        DbTools.query dbEngineType dbConnectionString sql sqlParams

    let getAllPendingTxHashes
        dbEngineType
        dbConnectionString
        : TxHash list
        =

        let sql =
            """
            SELECT tx_hash
            FROM tx
            """

        DbTools.query<string> dbEngineType dbConnectionString sql []
        |> List.map TxHash

    let getTx dbEngineType dbConnectionString (TxHash txHash) : TxInfoDto option =
        let sql =
            """
            SELECT tx_hash, sender_address, nonce, action_fee, action_count
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
        | _ -> failwithf "Multiple TXs found for hash %A" txHash

    let getTotalFeeForPendingTxs
        dbEngineType
        dbConnectionString
        (BlockchainAddress senderAddress)
        : ChxAmount
        =

        let sql =
            """
            SELECT SUM(action_fee * action_count)
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

    let getTxPoolInfo dbEngineType dbConnectionString : GetTxPoolInfoApiDto =
        let sql =
            """
            SELECT COUNT(*) AS pending_txs
            FROM tx
            """

        match DbTools.query<GetTxPoolInfoApiDto> dbEngineType dbConnectionString sql [] with
        | [info] -> info
        | _ -> failwithf "Couldn't get TX pool info from DB"

    let txExists dbEngineType dbConnectionString (TxHash txHash) : bool =
        let sql =
            """
            SELECT 1
            FROM tx
            WHERE tx_hash = @txHash
            """

        let sqlParams =
            [
                "@txHash", txHash |> box
            ]

        match DbTools.query<int> dbEngineType dbConnectionString sql sqlParams with
        | [] -> false
        | [_] -> true
        | _ -> failwithf "Multiple TXs found for hash %A" txHash

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // EquivocationProof
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let saveEquivocationProof
        dbEngineType
        dbConnectionString
        (equivocationInfoDto : EquivocationInfoDto)
        : Result<unit, AppErrors>
        =

        try
            let equivocationProofParams =
                [
                    "@equivocation_proof_hash", equivocationInfoDto.EquivocationProofHash |> box
                    "@validator_address", equivocationInfoDto.ValidatorAddress |> box
                    "@block_number", equivocationInfoDto.BlockNumber |> box
                    "@consensus_round", equivocationInfoDto.ConsensusRound |> box
                    "@consensus_step", equivocationInfoDto.ConsensusStep |> box
                ]

            let paramData = createInsertParams equivocationProofParams

            let insertSql =
                sprintf
                    """
                    INSERT INTO equivocation (%s)
                    VALUES (%s)
                    """
                    (snd paramData)
                    (fst paramData)

            let result = DbTools.execute dbEngineType dbConnectionString insertSql equivocationProofParams

            if result < 0 then
                failwith "Unknown DB error"
            else
                Ok ()
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "DB operation error"

    let getPendingEquivocationProofs
        dbEngineType
        dbConnectionString
        (BlockNumber blockNumber)
        : EquivocationInfoDto list
        =

        let sql =
            """
            SELECT
                equivocation_proof_hash,
                validator_address,
                block_number,
                consensus_round,
                consensus_step
            FROM equivocation
            WHERE block_number <= @blockNumber
            """
        [
            "@blockNumber", blockNumber |> box
        ]
        |> DbTools.query<EquivocationInfoDto> dbEngineType dbConnectionString sql

    let getAllPendingEquivocationProofHashes
        dbEngineType
        dbConnectionString
        : EquivocationProofHash list
        =

        let sql =
            """
            SELECT equivocation_proof_hash
            FROM equivocation
            """

        DbTools.query<string> dbEngineType dbConnectionString sql []
        |> List.map EquivocationProofHash

    let getEquivocationProof
        dbEngineType
        dbConnectionString
        (EquivocationProofHash equivocationProofHash)
        : EquivocationInfoDto option
        =

        let sql =
            """
            SELECT
                equivocation_proof_hash,
                validator_address,
                block_number,
                consensus_round,
                consensus_step
            FROM equivocation
            WHERE equivocation_proof_hash = @equivocationProofHash
            """

        let sqlParams =
            [
                "@equivocationProofHash", equivocationProofHash |> box
            ]

        match DbTools.query<EquivocationInfoDto> dbEngineType dbConnectionString sql sqlParams with
        | [] -> None
        | [proof] -> Some proof
        | _ -> failwithf "Multiple equivocation proofs found for hash %A" equivocationProofHash

    let equivocationProofExists
        dbEngineType
        dbConnectionString
        (EquivocationProofHash equivocationProofHash)
        : bool
        =

        let sql =
            """
            SELECT 1
            FROM equivocation
            WHERE equivocation_proof_hash = @equivocationProofHash
            """

        let sqlParams =
            [
                "@equivocationProofHash", equivocationProofHash |> box
            ]

        match DbTools.query<int> dbEngineType dbConnectionString sql sqlParams with
        | [] -> false
        | [_] -> true
        | _ -> failwithf "Multiple EquivocationProofs found for hash %A" equivocationProofHash

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Block
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let saveBlock dbEngineType dbConnectionString (blockInfo : BlockInfoDto) : Result<unit, AppErrors> =
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
            | _ -> Result.appError "Didn't insert block"
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to insert block"

    let getLastAppliedBlockNumber dbEngineType dbConnectionString : BlockNumber option =
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

    let getLastAppliedBlockTimestamp dbEngineType dbConnectionString : Timestamp option =
        let sql =
            """
            SELECT block_timestamp
            FROM block
            WHERE is_applied = TRUE
            """

        match DbTools.query<int64> dbEngineType dbConnectionString sql [] with
        | [] -> None
        | [blockTimestamp] -> blockTimestamp |> Timestamp |> Some
        | timestamps -> failwithf "Multiple applied block entries found: %A" timestamps

    let getLastStoredBlockNumber dbEngineType dbConnectionString : BlockNumber option =
        let sql =
            match dbEngineType with
            | Firebird ->
                """
                SELECT FIRST 1 block_number
                FROM block
                WHERE is_applied = FALSE
                ORDER BY block_number DESC
                """
            | Postgres ->
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
        | _ -> failwith "getLastStoredBlockNumber query retrieved multiple rows"

    let getStoredBlockNumbers dbEngineType dbConnectionString : BlockNumber list =
        let sql =
            """
            SELECT block_number
            FROM block
            WHERE is_applied = FALSE
            """

        DbTools.query<int64> dbEngineType dbConnectionString sql []
        |> List.map BlockNumber

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Consensus
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let saveConsensusMessage
        dbEngineType
        dbConnectionString
        (consensusMessageInfoDto : ConsensusMessageInfoDto)
        : Result<unit, AppErrors>
        =

        try
            let sql =
                """
                INSERT INTO consensus_message (block_number, consensus_round, consensus_step, message_envelope)
                VALUES (@blockNumber, @consensusRound, @consensusStep, @messageEnvelope)
                """

            let result =
                [
                    "@blockNumber", consensusMessageInfoDto.BlockNumber |> box
                    "@consensusRound", consensusMessageInfoDto.ConsensusRound |> box
                    "@consensusStep", consensusMessageInfoDto.ConsensusStep |> box
                    "@messageEnvelope", consensusMessageInfoDto.MessageEnvelope |> box
                ]
                |> DbTools.execute dbEngineType dbConnectionString sql

            if result = 1 then
                Ok ()
            else
                sprintf "Didn't insert consensus message (%i): %A" result consensusMessageInfoDto
                |> Result.appError
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            sprintf "Failed to insert consensus message: %A" consensusMessageInfoDto
            |> Result.appError

    let getConsensusMessages
        dbEngineType
        dbConnectionString
        : ConsensusMessageInfoDto list
        =

        let sql =
            """
            SELECT block_number, consensus_round, consensus_step, message_envelope
            FROM consensus_message
            """
        DbTools.query<ConsensusMessageInfoDto> dbEngineType dbConnectionString sql []

    let saveConsensusState
        dbEngineType
        dbConnectionString
        (consensusStateInfoDto : ConsensusStateInfoDto)
        : Result<unit, AppErrors>
        =

        try
            let sql =
                match dbEngineType with
                | Firebird ->
                    """
                    UPDATE OR INSERT INTO consensus_state (
                        consensus_state_id,
                        block_number,
                        consensus_round,
                        consensus_step,
                        locked_block,
                        locked_round,
                        valid_block,
                        valid_round,
                        valid_block_signatures
                    )
                    VALUES (
                        0,
                        @blockNumber,
                        @consensusRound,
                        @consensusStep,
                        @lockedBlock,
                        @lockedRound,
                        @validBlock,
                        @validRound,
                        @validBlockSignatures
                    )
                    """
                | Postgres ->
                    """
                    INSERT INTO consensus_state (
                        consensus_state_id,
                        block_number,
                        consensus_round,
                        consensus_step,
                        locked_block,
                        locked_round,
                        valid_block,
                        valid_round,
                        valid_block_signatures
                    )
                    VALUES (
                        0,
                        @blockNumber,
                        @consensusRound,
                        @consensusStep,
                        @lockedBlock,
                        @lockedRound,
                        @validBlock,
                        @validRound,
                        @validBlockSignatures
                    )
                    ON CONFLICT (consensus_state_id) DO UPDATE
                    SET block_number = @blockNumber,
                        consensus_round = @consensusRound,
                        consensus_step = @consensusStep,
                        locked_block = @lockedBlock,
                        locked_round = @lockedRound,
                        valid_block = @validBlock,
                        valid_round = @validRound,
                        valid_block_signatures = @validBlockSignatures
                    """

            let lockedBlockParamValue =
                if consensusStateInfoDto.LockedBlock.IsNullOrWhiteSpace() then
                    DBNull.Value |> box
                else
                    consensusStateInfoDto.LockedBlock |> box

            let validBlockParamValue =
                if consensusStateInfoDto.ValidBlock.IsNullOrWhiteSpace() then
                    DBNull.Value |> box
                else
                    consensusStateInfoDto.ValidBlock |> box

            let validBlockSignaturesParamValue =
                if consensusStateInfoDto.ValidBlockSignatures.IsNullOrWhiteSpace() then
                    DBNull.Value |> box
                else
                    consensusStateInfoDto.ValidBlockSignatures |> box

            let result =
                [
                    "@blockNumber", consensusStateInfoDto.BlockNumber |> box
                    "@consensusRound", consensusStateInfoDto.ConsensusRound |> box
                    "@consensusStep", consensusStateInfoDto.ConsensusStep |> box
                    "@lockedBlock", lockedBlockParamValue
                    "@lockedRound", consensusStateInfoDto.LockedRound |> box
                    "@validBlock", validBlockParamValue
                    "@validRound", consensusStateInfoDto.ValidRound |> box
                    "@validBlockSignatures", validBlockSignaturesParamValue
                ]
                |> DbTools.execute dbEngineType dbConnectionString sql

            if result = 1 then
                Ok ()
            else
                sprintf "Didn't insert consensus state (%i): %A" result consensusStateInfoDto
                |> Result.appError
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            sprintf "Failed to insert consensus state: %A" consensusStateInfoDto
            |> Result.appError

    let getConsensusState
        dbEngineType
        dbConnectionString
        : ConsensusStateInfoDto option
        =

        let sql =
            """
            SELECT
                block_number,
                consensus_round,
                consensus_step,
                locked_block,
                locked_round,
                valid_block,
                valid_round,
                valid_block_signatures
            FROM consensus_state
            """
        match DbTools.query<ConsensusStateInfoDto> dbEngineType dbConnectionString sql [] with
        | [] -> None
        | [state] -> Some state
        | _ -> failwith "Multiple consensus state entries found"

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Trading
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let getTradingPairs
        dbEngineType
        (dbConnectionString : string)
        : TradingPairApiDto list
        =

        let sql =
            """
            SELECT
                ba.asset_hash AS base_asset_hash,
                ba.asset_code AS base_asset_code,
                qa.asset_hash AS quote_asset_hash,
                qa.asset_code AS quote_asset_code,
                is_enabled,
                last_price,
                price_change
            FROM trading_pair
            JOIN asset AS ba ON ba.asset_id = base_asset_id
            JOIN asset AS qa ON qa.asset_id = quote_asset_id
            """

        DbTools.query<TradingPairApiDto> dbEngineType dbConnectionString sql []

    let getTradingPairControllers
        dbEngineType
        (dbConnectionString : string)
        : BlockchainAddress list
        =

        // TODO DSX: Implement governance
        []

    let getTradingPairState
        dbEngineType
        (dbConnectionString : string)
        (AssetHash baseAssetHash, AssetHash quoteAssetHash)
        : TradingPairStateDto option
        =

        let sql =
            """
            SELECT
                is_enabled,
                last_price,
                price_change
            FROM trading_pair
            JOIN asset AS ba ON ba.asset_id = base_asset_id
            JOIN asset AS qa ON qa.asset_id = quote_asset_id
            WHERE ba.asset_hash = @baseAssetHash
            AND qa.asset_hash = @quoteAssetHash
            """

        let sqlParams =
            [
                "@baseAssetHash", baseAssetHash |> box
                "@quoteAssetHash", quoteAssetHash |> box
            ]

        match DbTools.query<TradingPairStateDto> dbEngineType dbConnectionString sql sqlParams with
        | [] -> None
        | [tradingPairState] -> Some tradingPairState
        | _ -> failwithf "Multiple trading pairs found for asset pair %s / %s" baseAssetHash quoteAssetHash

    let getTradeOrderState
        dbEngineType
        (dbConnectionString : string)
        (TradeOrderHash tradeOrderHash)
        : TradeOrderStateDto option
        =

        let sql =
            """
            SELECT
                block_timestamp,
                block_number,
                tx_position,
                action_number,
                acc.account_hash,
                ba.asset_hash AS base_asset_hash,
                qa.asset_hash AS quote_asset_hash,
                side,
                amount,
                order_type,
                limit_price,
                stop_price,
                trailing_offset,
                trailing_offset_is_percentage,
                time_in_force,
                expiration_timestamp,
                is_executable,
                amount_filled
            FROM trade_order
            JOIN account AS acc USING (account_id)
            JOIN asset AS ba ON ba.asset_id = base_asset_id
            JOIN asset AS qa ON qa.asset_id = quote_asset_id
            WHERE trade_order_hash = @tradeOrderHash
            """

        let sqlParams =
            [
                "@tradeOrderHash", tradeOrderHash |> box
            ]

        match DbTools.query<TradeOrderStateDto> dbEngineType dbConnectionString sql sqlParams with
        | [] -> None
        | [tradeOrderState] -> Some tradeOrderState
        | _ -> failwithf "Multiple trade orders found for hash %A" tradeOrderHash

    let getTradeOrders
        dbEngineType
        (dbConnectionString : string)
        (AssetHash baseAssetHash, AssetHash quoteAssetHash)
        : TradeOrderInfoDto list
        =

        let sql =
            """
            SELECT
                trade_order_hash,
                block_timestamp,
                block_number,
                tx_position,
                action_number,
                acc.account_hash,
                @baseAssetHash AS base_asset_hash,
                @quoteAssetHash AS quote_asset_hash,
                side,
                amount,
                order_type,
                limit_price,
                stop_price,
                trailing_offset,
                trailing_offset_is_percentage,
                time_in_force,
                expiration_timestamp,
                is_executable,
                amount_filled
            FROM trade_order
            JOIN account AS acc USING (account_id)
            JOIN asset AS ba ON ba.asset_id = base_asset_id
            JOIN asset AS qa ON qa.asset_id = quote_asset_id
            WHERE ba.asset_hash = @baseAssetHash
            AND qa.asset_hash = @quoteAssetHash
            """

        [
            "@baseAssetHash", baseAssetHash |> box
            "@quoteAssetHash", quoteAssetHash |> box
        ]
        |> DbTools.query<TradeOrderInfoDto> dbEngineType dbConnectionString sql

    let getExpiredTradeOrders
        dbEngineType
        (dbConnectionString : string)
        (Timestamp blockTimestamp)
        : TradeOrderInfoDto list
        =

        let sql =
            """
            SELECT
                trade_order_hash,
                block_timestamp,
                block_number,
                tx_position,
                action_number,
                acc.account_hash,
                ba.asset_hash AS base_asset_hash,
                qa.asset_hash AS quote_asset_hash,
                side,
                amount,
                order_type,
                limit_price,
                stop_price,
                trailing_offset,
                trailing_offset_is_percentage,
                time_in_force,
                expiration_timestamp,
                is_executable,
                amount_filled
            FROM trade_order
            JOIN account AS acc USING (account_id)
            JOIN asset AS ba ON ba.asset_id = base_asset_id
            JOIN asset AS qa ON qa.asset_id = quote_asset_id
            WHERE expiration_timestamp <= @blockTimestamp
            """

        [
            "@blockTimestamp", blockTimestamp |> box
        ]
        |> DbTools.query<TradeOrderInfoDto> dbEngineType dbConnectionString sql

    let getExecutableTradeOrders
        dbEngineType
        (dbConnectionString : string)
        (AssetHash baseAssetHash, AssetHash quoteAssetHash)
        : TradeOrderInfoDto list
        =

        let sql =
            """
            SELECT
                trade_order_hash,
                block_timestamp,
                block_number,
                tx_position,
                action_number,
                acc.account_hash,
                @baseAssetHash AS base_asset_hash,
                @quoteAssetHash AS quote_asset_hash,
                side,
                amount,
                order_type,
                limit_price,
                stop_price,
                trailing_offset,
                trailing_offset_is_percentage,
                time_in_force,
                expiration_timestamp,
                is_executable,
                amount_filled
            FROM trade_order
            JOIN account AS acc USING (account_id)
            JOIN asset AS ba ON ba.asset_id = base_asset_id
            JOIN asset AS qa ON qa.asset_id = quote_asset_id
            WHERE ba.asset_hash = @baseAssetHash
            AND qa.asset_hash = @quoteAssetHash
            AND is_executable = TRUE
            """

        [
            "@baseAssetHash", baseAssetHash |> box
            "@quoteAssetHash", quoteAssetHash |> box
        ]
        |> DbTools.query<TradeOrderInfoDto> dbEngineType dbConnectionString sql

    let getAccountTradeOrders
        dbEngineType
        (dbConnectionString : string)
        (AccountHash accountHash)
        : TradeOrderInfoDto list
        =

        let sql =
            """
            SELECT
                block_timestamp,
                block_number,
                tx_position,
                action_number,
                acc.account_hash,
                ba.asset_hash AS base_asset_hash,
                qa.asset_hash AS quote_asset_hash,
                side,
                amount,
                order_type,
                limit_price,
                stop_price,
                trailing_offset,
                trailing_offset_is_percentage,
                time_in_force,
                expiration_timestamp,
                is_executable,
                amount_filled
            FROM trade_order
            JOIN account AS acc USING (account_id)
            JOIN asset AS ba ON ba.asset_id = base_asset_id
            JOIN asset AS qa ON qa.asset_id = quote_asset_id
            WHERE account_hash = @accountHash
            """

        [
            "@accountHash", accountHash |> box
        ]
        |> DbTools.query<TradeOrderInfoDto> dbEngineType dbConnectionString sql

    /// Returns orders that are acquiring the specified asset, which could be potentially ineligible.
    let getIneligibleTradeOrders
        dbEngineType
        (dbConnectionString : string)
        (accountHash : AccountHash option, AssetHash assetHash)
        : TradeOrderInfoDto list
        =

        let accountHashCondition, accountHashParamValue =
            match accountHash with
            | Some h -> "AND account_hash = @accountHash", h.Value |> Some
            | None -> "", None

        let sql =
            sprintf
                """
                SELECT
                    block_timestamp,
                    block_number,
                    tx_position,
                    action_number,
                    acc.account_hash,
                    ba.asset_hash AS base_asset_hash,
                    qa.asset_hash AS quote_asset_hash,
                    side,
                    amount,
                    order_type,
                    limit_price,
                    stop_price,
                    trailing_offset,
                    trailing_offset_is_percentage,
                    time_in_force,
                    expiration_timestamp,
                    is_executable,
                    amount_filled
                FROM trade_order
                JOIN account AS acc USING (account_id)
                JOIN asset AS ba ON ba.asset_id = base_asset_id
                JOIN asset AS qa ON qa.asset_id = quote_asset_id
                WHERE (
                    side = 1 AND ba.asset_hash = @assetHash -- Acquiring base asset through BUY order
                    OR side = 2 AND qa.asset_hash = @assetHash -- Acquiring quote asset through SELL order
                )
                %s
                """
                accountHashCondition

        [
            accountHashParamValue |> Option.map (fun v -> "@accountHash", v |> box)
            Some ("@assetHash", assetHash |> box)
        ]
        |> List.choose id
        |> DbTools.query<TradeOrderInfoDto> dbEngineType dbConnectionString sql

    let getOpenTradeOrderHashes
        dbEngineType
        (dbConnectionString : string)
        : TradeOrderHash list
        =

        let sql =
            """
            SELECT trade_order_hash
            FROM trade_order
            """

        DbTools.query<string> dbEngineType dbConnectionString sql []
        |> List.map TradeOrderHash

    let getHoldingInTradeOrders
        dbEngineType
        (dbConnectionString : string)
        (AccountHash accountHash, AssetHash assetHash)
        : AssetAmount
        =

        let sql =
            """
            SELECT
                SUM(amount - amount_filled)
            FROM trade_order
            JOIN account AS acc USING (account_id)
            JOIN asset AS ba ON ba.asset_id = base_asset_id
            WHERE acc.account_hash = @accountHash
            AND ba.asset_hash = @assetHash
            AND side = 2 -- SELL

            UNION

            SELECT
                SUM((amount - amount_filled) * limit_price)
            FROM trade_order
            JOIN account AS acc USING (account_id)
            JOIN asset AS qa ON qa.asset_id = quote_asset_id
            WHERE acc.account_hash = @accountHash
            AND qa.asset_hash = @assetHash
            AND side = 1 -- BUY
            """

        [
            "@accountHash", accountHash |> box
            "@assetHash", assetHash |> box
        ]
        |> DbTools.query<Nullable<decimal>> dbEngineType dbConnectionString sql
        |> List.choose Option.ofNullable
        |> List.sum
        |> AssetAmount

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // State
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let getChxAddressState
        dbEngineType
        dbConnectionString
        (BlockchainAddress address)
        : ChxAddressStateDto option
        =

        let sql =
            """
            SELECT nonce, balance
            FROM chx_address
            WHERE blockchain_address = @address
            """

        let sqlParams =
            [
                "@address", address |> box
            ]

        match DbTools.query<ChxAddressStateDto> dbEngineType dbConnectionString sql sqlParams with
        | [] -> None
        | [state] -> Some state
        | _ -> failwithf "Multiple CHX address entries found for address %A" address

    let getAddressAccounts
        dbEngineType
        dbConnectionString
        (BlockchainAddress address)
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

    let getAddressAssets
        dbEngineType
        dbConnectionString
        (BlockchainAddress address)
        =

        let sql =
            """
            SELECT asset_hash
            FROM asset
            WHERE controller_address = @controllerAddress
            ORDER BY asset_id
            """
        [
            "@controllerAddress", address |> box
        ]
        |> DbTools.query<string> dbEngineType dbConnectionString sql
        |> List.map AssetHash

    let getAddressStakes
        dbEngineType
        dbConnectionString
        (BlockchainAddress address)
        =

        let sql =
            """
            SELECT validator_address, amount
            FROM stake
            WHERE staker_address = @stakerAddress
            ORDER BY amount DESC, validator_address ASC
            """
        [
            "@stakerAddress", address |> box
        ]
        |> DbTools.query<AddressStakeInfoDto> dbEngineType dbConnectionString sql

    let getValidatorStakes
        dbEngineType
        dbConnectionString
        (BlockchainAddress address)
        =

        let sql =
            """
            SELECT staker_address, amount
            FROM stake
            WHERE validator_address = @validatorAddress
            ORDER BY amount DESC, staker_address ASC
            """
        [
            "@validatorAddress", address |> box
        ]
        |> DbTools.query<ValidatorStakeInfoDto> dbEngineType dbConnectionString sql

    let getAccountState dbEngineType dbConnectionString (AccountHash accountHash) : AccountStateDto option =
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
        dbConnectionString
        (AccountHash accountHash)
        (assetHash : AssetHash option)
        : AccountHoldingDto list
        =

        let filter =
            if assetHash.IsNone then
                ""
            else
                "AND asset_hash = @assetHash"

        let sql =
            sprintf
                """
                SELECT asset_hash, balance
                FROM holding
                JOIN account USING (account_id)
                JOIN asset USING (asset_id)
                WHERE account_hash = @accountHash
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

        DbTools.query<AccountHoldingDto> dbEngineType dbConnectionString sql sqlParams

    let getAccountVotes
        dbEngineType
        dbConnectionString
        (AccountHash accountHash)
        (assetHash : AssetHash option)
        : AccountVoteDto list
        =

        let filter =
            if assetHash.IsNone then
                ""
            else
                "AND asset_hash = @assetHash"

        let sql =
            sprintf
                """
                SELECT asset_hash, resolution_hash, vote_hash, vote_weight
                FROM vote
                JOIN holding USING (holding_id)
                JOIN account USING (account_id)
                JOIN asset USING (asset_id)
                WHERE account_hash = @accountHash
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

        DbTools.query<AccountVoteDto> dbEngineType dbConnectionString sql sqlParams

    let getAccountEligibilities
        dbEngineType
        dbConnectionString
        (AccountHash accountHash)
        : AccountEligibilityInfoDto list
        =

        let sql =
            """
            SELECT asset_hash, is_primary_eligible, is_secondary_eligible, kyc_controller_address
            FROM eligibility
            JOIN account USING (account_id)
            JOIN asset USING (asset_id)
            WHERE account_hash = @accountHash
            """

        let sqlParams =
            [
                "@accountHash", accountHash |> box
            ]

        DbTools.query<AccountEligibilityInfoDto> dbEngineType dbConnectionString sql sqlParams

    let getHoldingState
        dbEngineType
        dbConnectionString
        (AccountHash accountHash, AssetHash assetHash)
        : HoldingStateDto option
        =

        let sql =
            """
            SELECT balance, is_emission
            FROM holding
            JOIN account USING (account_id)
            JOIN asset USING (asset_id)
            WHERE account_hash = @accountHash
            AND asset_hash = @assetHash
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
        dbConnectionString
        (voteId : VoteId)
        : VoteStateDto option
        =

        let sql =
            """
            SELECT vote_hash, vote_weight
            FROM vote
            JOIN holding USING (holding_id)
            JOIN account USING (account_id)
            JOIN asset USING (asset_id)
            WHERE asset_hash = @assetHash
            AND account_hash = @accountHash
            AND resolution_hash = @resolutionHash
            """

        let sqlParams =
            [
                "@accountHash", voteId.AccountHash.Value |> box
                "@assetHash", voteId.AssetHash.Value |> box
                "@resolutionHash", voteId.ResolutionHash.Value |> box
            ]

        match DbTools.query<VoteStateDto> dbEngineType dbConnectionString sql sqlParams with
        | [] -> None
        | [vote] -> Some vote
        | _ ->
            failwithf
                "Multiple votes of resolution hash %s found for account hash %s and asset hash %s"
                voteId.ResolutionHash.Value
                voteId.AccountHash.Value
                voteId.AssetHash.Value

    let getEligibilityState
        dbEngineType
        dbConnectionString
        (AccountHash accountHash, AssetHash assetHash)
        : EligibilityStateDto option
        =

        let sql =
            """
            SELECT is_primary_eligible, is_secondary_eligible, kyc_controller_address
            FROM eligibility
            JOIN account USING (account_id)
            JOIN asset USING (asset_id)
            WHERE account_hash = @accountHash
            AND asset_hash = @assetHash
            """

        let sqlParams =
            [
                "@accountHash", accountHash |> box
                "@assetHash", assetHash |> box
            ]

        match DbTools.query<EligibilityStateDto> dbEngineType dbConnectionString sql sqlParams with
        | [] -> None
        | [eligibility] -> Some eligibility
        | _ ->
            failwithf
                "Multiple eligibility entries found for account hash %s and asset hash %s"
                accountHash
                assetHash

    let getAssetKycProviders
        dbEngineType
        dbConnectionString
        (AssetHash assetHash)
        : BlockchainAddress list
        =

        let sql =
            """
            SELECT provider_address
            FROM kyc_provider
            JOIN asset USING (asset_id)
            WHERE asset_hash = @assetHash
            """

        [
            "@assetHash", assetHash |> box
        ]
        |> DbTools.query<string> dbEngineType dbConnectionString sql
        |> List.map BlockchainAddress

    let getAccountKycProviders
        dbEngineType
        dbConnectionString
        (AccountHash accountHash)
        : BlockchainAddress list
        =

        let sql =
            """
            SELECT provider_address
            FROM kyc_provider
            WHERE provider_address = (SELECT controller_address FROM account WHERE account_hash = @accountHash)
            """

        [
            "@accountHash", accountHash |> box
        ]
        |> DbTools.query<string> dbEngineType dbConnectionString sql
        |> List.map BlockchainAddress

    let getAssetState dbEngineType dbConnectionString (AssetHash assetHash) : AssetStateDto option =
        let sql =
            """
            SELECT asset_code, controller_address, is_eligibility_required
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

    let getAssetHashByCode dbEngineType dbConnectionString (AssetCode assetCode) : AssetHash option =
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

    let getAllValidators
        dbEngineType
        dbConnectionString
        : GetValidatorInfoApiDto list
        =

        let sql =
            """
            SELECT
                validator_address,
                network_address,
                shared_reward_percent,
                (time_to_lock_deposit > 0 OR time_to_blacklist > 0) AS is_deposit_locked,
                (time_to_blacklist > 0) AS is_blacklisted
            FROM validator
            ORDER by validator_address
            """

        DbTools.query<GetValidatorInfoApiDto> dbEngineType dbConnectionString sql []

    let getValidatorState
        dbEngineType
        dbConnectionString
        (BlockchainAddress validatorAddress)
        : ValidatorStateDto option
        =

        let sql =
            """
            SELECT network_address, shared_reward_percent, time_to_lock_deposit, time_to_blacklist, is_enabled
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
        dbConnectionString
        (topCount : int)
        (ChxAmount threshold)
        (ChxAmount deposit)
        : ValidatorSnapshotDto list
        =

        let sql =
            match dbEngineType with
            | Firebird ->
                """
                SELECT FIRST @topCount validator_address, network_address, shared_reward_percent, total_stake
                FROM validator
                JOIN (
                    SELECT validator_address, SUM(amount) AS total_stake, COUNT(staker_address) AS staker_count
                    FROM stake
                    GROUP BY validator_address
                    HAVING SUM(amount) >= @threshold
                ) s USING (validator_address)
                JOIN chx_address ON
                    chx_address.blockchain_address = validator.validator_address
                LEFT JOIN (
                    SELECT staker_address, SUM(amount) AS total_delegation
                    FROM stake
                    GROUP BY staker_address
                ) d ON
                    d.staker_address = validator.validator_address
                WHERE time_to_blacklist = 0
                AND is_enabled
                AND (chx_address.balance - COALESCE(d.total_delegation, 0)) >= @deposit
                ORDER BY total_stake DESC, staker_count DESC, validator_address
                """
            | Postgres ->
                """
                SELECT validator_address, network_address, shared_reward_percent, total_stake
                FROM validator
                JOIN (
                    SELECT validator_address, SUM(amount) AS total_stake, COUNT(staker_address) AS staker_count
                    FROM stake
                    GROUP BY validator_address
                    HAVING SUM(amount) >= @threshold
                ) s USING (validator_address)
                JOIN chx_address ON
                    chx_address.blockchain_address = validator.validator_address
                LEFT JOIN (
                    SELECT staker_address, SUM(amount) AS total_delegation
                    FROM stake
                    GROUP BY staker_address
                ) d ON
                    d.staker_address = validator.validator_address
                WHERE time_to_blacklist = 0
                AND is_enabled
                AND (chx_address.balance - COALESCE(d.total_delegation, 0)) >= @deposit
                ORDER BY total_stake DESC, staker_count DESC, validator_address
                LIMIT @topCount
                """

        [
            "@topCount", topCount |> box
            "@threshold", threshold |> box
            "@deposit", deposit |> box
        ]
        |> DbTools.query<ValidatorSnapshotDto> dbEngineType dbConnectionString sql

    let getBlacklistedValidators
        dbEngineType
        dbConnectionString
        : BlockchainAddress list
        =

        let sql =
            """
            SELECT validator_address
            FROM validator
            WHERE time_to_blacklist > 0
            """

        DbTools.query<string> dbEngineType dbConnectionString sql []
        |> List.map BlockchainAddress

    let getLockedAndBlacklistedValidators
        dbEngineType
        dbConnectionString
        : BlockchainAddress list
        =

        let sql =
            """
            SELECT validator_address
            FROM validator
            WHERE time_to_lock_deposit > 0
            OR time_to_blacklist > 0
            """

        DbTools.query<string> dbEngineType dbConnectionString sql []
        |> List.map BlockchainAddress

    let getTopStakersByStake
        dbEngineType
        dbConnectionString
        (topCount : int)
        (BlockchainAddress validatorAddress)
        : StakerInfoDto list
        =

        let sql =
            match dbEngineType with
            | Firebird ->
                """
                SELECT FIRST @topCount staker_address, amount
                FROM stake
                WHERE validator_address = @validatorAddress
                AND amount > 0
                ORDER BY amount DESC, staker_address
                """
            | Postgres ->
                """
                SELECT staker_address, amount
                FROM stake
                WHERE validator_address = @validatorAddress
                AND amount > 0
                ORDER BY amount DESC, staker_address
                LIMIT @topCount
                """

        [
            "@topCount", topCount |> box
            "@validatorAddress", validatorAddress |> box
        ]
        |> DbTools.query<StakerInfoDto> dbEngineType dbConnectionString sql

    let getStakeState
        dbEngineType
        dbConnectionString
        (BlockchainAddress stakerAddress, BlockchainAddress validatorAddress)
        : StakeStateDto option
        =

        let sql =
            """
            SELECT amount
            FROM stake
            WHERE staker_address = @stakerAddress
            AND validator_address = @validatorAddress
            """

        let sqlParams =
            [
                "@stakerAddress", stakerAddress |> box
                "@validatorAddress", validatorAddress |> box
            ]

        match DbTools.query<StakeStateDto> dbEngineType dbConnectionString sql sqlParams with
        | [] -> None
        | [stakeState] -> Some stakeState
        | _ -> failwithf "Multiple stakes from address %A found for validator %A" stakerAddress validatorAddress

    let getStakers
        dbEngineType
        dbConnectionString
        (BlockchainAddress validatorAddress)
        : BlockchainAddress list
        =

        let sql =
            """
            SELECT staker_address
            FROM stake
            WHERE validator_address = @validatorAddress
            """

        [
            "@validatorAddress", validatorAddress |> box
        ]
        |> DbTools.query<string> dbEngineType dbConnectionString sql
        |> List.map BlockchainAddress

    let getTotalChxStaked
        dbEngineType
        dbConnectionString
        (BlockchainAddress stakerAddress)
        : ChxAmount
        =

        let sql =
            """
            SELECT SUM(amount)
            FROM stake
            WHERE staker_address = @stakerAddress
            """

        [
            "@stakerAddress", stakerAddress |> box
        ]
        |> DbTools.query<Nullable<decimal>> dbEngineType dbConnectionString sql
        |> List.tryHead
        |> Option.bind Option.ofNullable
        |? 0m
        |> ChxAmount

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

    let private removeProcessedEquivocationProof
        conn
        transaction
        (equivocationProofHash : string)
        : Result<unit, AppErrors>
        =

        let sql =
            """
            DELETE FROM equivocation
            WHERE equivocation_proof_hash = @equivocationProofHash
            """

        let sqlParams =
            [
                "@equivocationProofHash", equivocationProofHash |> box
            ]

        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 1 -> Ok ()
            | _ ->
                sprintf "Didn't remove processed equivocation proof from the pool: %s" equivocationProofHash
                |> Result.appError
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            sprintf "Failed to remove processed equivocation proof from the pool: %s" equivocationProofHash
            |> Result.appError

    let private removeProcessedEquivocationProofs
        conn
        transaction
        (equivocationProofResults : Map<string, EquivocationProofResultDto>) : Result<unit, AppErrors>
        =

        let foldFn result (equivocationProofHash, equivocationProofResult : EquivocationProofResultDto) =
            result
            >>= fun _ -> removeProcessedEquivocationProof conn transaction equivocationProofHash

        equivocationProofResults
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
            | _ -> Result.appError "Didn't update applied block number"
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to update applied block number"

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
            | 0 -> Result.appError "Didn't remove previous block number"
            | 1 -> Ok ()
            | c -> failwithf "Removed %i previous block numbers instead of only one" c
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to remove previous block number"

    let private removeOldConsensusMessages conn transaction (BlockNumber currentBlockNumber) =
        let sql =
            """
            DELETE FROM consensus_message
            WHERE block_number <= @currentBlockNumber
            """

        let sqlParams =
            [
                "@currentBlockNumber", currentBlockNumber |> box
            ]

        try
            if DbTools.executeWithinTransaction conn transaction sql sqlParams < 0 then
                Result.appError "Error removing old consensus messages"
            else
                Ok ()
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to remove old consensus messages"

    let private removeConsensusState conn transaction =
        let sql =
            """
            DELETE FROM consensus_state
            """

        try
            if DbTools.executeWithinTransaction conn transaction sql [] < 0 then
                Result.appError "Error removing persisted consensus state"
            else
                Ok ()
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to remove persisted consensus state"

    let private addChxAddress conn transaction (chxAddressInfo : ChxAddressInfoDto) : Result<unit, AppErrors> =
        let sql =
            """
            INSERT INTO chx_address (blockchain_address, nonce, balance)
            VALUES (@blockchainAddress, @nonce, @balance)
            """

        let sqlParams =
            [
                "@blockchainAddress", chxAddressInfo.BlockchainAddress |> box
                "@nonce", chxAddressInfo.ChxAddressState.Nonce |> box
                "@balance", chxAddressInfo.ChxAddressState.Balance |> box
            ]

        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 1 -> Ok ()
            | _ -> Result.appError "Didn't insert CHX address state"
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to insert CHX address state"

    let private updateChxAddress conn transaction (chxAddressInfo : ChxAddressInfoDto) : Result<unit, AppErrors> =
        let sql =
            """
            UPDATE chx_address
            SET nonce = @nonce,
                balance = @balance
            WHERE blockchain_address = @blockchainAddress
            """

        let sqlParams =
            [
                "@blockchainAddress", chxAddressInfo.BlockchainAddress |> box
                "@nonce", chxAddressInfo.ChxAddressState.Nonce |> box
                "@balance", chxAddressInfo.ChxAddressState.Balance |> box
            ]

        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 0 -> addChxAddress conn transaction chxAddressInfo
            | 1 -> Ok ()
            | _ -> Result.appError "Didn't update CHX address state"
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to update CHX address state"

    let private updateChxAddresses
        conn
        transaction
        (chxAddresses : Map<string, ChxAddressStateDto>)
        : Result<unit, AppErrors>
        =

        let foldFn result (blockchainAddress, chxAddressState : ChxAddressStateDto) =
            result
            >>= fun _ ->
                {
                    BlockchainAddress = blockchainAddress
                    ChxAddressState =
                        {
                            Nonce = chxAddressState.Nonce
                            Balance = chxAddressState.Balance
                        }
                }
                |> updateChxAddress conn transaction

        chxAddresses
        |> Map.toList
        |> List.fold foldFn (Ok ())

    let private addHolding conn transaction (holdingInfo : HoldingInfoDto) : Result<unit, AppErrors> =
        let sql =
            """
            INSERT INTO holding (account_id, asset_id, balance, is_emission)
            SELECT account_id, asset_id, @balance, @isEmission
            FROM account, asset
            WHERE account_hash = @accountHash
            AND asset_hash = @assetHash
            """

        let sqlParams =
            [
                "@accountHash", holdingInfo.AccountHash |> box
                "@assetHash", holdingInfo.AssetHash |> box
                "@balance", holdingInfo.HoldingState.Balance |> box
                "@isEmission", holdingInfo.HoldingState.IsEmission |> box
            ]

        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 1 -> Ok ()
            | _ -> Result.appError "Didn't insert holding state"
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to insert holding state"

    let private updateHolding conn transaction (holdingInfo : HoldingInfoDto) : Result<unit, AppErrors> =
        let sql =
            """
            UPDATE holding
            SET balance = @balance,
                is_emission = @isEmission
            WHERE account_id = (SELECT account_id FROM account WHERE account_hash = @accountHash)
            AND asset_id = (SELECT asset_id FROM asset WHERE asset_hash = @assetHash)
            """

        let sqlParams =
            [
                "@accountHash", holdingInfo.AccountHash |> box
                "@assetHash", holdingInfo.AssetHash |> box
                "@balance", holdingInfo.HoldingState.Balance |> box
                "@isEmission", holdingInfo.HoldingState.IsEmission |> box
            ]

        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 0 -> addHolding conn transaction holdingInfo
            | 1 -> Ok ()
            | _ -> Result.appError "Didn't update holding state"
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to update holding state"

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
                            Balance = holdingState.Balance
                            IsEmission = holdingState.IsEmission
                        }
                }
                |> updateHolding conn transaction

        holdings
        |> Map.toList
        |> List.fold foldFn (Ok ())

    let private addVote conn transaction (voteInfo : VoteInfoDto) : Result<unit, AppErrors> =
        let sql =
            """
            INSERT INTO vote (holding_id, resolution_hash, vote_hash, vote_weight)
            SELECT holding_id, @resolutionHash, @voteHash, @voteWeight
            FROM holding
            JOIN account USING (account_id)
            JOIN asset USING (asset_id)
            WHERE asset_hash = @assetHash
            AND account_hash = @accountHash
            """

        let sqlParams =
            [
                "@accountHash", voteInfo.AccountHash |> box
                "@assetHash", voteInfo.AssetHash |> box
                "@resolutionHash", voteInfo.ResolutionHash |> box
                "@voteHash", voteInfo.VoteState.VoteHash |> box
                "@voteWeight", voteInfo.VoteState.VoteWeight |> boxNullable
            ]

        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 1 -> Ok ()
            | _ -> Result.appError "Didn't insert vote state"
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to insert vote state"

    let private updateVote conn transaction (voteInfo : VoteInfoDto) : Result<unit, AppErrors> =
        let sql =
            """
            UPDATE vote
            SET vote_hash = @voteHash,
                vote_weight = @voteWeight
            WHERE holding_id = (
                SELECT holding_id
                FROM holding
                JOIN account USING (account_id)
                JOIN asset USING (asset_id)
                WHERE asset_hash = @assetHash
                AND account_hash = @accountHash
            )
            AND resolution_hash = @resolutionHash
            """

        let sqlParams =
            [
                "@accountHash", voteInfo.AccountHash |> box
                "@assetHash", voteInfo.AssetHash |> box
                "@resolutionHash", voteInfo.ResolutionHash |> box
                "@voteHash", voteInfo.VoteState.VoteHash |> box
                "@voteWeight", voteInfo.VoteState.VoteWeight |> boxNullable
            ]
        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 0 -> addVote conn transaction voteInfo
            | 1 -> Ok ()
            | _ -> Result.appError "Didn't update vote state"
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to update vote state"

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

    let private addEligibility conn transaction (eligibilityInfo : EligibilityInfoDto) : Result<unit, AppErrors> =
        let sql =
            """
            INSERT INTO eligibility (
                account_id, asset_id, is_primary_eligible, is_secondary_eligible, kyc_controller_address
            )
            SELECT account_id, asset_id, @isPrimaryEligible, @isSecondaryEligible, @kycControllerAddress
            FROM account, asset
            WHERE account_hash = @accountHash
            AND asset_hash = @assetHash
            """

        let sqlParams =
            [
                "@accountHash", eligibilityInfo.AccountHash |> box
                "@assetHash", eligibilityInfo.AssetHash |> box
                "@isPrimaryEligible", eligibilityInfo.EligibilityState.IsPrimaryEligible |> box
                "@isSecondaryEligible", eligibilityInfo.EligibilityState.IsSecondaryEligible |> box
                "@kycControllerAddress", eligibilityInfo.EligibilityState.KycControllerAddress |> box
            ]

        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 1 -> Ok ()
            | _ -> Result.appError "Didn't insert eligibility state"
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to insert eligibility state"

    let private updateEligibility conn transaction (eligibilityInfo : EligibilityInfoDto) : Result<unit, AppErrors> =
        let sql =
            """
            UPDATE eligibility
            SET is_primary_eligible = @isPrimaryEligible,
                is_secondary_eligible = @isSecondaryEligible,
                kyc_controller_address = @kycControllerAddress
            WHERE account_id = (SELECT account_id FROM account WHERE account_hash = @accountHash)
            AND asset_id = (SELECT asset_id FROM asset WHERE asset_hash = @assetHash)
            """

        let sqlParams =
            [
                "@accountHash", eligibilityInfo.AccountHash |> box
                "@assetHash", eligibilityInfo.AssetHash |> box
                "@isPrimaryEligible", eligibilityInfo.EligibilityState.IsPrimaryEligible |> box
                "@isSecondaryEligible", eligibilityInfo.EligibilityState.IsSecondaryEligible |> box
                "@kycControllerAddress", eligibilityInfo.EligibilityState.KycControllerAddress |> box
            ]
        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 0 -> addEligibility conn transaction eligibilityInfo
            | 1 -> Ok ()
            | _ -> Result.appError "Didn't update vote state"
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to update vote state"

    let private updateEligibilities
        conn
        transaction
        (eligibilities : Map<string * string, EligibilityStateDto>)
        : Result<unit, AppErrors>
        =

        let foldFn result ((accountHash, assetHash), eligibilityState : EligibilityStateDto) =
            result
            >>= fun _ ->
                {
                    AccountHash = accountHash
                    AssetHash = assetHash
                    EligibilityState =
                        {
                            IsPrimaryEligible = eligibilityState.IsPrimaryEligible
                            IsSecondaryEligible = eligibilityState.IsSecondaryEligible
                            KycControllerAddress = eligibilityState.KycControllerAddress
                        }
                }
                |> updateEligibility conn transaction

        eligibilities
        |> Map.toList
        |> List.fold foldFn (Ok ())

    let private addKycProvider
        conn
        transaction
        (assetHash : string, providerAddress : string)
        : Result<unit, AppErrors>
        =

        let sql =
            """
            INSERT INTO kyc_provider (asset_id, provider_address)
            SELECT asset_id, @providerAddress
            FROM asset
            WHERE asset_hash = @assetHash
            """

        let sqlParams =
            [
                "@assetHash", assetHash |> box
                "@providerAddress", providerAddress|> box
            ]

        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 1 -> Ok ()
            | _ -> Result.appError "Didn't insert KYC provider"
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to insert KYC provider"

    let private removeKycProvider
        conn
        transaction
        (assetHash : string, providerAddress : string)
        : Result<unit, AppErrors>
        =

        let sql =
            """
            DELETE FROM kyc_provider
            WHERE asset_id = (SELECT asset_id FROM asset WHERE asset_hash = @assetHash)
            AND provider_address = @providerAddress
            """

        let sqlParams =
            [
                "@assetHash", assetHash |> box
                "@providerAddress", providerAddress |> box
            ]

        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 0
            | 1 -> Ok ()
            | _ ->
                sprintf "Didn't remove KYC provider: %s" providerAddress
                |> Result.appError
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            sprintf "Failed to remove KYC provider: %s" providerAddress
            |> Result.appError

    let private updateKycProviders
        conn
        transaction
        (kycProviders : Map<string, Map<string, bool>>)
        : Result<unit, AppErrors>
        =

        let foldFn result (assetHash, providerAddress, addProvider) =
            result
            >>= (fun _ ->
                if addProvider then
                    addKycProvider conn transaction (assetHash, providerAddress)
                else
                    removeKycProvider conn transaction (assetHash, providerAddress)
            )

        kycProviders
        |> Map.toList
        |> List.collect (fun (asset, stateList) ->
            stateList |> Map.toList |> List.map (fun (address, change) ->
                (asset, address, change)
            )
        )
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
            | _ -> Result.appError "Didn't insert account state"
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to insert account state"

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
            | _ -> Result.appError "Didn't update account state"
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to update account state"

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
            INSERT INTO asset (asset_hash, asset_code, controller_address, is_eligibility_required)
            VALUES (@assetHash, @assetCode, @controllerAddress, @isEligibilityRequired)
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
                "@isEligibilityRequired", assetInfo.IsEligibilityRequired |> box
            ]

        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 1 -> Ok ()
            | _ -> Result.appError "Didn't insert asset state"
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to insert asset state"

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
                controller_address = @controllerAddress,
                is_eligibility_required = @isEligibilityRequired
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
                "@isEligibilityRequired", assetInfo.IsEligibilityRequired |> box
            ]

        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 0 -> addAsset conn transaction assetInfo
            | 1 -> Ok ()
            | _ -> Result.appError "Didn't update asset state"
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to update asset state"

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
                    IsEligibilityRequired = state.IsEligibilityRequired
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
            INSERT INTO validator (
                validator_address,
                network_address,
                shared_reward_percent,
                time_to_lock_deposit,
                time_to_blacklist,
                is_enabled
            )
            VALUES (
                @validatorAddress,
                @networkAddress,
                @sharedRewardPercent,
                @timeToLockDeposit,
                @timeToBlacklist,
                @isEnabled
            )
            """

        let sqlParams =
            [
                "@validatorAddress", validatorInfo.ValidatorAddress |> box
                "@networkAddress", validatorInfo.NetworkAddress |> box
                "@sharedRewardPercent", validatorInfo.SharedRewardPercent |> box
                "@timeToLockDeposit", validatorInfo.TimeToLockDeposit |> box
                "@timeToBlacklist", validatorInfo.TimeToBlacklist |> box
                "@isEnabled", validatorInfo.IsEnabled |> box
            ]

        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 1 -> Ok ()
            | _ -> Result.appError "Didn't insert validator state"
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to insert validator state"

    let private removeValidator
        conn
        transaction
        (validatorAddress : string)
        : Result<unit, AppErrors>
        =

        let sql =
            """
            DELETE FROM validator
            WHERE validator_address = @validatorAddress
            """

        let sqlParams =
            [
                "@validatorAddress", validatorAddress |> box
            ]

        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 0
            | 1 -> Ok ()
            | _ ->
                sprintf "Didn't remove validator: %s" validatorAddress
                |> Result.appError
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            sprintf "Failed to remove validator: %s" validatorAddress
            |> Result.appError

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
                shared_reward_percent = @sharedRewardPercent,
                time_to_lock_deposit = @timeToLockDeposit,
                time_to_blacklist = @timeToBlacklist,
                is_enabled = @isEnabled
            WHERE validator_address = @validatorAddress
            """

        let sqlParams =
            [
                "@validatorAddress", validatorInfo.ValidatorAddress |> box
                "@networkAddress", validatorInfo.NetworkAddress |> box
                "@sharedRewardPercent", validatorInfo.SharedRewardPercent |> box
                "@timeToLockDeposit", validatorInfo.TimeToLockDeposit |> box
                "@timeToBlacklist", validatorInfo.TimeToBlacklist |> box
                "@isEnabled", validatorInfo.IsEnabled |> box
            ]

        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 0 -> addValidator conn transaction validatorInfo
            | 1 -> Ok ()
            | _ -> Result.appError "Didn't update validator state"
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to update validator state"

    let private updateValidators
        (conn : DbConnection)
        (transaction : DbTransaction)
        (validators : Map<string, ValidatorStateDto * ValidatorChangeCode>)
        : Result<unit, AppErrors>
        =

        let foldFn result (validatorAddress, (state : ValidatorStateDto, change : ValidatorChangeCode)) =
            result
            >>= (fun _ ->
                let validatorInfo =
                    {
                        ValidatorAddress = validatorAddress
                        NetworkAddress = state.NetworkAddress
                        SharedRewardPercent = state.SharedRewardPercent
                        TimeToLockDeposit = state.TimeToLockDeposit
                        TimeToBlacklist = state.TimeToBlacklist
                        IsEnabled = state.IsEnabled
                    }
                match change with
                | ValidatorChangeCode.Add ->
                    addValidator conn transaction validatorInfo
                | ValidatorChangeCode.Remove ->
                    removeValidator conn transaction validatorAddress
                | ValidatorChangeCode.Update ->
                    updateValidator conn transaction validatorInfo
                | _ -> Result.appError (sprintf "Invalid validator change : %A" change)
            )

        validators
        |> Map.toList
        |> List.fold foldFn (Ok ())

    let private addStake conn transaction (stakeInfo : StakeInfoDto) : Result<unit, AppErrors> =
        let sql =
            """
            INSERT INTO stake (staker_address, validator_address, amount)
            VALUES (@stakerAddress, @validatorAddress, @amount)
            """

        let sqlParams =
            [
                "@stakerAddress", stakeInfo.StakerAddress |> box
                "@validatorAddress", stakeInfo.ValidatorAddress |> box
                "@amount", stakeInfo.StakeState.Amount |> box
            ]

        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 1 -> Ok ()
            | _ -> Result.appError "Didn't insert stake state"
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to insert stake state"

    let private removeStake conn transaction (stakeInfo : StakeInfoDto) : Result<unit, AppErrors> =
        let sql =
            """
            DELETE FROM stake
            WHERE staker_address = @stakerAddress
            AND validator_address = @validatorAddress
            """

        let sqlParams =
            [
                "@stakerAddress", stakeInfo.StakerAddress |> box
                "@validatorAddress", stakeInfo.ValidatorAddress |> box
            ]

        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 0
            | 1 -> Ok ()
            | _ ->
                sprintf "Didn't remove stake: (%s %s)" stakeInfo.StakerAddress stakeInfo.ValidatorAddress
                |> Result.appError
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            sprintf "Failed to remove stake: (%s %s)" stakeInfo.StakerAddress stakeInfo.ValidatorAddress
            |> Result.appError

    let private updateStake conn transaction (stakeInfo : StakeInfoDto) : Result<unit, AppErrors> =
        let sql =
            """
            UPDATE stake
            SET amount = @amount
            WHERE staker_address = @stakerAddress
            AND validator_address = @validatorAddress
            """

        let sqlParams =
            [
                "@stakerAddress", stakeInfo.StakerAddress |> box
                "@validatorAddress", stakeInfo.ValidatorAddress |> box
                "@amount", stakeInfo.StakeState.Amount |> box
            ]

        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 0 -> addStake conn transaction stakeInfo
            | 1 -> Ok ()
            | _ -> Result.appError "Didn't update stake state"
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to update stake state"

    let private updateStakes
        conn
        transaction
        (stakes : Map<string * string, StakeStateDto>)
        : Result<unit, AppErrors>
        =

        let foldFn result ((stakerAddress, validatorAddress), stakeState : StakeStateDto) =
            result
            >>= fun _ ->
                let stakeStateDto =
                    {
                        StakerAddress = stakerAddress
                        ValidatorAddress = validatorAddress
                        StakeState =
                            {
                                Amount = stakeState.Amount
                            }
                    }
                if stakeState.Amount = 0m then
                    removeStake conn transaction stakeStateDto
                else
                    updateStake conn transaction stakeStateDto

        stakes
        |> Map.toList
        |> List.fold foldFn (Ok ())

    let private addTradingPair
        conn
        transaction
        (tradingPairInfo : TradingPairInfoDto)
        : Result<unit, AppErrors>
        =

        let sql =
            """
            INSERT INTO trading_pair (
                base_asset_id,
                quote_asset_id,
                is_enabled,
                last_price,
                price_change
            )
            VALUES (
                (SELECT asset_id FROM asset WHERE asset_hash = @baseAssetHash),
                (SELECT asset_id FROM asset WHERE asset_hash = @quoteAssetHash),
                @isEnabled,
                @lastPrice,
                @priceChange
            )
            """

        let sqlParams =
            [
                "@baseAssetHash", tradingPairInfo.BaseAssetHash |> box
                "@quoteAssetHash", tradingPairInfo.QuoteAssetHash |> box
                "@isEnabled", tradingPairInfo.IsEnabled |> box
                "@lastPrice", tradingPairInfo.LastPrice |> box
                "@priceChange", tradingPairInfo.PriceChange |> box
            ]

        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 1 -> Ok ()
            | _ -> Result.appError "Didn't insert trading pair state"
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to insert trading pair state"

    let private updateTradingPair
        conn
        transaction
        (tradingPairInfo : TradingPairInfoDto)
        : Result<unit, AppErrors>
        =

        let sql =
            """
            UPDATE trading_pair
            SET is_enabled = @isEnabled,
                last_price = @lastPrice,
                price_change = @priceChange
            WHERE base_asset_id = (SELECT asset_id FROM asset WHERE asset_hash = @baseAssetHash)
            AND quote_asset_id = (SELECT asset_id FROM asset WHERE asset_hash = @quoteAssetHash)
            """

        let sqlParams =
            [
                "@baseAssetHash", tradingPairInfo.BaseAssetHash |> box
                "@quoteAssetHash", tradingPairInfo.QuoteAssetHash |> box
                "@isEnabled", tradingPairInfo.IsEnabled |> box
                "@lastPrice", tradingPairInfo.LastPrice |> box
                "@priceChange", tradingPairInfo.PriceChange |> box
            ]

        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 0 -> addTradingPair conn transaction tradingPairInfo
            | 1 -> Ok ()
            | _ -> Result.appError "Didn't update trading pair state"
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to update trading pair state"

    let private updateTradingPairs
        conn
        transaction
        (stakes : Map<string * string, TradingPairStateDto>)
        : Result<unit, AppErrors>
        =

        let foldFn result ((baseAssetHash, quoteAssetHash), state : TradingPairStateDto) =
            result
            >>= fun _ ->
                {
                    TradingPairInfoDto.BaseAssetHash = baseAssetHash
                    QuoteAssetHash = quoteAssetHash
                    IsEnabled = state.IsEnabled
                    LastPrice = state.LastPrice
                    PriceChange = state.PriceChange
                }
                |> updateTradingPair conn transaction

        stakes
        |> Map.toList
        |> List.fold foldFn (Ok ())

    let private addTradeOrder
        conn
        transaction
        (tradeOrderInfo : TradeOrderInfoDto)
        : Result<unit, AppErrors>
        =

        let sql =
            """
            INSERT INTO trade_order (
                trade_order_hash,
                block_timestamp,
                block_number,
                tx_position,
                action_number,
                account_id,
                base_asset_id,
                quote_asset_id,
                side,
                amount,
                order_type,
                limit_price,
                stop_price,
                trailing_offset,
                trailing_offset_is_percentage,
                time_in_force,
                expiration_timestamp,
                is_executable,
                amount_filled
            )
            VALUES (
                @tradeOrderHash,
                @blockTimestamp,
                @blockNumber,
                @txPosition,
                @actionNumber,
                (SELECT account_id FROM account WHERE account_hash = @accountHash),
                (SELECT asset_id FROM asset WHERE asset_hash = @baseAssetHash),
                (SELECT asset_id FROM asset WHERE asset_hash = @quoteAssetHash),
                @side,
                @amount,
                @orderType,
                @limitPrice,
                @stopPrice,
                @trailingOffset,
                @trailingOffsetIsPercentage,
                @timeInForce,
                @expirationTimestamp,
                @isExecutable,
                @amountFilled
            )
            """

        let sqlParams =
            [
                "@tradeOrderHash", tradeOrderInfo.TradeOrderHash |> box
                "@blockTimestamp", tradeOrderInfo.BlockTimestamp |> box
                "@blockNumber", tradeOrderInfo.BlockNumber |> box
                "@txPosition", tradeOrderInfo.TxPosition |> box
                "@actionNumber", tradeOrderInfo.ActionNumber |> box
                "@accountHash", tradeOrderInfo.AccountHash |> box
                "@baseAssetHash", tradeOrderInfo.BaseAssetHash |> box
                "@quoteAssetHash", tradeOrderInfo.QuoteAssetHash |> box
                "@side", tradeOrderInfo.Side |> box
                "@amount", tradeOrderInfo.Amount |> box
                "@orderType", tradeOrderInfo.OrderType |> box
                "@limitPrice", tradeOrderInfo.LimitPrice |> box
                "@stopPrice", tradeOrderInfo.StopPrice |> box
                "@trailingOffset", tradeOrderInfo.TrailingOffset |> box
                "@trailingOffsetIsPercentage", tradeOrderInfo.TrailingOffsetIsPercentage |> box
                "@timeInForce", tradeOrderInfo.TimeInForce |> box
                "@expirationTimestamp", tradeOrderInfo.ExpirationTimestamp |> box
                "@isExecutable", tradeOrderInfo.IsExecutable |> box
                "@amountFilled", tradeOrderInfo.AmountFilled |> box
            ]

        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 1 -> Ok ()
            | _ -> Result.appError "Didn't insert trade order state"
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to insert trade order state"

    let private updateTradeOrder
        conn
        transaction
        (tradeOrderInfo : TradeOrderInfoDto)
        : Result<unit, AppErrors>
        =

        let sql =
            """
            UPDATE trade_order
            SET limit_price = @limitPrice,
                stop_price = @stopPrice,
                is_executable = @isExecutable,
                amount_filled = @amountFilled
            WHERE trade_order_hash = @tradeOrderHash
            """

        let sqlParams =
            [
                "@tradeOrderHash", tradeOrderInfo.TradeOrderHash |> box
                "@limitPrice", tradeOrderInfo.LimitPrice |> box
                "@stopPrice", tradeOrderInfo.StopPrice |> box
                "@isExecutable", tradeOrderInfo.IsExecutable |> box
                "@amountFilled", tradeOrderInfo.AmountFilled |> box
            ]

        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 1 -> Ok ()
            | _ -> Result.appError "Didn't update trade order state"
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to update trade order state"

    let private removeTradeOrder
        conn
        transaction
        (tradeOrderHash : string)
        : Result<unit, AppErrors>
        =

        let sql =
            """
            DELETE FROM trade_order
            WHERE trade_order_hash = @tradeOrderHash
            """

        let sqlParams =
            [
                "@tradeOrderHash", tradeOrderHash |> box
            ]

        try
            match DbTools.executeWithinTransaction conn transaction sql sqlParams with
            | 1 -> Ok ()
            | _ ->
                sprintf "Didn't remove trade order: %s" tradeOrderHash
                |> Result.appError
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            sprintf "Failed to remove trade order: %s" tradeOrderHash
            |> Result.appError

    let private updateTradeOrders
        (conn : DbConnection)
        (transaction : DbTransaction)
        (blockNumber : BlockNumber)
        (tradeOrders : Map<string, TradeOrderStateDto * TradeOrderChangeCode>)
        : Result<unit, AppErrors>
        =

        let foldFn result (tradeOrderHash, (state : TradeOrderStateDto, change : TradeOrderChangeCode)) =
            result
            >>= (fun _ ->
                let tradeOrderInfo =
                    {
                        TradeOrderInfoDto.TradeOrderHash = tradeOrderHash
                        BlockTimestamp = state.BlockTimestamp
                        BlockNumber = state.BlockNumber
                        TxPosition = state.TxPosition
                        ActionNumber = state.ActionNumber
                        AccountHash = state.AccountHash
                        BaseAssetHash = state.BaseAssetHash
                        QuoteAssetHash = state.QuoteAssetHash
                        Side = state.Side
                        Amount = state.Amount
                        OrderType = state.OrderType
                        LimitPrice = state.LimitPrice
                        StopPrice = state.StopPrice
                        TrailingOffset = state.TrailingOffset
                        TrailingOffsetIsPercentage = state.TrailingOffsetIsPercentage
                        TimeInForce = state.TimeInForce
                        ExpirationTimestamp = state.ExpirationTimestamp
                        IsExecutable = state.IsExecutable
                        AmountFilled = state.AmountFilled
                    }
                match change with
                | TradeOrderChangeCode.Add ->
                    addTradeOrder conn transaction tradeOrderInfo
                | TradeOrderChangeCode.Update ->
                    updateTradeOrder conn transaction tradeOrderInfo
                | TradeOrderChangeCode.Remove ->
                    if state.BlockNumber = blockNumber.Value then
                        Ok () // Trade order, which came in current block, doesn't exist in the state DB.
                    else
                        removeTradeOrder conn transaction tradeOrderHash

                | _ -> Result.appError (sprintf "Invalid trade order change : %A" change)
            )

        tradeOrders
        |> Map.toList
        |> List.fold foldFn (Ok ())

    let persistStateChanges
        dbEngineType
        dbConnectionString
        (blockNumber : BlockNumber)
        (stateChanges : ProcessingOutputDto)
        : Result<unit, AppErrors>
        =

        Log.debugf "PERSISTING CHANGES:\n%A" stateChanges

        use conn = DbTools.newConnection dbEngineType dbConnectionString

        conn.Open()
        use transaction = conn.BeginTransaction(Data.IsolationLevel.ReadCommitted)

        let result =
            result {
                do! removeProcessedTxs conn transaction stateChanges.TxResults
                do! removeProcessedEquivocationProofs conn transaction stateChanges.EquivocationProofResults
                do! updateChxAddresses conn transaction stateChanges.ChxAddresses
                do! updateValidators conn transaction stateChanges.Validators
                do! updateStakes conn transaction stateChanges.Stakes
                do! updateAssets conn transaction stateChanges.Assets
                do! updateAccounts conn transaction stateChanges.Accounts
                do! updateHoldings conn transaction stateChanges.Holdings
                do! updateVotes conn transaction stateChanges.Votes
                do! updateKycProviders conn transaction stateChanges.KycProviders
                do! updateEligibilities conn transaction stateChanges.Eligibilities
                do! updateTradingPairs conn transaction stateChanges.TradingPairs
                do! updateTradeOrders conn transaction blockNumber stateChanges.TradeOrders
                do! updateBlock conn transaction blockNumber
                do! removePreviousBlock conn transaction blockNumber
                do! removeOldConsensusMessages conn transaction blockNumber
                do! removeConsensusState conn transaction
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
            Result.appError "Failed to persist state changes"

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Network
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let private getPeers dbEngineType dbConnectionString isDead =
        let sql =
            """
            SELECT network_address, session_timestamp, is_dead, dead_timestamp
            FROM peer
            WHERE is_dead = @isDead
            """
        let sqlParams =
            [
                "@isDead", isDead |> box
            ]

        DbTools.query<GossipPeerInfoDto> dbEngineType dbConnectionString sql sqlParams

    let getActivePeers dbEngineType dbConnectionString =
        getPeers dbEngineType dbConnectionString false

    let getDeadPeers dbEngineType dbConnectionString =
        getPeers dbEngineType dbConnectionString true

    let removePeer
        dbEngineType
        dbConnectionString
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
            | 0
            | 1 -> Ok ()
            | _ -> Result.appError "Didn't remove peer"
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to remove peer"

    let private addPeer
        dbEngineType
        dbConnectionString
        (peerInfo : GossipPeerInfoDto)
        : Result<unit, AppErrors>
        =

        let sql =
            """
            INSERT INTO peer (network_address, session_timestamp, is_dead, dead_timestamp)
            VALUES (@networkAddress, @sessionTimestamp, @isDead, @deadTimestamp)
            """

        let sqlParams =
            [
                "@networkAddress", peerInfo.NetworkAddress |> box
                "@sessionTimestamp", peerInfo.SessionTimestamp |> box
                "@isDead", peerInfo.IsDead |> box
                "@deadTimestamp", peerInfo.DeadTimestamp |> boxNullable
            ]

        try
            match DbTools.execute dbEngineType dbConnectionString sql sqlParams with
            | 1 -> Ok ()
            | _ -> Result.appError "Didn't insert peer"
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to insert peer"

    let savePeer
        dbEngineType
        dbConnectionString
        (peerInfo : GossipPeerInfoDto)
        : Result<unit, AppErrors>
        =

        let sql =
            """
            UPDATE peer
            SET network_address = @networkAddress,
                session_timestamp = @sessionTimestamp,
                is_dead = @isDead,
                dead_timestamp = @deadTimestamp
            WHERE network_address = @networkAddress
            """

        let sqlParams =
            [
                "@networkAddress", peerInfo.NetworkAddress |> box
                "@isDead", peerInfo.IsDead |> box
                "@sessionTimestamp", peerInfo.SessionTimestamp |> box
                "@deadTimestamp", peerInfo.DeadTimestamp |> boxNullable
            ]

        try
            match DbTools.execute dbEngineType dbConnectionString sql sqlParams with
            | 0 -> addPeer dbEngineType dbConnectionString peerInfo
            | 1 -> Ok ()
            | _ -> Result.appError "Didn't update peer"
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError "Failed to update peer"

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Raw Data
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    type RawDataType =
        | Block
        | Tx
        | TxResult
        | EquivocationProof
        | EquivocationProofResult
        | ClosedTradeOrder

        member __.CaseName =
            match __ with
            | Block -> "Block"
            | Tx -> "Tx"
            | TxResult -> "TxResult"
            | EquivocationProof -> "EquivocationProof"
            | EquivocationProofResult -> "EquivocationProofResult"
            | ClosedTradeOrder -> "ClosedTradeOrder"

        member __.CaseCode =
            match __ with
            | Block -> 1s
            | Tx -> 2s
            | TxResult -> 3s
            | EquivocationProof -> 4s
            | EquivocationProofResult -> 5s
            | ClosedTradeOrder -> 6s

    let getRawData dbEngineType dbConnectionString (itemType: RawDataType) itemKey =
        let sql =
            """
            SELECT payload
            FROM raw_data
            WHERE item_type = @item_type AND item_key = @item_key
            """

        let sqlParams =
            [
                "@item_type", itemType.CaseCode |> box
                "@item_key", itemKey |> box
            ]

        match DbTools.query<byte[]> dbEngineType dbConnectionString sql sqlParams with
        | [] -> None
        | [a] -> a |> Some
        | _ -> failwithf "Multiple entries found for %s %s" itemType.CaseName itemKey

    let rawDataExists dbEngineType dbConnectionString (itemType: RawDataType) itemKey =
        let sql =
            """
            SELECT 1
            FROM raw_data
            WHERE item_type = @item_type AND item_key = @item_key
            """

        let sqlParams =
            [
                "@item_type", itemType.CaseCode |> box
                "@item_key", itemKey |> box
            ]

        match DbTools.query<int> dbEngineType dbConnectionString sql sqlParams with
        | [] -> false
        | [_] -> true
        | _ -> failwithf "Multiple entries found for %s %s" itemType.CaseName itemKey

    let private insertRawData
        dbEngineType
        dbConnectionString
        (itemType: RawDataType)
        itemKey
        (payload : byte[])
        : Result<unit, AppErrors>
        =

        let sql =
            """
            INSERT INTO raw_data (item_type, item_key, payload)
            VALUES (@item_type, @item_key, @payload)
            """

        let sqlParams =
            [
                "@item_type", itemType.CaseCode |> box
                "@item_key", itemKey |> box
                "@payload", payload |> box
            ]

        try
            match DbTools.execute dbEngineType dbConnectionString sql sqlParams with
            | 1 -> Ok ()
            | _ -> Result.appError (sprintf "Didn't insert %s %s" itemType.CaseName itemKey)
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError (sprintf "Failed to insert %s %s" itemType.CaseName itemKey)

    let saveRawData
        dbEngineType
        dbConnectionString
        (itemType: RawDataType)
        itemKey
        (payload : byte[])
        : Result<unit, AppErrors>
        =

        match getRawData dbEngineType dbConnectionString itemType itemKey with
        | Some _ -> Result.appError (sprintf "%s %s already exists" itemType.CaseName itemKey)
        | None -> insertRawData dbEngineType dbConnectionString itemType itemKey payload

    let removeRawData
        dbEngineType
        dbConnectionString
        (itemType: RawDataType)
        itemKey
        : Result<unit, AppErrors>
        =

        let sql =
            """
            DELETE FROM raw_data
            WHERE item_type = @item_type AND item_key = @item_key
            """

        let sqlParams =
            [
                "@item_type", itemType.CaseCode |> box
                "@item_key", itemKey |> box
            ]

        try
            match DbTools.execute dbEngineType dbConnectionString sql sqlParams with
            | 0
            | 1 -> Ok ()
            | _ -> Result.appError (sprintf "%s %s not found" itemType.CaseName itemKey)
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError (sprintf "Failed to remove %s %s" itemType.CaseName itemKey)
