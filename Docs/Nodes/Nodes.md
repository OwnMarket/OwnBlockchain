# Own Public Blockchain Nodes

All nodes in the network have following characteristics:

- They serve as an entry point to the network by providing an [API](NodeApi.md) for transaction submission, as well as for getting the information about current blockchain state (e.g. address balance, account holding, transaction status, block info, etc.).
- They propagate received transactions and blocks to other nodes.
- They apply received blocks to their local state.


### Validator Nodes

There is a special type of nodes called _Validators_. In addition to the common node functionality mentioned above, validators also participate in consensus protocol, which is the process of creating new blocks.
Only validators can create new blocks - other nodes just apply received blocks to their state.
