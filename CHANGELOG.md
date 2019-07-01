# Changelog


## 1.3.0 (2019-07-01)

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


## 1.2.1 (2019-04-24)

- First stable release.
