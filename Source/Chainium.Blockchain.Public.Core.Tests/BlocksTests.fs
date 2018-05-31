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
    let ``Blocks.createTxResultHash`` () =
        let txHash = TxHash "ABC"
        let txStatus = Success // Code 1 = A

        // ACT
        let txResultHash = Blocks.createTxResultHash DummyHash.decode DummyHash.create (txHash, txStatus)

        // ASSERT
        test <@ txResultHash = "ABCA" @>

    [<Fact>]
    let ``Blocks.createChxBalanceStateHash`` () =
        let address = ChainiumAddress "ABC"
        let state = {ChxBalanceState.Amount = ChxAmount 1M; Nonce = Nonce 2L}

        // ACT
        let stateHash = Blocks.createChxBalanceStateHash DummyHash.decode DummyHash.create (address, state)

        // ASSERT
        test <@ stateHash = "ABC...A...................B" @>

    [<Fact>]
    let ``Blocks.createHoldingStateHash`` () =
        let account = AccountHash "HHH"
        let equity = EquityID "EE" // E => 69 % 10 => 9 => I
        let state = {HoldingState.Amount = EquityAmount 7M; Nonce = Nonce 4L}

        // ACT
        let stateHash = Blocks.createHoldingStateHash DummyHash.decode DummyHash.create (account, equity, state)

        // ASSERT
        test <@ stateHash = "HHHII...G...................D" @>

    [<Fact>]
    let ``Blocks.createBlockHash`` () =
        let blockNumber = BlockNumber 1L
        let previousBlockHash = BlockHash "B"
        let timestamp = Timestamp 3L
        let validator = ChainiumAddress "D"
        let txSetRoot = MerkleTreeRoot "E"
        let txResultSetRoot = MerkleTreeRoot "F"
        let stateRoot = MerkleTreeRoot "G"

        // ACT
        let (BlockHash blockHash) =
            Blocks.createBlockHash
                DummyHash.decode
                DummyHash.create
                blockNumber
                previousBlockHash
                timestamp
                validator
                txSetRoot
                txResultSetRoot
                stateRoot

        // ASSERT
        test <@ blockHash = ".......AB.......CDEFG" @>

    [<Fact>]
    let ``Blocks.assembleBlock`` () =
        let blockNumber = BlockNumber 1L
        let previousBlockHash = BlockHash "B"
        let timestamp = Timestamp 3L
        let validator = ChainiumAddress "D"

        let txSet =
            ["AAA"; "BBB"; "CCC"]
            |> List.map TxHash

        let txResults =
            [Success; Failure []; Success]
            |> List.zip txSet
            |> Map.ofList

        let chxBalances =
            [
                ChainiumAddress "HH", {ChxBalanceState.Amount = ChxAmount 5M; Nonce = Nonce 7L}
                ChainiumAddress "II", {ChxBalanceState.Amount = ChxAmount 6M; Nonce = Nonce 8L}
            ]
            |> Map.ofList

        let holdings =
            [
                (AccountHash "DDD", EquityID "EEE"), {HoldingState.Amount = EquityAmount 1M; Nonce = Nonce 3L}
                (AccountHash "FFF", EquityID "GGG"), {HoldingState.Amount = EquityAmount 2M; Nonce = Nonce 4L}
            ]
            |> Map.ofList

        let processingOutput =
            {
                TxResults = txResults
                ChxBalances = chxBalances
                Holdings = holdings
            }

        let stateRoot =
            [
                "HH...E...................G" // CHX balance 1
                "II...F...................H" // CHX balance 2
                "DDDIII...A...................C" // Holding balance 1
                "FFFAAA...B...................D" // Holding balance 2
            ]
            |> String.Concat

        let blockHash =
            [
                ".......A" // blockNumber
                "B" // previousBlockHash
                ".......C" // timestamp
                "D" // validator
                "AAABBBCCC" // txSetRoot
                "AAAABBBBCCCA" // txResultSetRoot
                stateRoot
            ]
            |> String.Concat

        // ACT
        let block =
            Blocks.assembleBlock
                DummyHash.decode
                DummyHash.create
                DummyHash.merkleTree
                validator
                blockNumber
                timestamp
                previousBlockHash
                txSet
                processingOutput

        // ASSERT
        test <@ block.Header.Number = blockNumber @>
        test <@ block.Header.PreviousHash = previousBlockHash @>
        test <@ block.Header.Timestamp = timestamp @>
        test <@ block.Header.Validator = validator @>
        test <@ block.Header.TxSetRoot = MerkleTreeRoot "AAABBBCCC" @>
        test <@ block.Header.TxResultSetRoot = MerkleTreeRoot "AAAABBBBCCCA" @>
        test <@ block.Header.StateRoot = MerkleTreeRoot stateRoot @>
        test <@ block.Header.Hash = BlockHash blockHash @>
        test <@ block.TxSet = [TxHash "AAA"; TxHash "BBB"; TxHash "CCC"] @>

    [<Fact>]
    let ``Blocks.assembleBlock and verify merkle proofs`` () =
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
