namespace Own.Blockchain.Public.Core.Tests

open System
open Xunit
open Xunit.Abstractions
open Swensen.Unquote
open Own.Common
open Own.Blockchain.Public.Core
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Core.Events
open Own.Blockchain.Public.Crypto
open Own.Blockchain.Public.Core.Tests.ConsensusTestHelpers

type ConsensusTests(output : ITestOutputHelper) =

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Supporting functions
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Fact>]
    member __.``Consensus.createConsensusMessageHash for Propose consensus message`` () =
        // ARRANGE
        let blockNumber = BlockNumber 2L
        let blockHash = BlockHash "HHH"
        let consensusRound = ConsensusRound 1

        let block : Block =
            {
                Header =
                    {
                        BlockHeader.Number = blockNumber
                        Hash = blockHash
                        PreviousHash = BlockHash ""
                        ConfigurationBlockNumber = BlockNumber 0L
                        Timestamp = Timestamp 0L
                        ProposerAddress = BlockchainAddress ""
                        TxSetRoot = MerkleTreeRoot ""
                        TxResultSetRoot = MerkleTreeRoot ""
                        EquivocationProofsRoot = MerkleTreeRoot ""
                        EquivocationProofResultsRoot = MerkleTreeRoot ""
                        StateRoot = MerkleTreeRoot ""
                        StakingRewardsRoot = MerkleTreeRoot ""
                        ConfigurationRoot = MerkleTreeRoot ""
                    }
                TxSet = []
                EquivocationProofs = []
                StakingRewards = []
                Configuration = None
            }

        let consensusMessage = ConsensusMessage.Propose (block, consensusRound)

        let expectedHash =
            [
                ".......B" // Block number
                "...A" // Consensus round
                "." // Message discriminator
                "HHH" // Block hash
                "...A" // Valid consensus round
            ]
            |> String.Concat

        // ACT
        let actualHash =
            Consensus.createConsensusMessageHash
                DummyHash.decode
                DummyHash.create
                blockNumber
                consensusRound
                consensusMessage

        // ASSERT
        test <@ actualHash = expectedHash @>

    [<Fact>]
    member __.``Consensus.createConsensusMessageHash for Vote consensus message`` () =
        // ARRANGE
        let blockNumber = BlockNumber 2L
        let blockHash = BlockHash "HHH"
        let consensusRound = ConsensusRound 1

        let consensusMessage = ConsensusMessage.Vote (Some blockHash)

        let expectedHash =
            [
                ".......B" // Block number
                "...A" // Consensus round
                "A" // Message discriminator
                "HHH" // Block hash
            ]
            |> String.Concat

        // ACT
        let actualHash =
            Consensus.createConsensusMessageHash
                DummyHash.decode
                DummyHash.create
                blockNumber
                consensusRound
                consensusMessage

        // ASSERT
        test <@ actualHash = expectedHash @>

    [<Fact>]
    member __.``Consensus.createConsensusMessageHash for Commit consensus message`` () =
        // ARRANGE
        let blockNumber = BlockNumber 2L
        let blockHash = BlockHash "HHH"
        let consensusRound = ConsensusRound 1

        let consensusMessage = ConsensusMessage.Commit (Some blockHash)

        let expectedHash =
            [
                ".......B" // Block number
                "...A" // Consensus round
                "B" // Message discriminator
                "HHH" // Block hash
            ]
            |> String.Concat

        // ACT
        let actualHash =
            Consensus.createConsensusMessageHash
                DummyHash.decode
                DummyHash.create
                blockNumber
                consensusRound
                consensusMessage

        // ASSERT
        test <@ actualHash = expectedHash @>

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Happy Path
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Fact>]
    member __.``Consensus - Proposer proposes a block`` () =
        // ARRANGE
        let net = new ConsensusSimulationNetwork()

        let validators =
            [1 .. 10]
            |> List.map (fun _ -> (Signing.generateWallet ()).Address)

        // ACT
        net.StartConsensus validators

        // ASSERT
        net.PrintTheState(output.WriteLine)

        test <@ net.Messages.Count = 1 @>
        test <@ net.Messages |> Seq.forall (snd >> isPropose) @>

    [<Fact>]
    member __.``Consensus - Validators vote for valid block`` () =
        // ARRANGE
        let net = new ConsensusSimulationNetwork()

        let validators =
            [1 .. 10]
            |> List.map (fun _ -> (Signing.generateWallet ()).Address)

        net.StartConsensus validators

        // ACT
        net.DeliverMessages() // Deliver Propose message

        // ASSERT
        net.PrintTheState(output.WriteLine)

        test <@ net.Messages.Count = 10 @>
        test <@ net.Messages |> Seq.forall (snd >> isVoteForBlock) @>

    [<Fact>]
    member __.``Consensus - Validators commit valid block`` () =
        // ARRANGE
        let net = new ConsensusSimulationNetwork()

        let validators =
            [1 .. 10]
            |> List.map (fun _ -> (Signing.generateWallet ()).Address)

        net.StartConsensus validators
        net.DeliverMessages() // Deliver Propose message

        // ACT
        net.DeliverMessages() // Deliver Vote messages

        // ASSERT
        net.PrintTheState(output.WriteLine)

        test <@ net.Messages.Count = 10 @>
        test <@ net.Messages |> Seq.forall (snd >> isCommitForBlock) @>

    [<Fact>]
    member __.``Consensus - Proposer proposes next block`` () =
        // ARRANGE
        let net = new ConsensusSimulationNetwork()

        let validators =
            [1 .. 10]
            |> List.map (fun _ -> (Signing.generateWallet ()).Address)

        net.StartConsensus validators
        net.DeliverMessages() // Deliver Propose message
        net.DeliverMessages() // Deliver Vote messages

        // ACT
        net.DeliverMessages() // Deliver Commit messages

        // ASSERT
        net.PrintTheState(output.WriteLine)

        test <@ net.Messages.Count = 1 @>
        test <@ net.Messages |> Seq.forall (snd >> isPropose) @>

    [<Fact>]
    member __.``Consensus - 100 blocks committed`` () =
        // ARRANGE
        let net = new ConsensusSimulationNetwork()

        let validators =
            [1 .. 10]
            |> List.map (fun _ -> (Signing.generateWallet ()).Address)

        // ACT
        net.StartConsensus validators
        for _ in [1 .. 100] do
            net.DeliverMessages() // Deliver Propose message
            net.DeliverMessages() // Deliver Vote messages
            net.DeliverMessages() // Deliver Commit messages

        // ASSERT
        net.PrintTheState(output.WriteLine)

        test <@ net.Messages.Count = 1 @>
        let _, envelope = net.Messages |> Seq.head
        let block =
            match envelope.ConsensusMessage with
            | Propose (block, _) -> Some block
            | _ -> None

        test <@ envelope.BlockNumber = BlockNumber 101L @>
        test <@ block <> None @>

        for s in net.States.Values do
            test <@ s.Decisions.Count = 100 @>

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // <= 2f + 1
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Fact>]
    member __.``Consensus - Validators don't vote for block without receiving proposal`` () =
        // ARRANGE
        let net = new ConsensusSimulationNetwork()

        let validators =
            [1 .. 10]
            |> List.map (fun _ -> (Signing.generateWallet ()).Address)

        net.StartConsensus validators

        // ACT
        net.DeliverMessages(validators |> List.take 6) // Deliver Propose message

        // ASSERT
        net.PrintTheState(output.WriteLine)

        test <@ net.Messages.Count = 6 @>
        test <@ net.Messages |> Seq.forall (snd >> isVoteForBlock) @>

    [<Fact>]
    member __.``Consensus - Validators don't commit block without 2f + 1 votes`` () =
        // ARRANGE
        let net = new ConsensusSimulationNetwork()

        let validators =
            [1 .. 10]
            |> List.map (fun _ -> (Signing.generateWallet ()).Address)

        net.StartConsensus validators
        net.DeliverMessages() // Deliver Propose message

        for _ in [1 .. 4] do
            net.Messages.RemoveAt(0) // Simulate lost Vote messages

        // ACT
        net.DeliverMessages() // Deliver Vote messages

        // ASSERT
        net.PrintTheState(output.WriteLine)

        test <@ net.Messages.Count = 0 @>

    [<Fact>]
    member __.``Consensus - Validators don't decide for block without 2f + 1 commits`` () =
        // ARRANGE
        let net = new ConsensusSimulationNetwork()

        let validators =
            [1 .. 10]
            |> List.map (fun _ -> (Signing.generateWallet ()).Address)

        net.StartConsensus validators
        net.DeliverMessages() // Deliver Propose message
        net.DeliverMessages() // Deliver Vote messages

        for _ in [1 .. 4] do
            net.Messages.RemoveAt(0) // Simulate lost Commit messages

        // ACT
        net.DeliverMessages() // Deliver Commit messages

        // ASSERT
        net.PrintTheState(output.WriteLine)

        test <@ net.Messages.Count = 0 @>

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Timeouts
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Fact>]
    member __.``Consensus - Validators don't vote for block if proposal timeouts`` () =
        // ARRANGE
        let net = new ConsensusSimulationNetwork()

        let validators =
            [1 .. 10]
            |> List.map (fun _ -> (Signing.generateWallet ()).Address)

        net.StartConsensus validators
        net.Messages.Clear()

        // ACT
        for s in net.States do
            Timeout (BlockNumber 1L, ConsensusRound 0, ConsensusStep.Propose)
            |> s.Value.HandleConsensusCommand

        // ASSERT
        net.PrintTheState(output.WriteLine)

        test <@ net.Messages.Count = 10 @>
        test <@ net.Messages |> Seq.forall (snd >> isVoteForNone) @>

    [<Fact>]
    member __.``Consensus - Validators don't commit block if votes timeout`` () =
        // ARRANGE
        let net = new ConsensusSimulationNetwork()

        let validators =
            [1 .. 10]
            |> List.map (fun _ -> (Signing.generateWallet ()).Address)

        net.StartConsensus validators
        net.DeliverMessages() // Deliver Propose message
        net.Messages.Clear()

        // ACT
        for s in net.States do
            Timeout (BlockNumber 1L, ConsensusRound 0, ConsensusStep.Vote)
            |> s.Value.HandleConsensusCommand

        // ASSERT
        net.PrintTheState(output.WriteLine)

        test <@ net.Messages.Count = 10 @>
        test <@ net.Messages |> Seq.forall (snd >> isCommitForNone) @>

    [<Fact>]
    member __.``Consensus - Validators don't decide for block if commits timeout`` () =
        // ARRANGE
        let net = new ConsensusSimulationNetwork()

        let validators =
            [1 .. 10]
            |> List.map (fun _ -> (Signing.generateWallet ()).Address)

        net.StartConsensus validators
        net.DeliverMessages() // Deliver Propose message
        net.DeliverMessages() // Deliver Vote messages
        net.Messages.Clear()

        // ACT
        for s in net.States do
            Timeout (BlockNumber 1L, ConsensusRound 0, ConsensusStep.Commit)
            |> s.Value.HandleConsensusCommand

        // ASSERT
        net.PrintTheState(output.WriteLine)

        test <@ net.Messages.Count = 1 @>
        test <@ net.Messages |> Seq.forall (snd >> isPropose) @>

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Equivocation
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Fact>]
    member __.``Consensus - Validators detect equivocation`` () =
        // ARRANGE
        let net = new ConsensusSimulationNetwork()

        let validators =
            [1 .. 10]
            |> List.map (fun _ -> (Signing.generateWallet ()).Address)

        let byzantineValidator = validators.[DateTime.Now.Second % 10]
        let equivocationMessage = ConsensusMessage.Vote Option<BlockHash>.None

        net.StartConsensus validators
        net.DeliverMessages() // Deliver Propose message

        net.Messages
        |> Seq.filter (fun (a, _) -> a = byzantineValidator)
        |> Seq.exactlyOne
        |> createEquivocationMessage equivocationMessage
        |> fun m -> net.Messages.Add(byzantineValidator, m)

        // ACT
        net.DeliverMessages() // Deliver Vote messages

        // ASSERT
        net.PrintTheState(output.WriteLine)

        test <@ net.Messages.Count = 10 @>
        test <@ net.Messages |> Seq.forall (snd >> isCommitForBlock) @>
        test <@ net.Events.Count = 10 @>

        let equivocationProof, detectedValidator =
            net.Events
            |> Seq.distinct
            |> Seq.exactlyOne
            |> function
                | AppEvent.EquivocationProofDetected (proof, address) -> proof, address
                | _ -> failwith "Unexpected event type."

        let proofBlockHash1 =
            equivocationProof.BlockHash1
            |> Option.ofObj
            |> Option.map BlockHash
            |> ConsensusMessage.Vote

        test <@ proofBlockHash1 = equivocationMessage @>
        test <@ equivocationProof.Signature1 = (byzantineValidator.Value + "_EQ") @>
        test <@ detectedValidator = byzantineValidator @>

    [<Fact>]
    member __.``Consensus - Blacklisted validator's messages are ignored`` () =
        // ARRANGE
        let validators =
            [1 .. 10]
            |> List.map (fun _ -> (Signing.generateWallet ()).Address)

        let blacklistedValidator = validators.[DateTime.Now.Second % 10]
        let mutable ignoredMessageCount = 0
        let isValidatorBlacklisted (validatorAddress, _, _) =
            if validatorAddress = blacklistedValidator then
                ignoredMessageCount <- ignoredMessageCount + 1
                true
            else
                false

        let net = new ConsensusSimulationNetwork(isValidatorBlacklisted = isValidatorBlacklisted)

        net.StartConsensus validators
        net.DeliverMessages() // Deliver Propose message

        // ACT
        net.DeliverMessages() // Deliver Vote messages

        // ASSERT
        net.PrintTheState(output.WriteLine)

        test <@ net.Messages.Count = 10 @>
        test <@ net.Messages |> Seq.forall (snd >> isCommitForBlock) @>
        test <@ ignoredMessageCount = 10 @>

