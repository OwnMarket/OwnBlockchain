namespace Chainium.Blockchain.Public.Core

open System
open System.Threading
open System.Collections.Concurrent
open Chainium.Common
open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Core.DomainTypes

module Synchronization =

    let private lastAvailableBlockNumber = ref 0L

    let getLastAvailableBlockNumber () =
        BlockNumber !lastAvailableBlockNumber

    let setLastAvailableBlockNumber (BlockNumber blockNumber) =
        if blockNumber > !lastAvailableBlockNumber then
            Interlocked.Exchange(lastAvailableBlockNumber, blockNumber) |> ignore

    let initSynchronizationState
        getLastAppliedBlockNumber
        blockExists
        =

        let rec findLastBlockAvailableInStorage (lastAvailableBlockNumber : BlockNumber) =
            let nextBlockNumber = lastAvailableBlockNumber + 1L
            if blockExists nextBlockNumber then
                findLastBlockAvailableInStorage nextBlockNumber
            else
                lastAvailableBlockNumber

        match getLastAppliedBlockNumber () with
        | None -> failwith "Cannot load last applied block info."
        | Some lastAppliedBlockNumber ->
            getLastAvailableBlockNumber ()
            |> max lastAppliedBlockNumber
            |> findLastBlockAvailableInStorage
            |> setLastAvailableBlockNumber

    let acquireAndApplyMissingBlocks
        (getLastAppliedBlockNumber : unit -> BlockNumber option)
        getBlock
        blockExists
        txExists
        requestBlockFromPeer
        requestTxFromPeer
        applyBlock
        maxNumberOfBlocksToFetchInParallel
        =

        match getLastAppliedBlockNumber () with
        | None -> failwith "Cannot load last applied block info."
        | Some lastAppliedBlockNumber ->
            let mutable lastAppliedBlockNumber = lastAppliedBlockNumber
            let mutable lastRequestedBlockNumber = lastAppliedBlockNumber

            let maxNumberOfBlocksToFetchInParallel =
                getLastAvailableBlockNumber () - lastAppliedBlockNumber
                |> min (BlockNumber maxNumberOfBlocksToFetchInParallel)

            for blockNumber in [lastAppliedBlockNumber + 1L .. getLastAvailableBlockNumber ()] do
                if not (blockExists blockNumber) then
                    requestBlockFromPeer blockNumber
                else
                    result {
                        let! blockEnvelopeDto = getBlock blockNumber
                        let! block = Blocks.extractBlockFromEnvelopeDto blockEnvelopeDto
                        let missingTxs =
                            [
                                for txHash in block.TxSet do
                                    if not (txExists txHash) then
                                        requestTxFromPeer txHash
                                        yield txHash
                            ]
                        if missingTxs.IsEmpty && blockNumber = (lastAppliedBlockNumber + 1L) then
                            do! applyBlock blockNumber blockEnvelopeDto
                            lastAppliedBlockNumber <- lastAppliedBlockNumber + 1L
                        return ()
                    }
                    |> Result.iterError Log.appErrors
