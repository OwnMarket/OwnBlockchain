namespace Own.Blockchain.Public.Core

open System
open System.Threading
open System.Collections.Concurrent
open Own.Common
open Own.Blockchain.Common
open Own.Blockchain.Public.Core.DomainTypes

module Synchronization =

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // State
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    type private SynchronizationStateKey =
        | LastStoredBlockNumber
        | LastStoredConfigurationBlockNumber
        | LastKnownBlockNumber
        | LastKnownConfigurationBlockNumber

    let private state = new ConcurrentDictionary<SynchronizationStateKey, BlockNumber>()

    let private getState stateKey =
        state.GetOrAdd(stateKey, BlockNumber 0L)

    let private setState stateKey newValue =
        state.AddOrUpdate(stateKey, newValue, fun _ currentValue -> max newValue currentValue) |> ignore

    let getLastStoredBlockNumber () = getState LastStoredBlockNumber
    let getLastStoredConfigurationBlockNumber () = getState LastStoredConfigurationBlockNumber
    let getLastKnownBlockNumber () = getState LastKnownBlockNumber
    let getLastKnownConfigurationBlockNumber () = getState LastKnownConfigurationBlockNumber

    let setLastStoredBlock (block : Block) =
        if block.Configuration <> None then
            block.Header.Number
        else
            block.Header.ConfigurationBlockNumber
        |> setState LastStoredConfigurationBlockNumber

        setState LastStoredBlockNumber block.Header.Number

    let setLastKnownBlock (block : Block) =
        if block.Configuration <> None then
            block.Header.Number
        else
            block.Header.ConfigurationBlockNumber
        |> setState LastKnownConfigurationBlockNumber

        setState LastKnownBlockNumber block.Header.Number

    let resetLastKnownBlock () =
        getLastStoredBlockNumber ()
        |> fun n -> state.AddOrUpdate(LastKnownBlockNumber, n, fun _ _ -> n)
        |> ignore

        getLastStoredConfigurationBlockNumber ()
        |> fun n -> state.AddOrUpdate(LastKnownConfigurationBlockNumber, n, fun _ _ -> n)
        |> ignore

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Logic
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let initSynchronizationState
        getLastAppliedBlockNumber
        blockExists
        getBlock
        =

        let rec findLastBlockAvailableInStorage (lastStoredBlockNumber : BlockNumber) =
            let nextBlockNumber = lastStoredBlockNumber + 1
            if blockExists nextBlockNumber then
                findLastBlockAvailableInStorage nextBlockNumber
            else
                lastStoredBlockNumber

        getLastAppliedBlockNumber ()
        |> max (getLastStoredBlockNumber ())
        |> findLastBlockAvailableInStorage
        |> getBlock
        >>= Blocks.extractBlockFromEnvelopeDto
        |> Result.handle
            (fun lastBlock ->
                setLastStoredBlock lastBlock
                resetLastKnownBlock ()
            )
            (fun errors ->
                Log.appErrors errors
                failwith "Cannot load last available block from storage."
            )

    let private buildConfigurationChain
        requestBlockFromPeer
        (configurationBlockDelta : int)
        (maxNumberOfBlocksToFetchInParallel : int)
        =

        let lastStoredConfigurationBlockNumber = getLastStoredConfigurationBlockNumber ()
        let lastKnownConfigurationBlockNumber = getLastKnownConfigurationBlockNumber ()

        let firstBlockToFetch = lastStoredConfigurationBlockNumber + configurationBlockDelta

        let lastBlockToFetch =
            firstBlockToFetch + (maxNumberOfBlocksToFetchInParallel * configurationBlockDelta)
            |> min lastKnownConfigurationBlockNumber

        for blockNumber in [firstBlockToFetch .. configurationBlockDelta .. lastBlockToFetch] do
            requestBlockFromPeer blockNumber

    let acquireAndApplyMissingBlocks
        (getLastAppliedBlockNumber : unit -> BlockNumber)
        getBlock
        blockExists
        txExists
        requestBlockFromPeer
        requestTxFromPeer
        applyBlock
        configurationBlockDelta
        maxNumberOfBlocksToFetchInParallel
        =

        buildConfigurationChain requestBlockFromPeer configurationBlockDelta maxNumberOfBlocksToFetchInParallel

        let mutable lastAppliedBlockNumber = getLastAppliedBlockNumber ()

        let lastBlockToFetch =
            lastAppliedBlockNumber + maxNumberOfBlocksToFetchInParallel
            |> min (getLastKnownBlockNumber ())

        for blockNumber in [lastAppliedBlockNumber + 1 .. lastBlockToFetch] do
            if not (blockExists blockNumber) then
                // Don't try fetching the block if the configuration block is not available
                if blockNumber <= (getLastStoredConfigurationBlockNumber () + configurationBlockDelta) then
                    requestBlockFromPeer blockNumber
            else
                result {
                    let! block =
                        getBlock blockNumber
                        >>= Blocks.extractBlockFromEnvelopeDto
                    let missingTxs =
                        [
                            for txHash in block.TxSet do
                                if not (txExists txHash) then
                                    requestTxFromPeer txHash
                                    yield txHash
                        ]
                    if missingTxs.IsEmpty && blockNumber = (lastAppliedBlockNumber + 1) then
                        do! applyBlock blockNumber
                        lastAppliedBlockNumber <- lastAppliedBlockNumber + 1
                    return ()
                }
                |> Result.iterError Log.appErrors

        resetLastKnownBlock () // Last known block might be a lie - we don't want to keep trying forever.
