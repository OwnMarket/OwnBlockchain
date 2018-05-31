namespace Chainium.Blockchain.Public.Core.Tests

open System
open Xunit
open Swensen.Unquote
open Chainium.Common
open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Core
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Core.Dtos
open Chainium.Blockchain.Public.Crypto

module BlocksTests =

    [<Fact>]
    let ``Blocks.assembleBlock`` () =
        let wallet1 = Signing.generateWallet None
        let wallet2 = Signing.generateWallet None
        let validatorWallet = Signing.generateWallet None
        let blockNumber = BlockNumber 1L
        let timestamp = Utils.getUnixTimestamp () |> Timestamp

        let previousBlockHash =
            "GENESIS_BLOCK"
            |> Conversion.stringToBytes
            |> Hashing.hash
            |> BlockHash

        let txSet =
            ["Tx1"; "Tx2"; "Tx3"]
            |> List.map (Conversion.stringToBytes >> Hashing.hash >> TxHash)

        let txResults =
            [Success; Failure []; Success]
            |> List.zip txSet
            |> Map.ofList

        let chxBalances =
            [
                wallet1.Address, {ChxBalanceState.Amount = ChxAmount 10M; Nonce = Nonce 1L}
                wallet2.Address, {ChxBalanceState.Amount = ChxAmount 20M; Nonce = Nonce 2L}
            ]
            |> Map.ofList

        let holdings =
            [
                (AccountHash "Acc1", EquityID "Eq1"), {HoldingState.Amount = EquityAmount 100M; Nonce = Nonce 10L}
                (AccountHash "Acc2", EquityID "Eq2"), {HoldingState.Amount = EquityAmount 200M; Nonce = Nonce 20L}
            ]
            |> Map.ofList

        let processingOutput =
            {
                TxResults = txResults
                ChxBalances = chxBalances
                Holdings = holdings
            }

        // ACT
        let block =
            Blocks.assembleBlock
                Hashing.decode
                Hashing.hash
                Hashing.merkleTree
                validatorWallet.Address
                blockNumber
                timestamp
                previousBlockHash
                txSet
                processingOutput

        // ASSERT
        test <@ block.Header.Number = blockNumber @>
        test <@ block.Header.PreviousHash = previousBlockHash @>
        test <@ block.Header.Timestamp = timestamp @>
        test <@ block.Header.Validator = validatorWallet.Address @>
        test <@ block.TxSet = txSet @>

        let txSetMerkleProofs =
            txSet
            |> List.map (fun (TxHash h) -> h)
            |> Helpers.verifyMerkleProofs block.Header.TxSetRoot

        test <@ txSetMerkleProofs = [true; true; true] @>

        let txResultSetMerkleProofs =
            txSet
            |> List.map (fun h -> h, txResults.[h])
            |> List.map (Blocks.createTxResultHash Hashing.decode Hashing.hash)
            |> Helpers.verifyMerkleProofs block.Header.TxResultSetRoot

        test <@ txResultSetMerkleProofs = [true; true; true] @>

        let stateMerkleProofs =
            [
                chxBalances
                |> Map.toList
                |> List.map (Blocks.createChxBalanceStateHash Hashing.decode Hashing.hash)

                holdings
                |> Map.toList
                |> List.map (fun ((accountHash, equityID), state) ->
                    Blocks.createHoldingStateHash Hashing.decode Hashing.hash (accountHash, equityID, state)
                )
            ]
            |> List.concat
            |> Helpers.verifyMerkleProofs block.Header.StateRoot

        test <@ stateMerkleProofs = [true; true; true; true] @>
