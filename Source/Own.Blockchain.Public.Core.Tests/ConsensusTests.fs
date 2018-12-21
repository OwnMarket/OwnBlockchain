namespace Own.Blockchain.Public.Core.Tests

open System
open Xunit
open Xunit.Abstractions
open Swensen.Unquote
open Own.Common
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Crypto
open Own.Blockchain.Public.Core.Tests.ConsensusTestHelpers

type ConsensusTests(output : ITestOutputHelper) =

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
        test <@ net.Messages |> Seq.forall isPropose @>

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
        test <@ net.Messages |> Seq.forall isVoteForBlock @>

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
        test <@ net.Messages |> Seq.forall isCommitForBlock @>

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
        test <@ net.Messages |> Seq.forall isPropose @>

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
        let envelope = net.Messages |> Seq.head
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
        test <@ net.Messages |> Seq.forall isVoteForBlock @>

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
        test <@ net.Messages |> Seq.forall isVoteForNone @>

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
        test <@ net.Messages |> Seq.forall isCommitForNone @>

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
        test <@ net.Messages |> Seq.forall isPropose @>
