namespace Own.Blockchain.Public.Core

open System.Collections.Concurrent
open Own.Common
open Own.Blockchain.Common
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Core.Dtos
open Own.Blockchain.Public.Core.Events

module Synchronization =

    let unverifiedBlocks = new ConcurrentDictionary<BlockNumber, BlockEnvelopeDto>()

    let private requestBlock requestFromPeer publishEvent blockNumber =
        match unverifiedBlocks.TryRemove(blockNumber) with
        | true, blockEnvelopeDto -> (blockNumber, blockEnvelopeDto) |> BlockFetched |> publishEvent
        | _ -> requestFromPeer blockNumber

    let fetchMissingBlocks
        (getLastAppliedBlockNumber : unit -> BlockNumber)
        (getLastStoredBlockNumber : unit -> BlockNumber option)
        getStoredBlockNumbers
        getBlock
        blockExists
        txExists
        requestBlockFromPeer
        requestTxFromPeer
        publishEvent
        (configurationBlockDelta : int)
        maxNumberOfBlocksToFetchInParallel
        =

        let lastAppliedBlockNumber = getLastAppliedBlockNumber ()
        let lastStoredBlockNumber = getLastStoredBlockNumber ()
        let lastVerifiedConfigBlock =
            lastStoredBlockNumber
            |? lastAppliedBlockNumber
            |> getBlock
            >>= Blocks.extractBlockFromEnvelopeDto
            >>= (fun b ->
                if b.Configuration.IsSome then
                    Ok b
                else
                    getBlock b.Header.ConfigurationBlockNumber
                    >>= Blocks.extractBlockFromEnvelopeDto
            )
            |> Result.handle id (fun _ -> failwith "Cannot get last verified configuration block.")

        // TODO: Use delta from block configuration
        let nextConfigBlockNumber = lastVerifiedConfigBlock.Header.Number + configurationBlockDelta

        unverifiedBlocks.Keys
        |> Seq.sortDescending
        |> Seq.tryHead
        |> Option.map (min nextConfigBlockNumber) // Because we cannot verify blocks after next missing config block.
        |> Option.orElse lastStoredBlockNumber
        |> Option.iter (fun lastVerifiableBlockNumber ->
            // Fetch next config block to build config chain in advance.
            if nextConfigBlockNumber <= lastVerifiableBlockNumber then
                requestBlock requestBlockFromPeer publishEvent nextConfigBlockNumber

            // Fetch verifiable blocks
            [lastAppliedBlockNumber + 1 .. lastVerifiableBlockNumber]
            |> Seq.except [nextConfigBlockNumber] // Config block is already requested above.
            |> Seq.filter (blockExists >> not)
            |> Seq.truncate maxNumberOfBlocksToFetchInParallel
            |> Seq.iter (requestBlock requestBlockFromPeer publishEvent)
        )

        // Fetch Txs for verified blocks
        getStoredBlockNumbers ()
        |> List.sort
        |> List.iter (fun bn ->
            getBlock bn
            >>= Blocks.extractBlockFromEnvelopeDto
            |> Result.handle
                (fun block ->
                    match block.TxSet |> List.filter (txExists >> not) with
                    | [] ->
                        if block.Header.Number = lastAppliedBlockNumber + 1 then
                            BlockCompleted block.Header.Number |> publishEvent
                    | missingTxs ->
                        missingTxs |> List.iter requestTxFromPeer
                )
                Log.appErrors
        )

    let tryApplyNextBlock
        (getLastAppliedBlockNumber : unit -> BlockNumber)
        getBlock
        applyBlock
        txExists
        publishEvent
        =

        getLastAppliedBlockNumber () + 1
        |> getBlock
        |> Result.iter
            (fun blockEnvelopeDto ->
                result {
                    let! block = Blocks.extractBlockFromEnvelopeDto blockEnvelopeDto
                    if block.TxSet |> List.forall txExists then
                        Log.noticef "Applying block %i" block.Header.Number.Value
                        do! applyBlock block.Header.Number
                        return (block.Header.Number |> BlockApplied |> Some)
                    else
                        return None
                }
                |> Result.handle
                    (Option.iter publishEvent)
                    Log.appErrors
            )
