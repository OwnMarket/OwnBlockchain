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
                failwith "Unknown error"
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

        // TODO: add condition to skip transactions
        let sql =
            """
            SELECT tx_hash, sender_address, nonce, fee, tx_id AS appearance_order
            FROM tx
            WHERE status = 0
            --AND tx_hash NOT IN (@txsToSkip)
            ORDER BY fee DESC, tx_id
            LIMIT @txCountToFetch
            """

        let txsToSkipParamValue =
            txsToSkip
            |> List.map (fun (TxHash t) -> t)
            |> List.toSeq

        let sqlParams =
            [
                "@txCountToFetch", txCountToFetch |> box
                "@txsToSkip", txsToSkipParamValue |> box
            ]

        DbTools.query dbConnectionString sql sqlParams

    let getLastBlockTimestamp (dbConnectionString : string) : Timestamp =
        (*
        Get block number from DB table, which should contain a single record.
        This is to enable atomic commit of new state together with an update of the last block number
        *)

        failwith "TODO: getLastBlockNumber"

    let getLastBlockNumber (dbConnectionString : string) : BlockNumber =
        (*
        Get block number from DB table, which should contain a single record.
        This is to enable atomic commit of new state together with an update of the last block number
        *)

        failwith "TODO: getLastBlockNumber"

    let applyNewState (dbConnectionString : string) state =
        (*
        in a single db transaction do
        apply new state to the db
        update last applied block number in the db
        *)

        failwith "TODO: applyNewState"

    let getChxBalanceState (dbConnectionString : string) address =
        failwith "TODO: getChxBalanceState"

    let getHoldingState (dbConnectionString : string) (accountHash, equityID) =
        failwith "TODO: getHoldingState"

    let getAccountController (dbConnectionString : string) accountHash =
        failwith "TODO: getAccountController"
