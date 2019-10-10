namespace Own.Blockchain.Public.Data

open System
open System.Collections.Concurrent
open MessagePack
open Own.Common.FSharp
open Own.Blockchain.Common
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Core.Dtos

module Raw =

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // General
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    type RawDataType =
        | Tx
        | TxResult
        | EquivocationProof
        | EquivocationProofResult
        | Block

    type RawDataType with
        member __.CaseName =
            match __ with
            | Tx -> "Tx"
            | TxResult -> "TxResult"
            | EquivocationProof -> "EquivocationProof"
            | EquivocationProofResult -> "EquivocationProofResult"
            | Block -> "Block"

    let private saveData
        dbEngineType
        (dbConnectionString : string)
        (dataType : RawDataType)
        (key : string)
        data
        : Result<unit, AppErrors>
        =

        data
        |> LZ4MessagePackSerializer.Serialize
        |> Db.saveRawData dbEngineType dbConnectionString dataType.CaseName key

    let private loadData<'T>
        dbEngineType
        (dbConnectionString : string)
        (dataType : RawDataType)
        (key : string)
        : Result<'T, AppErrors>
        =

        Db.getRawData dbEngineType dbConnectionString dataType.CaseName key
        |> function
            | Some bytes ->
                bytes
                |> LZ4MessagePackSerializer.Deserialize<'T>
                |> Ok
            | None ->
                Result.appError (sprintf "%s %s not found" dataType.CaseName key)

    let private deleteData
        dbEngineType
        (dbConnectionString : string)
        (dataType : RawDataType)
        (key : string)
        : Result<unit, AppErrors>
        =

        Db.removeRawData dbEngineType dbConnectionString dataType.CaseName key

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Caching
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let private txCache = new ConcurrentDictionary<TxHash, TxEnvelopeDto * DateTime>()
    let private blockCache = new ConcurrentDictionary<BlockNumber, BlockEnvelopeDto * DateTime>()

    let private getCacheItem
        (cache: ConcurrentDictionary<_, _ * DateTime>)
        maxCacheSize
        cacheItemKey
        (getItemFromStorage: _ -> Result<_, AppErrors>)
        =

        match cache.TryGetValue cacheItemKey with
        | true, (envelopeDto, _) ->
            Ok envelopeDto
        | false, _ ->
            cacheItemKey
            |> getItemFromStorage
            |> tee (
                Result.iter (fun envelope ->
                    if cache.Keys.Count < maxCacheSize then
                        let cacheValue = envelope, DateTime.UtcNow
                        try
                            cache.AddOrUpdate(cacheItemKey, cacheValue, fun _ _ -> cacheValue) |> ignore
                        with
                        | _ ->
                            Log.warningf "%A cannot be cached" cacheItemKey
                )
            )

    let private removeCacheItem (cache: ConcurrentDictionary<_, _ * DateTime>) cacheItem =
        cache.TryRemove cacheItem |> ignore

    let startCacheMonitor (cache: ConcurrentDictionary<_, _ * DateTime> ) cacheExpirationTime =
        let rec loop () =
            async {
                let lastValidTime = DateTime.UtcNow.AddSeconds(-cacheExpirationTime |> float)
                cache
                |> List.ofDict
                |> List.filter (fun (_, (_, fetchedAt)) -> fetchedAt < lastValidTime)
                |> List.iter (fun (txHash, _) -> removeCacheItem cache txHash)

                do! Async.Sleep(1000);
                return! loop ()
            }
        loop ()
        |> Async.Start

    let private getTxCached maxTxCacheSize txHash getTx =
        getCacheItem txCache maxTxCacheSize txHash getTx

    let private getBlockCached maxBlockCacheSize blockNr getBlock =
        getCacheItem blockCache maxBlockCacheSize blockNr getBlock

    let private removeTxFromCache txHash =
        removeCacheItem txCache txHash

    let private removeBlockFromCache blockNr =
        removeCacheItem blockCache blockNr

    let startTxCacheMonitor txCacheExpirationTime =
        startCacheMonitor txCache txCacheExpirationTime

    let startBlockCacheMonitor blockCacheExpirationTime =
        startCacheMonitor blockCache blockCacheExpirationTime

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Specific
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    // TX
    let saveTx
        dbEngineType
        (dbConnectionString : string)
        (TxHash txHash)
        (txEnvelopeDto : TxEnvelopeDto)
        : Result<unit, AppErrors>
        =

        saveData
            dbEngineType
            dbConnectionString
            Tx
            txHash
            txEnvelopeDto

    let getTx
        dbEngineType
        (dbConnectionString : string)
        maxTxCacheSize
        (TxHash txHash)
        : Result<TxEnvelopeDto, AppErrors>
        =

        getTxCached
            maxTxCacheSize
            (TxHash txHash)
            (fun (TxHash hash) ->
                loadData<TxEnvelopeDto>
                    dbEngineType
                    dbConnectionString
                    Tx
                    hash)

    let txExists
        dbEngineType
        (dbConnectionString : string)
        (txHash : TxHash)
        =

        txCache.ContainsKey txHash
        || Db.rawDataExists dbEngineType dbConnectionString Tx.CaseName txHash.Value

    // TxResult
    let saveTxResult
        dbEngineType
        (dbConnectionString : string)
        txHash
        (txResultDto : TxResultDto)
        : Result<unit, AppErrors>
        =

        removeTxFromCache txHash
        saveData
            dbEngineType
            dbConnectionString
            TxResult
            txHash.Value
            txResultDto

    let getTxResult
        dbEngineType
        (dbConnectionString : string)
        (TxHash txHash)
        : Result<TxResultDto, AppErrors>
        =

        loadData<TxResultDto>
            dbEngineType
            dbConnectionString
            TxResult
            txHash

    let txResultExists
        dbEngineType
        (dbConnectionString : string)
        (TxHash txHash)
        =

        Db.rawDataExists dbEngineType dbConnectionString TxResult.CaseName txHash

    let deleteTxResult
        dbEngineType
        (dbConnectionString : string)
        txHash
        : Result<unit, AppErrors>
        =

        removeTxFromCache txHash
        deleteData
            dbEngineType
            dbConnectionString
            TxResult
            txHash.Value

    // EquivocationProof
    let saveEquivocationProof
        dbEngineType
        (dbConnectionString : string)
        (EquivocationProofHash equivocationProofHash)
        (equivocationProofDto : EquivocationProofDto)
        : Result<unit, AppErrors>
        =

        saveData
            dbEngineType
            dbConnectionString
            EquivocationProof
            equivocationProofHash
            equivocationProofDto

    let getEquivocationProof
        dbEngineType
        (dbConnectionString : string)
        (EquivocationProofHash equivocationProofHash)
        : Result<EquivocationProofDto, AppErrors>
        =

        loadData<EquivocationProofDto>
            dbEngineType
            dbConnectionString
            EquivocationProof
            equivocationProofHash

    let equivocationProofExists
        dbEngineType
        (dbConnectionString : string)
        (EquivocationProofHash equivocationProofHash)
        =

        Db.rawDataExists
            dbEngineType
            dbConnectionString
            EquivocationProof.CaseName
            equivocationProofHash

    // EquivocationProofResult
    let saveEquivocationProofResult
        dbEngineType
        (dbConnectionString : string)
        (EquivocationProofHash equivocationProofHash)
        (equivocationProofResultDto : EquivocationProofResultDto)
        : Result<unit, AppErrors>
        =

        saveData
            dbEngineType
            dbConnectionString
            EquivocationProofResult
            equivocationProofHash
            equivocationProofResultDto

    let getEquivocationProofResult
        dbEngineType
        (dbConnectionString : string)
        (EquivocationProofHash equivocationProofHash)
        : Result<EquivocationProofResultDto, AppErrors>
        =

        loadData<EquivocationProofResultDto>
            dbEngineType
            dbConnectionString
            EquivocationProofResult
            equivocationProofHash

    let equivocationProofResultExists
        dbEngineType
        (dbConnectionString : string)
        (EquivocationProofHash equivocationProofHash)
        =

        Db.rawDataExists
            dbEngineType
            dbConnectionString
            EquivocationProofResult.CaseName
            equivocationProofHash

    let deleteEquivocationProofResult
        dbEngineType
        (dbConnectionString : string)
        (EquivocationProofHash equivocationProofHash)
        : Result<unit, AppErrors>
        =

        deleteData
            dbEngineType
            dbConnectionString
            EquivocationProofResult
            equivocationProofHash

    // Block
    let saveBlock
        dbEngineType
        (dbConnectionString : string)
        (BlockNumber blockNr)
        (blockEnvelopeDto : BlockEnvelopeDto)
        : Result<unit, AppErrors>
        =

        saveData dbEngineType dbConnectionString Block (string blockNr) blockEnvelopeDto

    let getBlock
        dbEngineType
        (dbConnectionString : string)
        maxBlockCacheSize
        (BlockNumber blockNumber)
        : Result<BlockEnvelopeDto, AppErrors>
        =

        getBlockCached
            maxBlockCacheSize
            (BlockNumber blockNumber)
            (fun blockNr ->
                loadData<BlockEnvelopeDto>
                    dbEngineType
                    dbConnectionString
                    Block
                    (string blockNr.Value))

    let blockExists
        dbEngineType
        (dbConnectionString : string)
        (blockNumber : BlockNumber)
        =

        blockCache.ContainsKey blockNumber
        || Db.rawDataExists
            dbEngineType
            dbConnectionString
            Block.CaseName
            (string blockNumber.Value)
