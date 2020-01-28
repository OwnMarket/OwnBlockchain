# Changelog


## 1.4.6 (2020-01-28)

- Improve node startup block restoration logic.
- Add API endpoint for info about single validator.


## 1.4.5 (2019-12-28)

- Improve cache management in consensus.
- Reduce redundancy in TX propagation.
- Add retry logic in block verification.


## 1.4.4 (2019-12-13)

- Improved request/response message handling.
- Improved peer list management by taking validators from the most recent available verified block.
- Improved cache management.
- Improved the synchronization speed.
- Fixed the issue with unresponsive consensus agent.
- Added /consensus API endpoint.


## 1.4.3 (2019-11-20)

- Implemented the logic to remove ghost peers.


## 1.4.2 (2019-11-14)

- Fixed the handling of the valid value certificate in consensus.
- Improved the management of whitelisted nodes (boot, validators) in the network layer.


## 1.4.1 (2019-11-13)

- Tuned network code.


## 1.4.0 (2019-11-11)

- Improved TX set preparation logic.
- Upgraded NetMQ to latest stable version.
- Improved async operations management in network layer.
- Implemented network segregation avoidance logic.
- Changed the calculation of available balance when deposit is slashed.


## 1.3.7 (2019-09-06)

- Add UNIX timestamps to the `/stats` API endpoint.


## 1.3.6 (2019-08-08)

- Fix the initialization of the validator state.


## 1.3.5 (2019-07-17)

- Fixed gossip peer removal issue.


## 1.3.4 (2019-07-14)

- Exposed network time in stats API endpoint.
- Cached prepared wallet frontend content to avoid repeated loading and parsing overhead.


## 1.3.3 (2019-07-09)

- Fixed DNS caching issue.


## 1.3.2 (2019-07-04)

- Fixed the issue causing duplicate responses in peer communication.
- Fixed the PeerRequestTimeouts stat counter value reporting.


## 1.3.1 (2019-07-03)

- Added support for Hierarchical Deterministic (HD) Wallets.
- Added wallet UI in the node deployment package and exposed it under `/wallet` endpoint, enabling interaction with the node.
- Updated NuGet packages for data access and API.
- Improved node setup docs.
- Documented node update and removal process.
- Improved TX propagation algorithm.
- Improved performance by introducing TX and block storage cache.
- Improved peer communication by introducing queue priorities.
- Added API endpoints for providing more insight into the node runtime state.
- Changed binary format of network messages.
- Reduced redundancy in network communication by introducing throttling.
- Fixed issue reporting stale consensus round for inactive validator.


## 1.2.6 (2019-07-17)

- Fixed some gossip discovery and peer management issues.


## 1.2.5 (2019-07-09)

- Fixed DNS caching issue.


## 1.2.4 (2019-06-21)

- Improved DNS resolver cache logic.
- Improved gossip peer discovery mechanism.


## 1.2.3 (2019-06-17)

- Fixed pending TX selection criteria, which prevented creation of new blocks.


## 1.2.2 (2019-05-29)

- Improved propagation reliability.
- Disabled stale consensus detection for inactive validators.
- Exposed `/network` API endpoint for more info about network state.
- Exposed `/block/head` and `/block/head/number` API endpoints for fetching the latest block info to support new wallet.


## 1.2.1 (2019-04-24)

- First stable release.
