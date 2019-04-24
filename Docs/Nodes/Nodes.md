# Own Public Blockchain Nodes

Own public blockchain network recognizes two types of nodes:

- Client nodes
- Validators


## Client Nodes

Client nodes have following characteristics:

- They serve as an entry point to the network by providing an [API](NodeApi.md) for transaction submission, as well as for getting the information about current blockchain state (e.g. address balance, account holding, transaction status, block info, etc.).
- They propagate received transactions and blocks to other nodes.
- They apply received blocks to their local state.


## Validators

A special subset of nodes on the network are called _validators_. In addition to the client node functionality mentioned above, validators also participate in consensus protocol, which is the process of creating new blocks.
Only validators can create new blocks - other nodes just apply received blocks to their state.
