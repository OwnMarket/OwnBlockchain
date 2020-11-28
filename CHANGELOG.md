# Changelog


## 1.6.0 (2020-11-28)

Backported following changes from vNext into the v1.6:

- Replace raw disk files (i.e. blocks and TXs) with the DB storage.
- Prevent TX submission if nonce is not greater than current address nonce.
- Delete TXs below min fee from the pool.
- Provide TX pool info by address.


## 1.5.7 (2020-08-13)

- Added support for installing the node on CentOS 8.


## 1.5.6 (2020-05-13)

- Fixed the issue with inconsistent block configuration structure at the activation of dormant validator logic.


## 1.5.5 (2020-05-13)

- Intermediate release for diagnostic purpose.


## 1.5.4 (2020-05-05)

- Activate dormant validator logic on `MAINNET` at block `1330000`.


## 1.5.3 (2020-04-29)

- Activated last proposed block tracking on `MAINNET` at block `1300000`.


## 1.5.2 (2020-04-19)

- Removed tracking of last proposed block until defined point in chain, to ensure full sync compatibility.


## 1.5.1 (2020-04-15)

- Activated dormant validator handling on `TESTNET`.


## 1.5.0 (2020-04-14)

- Implemented automatic disabling of dormant validators.


## 1.4.8 (2020-04-10)

- Don't allow removing a validator at the point of inclusion in the new active set.
- Exposed additional fields in validators API endpoint.


## 1.4.7 (2020-03-11)

- Restart failed consensus state instance on error, to prevent node from staling.
- Expose API endpoint to fetch asset by code.


## 1.4.6 (2020-01-29)

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
- Added `/consensus` API endpoint.


## 1.4.3 (2019-11-20)

- Implemented the logic to remove ghost peers.


## 1.4.2 (2019-11-14)

- Fixed the handling of the valid value certificate in consensus.
- Improved the management of whitelisted nodes (boot, validators) in the network layer.


## 1.4.1 (2019-11-13)

- Tuned network code.


## 1.3.8 (2019-11-13)

- Increase allowed number of incoming connections on network socket.


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
