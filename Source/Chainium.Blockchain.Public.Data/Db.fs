namespace Chainium.Blockchain.Public.Data

open System
open Chainium.Common
open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Core.Dtos

module Db =

    let saveTx (dbConnectionString : string) (txInfoDto : TxInfoDto) : Result<unit, AppErrors> =
        try
            // insert into DB
            failwith "TODO"
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

        let sql =
            """
            SELECT tx_hash, sender, nonce, fee, tx_id as appearance_order
            FROM tx
            WHERE tx_hash NOT IN (txsToSkip)
            ORDER BY fee, tx_id
            LIMIT txCountToFetch;
            """

        failwith "TODO"
