namespace Chainium.Blockchain.Public.Net.Tests

open System
open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Net.Peers
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Core.Events
open Xunit
open Swensen.Unquote

module PeerTests =

    let publishEvent appEvent =
        ()

    let processPeerMessage (peerMessage : PeerMessage) : Result<AppEvent option, AppError list> option =
        None

    let startGossip (node : NetworkNode) =
        node.StartGossip processPeerMessage publishEvent

    let stopGossip (node : NetworkNode) =
        node.StopGossip ()

    let checkGossipDiscoveryConvergence (nodeList : NetworkNode list) =
        let inputAddresses =
            nodeList
            |> List.map (fun n -> n.GetNetworkAddress ())
            |> List.sort

        nodeList
        |> List.iter(fun n ->
            let nodeAddresses =
                n.GetActiveMembers()
                |> List.map(fun m -> m.NetworkAddress)
                |> List.sort

            test <@ nodeAddresses = inputAddresses @>
        )

        nodeList |> List.iter(fun n -> stopGossip n)

    let testGossipDiscovery nodeConfigList cycleCount =
        // ARRANGE
        let fanout, tCycle, tFail = 4, 1000, 60000
        let createNode nodeConfig =
            NetworkNode (
                DbMock.getAllPeerNodes,
                DbMock.savePeerNode,
                DbMock.removePeerNode,
                TransportMock.sendGossipDiscoveryMessage,
                TransportMock.sendGossipMessage,
                TransportMock.sendMulticastMessage,
                TransportMock.sendUnicastMessage,
                TransportMock.receiveMessage,
                TransportMock.closeConnection,
                TransportMock.closeAllConnections,
                DbMock.getAllValidators,
                nodeConfig,
                fanout,
                tCycle,
                tFail
            )

        let nodeList =
            nodeConfigList
            |> List.map (fun config -> createNode config)

        // ACT
        nodeList |> List.iter(fun n -> startGossip n)

        System.Threading.Thread.Sleep (cycleCount * tCycle)

        // ASSERT
        checkGossipDiscoveryConvergence nodeList

    [<Fact>]
    let ``Gossip - test GossipDiscovery 3 nodes same bootstrap node`` () =
        // ARRANGE
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
        let nodeConfigList = [nodeConfig1; nodeConfig2; nodeConfig3]

        testGossipDiscovery nodeConfigList 5

    [<Fact>]
    let ``Gossip - test GossipDiscovery 3 nodes different bootstrap node`` () =
        // ARRANGE
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
        let nodeConfigList = [nodeConfig1; nodeConfig2; nodeConfig3]

        testGossipDiscovery nodeConfigList 5

    [<Fact>]
    let ``Gossip - test GossipDiscovery 100 nodes`` () =
        // ARRANGE
        let nodeConfig1 = {
            BootstrapNodes = []
            NetworkAddress = NetworkAddress "127.0.0.1:5555"
        }

        let nodeConfigList =
            [5556..5654]
            |> List.map(fun port ->
                {
                    BootstrapNodes = [NetworkAddress "127.0.0.1:5555"]
                    NetworkAddress = NetworkAddress (sprintf "127.0.0.1:%i" port)
                }
            )

        testGossipDiscovery (nodeConfig1 :: nodeConfigList) 20
