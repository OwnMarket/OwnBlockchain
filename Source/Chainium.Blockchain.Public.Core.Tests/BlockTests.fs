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
    let ``Blocks.createTxResultHash for Success`` () =
        let txHash = TxHash "ABC"
        let txStatus =
            {
                TxResult.Status = Success // Code 1 = A
                BlockNumber = BlockNumber 4L
            }

        // ACT
        let txResultHash = Blocks.createTxResultHash DummyHash.decode DummyHash.create (txHash, txStatus)

        // ASSERT
        test <@ txResultHash = "ABCA...........D" @>

    [<Fact>]
    let ``Blocks.createTxResultHash for TxError`` () =
        let txHash = TxHash "ABC"
        let txStatus =
            {
                TxResult.Status = TxErrorCode.NonceTooLow |> TxError |> Failure
                BlockNumber = BlockNumber 4L
            }

        // ACT
        let txResultHash = Blocks.createTxResultHash DummyHash.decode DummyHash.create (txHash, txStatus)

        // ASSERT
        test <@ txResultHash = "ABCB...........D" @>

    [<Fact>]
    let ``Blocks.createTxResultHash for TxActionError`` () =
        let txHash = TxHash "ABC"
        let txStatus =
            {
                TxResult.Status = (TxActionNumber 3s, TxErrorCode.NonceTooLow) |> TxActionError |> Failure
                BlockNumber = BlockNumber 4L
            }

        // ACT
        let txResultHash = Blocks.createTxResultHash DummyHash.decode DummyHash.create (txHash, txStatus)

        // ASSERT
        test <@ txResultHash = "ABCB...C.......D" @>

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
        let assetHash = AssetHash "II"
        let state = {HoldingState.Amount = AssetAmount 7M}

        // ACT
        let stateHash = Blocks.createHoldingStateHash DummyHash.decode DummyHash.create (account, assetHash, state)

        // ASSERT
        test <@ stateHash = "HHHII...G............" @>

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

        let txResult1 : TxResult = {
            Status = Success
            BlockNumber = BlockNumber 5L
        }

        let txResult2 : TxResult = {
            Status = (TxActionNumber 7s, TxErrorCode.InsufficientChxBalance) |> TxActionError |> Failure
            BlockNumber = BlockNumber 5L
        }

        let txResult3 : TxResult = {
            Status = Success
            BlockNumber = BlockNumber 5L
        }

        let txResults =
            [txResult1; txResult2; txResult3]
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
                (AccountHash "DDD", AssetHash "EEE"), {HoldingState.Amount = AssetAmount 1M}
                (AccountHash "FFF", AssetHash "GGG"), {HoldingState.Amount = AssetAmount 2M}
            ]
            |> Map.ofList

        let accounts =
            [
                AccountHash "AAAA", {AccountState.ControllerAddress = ChainiumAddress "BBBB"}
                AccountHash "CCCC", {AccountState.ControllerAddress = ChainiumAddress "DDDD"}
            ]
            |> Map.ofList

        let assets =
            [
                AssetHash "EEEE", {AssetState.AssetCode = None; ControllerAddress = ChainiumAddress "FFFF"}
                AssetHash "GGGG", {AssetState.AssetCode = None; ControllerAddress = ChainiumAddress "HHHH"}
            ]
            |> Map.ofList

        let processingOutput =
            {
                ProcessingOutput.TxResults = txResults
                ChxBalances = chxBalances
                Holdings = holdings
                Accounts = accounts
                Assets = assets
            }

        let txSetRoot = "AAABBBCCC"

        let txResultSetRoot =
            [
                "AAAA...........E" // Tx 1
                "BBBB...G.......E" // Tx 2
                "CCCA...........E" // Tx 3
            ]
            |> String.Concat

        let stateRoot =
            [
                "HH...E...................G" // CHX balance 1
                "II...F...................H" // CHX balance 2
                "DDDEEE...A............" // Holding balance 1
                "FFFGGG...B............" // Holding balance 2
                "AAAABBBB" // Account controller 1
                "CCCCDDDD" // Account controller 2
                "EEEEFFFF" // Asset controller 1
                "GGGGHHHH" // Asset controller 2
            ]
            |> String.Concat

        let blockHash =
            [
                ".......A" // blockNumber
                "B" // previousBlockHash
                ".......C" // timestamp
                "D" // validator
                txSetRoot
                txResultSetRoot
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
        test <@ block.Header.TxSetRoot = MerkleTreeRoot txSetRoot @>
        test <@ block.Header.TxResultSetRoot = MerkleTreeRoot txResultSetRoot @>
        test <@ block.Header.StateRoot = MerkleTreeRoot stateRoot @>
        test <@ block.Header.Hash = BlockHash blockHash @>
        test <@ block.TxSet = [TxHash "AAA"; TxHash "BBB"; TxHash "CCC"] @>

    [<Fact>]
    let ``Blocks.assembleBlock and verify merkle proofs`` () =
        let wallet1 = Signing.generateWallet ()
        let wallet2 = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let blockNumber = BlockNumber 1L
        let timestamp = Utils.getUnixTimestamp () |> Timestamp

        let previousBlockHash =
            Signing.generateRandomBytes 64
            |> Hashing.hash
            |> BlockHash

        let txSet =
            ["Tx1"; "Tx2"; "Tx3"]
            |> List.map (Conversion.stringToBytes >> Hashing.hash >> TxHash)

        let txResult1 : TxResult = {
            Status = Success
            BlockNumber = BlockNumber 5L
        }

        let txResult2 : TxResult = {
            Status = (TxActionNumber 0s, TxErrorCode.InsufficientChxBalance) |> TxActionError |> Failure
            BlockNumber = BlockNumber 5L
        }

        let txResult3 : TxResult = {
            Status = Success
            BlockNumber = BlockNumber 5L
        }

        let txResults =
            [txResult1; txResult2; txResult3]
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
                (AccountHash "Acc1", AssetHash "Eq1"), {HoldingState.Amount = AssetAmount 100M}
                (AccountHash "Acc2", AssetHash "Eq2"), {HoldingState.Amount = AssetAmount 200M}
            ]
            |> Map.ofList

        let accounts =
            [
                AccountHash "AAAA", {AccountState.ControllerAddress = ChainiumAddress "BBBB"}
                AccountHash "CCCC", {AccountState.ControllerAddress = ChainiumAddress "DDDD"}
            ]
            |> Map.ofList

        let assets =
            [
                AssetHash "EEEE", {AssetState.AssetCode = None; ControllerAddress = ChainiumAddress "FFFF"}
                AssetHash "GGGG", {AssetState.AssetCode = None; ControllerAddress = ChainiumAddress "HHHH"}
            ]
            |> Map.ofList

        let processingOutput =
            {
                ProcessingOutput.TxResults = txResults
                ChxBalances = chxBalances
                Holdings = holdings
                Accounts = accounts
                Assets = assets
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

        test <@ txSetMerkleProofs = List.replicate 3 true @>

        let txResultSetMerkleProofs =
            txSet
            |> List.map (fun h -> h, txResults.[h])
            |> List.map (Blocks.createTxResultHash Hashing.decode Hashing.hash)
            |> Helpers.verifyMerkleProofs block.Header.TxResultSetRoot

        test <@ txResultSetMerkleProofs = List.replicate 3 true @>

        let stateMerkleProofs =
            [
                chxBalances
                |> Map.toList
                |> List.map (Blocks.createChxBalanceStateHash Hashing.decode Hashing.hash)

                holdings
                |> Map.toList
                |> List.map (fun ((accountHash, assetHash), state) ->
                    Blocks.createHoldingStateHash Hashing.decode Hashing.hash (accountHash, assetHash, state)
                )

                accounts
                |> Map.toList
                |> List.map (Blocks.createAccountStateHash Hashing.decode Hashing.hash)

                assets
                |> Map.toList
                |> List.map (Blocks.createAssetStateHash Hashing.decode Hashing.hash)
            ]
            |> List.concat
            |> Helpers.verifyMerkleProofs block.Header.StateRoot

        test <@ stateMerkleProofs = List.replicate 8 true @>

    [<Theory>]
    [<InlineData ("RIGHT_PREVIOUS_BLOCK_HASH", true)>]
    [<InlineData ("WRONG_PREVIOUS_BLOCK_HASH", false)>]
    let ``Blocks.isValidBlock`` (previousBlockHashInTestedBlock, expectedSuccess) =
        let wallet1 = Signing.generateWallet ()
        let wallet2 = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let blockNumber = BlockNumber 1L
        let timestamp = Utils.getUnixTimestamp () |> Timestamp

        let previousBlockHash =
            "RIGHT_PREVIOUS_BLOCK_HASH"
            |> Conversion.stringToBytes
            |> Hashing.hash
            |> BlockHash

        let txSet =
            ["Tx1"; "Tx2"; "Tx3"]
            |> List.map (Conversion.stringToBytes >> Hashing.hash >> TxHash)

        let txResult1 : TxResult = {
            Status = Success
            BlockNumber = BlockNumber 5L
        }

        let txResult2 : TxResult = {
            Status = (TxActionNumber 0s, TxErrorCode.InsufficientChxBalance) |> TxActionError |> Failure
            BlockNumber = BlockNumber 5L
        }

        let txResult3 : TxResult = {
            Status = Success
            BlockNumber = BlockNumber 5L
        }

        let txResults =
            [txResult1; txResult2; txResult3]
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
                (AccountHash "Acc1", AssetHash "Eq1"), {HoldingState.Amount = AssetAmount 100M}
                (AccountHash "Acc2", AssetHash "Eq2"), {HoldingState.Amount = AssetAmount 200M}
            ]
            |> Map.ofList

        let accounts =
            [
                AccountHash "AAAA", {AccountState.ControllerAddress = ChainiumAddress "BBBB"}
                AccountHash "CCCC", {AccountState.ControllerAddress = ChainiumAddress "DDDD"}
            ]
            |> Map.ofList

        let assets =
            [
                AssetHash "EEEE", {AssetState.AssetCode = None; ControllerAddress = ChainiumAddress "FFFF"}
                AssetHash "GGGG", {AssetState.AssetCode = None; ControllerAddress = ChainiumAddress "HHHH"}
            ]
            |> Map.ofList

        let processingOutput =
            {
                ProcessingOutput.TxResults = txResults
                ChxBalances = chxBalances
                Holdings = holdings
                Accounts = accounts
                Assets = assets
            }

        let assembledBlock =
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

        let testedBlock =
            Blocks.assembleBlock
                Hashing.decode
                Hashing.hash
                Hashing.merkleTree
                validatorWallet.Address
                blockNumber
                timestamp
                (previousBlockHashInTestedBlock |> Conversion.stringToBytes |> Hashing.hash |> BlockHash)
                txSet
                processingOutput

        // ACT
        let isValid =
            Blocks.isValidBlock
                Hashing.decode
                Hashing.hash
                Hashing.merkleTree
                previousBlockHash
                testedBlock

        // ASSERT
        test <@ isValid = expectedSuccess @>
