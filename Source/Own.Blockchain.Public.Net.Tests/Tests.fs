namespace Own.Blockchain.Public.Net.Tests

open System.Threading
open Own.Blockchain.Public.Net.Peers
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Core.Events
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

    let requestFromPeer requestId (node : NetworkNode) =
        node.SendRequestDataMessage requestId

    let respondToPeer (node : NetworkNode) targetAddress peerMessage =
        node.SendResponseDataMessage targetAddress peerMessage

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

    let requestTx (node : NetworkNode) txHash =
        node |> requestFromPeer (Tx txHash)

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

    let requestBlock (node : NetworkNode) blockNr =
        node |> requestFromPeer (Block blockNr)

    let private txPropagator node txHash =
        gossipTx node txHash

    let private blockPropagator node blockNumber =
        gossipBlock node blockNumber

    let publishEvent node appEvent =
        match appEvent with
        | TxSubmitted e -> txPropagator node e
        | TxReceived (e, _) -> txPropagator node e
        | BlockCommitted (e, _) -> blockPropagator node e
        | BlockReceived (e, _) -> blockPropagator node e

    let startGossip (node : NetworkNode) =
        let processPeerMessage = WorkflowsMock.processPeerMessage (node.GetNetworkAddress()) (respondToPeer node)
        node.StartGossip (publishEvent node)

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
            let nodeHasReceivedTx = RawMock.hasData (n.GetNetworkAddress()) networkMessageId
            test <@ nodeHasReceivedTx = true @>
        )

    let checkResponseReceived (node : NetworkNode) networkMessageId messageExists =
        let nodeHasReceivedTx = RawMock.hasData (node.GetNetworkAddress()) networkMessageId
        test <@ nodeHasReceivedTx = messageExists @>

    let createNodes nodeConfigList =
        // ARRANGE
        let fanout, tCycle, tFail = 4, 1000, 60000

        let createNode (nodeConfig : NetworkNodeConfig) =
            let getAllPeerNodes = DbMock.getAllPeerNodes nodeConfig.NetworkAddress
            let savePeerNode = DbMock.savePeerNode nodeConfig.NetworkAddress
            let removePeerNode = DbMock.removePeerNode nodeConfig.NetworkAddress
            let getValidators = DbMock.getValidators nodeConfig.NetworkAddress

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
                getValidators,
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

    let create3NodesMeshedNetwork () =
        let nodeConfig1 = {
            BootstrapNodes = [NetworkAddress "127.0.0.1:5556"; NetworkAddress "127.0.0.1:5557"]
            NetworkAddress = NetworkAddress "127.0.0.1:5555"
        }
        let nodeConfig2 = {
            BootstrapNodes = [NetworkAddress "127.0.0.1:5555"; NetworkAddress "127.0.0.1:5557"]
            NetworkAddress = NetworkAddress "127.0.0.1:5556"
        }
        let nodeConfig3 = {
            BootstrapNodes = [NetworkAddress "127.0.0.1:5555"; NetworkAddress "127.0.0.1:5556"]
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

    let testGossipSingleMessage nodeConfigList cycleCount =
        // ARRANGE
        let nodeList, tCycle = createNodes nodeConfigList

        // ACT
        nodeList |> List.iter(fun n -> startGossip n)

        System.Threading.Thread.Sleep (cycleCount * tCycle)

        let txHash = TxHash "txHash"
        gossipTx nodeList.[0] txHash
        RawMock.savePeerData (nodeList.[0].GetNetworkAddress()) (Tx txHash)

        System.Threading.Thread.Sleep (cycleCount * tCycle)

        // ASSERT
        checkMessageReceived nodeList (Tx txHash)

    let testGossipMultipleDifferentMessageTypes nodeConfigList cycleCount =
        // ARRANGE
        let nodeList, tCycle = createNodes nodeConfigList

        // ACT
        nodeList |> List.iter(fun n -> startGossip n)

        System.Threading.Thread.Sleep (cycleCount * tCycle)

        let txHash = TxHash "txHash"
        let blockNr = BlockNumber 1L

        gossipTx nodeList.[0] txHash
        gossipBlock nodeList.[0] blockNr

        RawMock.savePeerData (nodeList.[0].GetNetworkAddress()) (Tx txHash)
        RawMock.savePeerData (nodeList.[0].GetNetworkAddress()) (Block blockNr)

        System.Threading.Thread.Sleep (cycleCount * tCycle)

        // ASSERT
        checkMessageReceived nodeList (Tx txHash)
        checkMessageReceived nodeList (Block blockNr)

    let testGossipMultipleSameMessageTypes nodeConfigList cycleCount =
        // ARRANGE
        let nodeList, tCycle = createNodes nodeConfigList

        // ACT
        nodeList |> List.iter(fun n -> startGossip n)

        System.Threading.Thread.Sleep (cycleCount * tCycle)

        let txHash1 = TxHash "txHash1"
        let txHash2 = TxHash "txHash2"

        gossipTx nodeList.[0] txHash1
        gossipTx nodeList.[0] txHash2

        RawMock.savePeerData (nodeList.[0].GetNetworkAddress()) (Tx txHash1)
        RawMock.savePeerData (nodeList.[0].GetNetworkAddress()) (Tx txHash2)

        System.Threading.Thread.Sleep (cycleCount * tCycle)

        // ASSERT
        checkMessageReceived nodeList (Tx txHash1)
        checkMessageReceived nodeList (Tx txHash2)

    let testMulticastSingleMessage nodeConfigList cycleCount =
        // ARRANGE
        let nodeList, tCycle = createNodes nodeConfigList

        // ACT
        nodeList |> List.iter(fun n -> startGossip n)

        System.Threading.Thread.Sleep (cycleCount * tCycle)

        let txHash = TxHash "txHash"
        multicastTx nodeList.[0] txHash
        RawMock.savePeerData (nodeList.[0].GetNetworkAddress()) (Tx txHash)

        System.Threading.Thread.Sleep (tCycle)

        // ASSERT
        checkMessageReceived nodeList (Tx txHash)

    let testMulticastMultipleDifferentMessageTypes nodeConfigList cycleCount =
        // ARRANGE
        let nodeList, tCycle = createNodes nodeConfigList

        // ACT
        nodeList |> List.iter(fun n -> startGossip n)

        System.Threading.Thread.Sleep (cycleCount * tCycle)

        let txHash = TxHash "txHash"
        let blockNr = BlockNumber 1L

        multicastTx nodeList.[0] txHash
        multicastBlock nodeList.[0] blockNr
        RawMock.savePeerData (nodeList.[0].GetNetworkAddress()) (Tx txHash)
        RawMock.savePeerData (nodeList.[0].GetNetworkAddress()) (Block blockNr)

        System.Threading.Thread.Sleep (tCycle)

        // ASSERT
        checkMessageReceived nodeList (Tx txHash)
        checkMessageReceived nodeList (Block blockNr)

    let testMulticastMultipleSameMessageTypes nodeConfigList cycleCount =
        // ARRANGE
        let nodeList, tCycle = createNodes nodeConfigList

        // ACT
        nodeList |> List.iter(fun n -> startGossip n)

        System.Threading.Thread.Sleep (cycleCount * tCycle)

        let txHash1 = TxHash "txHash1"
        let txHash2 = TxHash "txHash2"

        multicastTx nodeList.[0] txHash1
        multicastTx nodeList.[0] txHash2
        RawMock.savePeerData (nodeList.[0].GetNetworkAddress()) (Tx txHash1)
        RawMock.savePeerData (nodeList.[0].GetNetworkAddress()) (Tx txHash2)

        System.Threading.Thread.Sleep (tCycle)

        // ASSERT
        checkMessageReceived nodeList (Tx txHash1)
        checkMessageReceived nodeList (Tx txHash2)

    let testRequestResponseSingleMessage nodeConfigList cycleCount txExists =
        // ARRANGE
        let nodeList, tCycle = createNodes nodeConfigList

        // ACT
        nodeList |> List.iter(fun n -> startGossip n)

        System.Threading.Thread.Sleep (cycleCount * tCycle)

        let txHash = TxHash "txHash"
        requestTx nodeList.[0] txHash

        let nodeCount = nodeList.Length
        if (txExists) then
            // Last node contains the tx.
            RawMock.savePeerData (nodeList.[nodeCount - 1].GetNetworkAddress()) (Tx txHash)

        // Worst case scenario : a single node contains the Tx and it's the last contacted for it => (n-1) cycles
        System.Threading.Thread.Sleep (4 * (nodeCount - 1) * tCycle)

        // ASSERT
        checkResponseReceived nodeList.[0] (Tx txHash) txExists

    let testRequestResponseMultipleDifferentMessageTypes nodeConfigList cycleCount =
        // ARRANGE
        let nodeList, tCycle = createNodes nodeConfigList

        // ACT
        nodeList |> List.iter(fun n -> startGossip n)

        System.Threading.Thread.Sleep (cycleCount * tCycle)

        let txHash = TxHash "txHash"
        let blockNr = BlockNumber 1L
        requestTx nodeList.[0] txHash
        requestBlock nodeList.[0] blockNr

        let nodeCount = nodeList.Length

        // Last node contains the tx.
        RawMock.savePeerData (nodeList.[nodeCount - 1].GetNetworkAddress()) (Tx txHash)
        // Last node contains the block.
        RawMock.savePeerData (nodeList.[nodeCount - 1].GetNetworkAddress()) (Block blockNr)

        // Worst case scenario : a single node contains the Tx and it's the last contacted for it => (n-1) cycles
        System.Threading.Thread.Sleep (2 * 4 * (nodeCount - 1) * tCycle)

        // ASSERT
        checkResponseReceived nodeList.[0] (Tx txHash) true
        checkResponseReceived nodeList.[0] (Block blockNr) true

    let testRequestResponseMultipleSameMessageTypes nodeConfigList cycleCount =
        // ARRANGE
        let nodeList, tCycle = createNodes nodeConfigList

        // ACT
        nodeList |> List.iter(fun n -> startGossip n)

        System.Threading.Thread.Sleep (cycleCount * tCycle)

        let txHash1 = TxHash "txHash1"
        let txHash2 = TxHash "txHash2"
        requestTx nodeList.[0] txHash1
        requestTx nodeList.[0] txHash2

        let nodeCount = nodeList.Length

        // Last node contains the tx.
        RawMock.savePeerData (nodeList.[nodeCount - 1].GetNetworkAddress()) (Tx txHash1)
        // Last node contains the tx.
        RawMock.savePeerData (nodeList.[nodeCount - 1].GetNetworkAddress()) (Tx txHash2)

        // Worst case scenario : a single node contains the Tx and it's the last contacted for it => (n-1) cycles
        System.Threading.Thread.Sleep (2 * 4 * (nodeCount - 1) * tCycle)

        // ASSERT
        checkResponseReceived nodeList.[0] (Tx txHash1) true
        checkResponseReceived nodeList.[0] (Tx txHash2) true

    [<Fact>]
    let ``Network - GossipDiscovery 3 nodes same bootstrap node`` () =
        // ARRANGE
        testCleanup()

        let nodeConfigList = create3NodesConfigSameBootstrapNode ()

        testGossipDiscovery nodeConfigList 5

    [<Fact>]
    let ``Network - GossipDiscovery 3 nodes different bootstrap node`` () =
        // ARRANGE
        testCleanup()

        let nodeConfigList = create3NodesConfigDifferentBoostrapNode ()

        testGossipDiscovery nodeConfigList 5

    [<Fact>]
    let ``Network - GossipDiscovery 100 nodes`` () =
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
    let ``Network - GossipMessagePassing 3 nodes single message`` () =
        // ARRANGE
        testCleanup()

        let nodeConfigList = create3NodesMeshedNetwork ()

        testGossipSingleMessage nodeConfigList 5

    [<Fact>]
    let ``Network - GossipMessagePassing multiple different message types`` () =
        // ARRANGE
        testCleanup()

        let nodeConfigList = create3NodesMeshedNetwork ()

        testGossipMultipleDifferentMessageTypes nodeConfigList 5

    [<Fact>]
    let ``Network - GossipMessagePassing multiple same message types`` () =
        // ARRANGE
        testCleanup()

        let nodeConfigList = create3NodesMeshedNetwork ()

        testGossipMultipleSameMessageTypes nodeConfigList 5

    [<Fact>]
    let ``Network - MulticastMessagePassing 3 nodes single message`` () =
        // ARRANGE
        testCleanup()

        let nodeConfigList = create3NodesMeshedNetwork ()

        testMulticastSingleMessage nodeConfigList 5

    [<Fact>]
    let ``Network - MulticastMessagePassing multiple different message types`` () =
        // ARRANGE
        testCleanup()

        let nodeConfigList = create3NodesMeshedNetwork ()

        testMulticastMultipleDifferentMessageTypes nodeConfigList 5

    [<Fact>]
    let ``Network - MulticastMessagePassing multiple same message types`` () =
        // ARRANGE
        testCleanup()

        let nodeConfigList = create3NodesMeshedNetwork ()

        testMulticastMultipleSameMessageTypes nodeConfigList 5

    [<Fact>]
    let ``Network - Request/Response Tx exists`` () =
        // ARRANGE
        testCleanup()

        let nodeConfigList = create3NodesMeshedNetwork ()

        testRequestResponseSingleMessage nodeConfigList 5 true

    [<Fact>]
    let ``Network - Request/Response Tx doesn't exist`` () =
        // ARRANGE
        testCleanup()

        let nodeConfigList = create3NodesMeshedNetwork ()

        testRequestResponseSingleMessage nodeConfigList 5 false

    [<Fact>]
    let ``Network - Request/Response multiple different message types`` () =
        // ARRANGE
        testCleanup()

        let nodeConfigList = create3NodesMeshedNetwork ()

        testRequestResponseMultipleDifferentMessageTypes nodeConfigList 5

    [<Fact>]
    let ``Network - Request/Response multiple same message types`` () =
        // ARRANGE
        testCleanup()

        let nodeConfigList = create3NodesMeshedNetwork ()

        testRequestResponseMultipleSameMessageTypes nodeConfigList 5
