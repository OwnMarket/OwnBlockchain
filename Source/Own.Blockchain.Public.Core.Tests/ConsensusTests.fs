namespace Own.Blockchain.Public.Core.Tests

open System
open Xunit
open Xunit.Abstractions
open Swensen.Unquote
open Own.Common.FSharp
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
    member __.``Consensus - Happy Path - Proposer proposes a block`` () =
        // ARRANGE
        let validatorCount = 10
        let validators = List.init validatorCount (fun _ -> (Signing.generateWallet ()).Address)

        let net = new ConsensusSimulationNetwork(validators)

        // ACT
        net.StartConsensus()

        // ASSERT
        net.PrintTheState(output.WriteLine)

        test <@ net.Messages.Count = 1 @>
        test <@ net.Messages |> Seq.forall isPropose @>

    [<Fact>]
    member __.``Consensus - Happy Path - Validators vote for valid block`` () =
        // ARRANGE
        let validatorCount = 10
        let validators = List.init validatorCount (fun _ -> (Signing.generateWallet ()).Address)

        let net = new ConsensusSimulationNetwork(validators)

        net.StartConsensus()

        // ACT
        net.DeliverMessages() // Deliver Propose message

        // ASSERT
        net.PrintTheState(output.WriteLine)

        test <@ net.Messages.Count = validatorCount @>
        test <@ net.Messages |> Seq.forall isVoteForBlock @>

    [<Fact>]
    member __.``Consensus - Happy Path - Validators commit valid block`` () =
        // ARRANGE
        let validatorCount = 10
        let validators = List.init validatorCount (fun _ -> (Signing.generateWallet ()).Address)

        let net = new ConsensusSimulationNetwork(validators)

        net.StartConsensus()
        net.DeliverMessages() // Deliver Propose message

        // ACT
        net.DeliverMessages() // Deliver Vote messages

        // ASSERT
        net.PrintTheState(output.WriteLine)

        test <@ net.Messages.Count = validatorCount @>
        test <@ net.Messages |> Seq.forall isCommitForBlock @>

    [<Fact>]
    member __.``Consensus - Happy Path - Proposer proposes next block`` () =
        // ARRANGE
        let validatorCount = 10
        let validators = List.init validatorCount (fun _ -> (Signing.generateWallet ()).Address)

        let net = new ConsensusSimulationNetwork(validators)

        net.StartConsensus()
        net.DeliverMessages() // Deliver Propose message
        net.DeliverMessages() // Deliver Vote messages

        // ACT
        net.DeliverMessages() // Deliver Commit messages

        // ASSERT
        net.PrintTheState(output.WriteLine)

        test <@ net.Messages.Count = 1 @>
        test <@ net.Messages |> Seq.forall isPropose @>

    [<Fact>]
    member __.``Consensus - Happy Path - 100 blocks committed`` () =
        // ARRANGE
        let validatorCount = 10
        let validators = List.init validatorCount (fun _ -> (Signing.generateWallet ()).Address)

        let net = new ConsensusSimulationNetwork(validators)

        // ACT
        net.StartConsensus()
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

        for v in validators do
            test <@ net.Decisions.[v].Count = 100 @>

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Qualified Majority
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Fact>]
    member __.``Consensus - Qualified Majority - Validators don't vote for block without receiving proposal`` () =
        // ARRANGE
        let validatorCount = 10
        let validators = List.init validatorCount (fun _ -> (Signing.generateWallet ()).Address) |> List.sort
        let reachableValidators = validators |> List.take 6

        let net = new ConsensusSimulationNetwork(validators)

        net.StartConsensus()

        // ACT
        net.DeliverMessages(fun (s, r, m) -> reachableValidators |> List.contains r) // Deliver Propose message

        // ASSERT
        net.PrintTheState(output.WriteLine)

        test <@ net.Messages.Count = 6 @>
        test <@ net.Messages |> Seq.forall isVoteForBlock @>

    [<Fact>]
    member __.``Consensus - Qualified Majority - Validators don't commit block without 2f + 1 votes`` () =
        // ARRANGE
        let validatorCount = 10
        let validators = List.init validatorCount (fun _ -> (Signing.generateWallet ()).Address)

        let net = new ConsensusSimulationNetwork(validators)

        net.StartConsensus()
        net.DeliverMessages() // Deliver Propose message

        for _ in [1 .. 4] do
            net.Messages.RemoveAt(0) // Simulate lost Vote messages

        // ACT
        net.DeliverMessages() // Deliver Vote messages

        // ASSERT
        net.PrintTheState(output.WriteLine)

        test <@ net.Messages.Count = 0 @>

    [<Fact>]
    member __.``Consensus - Qualified Majority - Validators don't decide for block without 2f + 1 commits`` () =
        // ARRANGE
        let validatorCount = 10
        let validators = List.init validatorCount (fun _ -> (Signing.generateWallet ()).Address)

        let net = new ConsensusSimulationNetwork(validators)

        net.StartConsensus()
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
    member __.``Consensus - Timeouts - Validators don't vote for block if proposal timeouts`` () =
        // ARRANGE
        let validatorCount = 10
        let validators = List.init validatorCount (fun _ -> (Signing.generateWallet ()).Address)

        let net = new ConsensusSimulationNetwork(validators)

        net.StartConsensus()
        net.Messages.Clear()

        // ACT
        for s in net.States do
            Timeout (BlockNumber 1L, ConsensusRound 0, ConsensusStep.Propose)
            |> s.Value.HandleConsensusCommand

        // ASSERT
        net.PrintTheState(output.WriteLine)

        test <@ net.Messages.Count = validatorCount @>
        test <@ net.Messages |> Seq.forall isVoteForNone @>

    [<Fact>]
    member __.``Consensus - Timeouts - Validators don't commit block if votes timeout`` () =
        // ARRANGE
        let validatorCount = 10
        let validators = List.init validatorCount (fun _ -> (Signing.generateWallet ()).Address)

        let net = new ConsensusSimulationNetwork(validators)

        net.StartConsensus()
        net.DeliverMessages() // Deliver Propose message
        net.Messages.Clear()

        // ACT
        for s in net.States do
            Timeout (BlockNumber 1L, ConsensusRound 0, ConsensusStep.Vote)
            |> s.Value.HandleConsensusCommand

        // ASSERT
        net.PrintTheState(output.WriteLine)

        test <@ net.Messages.Count = validatorCount @>
        test <@ net.Messages |> Seq.forall isCommitForNone @>

    [<Fact>]
    member __.``Consensus - Timeouts - Validators don't decide for block if commits timeout`` () =
        // ARRANGE
        let validatorCount = 10
        let validators = List.init validatorCount (fun _ -> (Signing.generateWallet ()).Address)

        let net = new ConsensusSimulationNetwork(validators)

        net.StartConsensus()
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
        test <@ net.Messages |> Seq.forall isPropose @>

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Equivocation
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Fact>]
    member __.``Consensus - Equivocation - Proof detected`` () =
        // ARRANGE
        let validatorCount = 10
        let validators = List.init validatorCount (fun _ -> (Signing.generateWallet ()).Address) |> List.sort
        let proposer = validators |> Validators.getProposer (BlockNumber 1L) (ConsensusRound 0)
        test <@ proposer = validators.[1] @>

        let byzantineValidator =
            validators
            |> List.except [proposer]
            |> List.shuffle
            |> List.head

        let equivocationMessage = ConsensusMessage.Vote Option<BlockHash>.None

        let net = new ConsensusSimulationNetwork(validators)

        net.StartConsensus()
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

        test <@ net.Messages.Count = validatorCount @>
        test <@ net.Messages |> Seq.forall isCommitForBlock @>
        test <@ net.Events.Count = validatorCount @>

        let equivocationProof, detectedValidator =
            net.Events
            |> Seq.map snd
            |> Seq.distinct
            |> Seq.exactlyOne
            |> function
                | AppEvent.EquivocationProofDetected (proof, address) -> proof, address
                | _ -> failwith "Unexpected event type"

        let proofBlockHash1 =
            equivocationProof.BlockHash1
            |> Option.ofObj
            |> Option.map BlockHash
            |> ConsensusMessage.Vote

        test <@ proofBlockHash1 = equivocationMessage @>
        test <@ equivocationProof.Signature1.EndsWith(byzantineValidator.Value + "_EQ") @>
        test <@ detectedValidator = byzantineValidator @>

    [<Fact>]
    member __.``Consensus - Equivocation - Blacklisted validator's messages are ignored`` () =
        // ARRANGE
        let validatorCount = 10
        let validators = List.init validatorCount (fun _ -> (Signing.generateWallet ()).Address) |> List.sort
        let proposer = validators |> Validators.getProposer (BlockNumber 1L) (ConsensusRound 0)
        test <@ proposer = validators.[1] @>

        let blacklistedValidator =
            validators
            |> List.except [proposer]
            |> List.shuffle
            |> List.head

        let mutable ignoredMessageCount = 0
        let isValidatorBlacklisted (validatorAddress, _, _) =
            if validatorAddress = blacklistedValidator then
                ignoredMessageCount <- ignoredMessageCount + 1
                true
            else
                false

        let net = new ConsensusSimulationNetwork(validators, isValidatorBlacklisted = isValidatorBlacklisted)

        net.StartConsensus()
        net.DeliverMessages() // Deliver Propose message

        // ACT
        net.DeliverMessages() // Deliver Vote messages

        // ASSERT
        net.PrintTheState(output.WriteLine)

        test <@ net.Messages.Count = validatorCount @>
        test <@ net.Messages |> Seq.forall isCommitForBlock @>
        test <@ ignoredMessageCount = validatorCount @>

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Distributed Test Cases
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    [<Fact>]
    member __.``Consensus - Distributed Test Cases: CF1`` () =
        // ARRANGE
        let validators = List.init 4 (fun _ -> (Signing.generateWallet ()).Address) |> List.sort
        let proposer = validators |> Validators.getProposer (BlockNumber 1L) (ConsensusRound 0)
        test <@ proposer = validators.[1] @>
        let reachableValidators = validators |> List.except [validators.[3]]

        let net = new ConsensusSimulationNetwork(validators)

        // ACT
        net.StartConsensus()
        net.CrashValidator validators.[3]

        test <@ net.Messages.Count = 1 @>
        test <@ net.Messages.[0] |> fst = proposer @>
        let proposedBlock =
            net.Messages.[0]
            |> snd
            |> fun m -> m.ConsensusMessage
            |> function Propose (block, _) -> block | _ -> failwith "Propose message expected"
        test <@ proposedBlock.Header.Number = BlockNumber 1L @>

        net.DeliverMessages(fun (s, r, m) -> s <> validators.[3] && r <> validators.[3]) // Deliver Propose message
        test <@ net.Messages.Count = reachableValidators.Length @>
        test <@ net.Messages |> Seq.forall isVoteForBlock @>

        net.DeliverMessages(fun (s, r, m) -> s <> validators.[3] && r <> validators.[3]) // Deliver Vote messages
        test <@ net.Messages.Count = reachableValidators.Length @>
        test <@ net.Messages |> Seq.forall isCommitForBlock @>

        let committers = net.Messages |> Seq.map fst |> Seq.toList |> List.sort
        test <@ committers = reachableValidators @>

        let committedBlockNumber, committedRound =
            net.Messages
            |> Seq.map (fun (_, e) -> e.BlockNumber, e.Round)
            |> Seq.distinct
            |> Seq.exactlyOne
        test <@ committedBlockNumber = BlockNumber 1L @>
        test <@ committedRound = ConsensusRound 0 @>

        net.DeliverMessages(fun (s, r, m) -> s <> validators.[3] && r <> validators.[3]) // Deliver Commit messages
        test <@ net.Messages.Count = 1 @>

        // ASSERT
        net.PrintTheState(output.WriteLine)

        test <@ net.Decisions.[validators.[0]].[BlockNumber 1L] = proposedBlock @>
        test <@ net.Decisions.[validators.[1]].[BlockNumber 1L] = proposedBlock @>
        test <@ net.Decisions.[validators.[2]].[BlockNumber 1L] = proposedBlock @>
        test <@ net.Decisions.[validators.[3]].Count = 0 @>

        net, proposedBlock // Return the simulation state for dependent tests.

    [<Fact>]
    member __.``Consensus - Distributed Test Cases: CF2`` () =
        // ARRANGE
        let net, proposedBlock = __.``Consensus - Distributed Test Cases: CF1`` ()
        let validators = net.Validators

        // ACT
        net.RecoverValidator validators.[3]

        // ASSERT
        net.PrintTheState(output.WriteLine)

        test <@ net.Decisions.[validators.[3]].[BlockNumber 1L] = proposedBlock @>
        test <@ net.States.[validators.[3]].Variables.BlockNumber = BlockNumber 2L @>
        test <@ net.States.[validators.[3]].Variables.ConsensusRound = ConsensusRound 0 @>
        test <@ net.States.[validators.[3]].Variables.ConsensusStep = ConsensusStep.Propose @>

        net, proposedBlock // Return the simulation state for dependent tests.

    [<Fact>]
    member __.``Consensus - Distributed Test Cases: CF3`` () =
        // ARRANGE
        let validators = List.init 4 (fun _ -> (Signing.generateWallet ()).Address) |> List.sort
        let proposer = validators |> Validators.getProposer (BlockNumber 1L) (ConsensusRound 0)
        test <@ proposer = validators.[1] @>
        let reachableValidators = validators |> List.except [validators.[3]]

        let net = new ConsensusSimulationNetwork(validators)

        // ACT
        net.StartConsensus()
        net.CrashValidator validators.[3]

        test <@ net.Messages.Count = 1 @>
        test <@ net.Messages.[0] |> fst = proposer @>
        let proposedBlock =
            net.Messages.[0]
            |> snd
            |> fun m -> m.ConsensusMessage
            |> function Propose (block, _) -> block | _ -> failwith "Propose message expected"
        test <@ proposedBlock.Header.Number = BlockNumber 1L @>

        net.DeliverMessages(fun (s, r, m) -> s <> validators.[3] && r <> validators.[3]) // Deliver Propose message
        test <@ net.Messages.Count = reachableValidators.Length @>
        test <@ net.Messages |> Seq.forall isVoteForBlock @>

        net.DeliverMessages(fun (s, r, m) -> s <> validators.[3] && r <> validators.[3]) // Deliver Vote messages
        test <@ net.Messages.Count = reachableValidators.Length @>
        test <@ net.Messages |> Seq.forall isCommitForBlock @>

        let committers = net.Messages |> Seq.map fst |> Seq.toList |> List.sort
        test <@ committers = reachableValidators @>

        let committedBlockNumber, committedRound =
            net.Messages
            |> Seq.map (fun (_, e) -> e.BlockNumber, e.Round)
            |> Seq.distinct
            |> Seq.exactlyOne
        test <@ committedBlockNumber = BlockNumber 1L @>
        test <@ committedRound = ConsensusRound 0 @>

        net.DeliverMessages(
            (fun (s, r, m) -> s <> validators.[3] && r = validators.[0]),
            (fun (s, r, m) -> s <> validators.[3] && (r = validators.[1] || r = validators.[2])) // Delayed messages
        ) // Deliver Commit messages
        test <@ net.Messages.Count = 3 @>
        test <@ net.Messages |> Seq.forall isCommitForBlock @>

        // ASSERT
        net.PrintTheState(output.WriteLine)

        test <@ net.Decisions.[validators.[0]].[BlockNumber 1L] = proposedBlock @>
        test <@ net.Decisions.[validators.[1]].ContainsKey(BlockNumber 1L) = false @>
        test <@ net.Decisions.[validators.[2]].ContainsKey(BlockNumber 1L) = false @>
        test <@ net.Decisions.[validators.[3]].Count = 0 @>

        net, proposedBlock // Return the simulation state for dependent tests.

    [<Fact>]
    member __.``Consensus - Distributed Test Cases: CF4`` () =
        // ARRANGE
        let net, proposedBlock = __.``Consensus - Distributed Test Cases: CF3`` ()
        let validators = net.Validators

        // ACT
        net.CrashValidator validators.[0]
        net.DeliverMessages(
            fun (s, r, m) -> (s = validators.[1] || s = validators.[2]) && (r = validators.[1] || r = validators.[2])
        ) // Deliver delayed messages
        net.RecoverValidator validators.[3]
        test <@ net.Messages.Count = 2 @>
        test <@ net.Messages |> Seq.forall (fun (s, _) -> s = validators.[3]) @>
        test <@ net.Messages.[0] |> isVoteForBlock @>
        test <@ net.Messages.[1] |> isCommitForBlock @>
        net.DeliverMessages() // Deliver V3's messages

        // ASSERT
        net.PrintTheState(output.WriteLine)

        test <@ net.Decisions.[validators.[0]].[BlockNumber 1L] = proposedBlock @>
        test <@ net.Decisions.[validators.[1]].[BlockNumber 1L] = proposedBlock @>
        test <@ net.Decisions.[validators.[2]].[BlockNumber 1L] = proposedBlock @>
        test <@ net.Decisions.[validators.[3]].[BlockNumber 1L] = proposedBlock @>

        net, proposedBlock // Return the simulation state for dependent tests.

    [<Fact>]
    member __.``Consensus - Distributed Test Cases: CF4a`` () =
        // ARRANGE
        let net, proposedBlock = __.``Consensus - Distributed Test Cases: CF3`` ()
        let validators = net.Validators

        // ACT
        net.CrashValidator validators.[0]

        net.Messages.Clear()

        net.RecoverValidator validators.[3]
        test <@ net.Messages.Count = 2 @>
        test <@ net.Messages |> Seq.forall (fun (s, _) -> s = validators.[3]) @>
        test <@ net.Messages.[0] |> isVoteForBlock @>
        test <@ net.Messages.[1] |> isCommitForBlock @>
        test <@ net.Events.Count = 1 @> // Only V0's commit is there
        test <@ net.DecisionCount = 1 @> // No new decisions available

        net.DeliverMessages() // Deliver V3's messages
        test <@ net.Messages.Count = 0 @>
        test <@ net.Events.Count = 2 @>

        net.PropagateBlock validators.[3] (BlockNumber 1L)
        net.States.[validators.[1]].HandleConsensusCommand Synchronize
        net.States.[validators.[2]].HandleConsensusCommand Synchronize

        test <@ net.States.[validators.[1]].Variables.BlockNumber = BlockNumber 2L @>
        test <@ net.States.[validators.[2]].Variables.BlockNumber = BlockNumber 2L @>

        // ASSERT
        net.PrintTheState(output.WriteLine)

        test <@ net.Decisions.[validators.[0]].[BlockNumber 1L] = proposedBlock @>
        test <@ net.Decisions.[validators.[1]].[BlockNumber 1L] = proposedBlock @>
        test <@ net.Decisions.[validators.[2]].[BlockNumber 1L] = proposedBlock @>
        test <@ net.Decisions.[validators.[3]].[BlockNumber 1L] = proposedBlock @>

        net, proposedBlock // Return the simulation state for dependent tests.

    [<Fact>]
    member __.``Consensus - Distributed Test Cases: MF1`` () =
        // ARRANGE
        let net, proposedBlock = __.``Consensus - Distributed Test Cases: CF3`` ()
        let validators = net.Validators

        // ACT
        net.DeliverMessages(fun (s, r, m) -> s = r) // Deliver own messages only

        net.CrashValidator validators.[0]

        net.ResetValidator validators.[2]

        test <@ net.States.[validators.[1]].MessageCounts = (1, 3, 1) @>
        test <@ net.States.[validators.[2]].MessageCounts = (0, 0, 0) @>

        net.RecoverValidator validators.[3]
        test <@ net.Messages.Count = 2 @>
        test <@ net.Messages.[0] |> isVoteForBlock @>
        test <@ net.Messages.[1] |> isCommitForBlock @>

        test <@ net.States.[validators.[3]].MessageCounts = (1, 3, 1) @>

        net.DeliverMessages()
        test <@ net.Messages.Count = 0 @>

        test <@ net.States.[validators.[1]].MessageCounts = (1, 4, 2) @>
        test <@ net.States.[validators.[3]].MessageCounts = (1, 4, 2) @>
        test <@ net.States.[validators.[2]].MessageCounts = (0, 1, 1) @>

        test <@ net.DecisionCount = 1 @> // No new decision available

        net.RequestConsensusState validators.[2]
        test <@ net.Messages.Count = 2 @>
        test <@ net.Messages.[0] |> isVoteForBlock @>
        test <@ net.Messages.[1] |> isCommitForBlock @>

        net.DeliverMessages ()
        test <@ net.Messages.Count = 1 @>
        test <@ net.Messages |> Seq.forall isPropose @>

        test <@ net.States.[validators.[1]].Variables.BlockNumber = BlockNumber 2L @>
        test <@ net.States.[validators.[2]].Variables.BlockNumber = BlockNumber 2L @>
        test <@ net.States.[validators.[3]].Variables.BlockNumber = BlockNumber 2L @>

        // ASSERT
        net.PrintTheState(output.WriteLine)

        test <@ net.Decisions.[validators.[0]].[BlockNumber 1L] = proposedBlock @>
        test <@ net.Decisions.[validators.[1]].[BlockNumber 1L] = proposedBlock @>
        test <@ net.Decisions.[validators.[2]].[BlockNumber 1L] = proposedBlock @>
        test <@ net.Decisions.[validators.[3]].[BlockNumber 1L] = proposedBlock @>

        net, proposedBlock // Return the simulation state for dependent tests.

    [<Fact>]
    member __.``Consensus - Distributed Test Cases: CF5`` () =
        // ARRANGE
        let validators = List.init 4 (fun _ -> (Signing.generateWallet ()).Address) |> List.sort
        let proposer = validators |> Validators.getProposer (BlockNumber 1L) (ConsensusRound 0)
        test <@ proposer = validators.[1] @>
        let reachableValidators = validators |> List.except [validators.[3]]

        let net = new ConsensusSimulationNetwork(validators)

        // ACT
        net.StartConsensus()
        net.CrashValidator validators.[3]

        test <@ net.Messages.Count = 1 @>
        test <@ net.Messages.[0] |> fst = proposer @>
        let proposedBlock =
            net.Messages.[0]
            |> snd
            |> fun m -> m.ConsensusMessage
            |> function Propose (block, _) -> block | _ -> failwith "Propose message expected"
        test <@ proposedBlock.Header.Number = BlockNumber 1L @>

        net.DeliverMessages(fun (s, r, m) -> s <> validators.[3] && r <> validators.[3]) // Deliver Propose message
        test <@ net.Messages.Count = reachableValidators.Length @>
        test <@ net.Messages |> Seq.forall isVoteForBlock @>

        net.DeliverMessages(fun (s, r, m) -> s <> validators.[3] && r <> validators.[3]) // Deliver Vote messages
        test <@ net.Messages.Count = reachableValidators.Length @>
        test <@ net.Messages |> Seq.forall isCommitForBlock @>

        let committers = net.Messages |> Seq.map fst |> Seq.toList |> List.sort
        test <@ committers = reachableValidators @>

        let committedBlockNumber, committedRound =
            net.Messages
            |> Seq.map (fun (_, e) -> e.BlockNumber, e.Round)
            |> Seq.distinct
            |> Seq.exactlyOne
        test <@ committedBlockNumber = BlockNumber 1L @>
        test <@ committedRound = ConsensusRound 0 @>

        net.DeliverMessages(
            (fun (s, r, m) -> s <> validators.[3] && r = validators.[1]),
            (fun (s, r, m) -> s <> validators.[3] && (r = validators.[0] || r = validators.[2])) // Delayed messages
        ) // Deliver Commit messages
        test <@ net.Messages.Count = 3 @>
        test <@ net.Messages |> Seq.forall isCommitForBlock @>

        // ASSERT
        net.PrintTheState(output.WriteLine)

        test <@ net.Decisions.[validators.[0]].ContainsKey(BlockNumber 1L) = false @>
        test <@ net.Decisions.[validators.[1]].[BlockNumber 1L] = proposedBlock @>
        test <@ net.Decisions.[validators.[2]].ContainsKey(BlockNumber 1L) = false @>
        test <@ net.Decisions.[validators.[3]].Count = 0 @>

        test <@ net.States.[validators.[0]].Variables.LockedBlock = Some proposedBlock @>
        test <@ net.States.[validators.[0]].Variables.LockedBlockSignatures.Length = 3 @>
        test <@ net.States.[validators.[2]].Variables.LockedBlock = Some proposedBlock @>
        test <@ net.States.[validators.[2]].Variables.LockedBlockSignatures.Length = 3 @>

        net, proposedBlock // Return the simulation state for dependent tests.

    [<Fact>]
    member __.``Consensus - Distributed Test Cases: CL1`` () =
        // ARRANGE
        let net, proposedBlock = __.``Consensus - Distributed Test Cases: CF5`` ()
        let validators = net.Validators

        // ACT
        net.CrashValidator validators.[1]

        net.Messages.Clear()

        net.RecoverValidator validators.[3]
        test <@ net.Messages.Count = 2 @>
        test <@ net.Messages |> Seq.forall (fun (s, _) -> s = validators.[3]) @>
        test <@ net.Messages.[0] |> isVoteForBlock @>
        test <@ net.Messages.[1] |> isCommitForBlock @>
        net.DeliverMessages() // Deliver V3's messages

        test <@ net.Messages.Count = 0 @>

        net.PropagateBlock validators.[3] (BlockNumber 1L)
        net.States.[validators.[0]].HandleConsensusCommand Synchronize
        net.States.[validators.[2]].HandleConsensusCommand Synchronize

        test <@ net.Messages.Count = 1 @>
        test <@ net.Messages |> Seq.forall (fun (s, e) -> s = validators.[2] && e.ConsensusMessage.IsPropose) @>

        test <@ net.States.[validators.[0]].Variables.BlockNumber = BlockNumber 2L @>
        test <@ net.States.[validators.[2]].Variables.BlockNumber = BlockNumber 2L @>
        test <@ net.States.[validators.[3]].Variables.BlockNumber = BlockNumber 2L @>

        // ASSERT
        net.PrintTheState(output.WriteLine)

        test <@ net.Decisions.[validators.[0]].[BlockNumber 1L] = proposedBlock @>
        test <@ net.Decisions.[validators.[1]].[BlockNumber 1L] = proposedBlock @>
        test <@ net.Decisions.[validators.[2]].[BlockNumber 1L] = proposedBlock @>
        test <@ net.Decisions.[validators.[3]].[BlockNumber 1L] = proposedBlock @>

        net, proposedBlock // Return the simulation state for dependent tests.

    [<Fact>]
    member __.``Consensus - Distributed Test Cases: AC1`` () =
        // ARRANGE
        let net, proposedBlock = __.``Consensus - Distributed Test Cases: CF5`` ()
        let validators = net.Validators

        // ACT
        test <@ net.States.[validators.[0]].MessageCounts = (1, 3, 1) @>
        test <@ net.States.[validators.[1]].MessageCounts = (0, 0, 0) @> // Moved on to height 2
        test <@ net.States.[validators.[2]].MessageCounts = (1, 3, 1) @>

        net.CrashValidator validators.[0]
        net.CrashValidator validators.[1]
        net.CrashValidator validators.[2]

        net.RecoverValidator validators.[0]
        test <@ net.Messages.Count = 0 @>

        net.RecoverValidator validators.[2]
        test <@ net.Messages.Count = 0 @>

        net.RecoverValidator validators.[3]
        test <@ net.Messages.Count = 2 @>
        test <@ net.Messages |> Seq.forall (fun (s, _) -> s = validators.[3]) @>
        test <@ net.Messages.[0] |> isVoteForBlock @>
        test <@ net.Messages.[1] |> isCommitForBlock @>

        test <@ net.States.[validators.[0]].MessageCounts = (1, 1, 1) @>
        test <@ net.States.[validators.[2]].MessageCounts = (1, 2, 2) @>
        test <@ net.States.[validators.[3]].MessageCounts = (1, 3, 2) @>
        test <@ net.DecisionCount = 1 @> // No new decisions yet

        net.DeliverMessages() // Deliver V3's messages

        test <@ net.States.[validators.[0]].MessageCounts = (1, 2, 2) @>
        test <@ net.States.[validators.[2]].MessageCounts = (0, 0, 0) @>
        test <@ net.States.[validators.[3]].MessageCounts = (0, 0, 0) @>
        test <@ net.DecisionCount = 3 @>

        test <@ net.Messages.Count = 1 @> // New proposal
        test <@ net.Messages |> Seq.forall (fun (s, _) -> s = validators.[2]) @>
        test <@ net.States.[validators.[2]].Variables.BlockNumber = BlockNumber 2L @>
        test <@ net.States.[validators.[3]].Variables.BlockNumber = BlockNumber 2L @>

        net.PropagateBlock validators.[3] (BlockNumber 1L)
        net.States.[validators.[0]].HandleConsensusCommand Synchronize
        test <@ net.DecisionCount = 4 @>
        test <@ net.States.[validators.[0]].Variables.BlockNumber = BlockNumber 2L @>

        // ASSERT
        net.PrintTheState(output.WriteLine)

        test <@ net.Decisions.[validators.[0]].[BlockNumber 1L] = proposedBlock @>
        test <@ net.Decisions.[validators.[1]].[BlockNumber 1L] = proposedBlock @>
        test <@ net.Decisions.[validators.[2]].[BlockNumber 1L] = proposedBlock @>
        test <@ net.Decisions.[validators.[3]].[BlockNumber 1L] = proposedBlock @>

        net, proposedBlock // Return the simulation state for dependent tests.

    [<Fact>]
    member __.``Consensus - Distributed Test Cases: AC2`` () =
        // ARRANGE
        let net, proposedBlock = __.``Consensus - Distributed Test Cases: CF5`` ()
        let validators = net.Validators

        // ACT
        test <@ net.States.[validators.[0]].MessageCounts = (1, 3, 1) @>
        test <@ net.States.[validators.[1]].MessageCounts = (0, 0, 0) @> // Moved on to height 2
        test <@ net.States.[validators.[2]].MessageCounts = (1, 3, 1) @>

        net.CrashValidator validators.[0]
        net.CrashValidator validators.[1]
        net.CrashValidator validators.[2]

        net.RecoverValidator validators.[0]
        test <@ net.Messages.Count = 0 @>

        net.RecoverValidator validators.[1]
        test <@ net.Messages.Count = 0 @>

        net.RecoverValidator validators.[3]
        test <@ net.Messages.Count = 0 @>

        test <@ net.States.[validators.[0]].MessageCounts = (1, 1, 1) @>
        test <@ net.States.[validators.[1]].MessageCounts = (0, 0, 0) @>
        test <@ net.States.[validators.[3]].MessageCounts = (0, 0, 0) @>
        test <@ net.DecisionCount = 2 @> // V3 got block through sync

        test <@ net.States.[validators.[1]].Variables.BlockNumber = BlockNumber 2L @>
        test <@ net.States.[validators.[3]].Variables.BlockNumber = BlockNumber 2L @>

        net.PropagateBlock validators.[3] (BlockNumber 1L)
        net.States.[validators.[0]].HandleConsensusCommand Synchronize
        test <@ net.DecisionCount = 3 @>
        test <@ net.States.[validators.[0]].Variables.BlockNumber = BlockNumber 2L @>

        // ASSERT
        net.PrintTheState(output.WriteLine)

        test <@ net.Decisions.[validators.[0]].[BlockNumber 1L] = proposedBlock @>
        test <@ net.Decisions.[validators.[1]].[BlockNumber 1L] = proposedBlock @>
        test <@ net.Decisions.[validators.[2]].Count = 0 @>
        test <@ net.Decisions.[validators.[3]].[BlockNumber 1L] = proposedBlock @>

        net, proposedBlock // Return the simulation state for dependent tests.

    [<Fact>]
    member __.``Consensus - Distributed Test Cases: AC3`` () =
        // ARRANGE
        let net, proposedBlock = __.``Consensus - Distributed Test Cases: AC2`` ()
        let validators = net.Validators

        // ACT
        net.RecoverValidator validators.[2]
        test <@ net.Messages.Count = 1 @>
        test <@ net.Messages |> Seq.forall (fun (s, m) -> s = validators.[2] && m.ConsensusMessage.IsPropose) @>

        test <@ net.DecisionCount = 4 @> // V2 got block through sync

        test <@ net.States.[validators.[2]].Variables.BlockNumber = BlockNumber 2L @>

        // ASSERT
        net.PrintTheState(output.WriteLine)

        test <@ net.Decisions.[validators.[0]].[BlockNumber 1L] = proposedBlock @>
        test <@ net.Decisions.[validators.[1]].[BlockNumber 1L] = proposedBlock @>
        test <@ net.Decisions.[validators.[2]].[BlockNumber 1L] = proposedBlock @>
        test <@ net.Decisions.[validators.[3]].[BlockNumber 1L] = proposedBlock @>

        net, proposedBlock // Return the simulation state for dependent tests.
