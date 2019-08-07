# Decentralised Asset Management Protocol (DAM)

This document describes the concepts and interface enabling other systems to interact with and manage the assets created on the Own public blockchain.


## Address vs Account

In most blockchain systems available today, terms _address_ and _account_ are used interchangeably and are usually considered to be representing the same concept. In Own blockchains these are two distinct entities.

_Address_ represents the authentication and control mechanism, while _account_ is related to the ownership of the tokenized assets.

One address can control multiple accounts, which can belong to the same or different investors. Control over the account can be transferred to another address, without polluting the ownership history, because holding doesn't have to be transferred to a different account just because the account controler has changed, if there was no change in the ownership itself. 

For example, asset holding can initially be acquired on behalf of the investor, on the investor's account controlled by a custodian. However, investor might require to be given the control over his account and would provide the address (for which he controls the private key) to which control should be transferred. Custodian would transfer the control over the account to provided address, but during the whole process investor's holding would stay the same and no artificial transfers of the ownership would be recorded.

While address can only have CHX balance related to it, an account can have multiple holdings of different assets.


## Asset Management Mechanisms

Own public blockchain exposes following mechanisms for managing assets:

- [Creating accounts](../Transactions/TxActions.md#createaccount)
- [Changing account controller](../Transactions/TxActions.md#setaccountcontroller)
- [Creating assets](../Transactions/TxActions.md#createasset)
- [Changing asset controller](../Transactions/TxActions.md#setassetcontroller)
- [Setting asset code](../Transactions/TxActions.md#setassetcode)
- [Issuing assets](../Transactions/TxActions.md#createassetemission)
- [Transferring assets](../Transactions/TxActions.md#transferasset)
- [Checking the account balance per asset](../Nodes/NodeApi.md#get-accountaccounthashassetassethash)

These mechanisms are invoked by submitting the [transactions](../Transactions/TxComposition.md) using the [API](../Nodes/NodeApi.md) exposed by the Own public blockchain network [node](../Nodes/Nodes.md).
