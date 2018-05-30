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
            AND tx_hash NOT IN @txsToSkip
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

    let getLastBlockTimestamp (dbConnectionString : string) : Timestamp option
        =
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


    let applyNewState (dbConnectionString : string) state =
        (*
        in a single db transaction do
        apply new state to the db
        update last applied block number in the db
        *)

        failwith "TODO: applyNewState"

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
        | _ -> failwith "More than one Chx Address exists"

    let getHoldingState (dbConnectionString : string) (accountHash, equityID) =
        let sql =
            """
            SELECT h.amount, h.nonce
            FROM holding h
            JOIN account a
            USING (account_id)
            WHERE a.account_hash = @accountHash
            AND h.asset = @equityId
            """

        let sqlParams =
            [
                "@accountHash", accountHash |> box
                "@equityId", equityID |> box
            ]

        match DbTools.query<HoldingStateDto> dbConnectionString sql sqlParams with
        | [] -> None
        | [holdingDetails] -> Some holdingDetails
        | _ -> failwith "More than one Holding exists"

    let getAccountController (dbConnectionString : string) accountHash : ChainiumAddress option =
        let sql =
            """
            SELECT chainium_address
            FROM account
            WHERE account_hash = @accountHash
            """

        let sqlParams =
            [
                "@accountHash", accountHash |> box
            ]

        match DbTools.query<AccountControllerDto> dbConnectionString sql sqlParams with
        | [] -> None
        | [accountDetails] -> accountDetails.ChainiumAddress |> ChainiumAddress |> Some
        | _ -> failwith "More than one Controller exists"
