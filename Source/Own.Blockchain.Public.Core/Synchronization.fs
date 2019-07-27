namespace Own.Blockchain.Public.Core

open System.Collections.Concurrent
open Own.Common.FSharp
open Own.Blockchain.Common
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Core.Dtos
open Own.Blockchain.Public.Core.Events

module Synchronization =

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Network Time
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let updateNetworkTimeOffset getNetworkTimeOffset =
        Utils.networkTimeOffset <- getNetworkTimeOffset ()
        Log.debugf "Network time offset set to %i" Utils.networkTimeOffset

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Blockchain Head
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let synchronizeBlockchainHead
        (getLastStoredBlockNumber : unit -> BlockNumber option)
        (getLastAppliedBlockNumber : unit -> BlockNumber)
        (getBlock : BlockNumber -> Result<BlockEnvelopeDto, AppErrors>)
        requestBlockchainHeadFromPeer
        blockchainHeadPollInterval
        =

        getLastStoredBlockNumber ()
        |?> getLastAppliedBlockNumber
        |> getBlock
        |> Result.map Blocks.extractBlockFromEnvelopeDto
        |> Result.handle
            (fun block ->
                let currentTimestamp = Utils.getNetworkTimestamp ()
                if currentTimestamp - block.Header.Timestamp.Value >= int64 blockchainHeadPollInterval then
                    requestBlockchainHeadFromPeer ()
            )
            Log.appErrors

    let handleReceivedBlockchainHead
        blockExists
        getLastAppliedBlockNumber
        requestBlocksFromPeer
        (blockNumber : BlockNumber)
        =

        if blockExists blockNumber then
            if getLastAppliedBlockNumber () = blockNumber then
                Log.info "Node is synchronized"
        else
            requestBlocksFromPeer [ blockNumber ]

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Rebuilding the chain
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let unverifiedBlocks = new ConcurrentDictionary<BlockNumber, BlockEnvelopeDto>()

    let private requestBlock requestFromPeer publishEvent blockNumber =
        match unverifiedBlocks.TryRemove(blockNumber) with
        | true, blockEnvelopeDto -> (blockNumber, blockEnvelopeDto) |> BlockFetched |> publishEvent
        | _ -> requestFromPeer [ blockNumber ]

    let fetchMissingBlocks
        (getLastAppliedBlockNumber : unit -> BlockNumber)
        (getLastStoredBlockNumber : unit -> BlockNumber option)
        getStoredBlockNumbers
        getBlock
        blockExists
        txExists
        equivocationProofExists
        txExistsInDb
        equivocationProofExistsInDb
        requestBlocksFromPeer
        requestTxsFromPeer
        requestEquivocationProofsFromPeer
        publishEvent
        maxBlockFetchQueue
        =

        let lastAppliedBlockNumber = getLastAppliedBlockNumber ()
        let lastStoredBlockNumber = getLastStoredBlockNumber ()
        let lastVerifiedConfigBlock =
            lastStoredBlockNumber
            |? lastAppliedBlockNumber
            |> getBlock
            |> Result.map Blocks.extractBlockFromEnvelopeDto
            >>= (fun b ->
                if b.Configuration.IsSome then
                    Ok b
                else
                    getBlock b.Header.ConfigurationBlockNumber
                    |> Result.map Blocks.extractBlockFromEnvelopeDto
            )
            |> Result.handle id (fun _ -> failwith "Cannot get last verified configuration block")

        let lastVerifiedConfiguration =
            lastVerifiedConfigBlock.Configuration
            |?> fun _ ->
                failwithf "Cannot find configuration in config block %i" lastVerifiedConfigBlock.Header.Number.Value

        let nextConfigBlockNumber =
            lastVerifiedConfigBlock.Header.Number + lastVerifiedConfiguration.ConfigurationBlockDelta

        unverifiedBlocks.Keys
        |> Seq.sortDescending
        |> Seq.tryHead
        |> Option.map (min nextConfigBlockNumber) // Because we cannot verify blocks after next missing config block.
        |> Option.orElse lastStoredBlockNumber
        |> Option.iter (fun lastVerifiableBlockNumber ->
            // Fetch next config block to build config chain in advance.
            if nextConfigBlockNumber <= lastVerifiableBlockNumber then
                requestBlock requestBlocksFromPeer publishEvent nextConfigBlockNumber

            // Fetch verifiable blocks
            [lastAppliedBlockNumber + 1 .. lastVerifiableBlockNumber]
            |> Seq.except [nextConfigBlockNumber] // Config block is already requested above.
            |> Seq.filter (blockExists >> not)
            |> Seq.truncate maxBlockFetchQueue
            |> Seq.iter (requestBlock requestBlocksFromPeer publishEvent)
        )

        // Fetch TXs and EquivocationProofs for verified blocks
        getStoredBlockNumbers ()
        |> List.sort
        |> List.iter (fun bn ->
            getBlock bn
            |> Result.map Blocks.extractBlockFromEnvelopeDto
            |> Result.handle
                (fun block ->
                    let missingTxs =
                        block.TxSet
                        |> List.filter (txExists >> not)

                    let missingEquivocationProofs =
                        block.EquivocationProofs
                        |> List.filter (equivocationProofExists >> not)

                    if missingTxs.IsEmpty && missingEquivocationProofs.IsEmpty then
                        if block.Header.Number = lastAppliedBlockNumber + 1 then
                            let missingTxs =
                                block.TxSet
                                |> List.filter (txExistsInDb >> not)

                            let missingEquivocationProofs =
                                block.EquivocationProofs
                                |> List.filter (equivocationProofExistsInDb >> not)

                            if missingTxs.IsEmpty && missingEquivocationProofs.IsEmpty then
                                BlockReady block.Header.Number |> publishEvent
                    else
                        requestTxsFromPeer missingTxs
                        requestEquivocationProofsFromPeer missingEquivocationProofs
                )
                Log.appErrors
        )

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Applying blocks to the state
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let appliedBlocks = new BlockingCollection<BlockNumber>()

    let tryApplyNextBlock
        (getLastAppliedBlockNumber : unit -> BlockNumber)
        getBlock
        applyBlock
        txExists
        equivocationProofExists
        txExistsInDb
        equivocationProofExistsInDb
        removeOrphanTxResults
        removeOrphanEquivocationProofResults
        publishEvent
        =

        getLastAppliedBlockNumber () + 1
        |> getBlock
        |> Result.iter (fun blockEnvelopeDto ->
            result {
                let block = Blocks.extractBlockFromEnvelopeDto blockEnvelopeDto
                if block.TxSet |> Array.AsyncParallel.forall txExists
                    && block.EquivocationProofs |> Array.AsyncParallel.forall equivocationProofExists
                    && block.TxSet |> Array.AsyncParallel.forall txExistsInDb
                    && block.EquivocationProofs |> Array.AsyncParallel.forall equivocationProofExistsInDb
                then
                    Log.noticef "Applying block %i" block.Header.Number.Value
                    do! applyBlock block.Header.Number
                    appliedBlocks.Add block.Header.Number
                    return (block.Header.Number |> BlockApplied |> Some)
                else
                    return None
            }
            |> Result.handle
                (Option.iter publishEvent)
                (fun errors ->
                    Log.appErrors errors
                    removeOrphanTxResults ()
                    removeOrphanEquivocationProofResults ()
                )
        )
