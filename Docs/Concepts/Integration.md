# External System Integration

To be able to interact with Own public blockchain, external systems must fulfill following prerequisites:

- Access to the Own public blockchain network [node](../Nodes/Nodes.md) API.
- Possession of the necessary private key for signing the transaction, depending on the object of interaction.

Please take a look at instructions on how to [setup the public blockchain node](../Nodes/NodeSetup.md) and use [its API](../Nodes/NodeApi.md), as well as the information about [transaction composition](../Transactions/TxComposition.md) and [signing](../Transactions/TxSigning.md).

[Own Asset Management Protocol](AssetManagementProtocol.md) and [Tx Action Types](../Transactions/TxActions.md) documents provide important information about the high level concepts, which help to understand the behavior and logical capabilities of Own public blockchain.

## Exchanges

Most common use-cases for exchanges are deposits and withdrawals, which usually result in following interactions with the blockchain:

- Checking the balance of an address or an account.
- Transferring the amounts from one address or account to the other.
- Checking the status of the transaction.
- Getting the block info.

Address and account balances, as well as the transaction and block info, can be fetched by using [node API endpoints](../Nodes/NodeApi.md).

Depending on the type of transfer, different [actions](../Transactions/TxActions.md) are used within the transaction:

- Transfers of the CHX can be initiated by submitting a transaction containing a [`TransferChx`](../Transactions/TxActions.md#transferchx) action.
- Transfers of an asset can be initiated by submitting a transaction containing a [`TransferAsset`](../Transactions/TxActions.md#transferasset) action.

Having multiple transfer actions inside one transaction enables batching, where multiple withdrawals can be executed using a single transaction.

Integration of the asset transfer protocol into the exchange system is a one-time effort, and it enables the execution of transfers for any asset available on the Own public blockchain.
