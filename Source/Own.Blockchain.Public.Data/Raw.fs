namespace Own.Blockchain.Public.Data

open System
open System.IO
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

    let private createFileName (dataType : RawDataType) (key : string) =
        sprintf "%s_%s" dataType.CaseName key

    let private extractHash (key : string) =
        let index = key.LastIndexOf "_"
        if index > 0 then
            key.Substring(0, index)
        else
            key

    let createMixedHashKey decode encodeHex (key : string) =
        sprintf "%s_%s" key (key |> decode |> encodeHex)

    let private saveData (dataDir : string) (dataType : RawDataType) (key : string) data : Result<unit, AppErrors> =
        let dataTypeName = dataType.CaseName
        let fileName = createFileName dataType key
        let path = Path.Combine(dataDir, fileName)

        try
            if not (Directory.Exists(dataDir)) then
                Directory.CreateDirectory(dataDir) |> ignore

            if File.Exists(path) then
                Result.appError (sprintf "%s %s already exists" dataTypeName (extractHash key))
            else
                let bytes = data |> LZ4MessagePackSerializer.Serialize
                use fs = File.Open(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)
                use bw = new BinaryWriter(fs)
                bw.Write(bytes)
                Ok ()
        with
        | ex ->
            if ex.Message.EndsWith("already exists") then
                Log.warning ex.AllMessages
                Log.debug ex.AllMessagesAndStackTraces
            else
                Log.error ex.AllMessagesAndStackTraces

            if (new FileInfo(path)).Length = 0L then
                Log.warningf "Removing empty file: %s" path
                File.Delete(path) // Remove empty file left after unsuccessful save.

            Result.appError (sprintf "Saving %s %s failed" dataTypeName (extractHash key))

    let private loadData<'T> (dataDir : string) (dataType : RawDataType) (key : string) : Result<'T, AppErrors> =
        let dataTypeName = dataType.CaseName
        try
            let fileName = createFileName dataType key
            let path = Path.Combine(dataDir, fileName)

            if File.Exists(path) then
                use fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read)
                use br = new BinaryReader(fs)
                br.ReadBytes (fs.Length |> Convert.ToInt32)
                |> LZ4MessagePackSerializer.Deserialize<'T>
                |> Ok
            else
                Result.appError (sprintf "%s %s not found in storage" dataTypeName (extractHash key))
        with
        | ex ->
            if ex.Message.EndsWith("used by another process.") then
                Log.warning ex.AllMessages
                Log.debug ex.AllMessagesAndStackTraces
            else
                Log.error ex.AllMessagesAndStackTraces
            Result.appError (sprintf "Loading %s %s failed" dataTypeName (extractHash key))

    let private deleteData (dataDir : string) (dataType : RawDataType) (key : string) : Result<unit, AppErrors> =
        let dataTypeName = dataType.CaseName
        try
            let fileName = createFileName dataType key
            let path = Path.Combine(dataDir, fileName)

            if File.Exists(path) then
                File.Delete path
                Ok ()
            else
                Result.appError (sprintf "%s %s not found in storage" dataTypeName (extractHash key))
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError (sprintf "Deleting %s %s failed" dataTypeName (extractHash key))

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
            |> tap (
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
                |> List.iter (fun (cacheItem, _) -> removeCacheItem cache cacheItem)

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
        (dataDir : string)
        createMixedHashKey
        (TxHash txHash)
        (txEnvelopeDto : TxEnvelopeDto)
        : Result<unit, AppErrors>
        =

        let key = createMixedHashKey txHash
        saveData dataDir Tx key txEnvelopeDto

    let getTx
        (dataDir : string)
        maxTxCacheSize
        createMixedHashKey
        (TxHash txHash)
        : Result<TxEnvelopeDto, AppErrors>
        =

        getTxCached
            maxTxCacheSize
            (TxHash txHash)
            (fun hash ->
                let key = createMixedHashKey hash.Value
                loadData<TxEnvelopeDto> dataDir Tx key)

    let txExists (dataDir : string) createMixedHashKey (txHash : TxHash) =
        txHash.Value
        |> string
        |> createMixedHashKey
        |> createFileName Tx
        |> fun fileName -> Path.Combine (dataDir, fileName)
        |> fun key -> txCache.ContainsKey txHash || File.Exists key

    // TxResult
    let saveTxResult
        (dataDir : string)
        createMixedHashKey
        txHash
        (txResultDto : TxResultDto)
        : Result<unit, AppErrors>
        =

        removeTxFromCache txHash
        let key = createMixedHashKey txHash.Value
        saveData dataDir TxResult key txResultDto

    let getTxResult
        (dataDir : string)
        createMixedHashKey
        (TxHash txHash)
        : Result<TxResultDto, AppErrors>
        =

        let key = createMixedHashKey txHash
        loadData<TxResultDto> dataDir TxResult key

    let txResultExists (dataDir : string) createMixedHashKey (TxHash txHash) =
        txHash
        |> string
        |> createMixedHashKey
        |> createFileName TxResult
        |> fun fileName -> Path.Combine (dataDir, fileName)
        |> File.Exists

    let deleteTxResult
        (dataDir : string)
        createMixedHashKey
        txHash
        : Result<unit, AppErrors>
        =

        removeTxFromCache txHash
        let key = createMixedHashKey txHash.Value
        deleteData dataDir TxResult key

    // EquivocationProof
    let saveEquivocationProof
        (dataDir : string)
        createMixedHashKey
        (EquivocationProofHash equivocationProofHash)
        (equivocationProofDto : EquivocationProofDto)
        : Result<unit, AppErrors>
        =

        let key = createMixedHashKey equivocationProofHash
        saveData dataDir EquivocationProof key equivocationProofDto

    let getEquivocationProof
        (dataDir : string)
        createMixedHashKey
        (EquivocationProofHash equivocationProofHash)
        : Result<EquivocationProofDto, AppErrors>
        =

        let key = createMixedHashKey equivocationProofHash
        loadData<EquivocationProofDto> dataDir EquivocationProof key

    let equivocationProofExists
        (dataDir : string)
        createMixedHashKey
        (EquivocationProofHash equivocationProofHash)
        =

        equivocationProofHash
        |> string
        |> createMixedHashKey
        |> createFileName EquivocationProof
        |> fun fileName -> Path.Combine (dataDir, fileName)
        |> File.Exists

    // EquivocationProofResult
    let saveEquivocationProofResult
        (dataDir : string)
        createMixedHashKey
        (EquivocationProofHash equivocationProofHash)
        (equivocationProofResultDto : EquivocationProofResultDto)
        : Result<unit, AppErrors>
        =

        let key = createMixedHashKey equivocationProofHash
        saveData dataDir EquivocationProofResult key equivocationProofResultDto

    let getEquivocationProofResult
        (dataDir : string)
        createMixedHashKey
        (EquivocationProofHash equivocationProofHash)
        : Result<EquivocationProofResultDto, AppErrors>
        =

        let key = createMixedHashKey equivocationProofHash
        loadData<EquivocationProofResultDto> dataDir EquivocationProofResult key

    let equivocationProofResultExists
        (dataDir : string)
        createMixedHashKey
        (EquivocationProofHash equivocationProofHash)
        =

        equivocationProofHash
        |> string
        |> createMixedHashKey
        |> createFileName EquivocationProofResult
        |> fun fileName -> Path.Combine (dataDir, fileName)
        |> File.Exists

    let deleteEquivocationProofResult
        (dataDir : string)
        createMixedHashKey
        (EquivocationProofHash equivocationProofHash)
        : Result<unit, AppErrors>
        =

        let key = createMixedHashKey equivocationProofHash
        deleteData dataDir EquivocationProofResult key

    // Block
    let saveBlock
        (dataDir : string)
        (BlockNumber blockNr)
        (blockEnvelopeDto : BlockEnvelopeDto)
        : Result<unit, AppErrors>
        =

        saveData dataDir Block (string blockNr) blockEnvelopeDto

    let getBlock
        (dataDir : string)
        maxBlockCacheSize
        (BlockNumber blockNumber)
        : Result<BlockEnvelopeDto, AppErrors>
        =

        getBlockCached
            maxBlockCacheSize
            (BlockNumber blockNumber)
            (fun blockNr ->
                loadData<BlockEnvelopeDto> dataDir Block (string blockNr.Value))

    let blockExists (dataDir : string) (blockNumber : BlockNumber) =
        blockNumber.Value
        |> string
        |> createFileName Block
        |> fun fileName -> Path.Combine (dataDir, fileName)
        |> fun key -> blockCache.ContainsKey blockNumber || File.Exists key
