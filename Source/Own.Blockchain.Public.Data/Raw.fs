namespace Own.Blockchain.Public.Data

open System.IO
open MessagePack
open Own.Common
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

    let private createFileName (dataType : RawDataType) (key : string) =
        sprintf "%s_%s" (unionCaseName dataType) key

    let createMixedHashKey decode encodeHex (key : string) =
        sprintf "%s_%s" key (key |> decode |> encodeHex)

    let private saveData (dataDir : string) (dataType : RawDataType) (key : string) data : Result<unit, AppErrors> =
        let dataTypeName = unionCaseName dataType
        try
            if not (Directory.Exists(dataDir)) then
                Directory.CreateDirectory(dataDir) |> ignore

            let fileName = createFileName dataType key
            let path = Path.Combine(dataDir, fileName)

            if File.Exists(path) then
                Result.appError (sprintf "%s %s already exists." dataTypeName key)
            else
                let bytes = data |> LZ4MessagePackSerializer.Serialize
                use fs = new FileStream(path, FileMode.OpenOrCreate)
                use bw = new BinaryWriter(fs)
                bw.Write(bytes)
                Ok ()
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError (sprintf "Saving %s %s failed" dataTypeName key)

    let private loadData<'T> (dataDir : string) (dataType : RawDataType) (key : string) : Result<'T, AppErrors> =
        let dataTypeName = unionCaseName dataType
        try
            let fileName = createFileName dataType key
            let path = Path.Combine(dataDir, fileName)

            if File.Exists(path) then
                File.ReadAllBytes path
                |> LZ4MessagePackSerializer.Deserialize<'T>
                |> Ok
            else
                Result.appError (sprintf "%s %s not found in storage." dataTypeName key)
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError (sprintf "Loading %s %s failed" dataTypeName key)

    let private deleteData (dataDir : string) (dataType : RawDataType) (key : string) : Result<unit, AppErrors> =
        let dataTypeName = unionCaseName dataType
        try
            let fileName = createFileName dataType key
            let path = Path.Combine(dataDir, fileName)

            if File.Exists(path) then
                File.Delete path
                Ok ()
            else
                Result.appError (sprintf "%s %s not found in storage." dataTypeName key)
        with
        | ex ->
            Log.error ex.AllMessagesAndStackTraces
            Result.appError (sprintf "Deleting %s %s failed" dataTypeName key)

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Specific
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    // Tx
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
        createMixedHashKey
        (TxHash txHash)
        : Result<TxEnvelopeDto, AppErrors>
        =

        let key = createMixedHashKey txHash
        loadData<TxEnvelopeDto> dataDir Tx key

    let txExists (dataDir : string) createMixedHashKey (TxHash txHash) =
        txHash
        |> string
        |> createMixedHashKey
        |> createFileName Tx
        |> fun fileName -> Path.Combine (dataDir, fileName)
        |> File.Exists

    // TxResult
    let saveTxResult (dataDir : string) (TxHash txHash) (txResultDto : TxResultDto) : Result<unit, AppErrors> =
        saveData dataDir TxResult txHash txResultDto

    let getTxResult (dataDir : string) (TxHash txHash) : Result<TxResultDto, AppErrors> =
        loadData<TxResultDto> dataDir TxResult txHash

    let txResultExists (dataDir : string) (TxHash txHash) =
        txHash
        |> string
        |> createFileName TxResult
        |> fun fileName -> Path.Combine (dataDir, fileName)
        |> File.Exists

    let deleteTxResult (dataDir : string) (TxHash txHash) : Result<unit, AppErrors> =
        deleteData dataDir TxResult txHash

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
        (EquivocationProofHash equivocationProofHash)
        (equivocationProofResultDto : EquivocationProofResultDto)
        : Result<unit, AppErrors>
        =

        saveData dataDir EquivocationProofResult equivocationProofHash equivocationProofResultDto

    let getEquivocationProofResult
        (dataDir : string)
        (EquivocationProofHash equivocationProofHash)
        : Result<EquivocationProofResultDto, AppErrors>
        =

        loadData<EquivocationProofResultDto> dataDir EquivocationProofResult equivocationProofHash

    let equivocationProofResultExists (dataDir : string) (EquivocationProofHash equivocationProofHash) =
        equivocationProofHash
        |> string
        |> createFileName EquivocationProofResult
        |> fun fileName -> Path.Combine (dataDir, fileName)
        |> File.Exists

    let deleteEquivocationProofResult
        (dataDir : string)
        (EquivocationProofHash equivocationProofHash)
        : Result<unit, AppErrors>
        =

        deleteData dataDir EquivocationProofResult equivocationProofHash

    // Block
    let saveBlock
        (dataDir : string)
        (BlockNumber blockNr)
        (blockEnvelopeDto : BlockEnvelopeDto)
        : Result<unit, AppErrors>
        =

        saveData dataDir Block (string blockNr) blockEnvelopeDto

    let getBlock (dataDir : string) (BlockNumber blockNumber) : Result<BlockEnvelopeDto, AppErrors> =
        loadData<BlockEnvelopeDto> dataDir Block (string blockNumber)

    let blockExists (dataDir : string) (BlockNumber blockNumber) =
        blockNumber
        |> string
        |> createFileName Block
        |> fun fileName -> Path.Combine (dataDir, fileName)
        |> File.Exists
