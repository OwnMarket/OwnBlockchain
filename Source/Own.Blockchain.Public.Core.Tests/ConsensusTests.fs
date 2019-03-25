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
    member __.``Consensus - Happy Path - 20 blocks committed`` () =
        // ARRANGE
        let validatorCount = 10
        let validators = List.init validatorCount (fun _ -> (Signing.generateWallet ()).Address)

        let net = new ConsensusSimulationNetwork(validators)

        // ACT
        net.StartConsensus()
        for _ in [1 .. 20] do
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

        test <@ envelope.BlockNumber = BlockNumber 21L @>
        test <@ block <> None @>

        for v in validators do
            test <@ net.Decisions.[v].Count = 20 @>

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
    member __.``Consensus - BFT - CF1`` () =
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
    member __.``Consensus - BFT - CF2`` () =
        // ARRANGE
        let net, proposedBlock = __.``Consensus - BFT - CF1`` ()
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
    member __.``Consensus - BFT - CF3`` () =
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

        test <@ net.States.[validators.[1]].Variables.LockedRound.Value = 0 @>
        test <@ net.States.[validators.[1]].Variables.LockedBlock = Some proposedBlock @>
        test <@ net.States.[validators.[1]].Variables.LockedBlockSignatures.Length = 3 @>

        // ASSERT
        net.PrintTheState(output.WriteLine)

        test <@ net.Decisions.[validators.[0]].[BlockNumber 1L] = proposedBlock @>
        test <@ net.Decisions.[validators.[1]].ContainsKey(BlockNumber 1L) = false @>
        test <@ net.Decisions.[validators.[2]].ContainsKey(BlockNumber 1L) = false @>
        test <@ net.Decisions.[validators.[3]].Count = 0 @>

        net, proposedBlock // Return the simulation state for dependent tests.

    [<Fact>]
    member __.``Consensus - BFT - CF4`` () =
        // ARRANGE
        let net, proposedBlock = __.``Consensus - BFT - CF3`` ()
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
    member __.``Consensus - BFT - CF4a`` () =
        // ARRANGE
        let net, proposedBlock = __.``Consensus - BFT - CF3`` ()
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
    member __.``Consensus - BFT - MF1`` () =
        // ARRANGE
        let net, proposedBlock = __.``Consensus - BFT - CF3`` ()
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

        test <@ net.States.[validators.[1]].Variables.ConsensusStep = ConsensusStep.Commit @>
        test <@ net.IsTimeoutScheduled(validators.[1], BlockNumber 1L, ConsensusRound 0, ConsensusStep.Commit) |> not @>
        test <@ net.States.[validators.[3]].Variables.ConsensusStep = ConsensusStep.Commit @>
        test <@ net.IsTimeoutScheduled(validators.[3], BlockNumber 1L, ConsensusRound 0, ConsensusStep.Commit) |> not @>
        test <@ net.States.[validators.[2]].Variables.ConsensusStep = ConsensusStep.Propose @>
        test <@ net.IsTimeoutScheduled(validators.[2], BlockNumber 1L, ConsensusRound 0, ConsensusStep.Propose) @>

        // Propose timeout triggered
        net.Events.Clear()
        net.TriggerScheduledTimeout(validators.[2], BlockNumber 1L, ConsensusRound 0, ConsensusStep.Propose)
        test <@ net.Messages.Count = 1 @>
        test <@ net.Messages |> Seq.forall (fun (s, _) -> s = validators.[2]) @>
        test <@ net.Messages.[0] |> isVoteForNone @>

        test <@ net.Events.Count = 0 @>

        net.DeliverMessages()
        test <@ net.Messages.Count = 0 @>

        test <@ net.Events.Count = 2 @> // V1 and V3 have detected V2's equivocation
        test <@ net.Events |> Seq.map fst |> Seq.contains validators.[1] @>
        test <@ net.Events |> Seq.map fst |> Seq.contains validators.[3] @>

        let equivocationProof, equivocationValidator =
            net.Events
            |> Seq.map snd
            |> Seq.distinct
            |> Seq.exactlyOne
            |> function
                | EquivocationProofDetected (p, v) -> p, v
                | e -> failwithf "Unexpected event %A" e
        test <@ equivocationValidator = validators.[2] @>
        test <@ equivocationProof.BlockNumber = 1L @>
        test <@ equivocationProof.ConsensusRound = 0 @>
        test <@ equivocationProof.ConsensusStep = 1uy @>
        test <@ equivocationProof.BlockHash1 |> isNull @>
        test <@ equivocationProof.BlockHash2 = proposedBlock.Header.Hash.Value @>
        test <@ equivocationProof.Signature1.Contains(validators.[2].Value) @>
        test <@ equivocationProof.Signature2.Contains(validators.[2].Value) @>

        test <@ net.States.[validators.[1]].MessageCounts = (1, 4, 2) @>
        test <@ net.States.[validators.[3]].MessageCounts = (1, 4, 2) @>
        test <@ net.States.[validators.[2]].MessageCounts = (0, 2, 1) @>

        test <@ net.States.[validators.[1]].Variables.ConsensusStep = ConsensusStep.Commit @>
        test <@ net.IsTimeoutScheduled(validators.[1], BlockNumber 1L, ConsensusRound 0, ConsensusStep.Commit) |> not @>
        test <@ net.States.[validators.[3]].Variables.ConsensusStep = ConsensusStep.Commit @>
        test <@ net.IsTimeoutScheduled(validators.[3], BlockNumber 1L, ConsensusRound 0, ConsensusStep.Commit) |> not @>
        test <@ net.States.[validators.[2]].Variables.ConsensusStep = ConsensusStep.Vote @>
        test <@ net.IsTimeoutScheduled(validators.[2], BlockNumber 1L, ConsensusRound 0, ConsensusStep.Vote) |> not @>

        // Stale round detected
        net.RequestConsensusState validators.[2]
        test <@ net.Messages.Count = 1 @>
        test <@ net.Messages.[0] |> isCommitForBlock @>

        net.DeliverMessages ()
        test <@ net.Messages.Count = 1 @>
        test <@ net.Messages |> Seq.forall isPropose @>

        test <@ net.States.[validators.[1]].MessageCounts = (0, 0, 0) @>
        test <@ net.States.[validators.[3]].MessageCounts = (0, 0, 0) @>
        test <@ net.States.[validators.[2]].MessageCounts = (0, 0, 0) @>

        test <@ net.States.[validators.[1]].Variables.BlockNumber = BlockNumber 2L @>
        test <@ net.States.[validators.[3]].Variables.BlockNumber = BlockNumber 2L @>
        test <@ net.States.[validators.[2]].Variables.BlockNumber = BlockNumber 2L @>

        // ASSERT
        net.PrintTheState(output.WriteLine)

        test <@ net.Decisions.[validators.[0]].[BlockNumber 1L] = proposedBlock @>
        test <@ net.Decisions.[validators.[1]].[BlockNumber 1L] = proposedBlock @>
        test <@ net.Decisions.[validators.[2]].[BlockNumber 1L] = proposedBlock @>
        test <@ net.Decisions.[validators.[3]].[BlockNumber 1L] = proposedBlock @>

        net, proposedBlock // Return the simulation state for dependent tests.

    [<Fact>]
    member __.``Consensus - BFT - CF5`` () =
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
    member __.``Consensus - BFT - CL1`` () =
        // ARRANGE
        let net, proposedBlock = __.``Consensus - BFT - CF5`` ()
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
        test <@ net.Messages |> Seq.forall isPropose @>
        test <@ net.Messages |> Seq.forall (fun (s, _) -> s = validators.[2]) @>

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
    member __.``Consensus - BFT - AC1`` () =
        // ARRANGE
        let net, proposedBlock = __.``Consensus - BFT - CF5`` ()
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
    member __.``Consensus - BFT - AC2`` () =
        // ARRANGE
        let net, proposedBlock = __.``Consensus - BFT - CF5`` ()
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
    member __.``Consensus - BFT - AC3`` () =
        // ARRANGE
        let net, proposedBlock = __.``Consensus - BFT - AC2`` ()
        let validators = net.Validators

        // ACT
        net.RecoverValidator validators.[2]
        test <@ net.Messages.Count = 1 @>
        test <@ net.Messages |> Seq.forall isPropose @>
        test <@ net.Messages |> Seq.forall (fun (s, _) -> s = validators.[2]) @>

        test <@ net.DecisionCount = 4 @> // V2 got block through sync

        test <@ net.States.[validators.[2]].Variables.BlockNumber = BlockNumber 2L @>

        // ASSERT
        net.PrintTheState(output.WriteLine)

        test <@ net.Decisions.[validators.[0]].[BlockNumber 1L] = proposedBlock @>
        test <@ net.Decisions.[validators.[1]].[BlockNumber 1L] = proposedBlock @>
        test <@ net.Decisions.[validators.[2]].[BlockNumber 1L] = proposedBlock @>
        test <@ net.Decisions.[validators.[3]].[BlockNumber 1L] = proposedBlock @>

        net, proposedBlock // Return the simulation state for dependent tests.

    [<Fact>]
    member __.``Consensus - BFT - AC4`` () =
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

        net.DeliverMessages(fun (s, r, m) -> s <> validators.[3] && r = validators.[2]) // Deliver Vote messages to V2
        test <@ net.Messages.Count = 1 @>
        test <@ net.Messages |> Seq.forall isCommitForBlock @>
        test <@ net.Messages |> Seq.forall (fun (v, _) -> v = validators.[2]) @>

        net.DeliverMessages(fun (s, r, m) -> s = r) // Deliver own message only

        test <@ net.States.[validators.[0]].MessageCounts = (1, 1, 0) @>
        test <@ net.States.[validators.[1]].MessageCounts = (1, 1, 0) @>
        test <@ net.States.[validators.[2]].MessageCounts = (1, 3, 1) @>
        test <@ net.DecisionCount = 0 @>

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

        test <@ net.States.[validators.[0]].MessageCounts = (1, 1, 0) @>
        test <@ net.States.[validators.[2]].MessageCounts = (1, 2, 1) @>
        test <@ net.States.[validators.[3]].MessageCounts = (1, 3, 1) @>

        net.DeliverMessages() // Deliver V3's messages
        test <@ net.Messages.Count = 0 @>

        test <@ net.States.[validators.[0]].MessageCounts = (1, 2, 1) @>
        test <@ net.States.[validators.[2]].MessageCounts = (1, 3, 2) @>
        test <@ net.States.[validators.[3]].MessageCounts = (1, 4, 2) @>
        test <@ net.DecisionCount = 0 @>

        test <@ net.ScheduledTimeouts.[validators.[0]].Count = 0 @>
        test <@ net.ScheduledTimeouts.[validators.[2]].Count = 0 @>
        test <@ net.ScheduledTimeouts.[validators.[3]].Count = 2 @>
        test <@ net.IsTimeoutScheduled(validators.[3], BlockNumber 1L, ConsensusRound 0, ConsensusStep.Propose) @>
        test <@ net.IsTimeoutScheduled(validators.[3], BlockNumber 1L, ConsensusRound 0, ConsensusStep.Vote) @>
        test <@ net.IsTimeoutScheduled(validators.[3], BlockNumber 1L, ConsensusRound 0, ConsensusStep.Commit) |> not @>
        test <@ net.States.[validators.[3]].Variables.ConsensusStep = ConsensusStep.Commit @>

        net.RequestConsensusState validators.[0]

        test <@ net.Messages.Count = 1 @>
        test <@ net.Messages |> Seq.forall (fun (s, _) -> s = validators.[0]) @>
        test <@ net.Messages.[0] |> isCommitForBlock @>
        test <@ net.DecisionCount = 0 @>

        test <@ net.States.[validators.[0]].MessageCounts = (1, 4, 2) @>
        test <@ net.States.[validators.[2]].MessageCounts = (1, 3, 2) @>
        test <@ net.States.[validators.[3]].MessageCounts = (1, 4, 2) @>
        test <@ net.DecisionCount = 0 @>

        net.DeliverMessages() // Deliver V0's messages

        test <@ net.States.[validators.[0]].MessageCounts = (0, 0, 0) @>
        test <@ net.States.[validators.[2]].MessageCounts = (0, 0, 0) @>
        test <@ net.States.[validators.[3]].MessageCounts = (0, 0, 0) @>
        test <@ net.DecisionCount = 3 @>

        // ASSERT
        net.PrintTheState(output.WriteLine)

        test <@ net.Decisions.[validators.[0]].[BlockNumber 1L] = proposedBlock @>
        test <@ net.Decisions.[validators.[1]].Count = 0 @>
        test <@ net.Decisions.[validators.[2]].[BlockNumber 1L] = proposedBlock @>
        test <@ net.Decisions.[validators.[3]].[BlockNumber 1L] = proposedBlock @>

        net, proposedBlock // Return the simulation state for dependent tests.

    [<Fact>]
    member __.``Consensus - BFT - ML1`` () =
        // ARRANGE
        let validators = List.init 4 (fun _ -> (Signing.generateWallet ()).Address) |> List.sort
        let proposer = validators |> Validators.getProposer (BlockNumber 1L) (ConsensusRound 0)
        test <@ proposer = validators.[1] @>

        let net = new ConsensusSimulationNetwork(validators)

        // ACT
        net.StartConsensus()

        test <@ net.Messages.Count = 1 @>
        test <@ net.Messages.[0] |> fst = proposer @>
        let proposedBlock =
            net.Messages.[0]
            |> snd
            |> fun m -> m.ConsensusMessage
            |> function Propose (block, _) -> block | _ -> failwith "Propose message expected"
        test <@ proposedBlock.Header.Number = BlockNumber 1L @>

        // Prepare malicious message
        let maliciousBlock =
            {proposedBlock with
                Header =
                    {proposedBlock.Header with
                        Hash = Helpers.randomString () |> BlockHash
                    }
            }
        test <@ maliciousBlock.Header.Hash <> proposedBlock.Header.Hash @>
        let maliciousMessage =
            net.Messages.[0]
            |> snd
            |> fun e ->
                let vr = e.ConsensusMessage |> function Propose (b, vr) -> vr | _ -> failwith "Unexpected message"
                {e with
                    ConsensusMessage = Propose (maliciousBlock, vr)
                }
        net.Messages.Add(validators.[1], maliciousMessage)

        // Deliver Propose message
        net.DeliverMessages(
            (fun (s, r, e) ->
                match e.ConsensusMessage with
                | Propose (b, _) -> b = proposedBlock && r <> validators.[3] || b = maliciousBlock && r = validators.[3]
                | _ -> failwith "Unexpected message"
            ),
            ?implicitlyDeliverOwnMessages = Some false // To prevent delivering proposal for B1' to V1
        )
        test <@ net.Messages.Count = 4 @>
        test <@ net.Messages |> Seq.forall isVoteForBlock @>

        for i in [0 .. 3] do
            let message =
                net.Messages
                |> Seq.filter (fun (v, _) -> v = validators.[i])
                |> Seq.exactlyOne
                |> snd
                |> fun e -> e.ConsensusMessage
            if i = 3 then
                test <@ message = Vote (Some maliciousBlock.Header.Hash) @>
            else
                test <@ message = Vote (Some proposedBlock.Header.Hash) @>

        net.DeliverMessages() // Deliver Vote messages
        test <@ net.Messages.Count = 3 @>
        test <@ net.Messages |> Seq.forall isCommitForBlock @>

        for v in validators do
            let goodVotesCount =
                net.States.[v].Votes
                |> Seq.filter (fun (_, (h, _)) -> Vote h = Vote (Some proposedBlock.Header.Hash))
                |> Seq.length
            let badVotesCount =
                net.States.[v].Votes
                |> Seq.filter (fun (_, (h, _)) -> Vote h = Vote (Some maliciousBlock.Header.Hash))
                |> Seq.length
            test <@ goodVotesCount = 3 @>
            test <@ badVotesCount = 1 @>

        net.DeliverMessages() // Deliver Commit messages
        test <@ net.Messages.Count = 1 @>
        test <@ net.Messages |> Seq.forall isPropose @>
        test <@ net.DecisionCount = 3 @>

        net.Messages.Clear()

        test <@ net.States.[validators.[3]].Variables.ConsensusStep = ConsensusStep.Vote @>
        net.TriggerScheduledTimeout (validators.[3], BlockNumber 1L, ConsensusRound 0, ConsensusStep.Vote)
        test <@ net.Messages.Count = 1 @>
        test <@ net.Messages |> Seq.forall isCommitForNone @>

        net.DeliverMessages()
        test <@ net.Messages.Count = 0 @>

        test <@ net.States.[validators.[3]].Variables.ConsensusStep = ConsensusStep.Commit @>
        net.TriggerScheduledTimeout (validators.[3], BlockNumber 1L, ConsensusRound 0, ConsensusStep.Commit)
        test <@ net.Messages.Count = 0 @>
        test <@ net.States.[validators.[3]].Variables.BlockNumber.Value = 1L @>
        test <@ net.States.[validators.[3]].Variables.ConsensusRound.Value = 1 @>

        test <@ net.States.[validators.[3]].Variables.ConsensusStep = ConsensusStep.Propose @>
        net.TriggerScheduledTimeout (validators.[3], BlockNumber 1L, ConsensusRound 1, ConsensusStep.Propose)

        test <@ net.DecisionCount = 3 @>
        test <@ net.Decisions.[validators.[3]].Count = 0 @>
        net.PropagateBlock validators.[0] (BlockNumber 1L)

        // ASSERT
        net.PrintTheState(output.WriteLine)

        test <@ net.Decisions.[validators.[0]].[BlockNumber 1L] = proposedBlock @>
        test <@ net.Decisions.[validators.[1]].[BlockNumber 1L] = proposedBlock @>
        test <@ net.Decisions.[validators.[2]].[BlockNumber 1L] = proposedBlock @>
        test <@ net.Decisions.[validators.[3]].[BlockNumber 1L] = proposedBlock @>

        net, proposedBlock // Return the simulation state for dependent tests.

    [<Fact>]
    member __.``Consensus - BFT - ML2`` () =
        // ARRANGE
        let validators = List.init 4 (fun _ -> (Signing.generateWallet ()).Address) |> List.sort
        let proposer = validators |> Validators.getProposer (BlockNumber 1L) (ConsensusRound 0)
        test <@ proposer = validators.[1] @>

        let net = new ConsensusSimulationNetwork(validators)

        // ACT
        net.StartConsensus()

        test <@ net.Messages.Count = 1 @>
        test <@ net.Messages.[0] |> fst = proposer @>
        let proposedBlock =
            net.Messages.[0]
            |> snd
            |> fun m -> m.ConsensusMessage
            |> function Propose (block, _) -> block | _ -> failwith "Propose message expected"
        test <@ proposedBlock.Header.Number = BlockNumber 1L @>

        // Prepare malicious message
        let maliciousBlock =
            {proposedBlock with
                Header =
                    {proposedBlock.Header with
                        Hash = Helpers.randomString () |> BlockHash
                    }
            }
        test <@ maliciousBlock.Header.Hash <> proposedBlock.Header.Hash @>
        let maliciousMessage =
            net.Messages.[0]
            |> snd
            |> fun e ->
                let vr = e.ConsensusMessage |> function Propose (b, vr) -> vr | _ -> failwith "Unexpected message"
                {e with
                    ConsensusMessage = Propose (maliciousBlock, vr)
                }
        net.Messages.Add(validators.[1], maliciousMessage)

        // Deliver Propose message
        net.DeliverMessages(
            (fun (s, r, e) ->
                match e.ConsensusMessage with
                | Propose (b, _) ->
                    b = proposedBlock && (r = validators.[0] || r = validators.[1])
                    || b = maliciousBlock && (r = validators.[2] || r = validators.[3])
                | _ -> failwith "Unexpected message"
            ),
            ?implicitlyDeliverOwnMessages = Some false // To prevent delivering proposal for B1' to V1
        )
        test <@ net.Messages.Count = 4 @>
        test <@ net.Messages |> Seq.forall isVoteForBlock @>

        for i in [0 .. 3] do
            let message =
                net.Messages
                |> Seq.filter (fun (v, _) -> v = validators.[i])
                |> Seq.exactlyOne
                |> snd
                |> fun e -> e.ConsensusMessage
            if i = 2 || i = 3 then
                test <@ message = Vote (Some maliciousBlock.Header.Hash) @>
            else
                test <@ message = Vote (Some proposedBlock.Header.Hash) @>

        net.DeliverMessages() // Deliver Vote messages
        test <@ net.Messages.Count = 0 @>

        for v in validators do
            let goodVotesCount =
                net.States.[v].Votes
                |> Seq.filter (fun (_, (h, _)) -> Vote h = Vote (Some proposedBlock.Header.Hash))
                |> Seq.length
            let badVotesCount =
                net.States.[v].Votes
                |> Seq.filter (fun (_, (h, _)) -> Vote h = Vote (Some maliciousBlock.Header.Hash))
                |> Seq.length
            test <@ goodVotesCount = 2 @>
            test <@ badVotesCount = 2 @>

        for i in [0 .. 3] do
            test <@ net.States.[validators.[i]].MessageCounts = (1, 4, 0) @>

        test <@ net.DecisionCount = 0 @>

        for i in [0 .. 3] do
            test <@ net.States.[validators.[i]].Variables.ConsensusStep = ConsensusStep.Vote @>
            test <@ net.IsTimeoutScheduled(validators.[i], BlockNumber 1L, ConsensusRound 0, ConsensusStep.Vote) @>
            net.TriggerScheduledTimeout (validators.[i], BlockNumber 1L, ConsensusRound 0, ConsensusStep.Vote)
            test <@ net.Messages.Count = i + 1 @>
            test <@ net.Messages |> Seq.forall isCommitForNone @>

        net.DeliverMessages()
        test <@ net.Messages.Count = 0 @>
        test <@ net.DecisionCount = 0 @>

        for i in [0 .. 3] do
            test <@ net.States.[validators.[i]].Variables.ConsensusStep = ConsensusStep.Commit @>
            test <@ net.IsTimeoutScheduled(validators.[i], BlockNumber 1L, ConsensusRound 0, ConsensusStep.Commit) @>
            net.TriggerScheduledTimeout (validators.[i], BlockNumber 1L, ConsensusRound 0, ConsensusStep.Commit)

        test <@ net.Messages.Count = 1 @>
        test <@ net.Messages.[0] |> fst = validators.[2] @>
        test <@ net.Messages |> Seq.forall isPropose @>
        test <@ net.DecisionCount = 0 @>

        for i in [0 .. 3] do
            test <@ net.States.[validators.[i]].Variables.BlockNumber.Value = 1L @>
            test <@ net.States.[validators.[i]].Variables.ConsensusRound.Value = 1 @>
            test <@ net.States.[validators.[i]].Variables.ConsensusStep = ConsensusStep.Propose @>

        // ASSERT
        net.PrintTheState(output.WriteLine)

        test <@ net.Decisions.[validators.[0]].Count = 0 @>
        test <@ net.Decisions.[validators.[1]].Count = 0 @>
        test <@ net.Decisions.[validators.[2]].Count = 0 @>
        test <@ net.Decisions.[validators.[3]].Count = 0 @>

        net, proposedBlock // Return the simulation state for dependent tests.

    [<Fact>]
    member __.``Consensus - BFT - MF2`` () =
        // ARRANGE
        let validators = List.init 4 (fun _ -> (Signing.generateWallet ()).Address) |> List.sort
        let proposer = validators |> Validators.getProposer (BlockNumber 1L) (ConsensusRound 0)
        test <@ proposer = validators.[1] @>

        let net = new ConsensusSimulationNetwork(validators)

        // ACT
        net.StartConsensus()

        test <@ net.Messages.Count = 1 @>
        test <@ net.Messages.[0] |> fst = proposer @>
        let proposedBlock =
            net.Messages.[0]
            |> snd
            |> fun m -> m.ConsensusMessage
            |> function Propose (block, _) -> block | _ -> failwith "Propose message expected"
        test <@ proposedBlock.Header.Number = BlockNumber 1L @>

        net.DeliverMessages() // Deliver Propose message
        test <@ net.Messages.Count = 4 @>
        test <@ net.Messages |> Seq.forall isVoteForBlock @>

        let originalVoteFromV2 =
            net.Messages
            |> Seq.choose (fun (v, m) -> if v = validators.[2] then Some m else None)
            |> Seq.exactlyOne
        test <@ net.Messages.Remove(validators.[2], originalVoteFromV2) @>

        let maliciousVoteFromV2 =
            {originalVoteFromV2 with
                ConsensusMessage = Helpers.randomString () |> BlockHash |> Some |> Vote // Vote for block 1'
            }
        test <@ originalVoteFromV2.ConsensusMessage <> maliciousVoteFromV2.ConsensusMessage @>
        net.Messages.Add(validators.[2], maliciousVoteFromV2)

        net.DeliverMessages() // Deliver Vote messages
        test <@ net.Messages.Count = 4 @>
        test <@ net.Messages |> Seq.forall isCommitForBlock @>

        for v in validators do
            let goodVotesCount =
                net.States.[v].Votes
                |> Seq.filter (fun (_, (h, _)) -> Vote h = originalVoteFromV2.ConsensusMessage)
                |> Seq.length
            let badVotesCount =
                net.States.[v].Votes
                |> Seq.filter (fun (_, (h, _)) -> Vote h = maliciousVoteFromV2.ConsensusMessage)
                |> Seq.length
            test <@ goodVotesCount = 3 @>
            test <@ badVotesCount = 1 @>

        net.DeliverMessages() // Deliver Commit messages

        // ASSERT
        net.PrintTheState(output.WriteLine)

        test <@ net.Decisions.[validators.[0]].[BlockNumber 1L] = proposedBlock @>
        test <@ net.Decisions.[validators.[1]].[BlockNumber 1L] = proposedBlock @>
        test <@ net.Decisions.[validators.[2]].[BlockNumber 1L] = proposedBlock @>
        test <@ net.Decisions.[validators.[3]].[BlockNumber 1L] = proposedBlock @>

        net, proposedBlock // Return the simulation state for dependent tests.
