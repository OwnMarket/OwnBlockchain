namespace Own.Blockchain.Public.Core

open System
open System.Threading
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
        (getLastStoredBlockNumber : unit -> BlockNumber option)
        (getLastAppliedBlockNumber : unit -> BlockNumber)
        getBlock
        blockExists
        txExists
        requestBlockFromPeer
        requestTxFromPeer
        publishEvent
        (configurationBlockDelta : int)
        maxNumberOfBlocksToFetchInParallel
        =

        let lastStoredBlockNumber = getLastStoredBlockNumber ()
        let lastAppliedBlockNumber = getLastAppliedBlockNumber ()
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

        unverifiedBlocks.Keys
        |> Seq.sortDescending
        |> Seq.tryHead
        |> Option.iter (fun lastUnverifiedBlock ->
            // Fetch next configuration block
            // TODO: Use delta from block configuration
            let nextConfigBlockNumber = lastVerifiedConfigBlock.Header.Number + configurationBlockDelta
            if nextConfigBlockNumber <= lastUnverifiedBlock then
                requestBlock requestBlockFromPeer publishEvent nextConfigBlockNumber

            // Fetch verifiable blocks
            let lastVerifiableBlockNumber = min nextConfigBlockNumber lastUnverifiedBlock
            seq {
                for bn in [lastAppliedBlockNumber + 1 .. lastVerifiableBlockNumber] do
                    if bn <= lastVerifiableBlockNumber && not (blockExists bn) then
                        yield bn
            }
            |> Seq.truncate maxNumberOfBlocksToFetchInParallel
            |> Seq.iter (requestBlock requestBlockFromPeer publishEvent)
        )

        // Fetch Txs for verified blocks
        lastStoredBlockNumber
        |> Option.iter (fun lastStoredBlockNumber ->
            for bn in [lastAppliedBlockNumber + 1 .. lastStoredBlockNumber] do
                getBlock bn
                >>= Blocks.extractBlockFromEnvelopeDto
                |> Result.handle
                    (fun block ->
                        block.TxSet
                        |> List.filter (txExists >> not)
                        |> List.iter requestTxFromPeer
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
