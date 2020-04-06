namespace Own.Blockchain.Public.IntegrationTests

open System
open System.IO
open System.Data
open Own.Blockchain.Public.Core.Dtos
open Own.Blockchain.Public.Data
open Own.Blockchain.Public.Node

module Helpers =

    let private boxNullable (v : Nullable<_>) =
        if v.HasValue then
            v.Value |> box
        else
            DBNull.Value |> box

    let dbExecute =
        DbTools.execute Config.DbEngineType Config.DbConnectionString

    let private runInDbTransaction f =
        use conn = DbTools.newConnection Config.DbEngineType Config.DbConnectionString

        conn.Open()
        use transaction = conn.BeginTransaction(IsolationLevel.ReadCommitted)

        f transaction |> ignore

        transaction.Commit()
        conn.Close()

    let resetTestData () =
        let schemaName = (Npgsql.NpgsqlConnectionStringBuilder Config.DbConnectionString).SearchPath

        let removeAllTables =
            sprintf
                """
                DO $$
                DECLARE
                    v_table_name TEXT;
                BEGIN
                    FOR v_table_name IN
                        SELECT tablename
                        FROM pg_tables
                        WHERE schemaname = '%s'
                    LOOP
                        EXECUTE 'DROP TABLE IF EXISTS ' || QUOTE_IDENT(v_table_name) || ' CASCADE';
                    END LOOP;
                END $$;
                """
                schemaName

        dbExecute removeAllTables [] |> ignore

        if Directory.Exists Config.DataDir then
            Directory.Delete(Config.DataDir, true)

        DbInit.init Config.DbEngineType Config.DbConnectionString

    let addChxAddress (chxAddressInfo : ChxAddressInfoDto) =
        let sql =
            """
            INSERT INTO chx_address (blockchain_address, nonce, balance)
            VALUES (@blockchainAddress, @nonce, @balance)
            """

        [
            "@blockchainAddress", chxAddressInfo.BlockchainAddress |> box
            "@nonce", chxAddressInfo.ChxAddressState.Nonce |> box
            "@balance", chxAddressInfo.ChxAddressState.Balance |> box
        ]
        |> dbExecute sql
        |> ignore

    let addValidator (validatorInfo : ValidatorInfoDto) =
        let sql =
            """
            INSERT INTO validator (
                validator_address,
                network_address,
                shared_reward_percent,
                time_to_lock_deposit,
                time_to_blacklist,
                is_enabled,
                last_proposed_block_number,
                last_proposed_block_timestamp
            )
            VALUES (
                @validatorAddress,
                @networkAddress,
                @sharedRewardPercent,
                @timeToLockDeposit,
                @timeToBlacklist,
                @isEnabled,
                @lastProposedBlockNumber,
                @lastProposedBlockTimestamp
            )
            """

        [
            "@validatorAddress", validatorInfo.ValidatorAddress |> box
            "@networkAddress", validatorInfo.NetworkAddress |> box
            "@sharedRewardPercent", validatorInfo.SharedRewardPercent |> box
            "@timeToLockDeposit", validatorInfo.TimeToLockDeposit |> box
            "@timeToBlacklist", validatorInfo.TimeToBlacklist |> box
            "@isEnabled", validatorInfo.IsEnabled |> box
            "@lastProposedBlockNumber", validatorInfo.LastProposedBlockNumber |> boxNullable
            "@lastProposedBlockTimestamp", validatorInfo.LastProposedBlockTimestamp |> boxNullable
        ]
        |> dbExecute sql
        |> ignore

    let addStake (stakeInfo : StakeInfoDto) =
        let sql =
            """
            INSERT INTO stake (staker_address, validator_address, amount)
            VALUES (@stakerAddress, @validatorAddress, @amount)
            """

        [
            "@stakerAddress", stakeInfo.StakerAddress |> box
            "@validatorAddress", stakeInfo.ValidatorAddress |> box
            "@amount", stakeInfo.StakeState.Amount |> box
        ]
        |> dbExecute sql
        |> ignore
