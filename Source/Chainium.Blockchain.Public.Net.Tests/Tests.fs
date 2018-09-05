namespace Chainium.Blockchain.Public.Net.Tests

open System.Threading
open Chainium.Blockchain.Public.Net.Peers
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Core.Events
open Xunit
open Swensen.Unquote

module PeerTests =

    let testCleanup () =
        DbMock.reset()
        RawMock.reset()
        TransportMock.closeAllConnections()
        Thread.Sleep(2000)

    let sendMessage peerMessage (node : NetworkNode) =
        node.SendMessage peerMessage

    let gossipTx (node : NetworkNode) txHash =
        let gossipMessage = GossipMessage {
            MessageId = Tx txHash
            SenderAddress = node.GetNetworkAddress()
            Data = "txEnvelope" |> box
        }

        node |> sendMessage gossipMessage

    let multicastTx (node : NetworkNode) txHash =
        let multicastMessage = MulticastMessage {
            MessageId = Tx txHash
            Data = "txEnvelope" |> box
        }

        node |> sendMessage multicastMessage

    let gossipBlock (node : NetworkNode) blockNr =
        let peerMessage = GossipMessage {
            MessageId = Block blockNr
            SenderAddress = node.GetNetworkAddress()
            Data = "blockEnvelope" |> box
        }

        node |> sendMessage peerMessage

    let multicastBlock (node : NetworkNode) blockNr =
        let peerMessage = GossipMessage {
            MessageId = Block blockNr
            SenderAddress = node.GetNetworkAddress()
            Data = "blockEnvelope" |> box
        }

        node |> sendMessage peerMessage

    let private txPropagator node (message : TxReceivedEventData) =
        gossipTx node message.TxHash

    let private blockPropagator node (message : BlockCreatedEventData) =
        gossipBlock node message.BlockNumber

    let publishEvent node appEvent =
        match appEvent with
        | TxSubmitted e -> txPropagator node e
        | TxReceived e -> txPropagator node e
        | BlockCreated e -> blockPropagator node e
        | BlockReceived e -> blockPropagator node e

    let startGossip (node : NetworkNode) =
        node.StartGossip (node.GetNetworkAddress() |> WorkflowsMock.processPeerMessage) (publishEvent node)

    let stopGossip (node : NetworkNode) =
        node.StopGossip ()

    let checkGossipDiscoveryConvergence (nodeList : NetworkNode list) =
        let expectedPeerList =
            nodeList
            |> List.map (fun n -> n.GetNetworkAddress ())
            |> List.sort

        nodeList
        |> List.iter(fun n ->
            let nodePeerList =
                n.GetActiveMembers()
                |> List.map(fun m -> m.NetworkAddress)
                |> List.sort

            test <@ nodePeerList = expectedPeerList @>
        )

        nodeList |> List.iter(fun n -> stopGossip n)

    let checkMessageReceived (nodeList : NetworkNode list) networkMessageId =
        nodeList |> List.iter(fun n ->
            let nodeHashReceivedTx = RawMock.hasData (n.GetNetworkAddress()) networkMessageId
            test <@ nodeHashReceivedTx = true @>
        )

    let createNodes nodeConfigList =
        // ARRANGE
        let fanout, tCycle, tFail = 4, 1000, 60000

        let createNode (nodeConfig : NetworkNodeConfig) =
            let getAllPeerNodes = DbMock.getAllPeerNodes nodeConfig.NetworkAddress
            let savePeerNode = DbMock.savePeerNode nodeConfig.NetworkAddress
            let removePeerNode = DbMock.removePeerNode nodeConfig.NetworkAddress
            let getAllValidators = DbMock.getAllValidators nodeConfig.NetworkAddress

            NetworkNode (
                getAllPeerNodes,
                savePeerNode,
                removePeerNode,
                TransportMock.sendGossipDiscoveryMessage,
                TransportMock.sendGossipMessage,
                TransportMock.sendMulticastMessage,
                TransportMock.sendUnicastMessage,
                TransportMock.receiveMessage,
                TransportMock.closeConnection,
                TransportMock.closeAllConnections,
                getAllValidators,
                nodeConfig,
                fanout,
                tCycle,
                tFail
            )

        let nodeList =
            nodeConfigList
            |> List.map (fun config -> createNode config)

        (nodeList, tCycle)

    let create3NodesConfigSameBootstrapNode () =
        let nodeConfig1 = {
            BootstrapNodes = []
            NetworkAddress = NetworkAddress "127.0.0.1:5555"
        }
        let nodeConfig2 = {
            BootstrapNodes = [NetworkAddress "127.0.0.1:5555"]
            NetworkAddress = NetworkAddress "127.0.0.1:5556"
        }
        let nodeConfig3 = {
            BootstrapNodes = [NetworkAddress "127.0.0.1:5555"]
            NetworkAddress = NetworkAddress "127.0.0.1:5557"
        }
        [nodeConfig1; nodeConfig2; nodeConfig3]

    let create3NodesConfigDifferentBoostrapNode () =
        let nodeConfig1 = {
            BootstrapNodes = []
            NetworkAddress = NetworkAddress "127.0.0.1:5555"
        }
        let nodeConfig2 = {
            BootstrapNodes = [NetworkAddress "127.0.0.1:5555"]
            NetworkAddress = NetworkAddress "127.0.0.1:5556"
        }
        let nodeConfig3 = {
            BootstrapNodes = [NetworkAddress "127.0.0.1:5556"]
            NetworkAddress = NetworkAddress "127.0.0.1:5557"
        }
        [nodeConfig1; nodeConfig2; nodeConfig3]

    let testGossipDiscovery nodeConfigList cycleCount =
        // ARRANGE
        let nodeList, tCycle = createNodes nodeConfigList

        // ACT
        nodeList |> List.iter(fun n -> startGossip n)

        System.Threading.Thread.Sleep (cycleCount * tCycle)

        // ASSERT
        checkGossipDiscoveryConvergence nodeList

    let testGossipMessagePassing nodeConfigList cycleCount =
        // ARRANGE
        let nodeList, tCycle = createNodes nodeConfigList

        // ACT
        nodeList |> List.iter(fun n -> startGossip n)

        System.Threading.Thread.Sleep (cycleCount * tCycle)

        let txHash = TxHash "txHash"
        gossipTx nodeList.[0] txHash
        RawMock.savePeerData (nodeList.[0].GetNetworkAddress()) (Tx txHash) |> ignore

        System.Threading.Thread.Sleep (cycleCount * tCycle)

        // ASSERT
        checkMessageReceived nodeList (Tx txHash)

    let testMulticastMessagePassing nodeConfigList cycleCount =
        // ARRANGE
        let nodeList, tCycle = createNodes nodeConfigList

        // ACT
        nodeList |> List.iter(fun n -> startGossip n)

        System.Threading.Thread.Sleep (cycleCount * tCycle)

        let txHash = TxHash "txHash"
        multicastTx nodeList.[0] txHash
        RawMock.savePeerData (nodeList.[0].GetNetworkAddress()) (Tx txHash) |> ignore

        System.Threading.Thread.Sleep (tCycle)

        // ASSERT
        checkMessageReceived nodeList (Tx txHash)

    [<Fact>]
    let ``Gossip - test GossipDiscovery 3 nodes same bootstrap node`` () =
        // ARRANGE
        testCleanup()

        let nodeConfigList = create3NodesConfigSameBootstrapNode ()

        testGossipDiscovery nodeConfigList 5

    [<Fact>]
    let ``Gossip - test GossipDiscovery 3 nodes different bootstrap node`` () =
        // ARRANGE
        testCleanup()

        let nodeConfigList = create3NodesConfigDifferentBoostrapNode ()

        testGossipDiscovery nodeConfigList 5

    [<Fact>]
    let ``Gossip - test GossipDiscovery 100 nodes`` () =
        // ARRANGE
        testCleanup()

        let nodeConfig1 = {
            BootstrapNodes = []
            NetworkAddress = NetworkAddress "127.0.0.1:6555"
        }

        let nodeConfigList =
            [6556..6654]
            |> List.map(fun port ->
                {
                    BootstrapNodes = [NetworkAddress "127.0.0.1:6555"]
                    NetworkAddress = NetworkAddress (sprintf "127.0.0.1:%i" port)
                }
            )

        testGossipDiscovery (nodeConfig1 :: nodeConfigList) 20

    [<Fact>]
    let ``Gossip - test GossipMessagePassing 3 nodes same bootstrap node`` () =
        // ARRANGE
        testCleanup()

        let nodeConfigList = create3NodesConfigSameBootstrapNode ()

        testGossipMessagePassing nodeConfigList 5

    [<Fact>]
    let ``Gossip - test GossipMessagePassing 3 nodes different bootstrap node`` () =
        // ARRANGE
        testCleanup()

        let nodeConfigList = create3NodesConfigDifferentBoostrapNode ()

        testGossipMessagePassing nodeConfigList 5

    [<Fact>]
    let ``Gossip - test MulticastMessagePassing 3 nodes same bootstrap node`` () =
        // ARRANGE
        testCleanup()

        let nodeConfigList = create3NodesConfigSameBootstrapNode ()

        testMulticastMessagePassing nodeConfigList 5

    [<Fact>]
    let ``Gossip - test MulticastMessagePassing 3 nodes different bootstrap node`` () =
        // ARRANGE
        testCleanup()

        let nodeConfigList = create3NodesConfigDifferentBoostrapNode ()

        testMulticastMessagePassing nodeConfigList 5
