namespace Own.Blockchain.Public.Net.Tests

open System.Threading
open System.Collections.Concurrent
open Own.Common.FSharp
open Own.Blockchain.Public.Crypto
open Own.Blockchain.Common
open Own.Blockchain.Public.Net
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Core.Events
open Xunit
open Swensen.Unquote
open MessagePack.Resolvers
open MessagePack.FSharp

module PeerTests =

    CompositeResolver.RegisterAndSetAsDefault(
        FSharpResolver.Instance,
        StandardResolver.Instance
    )

    let nodeConfigBase = {
        Identity = Conversion.stringToBytes "" |> PeerNetworkIdentity
        ListeningAddress = NetworkAddress ""
        PublicAddress = NetworkAddress "" |> Some
        BootstrapNodes = []
        AllowPrivateNetworkPeers = true
        MaxConnectedPeers = 200
        DnsResolverCacheExpirationTime = 600
        NetworkSendoutRetryTimeout = 500
        PeerMessageMaxSize = 1_000_000
    }

    let gossipConfigBase = {
        FanoutPercentage = 4
        GossipDiscoveryIntervalMillis = 100
        GossipIntervalMillis = 100
        MissedHeartbeatIntervalMillis = 60000
        PeerResponseThrottlingTime = 5000
    }

    let testCleanup () =
        DbMock.reset ()
        RawMock.reset ()
        Thread.Sleep(1000)

    let setupTest () =
        testCleanup ()

    let getNetworkId =
        let networkCode = "OWN_PUBLIC_BLOCKCHAIN_TEST"
        let networkId = lazy (Hashing.networkId networkCode)
        fun () -> networkId.Value

    let sendMessage peerMessage (node : NetworkNode) =
        node.SendMessage peerMessage

    let requestFromPeer requestIds (node : NetworkNode) =
        node.SendRequestDataMessage requestIds None

    let respondToPeer (node : NetworkNode) targetAddress peerMessageEnvelope =
        node.SendResponseDataMessage targetAddress peerMessageEnvelope

    let getPeerList (node : NetworkNode) =
        fun () -> node.GetActivePeers()

    let gossipTx (node : NetworkNode) txHash =
        let gossipMessage = GossipMessage {
            MessageId = Tx txHash
            SenderAddress = None
            Data = "txEnvelope" |> Conversion.stringToBytes
        }
        let peerMessageEnvelope = {
            PeerMessageEnvelope.NetworkId = getNetworkId ()
            PeerMessage = gossipMessage
        }
        node |> sendMessage peerMessageEnvelope

    let multicastTx (node : NetworkNode) txHash =
        let multicastMessage = MulticastMessage {
            MessageId = Tx txHash
            SenderIdentity = None
            Data = "txEnvelope" |> Conversion.stringToBytes
        }
        let peerMessageEnvelope = {
            PeerMessageEnvelope.NetworkId = getNetworkId ()
            PeerMessage = multicastMessage
        }
        node |> sendMessage peerMessageEnvelope

    let requestTx (node : NetworkNode) txHash =
        node |> requestFromPeer [ (Tx txHash) ]

    let gossipBlock (node : NetworkNode) blockNr =
        let gossipMessage = GossipMessage {
            MessageId = Block blockNr
            SenderAddress = None
            Data = "blockEnvelope" |> Conversion.stringToBytes
        }
        let peerMessageEnvelope = {
            PeerMessageEnvelope.NetworkId = getNetworkId ()
            PeerMessage = gossipMessage
        }
        node |> sendMessage peerMessageEnvelope

    let multicastBlock (node : NetworkNode) blockNr =
        let gossipMessage = GossipMessage {
            MessageId = Block blockNr
            SenderAddress = None
            Data = "blockEnvelope" |> Conversion.stringToBytes
        }
        let peerMessageEnvelope = {
            PeerMessageEnvelope.NetworkId = getNetworkId ()
            PeerMessage = gossipMessage
        }
        node |> sendMessage peerMessageEnvelope

    let requestBlock (node : NetworkNode) blockNr =
        node |> requestFromPeer [ (Block blockNr) ]

    let private txPropagator node txHash =
        gossipTx node txHash

    let private blockPropagator node blockNumber =
        gossipBlock node blockNumber

    let private peerMessageHandlers =
        new ConcurrentDictionary<NetworkAddress, MailboxProcessor<PeerMessageEnvelope> option>()

    let private updatePeerListHandlers =
        new ConcurrentDictionary<NetworkAddress, MailboxProcessor<GossipPeer list> option>()

    let private invokePeerMessageHandler (node : NetworkNode) m =
        let found, peerMessageHandler = peerMessageHandlers.TryGetValue (node.GetListenAddress())
        if found then
            match peerMessageHandler with
            | Some h -> h.Post m
            | None -> Log.error "PeerMessageHandler agent not started for node %s"

    let private invokeUpdatePeerListHandler (node : NetworkNode) peerList =
        let found, updatePeerListHandler = updatePeerListHandlers.TryGetValue (node.GetListenAddress())
        if found then
            match updatePeerListHandler with
            | Some h -> h.Post peerList
            | None -> Log.error "UpdatePeerListHandler agent not started for node %s"

    // TODO: Fix the network tests by adjusting the mocked propagation logic.
    let publishEvent node appEvent =
        match appEvent with
        | PeerMessageReceived message ->
            invokePeerMessageHandler node message
        | TxSubmitted (txHash, _) ->
            txPropagator node txHash
        | TxReceived (txHash, _)
        | TxFetched (txHash, _) ->
            ()
        | TxVerified (txHash, _) ->
            txPropagator node txHash
        | TxStored (txHash, isFetched) ->
            if not isFetched then
                txPropagator node txHash
        | BlockCommitted (blockNumber, blockEnvelopeDto)
        | BlockReceived (blockNumber, blockEnvelopeDto)
        | BlockFetched (blockNumber, blockEnvelopeDto) ->
            ()
        | BlockStored (blockNumber, isFetched) ->
            if not isFetched then
                blockPropagator node blockNumber
        | BlockReady blockNumber
        | BlockApplied blockNumber ->
            ()
        | ConsensusMessageReceived c
        | ConsensusCommandInvoked c ->
            ()
        | ConsensusStateRequestReceived _ ->
            ()
        | ConsensusStateResponseReceived _ ->
            ()
        | EquivocationProofDetected (proof, validatorAddress) ->
            ()
        | EquivocationProofReceived proof
        | EquivocationProofFetched proof ->
            ()
        | EquivocationProofStored (equivocationProofHash, isFetched) ->
            ()
        | BlockchainHeadReceived _ ->
            ()
        | PeerListReceived peerList ->
            invokeUpdatePeerListHandler node peerList

    let private startPeerMessageHandler (node : NetworkNode) =
        let found, _ = peerMessageHandlers.TryGetValue (node.GetListenAddress())
        if not found then
            let peerMessageHandler =
                Agent.start <| fun peerMessage ->
                    async {
                        WorkflowsMock.processPeerMessage
                            (node.GetListenAddress())
                            (respondToPeer node)
                            (getPeerList node)
                            getNetworkId
                            peerMessage
                        |> Option.iter (
                            List.iter (
                                Result.handle
                                    (Option.iter (publishEvent node))
                                    Log.appErrors
                            )
                        )
                    }
                |> Some

            peerMessageHandlers.AddOrUpdate (
                (node.GetListenAddress()),
                peerMessageHandler,
                fun _ _ -> peerMessageHandler
            )
            |> ignore

    let private startUpdatePeerListHandler (node : NetworkNode) =
        let found, _ = updatePeerListHandlers.TryGetValue (node.GetListenAddress())
        if not found then
            let updatePeerListHandler =
                Agent.start <| fun peerList ->
                    async {
                        node.ReceivePeers {ActivePeers = peerList}
                    }
                |> Some

            updatePeerListHandlers.AddOrUpdate (
                (node.GetListenAddress()),
                updatePeerListHandler,
                fun _ _ -> updatePeerListHandler
            )
            |> ignore

    let startGossip (node : NetworkNode) =
        startPeerMessageHandler node
        startUpdatePeerListHandler node
        node.StartGossip (publishEvent node)

    let stopGossip (node : NetworkNode) =
        node.StopGossip ()

    let checkGossipDiscoveryConvergence (nodeList : NetworkNode list) =
        let expectedPeerList =
            nodeList
            |> List.map (fun n -> n.GetListenAddress ())
            |> List.sort

        nodeList
        |> List.iter (fun n ->
            let nodePeerList =
                n.GetActivePeers()
                |> List.map (fun m -> m.NetworkAddress)
                |> List.sort

            test <@ nodePeerList = expectedPeerList @>
        )

        nodeList |> List.iter stopGossip

    let checkMessageReceived (nodeList : NetworkNode list) networkMessageId =
        nodeList |> List.iter (fun n ->
            let nodeHasReceivedTx = RawMock.hasData (n.GetListenAddress()) networkMessageId
            test <@ nodeHasReceivedTx = true @>
        )

        nodeList |> List.iter stopGossip

    let checkResponseReceived (node : NetworkNode) networkMessageId messageExists =
        let nodeHasReceivedTx = RawMock.hasData (node.GetListenAddress()) networkMessageId
        test <@ nodeHasReceivedTx = messageExists @>

    let resolveHostToIpAddress (networkAddress : string) allowPrivatePeers =
        if networkAddress.Contains("Node1") then
            "127.0.0.1:1001" |> NetworkAddress |> Some
        elif networkAddress.Contains("Node2") then
            "127.0.0.1:1002" |> NetworkAddress |> Some
        elif networkAddress.Contains("Node3") then
            "127.0.0.1:1003" |> NetworkAddress |> Some
        else
            Own.Blockchain.Public.Net.Utils.resolveHostToIpAddress networkAddress allowPrivatePeers

    let createNodesWithGossipConfig gossipConfig nodeConfigList =
        // ARRANGE
        let createNode (nodeConfig : NetworkNodeConfig) =
            let getAllPeerNodes = DbMock.getAllPeerNodes nodeConfig.ListeningAddress
            let savePeerNode = DbMock.savePeerNode nodeConfig.ListeningAddress
            let removePeerNode = DbMock.removePeerNode nodeConfig.ListeningAddress
            let getValidators = DbMock.getValidators nodeConfig.ListeningAddress

            NetworkNode (
                getNetworkId,
                getAllPeerNodes,
                savePeerNode,
                removePeerNode,
                resolveHostToIpAddress,
                TransportMock.init,
                TransportMock.sendGossipDiscoveryMessage,
                TransportMock.sendGossipMessage,
                TransportMock.sendMulticastMessage,
                TransportMock.sendRequestMessage,
                TransportMock.sendResponseMessage,
                TransportMock.receiveMessage,
                TransportMock.closeConnection,
                TransportMock.closeAllConnections,
                getValidators,
                nodeConfig,
                gossipConfig
            )

        let nodeList =
            nodeConfigList
            |> List.map (fun config -> createNode config)

        (nodeList, gossipConfig.GossipIntervalMillis)

    let createNodes nodeConfigList =
        createNodesWithGossipConfig gossipConfigBase nodeConfigList

    let create3NodesWithHostNames (hosts : string list) =
        let mutable nodeIndex = 0;
        let nodeConfigs =
            hosts
            |> List.map (fun address ->
                nodeIndex <- nodeIndex + 1
                {
                    nodeConfigBase with
                        Identity = Conversion.stringToBytes address |> PeerNetworkIdentity
                        ListeningAddress = NetworkAddress (sprintf "127.0.0.1:100%i" nodeIndex)
                        PublicAddress = NetworkAddress address |> Some
                        BootstrapNodes = []
                }
            )

        let nodeConfig0 = nodeConfigs.[0]

        let nodeConfig1 = {
            nodeConfigs.[1] with
                BootstrapNodes = [NetworkAddress hosts.[0]]
        }

        let nodeConfig2 = {
            nodeConfigs.[2] with
                BootstrapNodes = [NetworkAddress hosts.[0]]
        }

        [nodeConfig0; nodeConfig1; nodeConfig2]

    let create3NodesConfigSameBootstrapNode (ports : int list) =
        let addresses =
            ports
            |> List.map (sprintf "127.0.0.1:%i")

        let nodeConfigs =
            addresses
            |> List.map (fun address ->
                {
                    nodeConfigBase with
                        Identity = Conversion.stringToBytes address |> PeerNetworkIdentity
                        ListeningAddress = NetworkAddress address
                        PublicAddress = NetworkAddress address |> Some
                        BootstrapNodes = []
                }
            )

        let nodeConfig0 = nodeConfigs.[0]

        let nodeConfig1 = {
            nodeConfigs.[1] with
                BootstrapNodes = [NetworkAddress addresses.[0]]
        }

        let nodeConfig2 = {
            nodeConfigs.[2] with
                BootstrapNodes = [NetworkAddress addresses.[0]]
        }

        [nodeConfig0; nodeConfig1; nodeConfig2]

    let create3NodesNoPrivateAllowed (ports : int list) =
        ports
        |> create3NodesConfigSameBootstrapNode
        |> List.map (fun nodeConfig -> {nodeConfig with AllowPrivateNetworkPeers = false})

    let create3Nodes1PrivateConfigSameBootstrapNode (ports : int list) =
        let nodes =
            ports
            |> create3NodesConfigSameBootstrapNode
        [nodes.[0]; nodes.[1]; { nodes.[2] with PublicAddress = None }]

    let create3NodesConfigDifferentBoostrapNode (ports : int list) =
        let addresses =
            ports
            |> List.map (sprintf "127.0.0.1:%i")

        let nodeConfigs =
            addresses
            |> List.map (fun address ->
                {
                    nodeConfigBase with
                        Identity = Conversion.stringToBytes address |> PeerNetworkIdentity
                        ListeningAddress = NetworkAddress address
                        PublicAddress = NetworkAddress address|> Some
                        BootstrapNodes = []
                }
            )

        let nodeConfig0 = nodeConfigs.[0]

        let nodeConfig1 = {
            nodeConfigs.[1] with
                BootstrapNodes = [NetworkAddress addresses.[0]]
        }
        let nodeConfig2 = {
            nodeConfigs.[2] with
                BootstrapNodes = [NetworkAddress addresses.[1]]
        }
        [nodeConfig0; nodeConfig1; nodeConfig2]

    let create5NodesConfig2BoostrapNodes (ports : int list) =
        let addresses =
            ports
            |> List.map (sprintf "127.0.0.1:%i")

        addresses
        |> List.map (fun address ->
            {
                nodeConfigBase with
                    Identity = Conversion.stringToBytes address |> PeerNetworkIdentity
                    ListeningAddress = NetworkAddress address
                    PublicAddress = NetworkAddress address|> Some
                    BootstrapNodes = addresses |> List.truncate 2 |> List.map NetworkAddress
            }
        )

    let create3NodesMeshedNetwork (ports : int list) =
        let address1, address2, address3 =
            sprintf "127.0.0.1:%i" ports.[0],
            sprintf "127.0.0.1:%i" ports.[1],
            sprintf "127.0.0.1:%i" ports.[2]

        let nodeConfig1 = {
            nodeConfigBase with
                Identity = Conversion.stringToBytes address1 |> PeerNetworkIdentity
                ListeningAddress = NetworkAddress address1
                PublicAddress = NetworkAddress address1 |> Some
                BootstrapNodes = [NetworkAddress address2; NetworkAddress address3]
        }
        let nodeConfig2 = {
            nodeConfigBase with
                Identity = Conversion.stringToBytes address2 |> PeerNetworkIdentity
                ListeningAddress = NetworkAddress address2
                PublicAddress = NetworkAddress address2 |> Some
                BootstrapNodes = [NetworkAddress address1; NetworkAddress address3]
        }
        let nodeConfig3 = {
            nodeConfigBase with
                Identity = Conversion.stringToBytes address3 |> PeerNetworkIdentity
                ListeningAddress = NetworkAddress address3
                PublicAddress = NetworkAddress address3 |> Some
                BootstrapNodes = [NetworkAddress address1; NetworkAddress address2]
        }
        [nodeConfig1; nodeConfig2; nodeConfig3]

    let testGossipDiscovery nodeConfigList cycleCount =
        // ARRANGE
        let nodeList, tCycle = createNodes nodeConfigList

        // ACT
        nodeList |> List.iter (fun n -> startGossip n)

        System.Threading.Thread.Sleep (cycleCount * tCycle)

        // ASSERT
        checkGossipDiscoveryConvergence nodeList

    let testGossipDiscoveryMaxPeers nodeConfigList maxPeerCount cycleCount =
        // ARRANGE
        let nodeList, tCycle = createNodes nodeConfigList

        // ACT
        nodeList |> List.iter (fun n -> startGossip n)

        System.Threading.Thread.Sleep (cycleCount * tCycle)

        nodeList
        |> List.iter (fun n ->
            let peerCount = n.GetActivePeers().Length
            test <@ peerCount = maxPeerCount @>
        )

        nodeList |> List.iter stopGossip

    let testGossipDiscoveryNotAchieved nodeConfigList cycleCount =
        // ARRANGE
        let nodeList, tCycle = createNodes nodeConfigList

        // ACT
        nodeList |> List.iter (fun n -> startGossip n)

        System.Threading.Thread.Sleep (cycleCount * tCycle)

        // ASSERT
        nodeList
        |> List.iter (fun node ->
            test <@ node.GetActivePeers().Length = 0 @>
        )

        nodeList |> List.iter stopGossip

    let testNodeLeavesNetwork nodeConfigList cycleCount =
        // ARRANGE
        let gossipConfig = { gossipConfigBase with MissedHeartbeatIntervalMillis = 1000 }
        let nodeList, tCycle = createNodesWithGossipConfig gossipConfig nodeConfigList

        // ACT
        nodeList |> List.iter (fun n -> startGossip n)

        System.Threading.Thread.Sleep (cycleCount * tCycle)

        // ASSERT
        stopGossip nodeList.[2]

        System.Threading.Thread.Sleep (2 * cycleCount * tCycle)

        nodeList
        |> List.take 2
        |> List.iter (fun node ->
            test <@ node.GetActivePeers().Length = 2 @>
        )

        nodeList |> List.take 2 |> List.iter stopGossip

    let testNodeFallsbackToBootstrap nodeConfigList cycleCount =
        // ARRANGE
        let gossipConfig = { gossipConfigBase with MissedHeartbeatIntervalMillis = 1000 }
        let nodeList, tCycle = createNodesWithGossipConfig gossipConfig nodeConfigList

        // ACT
        nodeList |> List.iter (fun n -> startGossip n)

        System.Threading.Thread.Sleep (cycleCount * tCycle)

        // ASSERT
        nodeList |> List.iter stopGossip

        System.Threading.Thread.Sleep (2 * cycleCount * tCycle)

        nodeList
        |> List.skip 2
        |> List.iter (fun node ->
            // The non-bootstrap nodes have themselves + 2 bootstrap as peers.
            test <@ node.GetActivePeers().Length = 3 @>
        )

    let testGossipDiscoveryDeadNodeRejoins nodeConfigList cycleCount =
        // ARRANGE
        let gossipConfig = { gossipConfigBase with MissedHeartbeatIntervalMillis = 1000 }
        let nodeList, tCycle = createNodesWithGossipConfig gossipConfig nodeConfigList

        // ACT
        nodeList |> List.iter (fun n -> startGossip n)

        System.Threading.Thread.Sleep (cycleCount * tCycle)

        // ASSERT
        nodeList
        |> List.iter (fun node ->
            test <@ node.GetActivePeers().Length = 3 @>
        )

        // Last node becomes dead.
        stopGossip nodeList.[2]

        System.Threading.Thread.Sleep (2 * gossipConfig.MissedHeartbeatIntervalMillis)

        // Dead node removed from active peers.
        nodeList
        |> List.truncate 2
        |> List.iter (fun node ->
            test <@ node.GetActivePeers().Length = 2 @>
        )

        // Dead node rejoins network.
        startGossip nodeList.[2]

        System.Threading.Thread.Sleep (cycleCount * tCycle)

        // The formerly dead none added as active peer.
        nodeList
        |> List.iter (fun node ->
            test <@ node.GetActivePeers().Length = 3 @>
        )

        // Cleanup
        nodeList |> List.iter stopGossip

    let testGossipDiscoveryForPrivateNode nodeConfigList cycleCount =
        // ARRANGE
        let nodeList, tCycle = createNodes nodeConfigList

        // ACT
        nodeList |> List.iter (fun n -> startGossip n)

        // Wait for Gossip Discovery among the public nodes.
        System.Threading.Thread.Sleep (2 * cycleCount * tCycle)

        // Wait for peer list request.
        System.Threading.Thread.Sleep (8 * cycleCount * tCycle)

        // ASSERT
        // Expect bootstrap + the public node
        test <@ nodeList.[2].GetActivePeers().Length = 2 @>

        // Cleanup
        nodeList |> List.iter stopGossip

    let testGossipDiscoveryHostResolvedToIp nodeConfigList cycleCount =
        // ARRANGE
        let nodeList, tCycle = createNodes nodeConfigList

        // ACT
        nodeList |> List.iter (fun n -> startGossip n)

        System.Threading.Thread.Sleep (cycleCount * tCycle)

        // ASSERT
        nodeList
        |> List.iter (fun node ->
            let peers = node.GetActivePeers()
            test <@ peers.Length = 3 @>
            peers |> List.iter (fun peer ->
                test <@ peer.NetworkAddress.Value.Contains("127.0.0.1") @>
            )
        )

        // Cleanup
        nodeList |> List.iter stopGossip

    let testGossipSingleMessage nodeConfigList cycleCount =
        // ARRANGE
        let nodeList, tCycle = createNodes nodeConfigList

        // ACT
        nodeList |> List.iter (fun n -> startGossip n)

        System.Threading.Thread.Sleep (cycleCount * tCycle)

        let txHash = TxHash "txHash"
        gossipTx nodeList.[0] txHash
        RawMock.savePeerData (nodeList.[0].GetListenAddress()) (Tx txHash)

        System.Threading.Thread.Sleep (cycleCount * tCycle)

        // ASSERT
        checkMessageReceived nodeList (Tx txHash)

    let testGossipMultipleDifferentMessageTypes nodeConfigList cycleCount =
        // ARRANGE
        let nodeList, tCycle = createNodes nodeConfigList

        // ACT
        nodeList |> List.iter (fun n -> startGossip n)

        System.Threading.Thread.Sleep (cycleCount * tCycle)

        let txHash = TxHash "txHash"
        let blockNr = BlockNumber 1L

        gossipTx nodeList.[0] txHash
        gossipBlock nodeList.[0] blockNr

        RawMock.savePeerData (nodeList.[0].GetListenAddress()) (Tx txHash)
        RawMock.savePeerData (nodeList.[0].GetListenAddress()) (Block blockNr)

        System.Threading.Thread.Sleep (cycleCount * tCycle)

        // ASSERT
        checkMessageReceived nodeList (Tx txHash)
        checkMessageReceived nodeList (Block blockNr)

    let testGossipMultipleSameMessageTypes nodeConfigList cycleCount =
        // ARRANGE
        let nodeList, tCycle = createNodes nodeConfigList

        // ACT
        nodeList |> List.iter (fun n -> startGossip n)

        System.Threading.Thread.Sleep (cycleCount * tCycle)

        let txHash1 = TxHash "txHash1"
        let txHash2 = TxHash "txHash2"

        gossipTx nodeList.[0] txHash1
        gossipTx nodeList.[0] txHash2

        RawMock.savePeerData (nodeList.[0].GetListenAddress()) (Tx txHash1)
        RawMock.savePeerData (nodeList.[0].GetListenAddress()) (Tx txHash2)

        System.Threading.Thread.Sleep (cycleCount * tCycle)

        // ASSERT
        checkMessageReceived nodeList (Tx txHash1)
        checkMessageReceived nodeList (Tx txHash2)

    let testMulticastSingleMessage nodeConfigList cycleCount =
        // ARRANGE
        let nodeList, tCycle = createNodes nodeConfigList

        // ACT
        nodeList |> List.iter (fun n -> startGossip n)

        System.Threading.Thread.Sleep (cycleCount * tCycle)

        let txHash = TxHash "txHash"
        multicastTx nodeList.[0] txHash
        RawMock.savePeerData (nodeList.[0].GetListenAddress()) (Tx txHash)

        System.Threading.Thread.Sleep tCycle

        // ASSERT
        checkMessageReceived nodeList (Tx txHash)

    let testMulticastMultipleDifferentMessageTypes nodeConfigList cycleCount =
        // ARRANGE
        let nodeList, tCycle = createNodes nodeConfigList

        // ACT
        nodeList |> List.iter (fun n -> startGossip n)

        System.Threading.Thread.Sleep (cycleCount * tCycle)

        let txHash = TxHash "txHash"
        let blockNr = BlockNumber 1L

        multicastTx nodeList.[0] txHash
        multicastBlock nodeList.[0] blockNr
        RawMock.savePeerData (nodeList.[0].GetListenAddress()) (Tx txHash)
        RawMock.savePeerData (nodeList.[0].GetListenAddress()) (Block blockNr)

        System.Threading.Thread.Sleep tCycle

        // ASSERT
        checkMessageReceived nodeList (Tx txHash)
        checkMessageReceived nodeList (Block blockNr)

    let testMulticastMultipleSameMessageTypes nodeConfigList cycleCount =
        // ARRANGE
        let nodeList, tCycle = createNodes nodeConfigList

        // ACT
        nodeList |> List.iter (fun n -> startGossip n)

        System.Threading.Thread.Sleep (cycleCount * tCycle)

        let txHash1 = TxHash "txHash1"
        let txHash2 = TxHash "txHash2"

        multicastTx nodeList.[0] txHash1
        multicastTx nodeList.[0] txHash2
        RawMock.savePeerData (nodeList.[0].GetListenAddress()) (Tx txHash1)
        RawMock.savePeerData (nodeList.[0].GetListenAddress()) (Tx txHash2)

        System.Threading.Thread.Sleep tCycle

        // ASSERT
        checkMessageReceived nodeList (Tx txHash1)
        checkMessageReceived nodeList (Tx txHash2)

    let testRequestResponseSingleMessage nodeConfigList cycleCount txExists =
        // ARRANGE
        let nodeList, tCycle = createNodes nodeConfigList

        // ACT
        nodeList |> List.iter (fun n -> startGossip n)

        System.Threading.Thread.Sleep (cycleCount * tCycle)

        let txHash = TxHash "txHash"
        requestTx nodeList.[0] txHash

        let nodeCount = nodeList.Length
        if txExists then
            // Last node contains the tx.
            RawMock.savePeerData (nodeList.[nodeCount - 1].GetListenAddress()) (Tx txHash)

        // Worst case scenario : a single node contains the TX and it's the last contacted for it => (n-1) cycles
        System.Threading.Thread.Sleep (4 * (nodeCount - 1) * tCycle)

        // ASSERT
        checkResponseReceived nodeList.[0] (Tx txHash) txExists

        nodeList |> List.iter stopGossip

    let testRequestResponseMultipleDifferentMessageTypes nodeConfigList cycleCount =
        // ARRANGE
        let nodeList, tCycle = createNodes nodeConfigList

        // ACT
        nodeList |> List.iter (fun n -> startGossip n)

        System.Threading.Thread.Sleep (cycleCount * tCycle)

        let txHash = TxHash "txHash"
        let blockNr = BlockNumber 1L
        requestTx nodeList.[0] txHash
        requestBlock nodeList.[0] blockNr

        let nodeCount = nodeList.Length

        // Last node contains the tx.
        RawMock.savePeerData (nodeList.[nodeCount - 1].GetListenAddress()) (Tx txHash)
        // Last node contains the block.
        RawMock.savePeerData (nodeList.[nodeCount - 1].GetListenAddress()) (Block blockNr)

        // Worst case scenario : a single node contains the TX and it's the last contacted for it => (n-1) cycles
        System.Threading.Thread.Sleep (2 * 4 * (nodeCount - 1) * tCycle)

        // ASSERT
        checkResponseReceived nodeList.[0] (Tx txHash) true
        checkResponseReceived nodeList.[0] (Block blockNr) true

        nodeList |> List.iter stopGossip

    let testRequestResponseMultipleSameMessageTypes nodeConfigList cycleCount =
        // ARRANGE
        let nodeList, tCycle = createNodes nodeConfigList

        // ACT
        nodeList |> List.iter (fun n -> startGossip n)

        System.Threading.Thread.Sleep (cycleCount * tCycle)

        let txHash1 = TxHash "txHash1"
        let txHash2 = TxHash "txHash2"
        requestTx nodeList.[0] txHash1
        requestTx nodeList.[0] txHash2

        let nodeCount = nodeList.Length

        // Last node contains the tx.
        RawMock.savePeerData (nodeList.[nodeCount - 1].GetListenAddress()) (Tx txHash1)
        // Last node contains the tx.
        RawMock.savePeerData (nodeList.[nodeCount - 1].GetListenAddress()) (Tx txHash2)

        // Worst case scenario : a single node contains the TX and it's the last contacted for it => (n-1) cycles
        System.Threading.Thread.Sleep (2 * 4 * (nodeCount - 1) * tCycle)

        // ASSERT
        checkResponseReceived nodeList.[0] (Tx txHash1) true
        checkResponseReceived nodeList.[0] (Tx txHash2) true

        nodeList |> List.iter stopGossip

    [<Fact>]
    let ``Network - GossipDiscovery 3 nodes same bootstrap node`` () =
        // ARRANGE
        setupTest ()

        let nodeConfigList = [111; 112; 113] |> create3NodesConfigSameBootstrapNode

        testGossipDiscovery nodeConfigList 5

    [<Fact>]
    let ``Network - GossipDiscovery 3 nodes different bootstrap node`` () =
        // ARRANGE
        setupTest ()

        let nodeConfigList = [211; 212; 213] |> create3NodesConfigDifferentBoostrapNode

        testGossipDiscovery nodeConfigList 5

    [<Fact>]
    let ``Network - GossipDiscovery private peers not allowed`` () =
        // ARRANGE
        setupTest ()

        let nodeConfigList = [311; 312; 313] |> create3NodesNoPrivateAllowed

        testGossipDiscoveryNotAchieved nodeConfigList 5

    [<Fact>]
    let ``Network - GossipDiscovery invalid ip/port`` () =
        // ARRANGE
        setupTest ()

        let nodeConfigList = [0; 123456; 123457] |> create3NodesNoPrivateAllowed

        testGossipDiscoveryNotAchieved nodeConfigList 5

    [<Fact>]
    let ``Network - GossipDiscovery node leaves the network`` () =
        // ARRANGE
        setupTest ()

        let nodeConfigList = [411; 412; 413] |> create3NodesConfigSameBootstrapNode

        testNodeLeavesNetwork nodeConfigList 5

    [<Fact>]
    let ``Network - GossipDiscovery node fallback to bootstrap after losing peers`` () =
        // ARRANGE
        setupTest ()

        let nodeConfigList = [511..515] |> create5NodesConfig2BoostrapNodes

        testNodeFallsbackToBootstrap nodeConfigList 5

    // [<Fact>]
    let ``Network - GossipDiscovery 10 max peers nodes`` () =
        // ARRANGE
        setupTest ()

        let address611 = "127.0.0.1:611"

        // Max connected peers = 10.
        let nodeConfig1 = {
            nodeConfigBase with
                Identity = Conversion.stringToBytes address611 |> PeerNetworkIdentity
                ListeningAddress = NetworkAddress address611
                PublicAddress = NetworkAddress address611 |> Some
                BootstrapNodes = []
                MaxConnectedPeers = 10
        }

        // Total peers = 20.
        let nodeConfigList =
            [612..630]
            |> List.map (fun port ->
                {
                    nodeConfigBase with
                        Identity = (sprintf "127.0.0.1:%i" port) |> Conversion.stringToBytes |> PeerNetworkIdentity
                        ListeningAddress = NetworkAddress (sprintf "127.0.0.1:%i" port)
                        PublicAddress = NetworkAddress (sprintf "127.0.0.1:%i" port) |> Some
                        BootstrapNodes = [NetworkAddress address611]
                        MaxConnectedPeers = 10
                }
            )

        testGossipDiscoveryMaxPeers (nodeConfig1 :: nodeConfigList) nodeConfig1.MaxConnectedPeers 80

    [<Fact>]
    let ``Network - GossipDiscovery dead node rejoins network`` () =
        // ARRANGE
        setupTest ()

        let nodeConfigList = [711; 712; 713] |> create3NodesConfigSameBootstrapNode

        testGossipDiscoveryDeadNodeRejoins nodeConfigList 5

    [<Fact>]
    let ``Network - GossipDiscovery private node`` () =
        // ARRANGE
        setupTest ()

        let nodeConfigList = [811; 812; 813] |> create3Nodes1PrivateConfigSameBootstrapNode

        testGossipDiscoveryForPrivateNode nodeConfigList 10

    [<Fact>]
    let ``Network - Gossip Discovery host resolves to Ip`` () =
        // ARRANGE
        setupTest ()

        let nodeConfigList =
            [1..3]
            |> List.map (sprintf "Node%i")
            |> create3NodesWithHostNames

        testGossipDiscoveryHostResolvedToIp nodeConfigList 5

    [<Fact>]
    let ``Network - GossipDiscovery 100 nodes`` () =
        // ARRANGE
        setupTest ()

        let address311 = "127.0.0.1:311"
        let nodeConfig1 = {
            nodeConfigBase with
                Identity = Conversion.stringToBytes address311 |> PeerNetworkIdentity
                ListeningAddress = NetworkAddress address311
                PublicAddress = NetworkAddress address311 |> Some
                BootstrapNodes = []
        }

        let nodeConfigList =
            [312..410]
            |> List.map (fun port ->
                {
                    nodeConfigBase with
                        Identity = (sprintf "127.0.0.1:%i" port) |> Conversion.stringToBytes |> PeerNetworkIdentity
                        ListeningAddress = NetworkAddress (sprintf "127.0.0.1:%i" port)
                        PublicAddress = NetworkAddress (sprintf "127.0.0.1:%i" port) |> Some
                        BootstrapNodes = [NetworkAddress address311]
                }
            )

        testGossipDiscovery (nodeConfig1 :: nodeConfigList) 80

    [<Fact>]
    let ``Network - GossipMessagePassing 3 nodes single message`` () =
        // ARRANGE
        setupTest ()

        let nodeConfigList = [1111; 1112; 1113] |> create3NodesMeshedNetwork

        testGossipSingleMessage nodeConfigList 5

    [<Fact>]
    let ``Network - GossipMessagePassing multiple different message types`` () =
        // ARRANGE
        setupTest ()

        let nodeConfigList = [1121; 1122; 1123] |> create3NodesMeshedNetwork

        testGossipMultipleDifferentMessageTypes nodeConfigList 5

    [<Fact>]
    let ``Network - GossipMessagePassing multiple same message types`` () =
        // ARRANGE
        setupTest ()

        let nodeConfigList = [1131; 1132; 1133] |> create3NodesMeshedNetwork

        testGossipMultipleSameMessageTypes nodeConfigList 5

    [<Fact>]
    let ``Network - MulticastMessagePassing 3 nodes single message`` () =
        // ARRANGE
        setupTest ()

        let nodeConfigList = [1141; 1142; 1143] |> create3NodesMeshedNetwork

        testMulticastSingleMessage nodeConfigList 5

    [<Fact>]
    let ``Network - MulticastMessagePassing multiple different message types`` () =
        // ARRANGE
        setupTest ()

        let nodeConfigList = [1151; 1152; 1153] |> create3NodesMeshedNetwork

        testMulticastMultipleDifferentMessageTypes nodeConfigList 5

    [<Fact>]
    let ``Network - MulticastMessagePassing multiple same message types`` () =
        // ARRANGE
        setupTest ()

        let nodeConfigList = [1161; 1162; 1163] |> create3NodesMeshedNetwork

        testMulticastMultipleSameMessageTypes nodeConfigList 5

    [<Fact>]
    let ``Network - Request/Response Tx exists`` () =
        // ARRANGE
        setupTest ()

        let nodeConfigList = [1171; 1172; 1173] |> create3NodesMeshedNetwork

        testRequestResponseSingleMessage nodeConfigList 5 true

    [<Fact>]
    let ``Network - Request/Response Tx doesn't exist`` () =
        // ARRANGE
        setupTest ()

        let nodeConfigList = [1181; 1182; 1183] |> create3NodesMeshedNetwork

        testRequestResponseSingleMessage nodeConfigList 5 false

    [<Fact>]
    let ``Network - Request/Response multiple different message types`` () =
        // ARRANGE
        setupTest ()

        let nodeConfigList = [1191; 1192; 1193] |> create3NodesMeshedNetwork

        testRequestResponseMultipleDifferentMessageTypes nodeConfigList 5

    [<Fact>]
    let ``Network - Request/Response multiple same message types`` () =
        // ARRANGE
        setupTest ()

        let nodeConfigList = [1101; 1102; 1103] |> create3NodesMeshedNetwork

        testRequestResponseMultipleSameMessageTypes nodeConfigList 5
