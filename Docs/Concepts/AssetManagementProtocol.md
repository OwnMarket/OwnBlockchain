# Own Asset Management Protocol

This document describes the concepts and interface enabling other systems to interact with and manage the assets created on the Own public blockchain.


## Address vs Account

While in most blockchain systems available today _address_ and _account_ are used interchangeably (and usually considered to be representing the same concept), in Own blockchain these are two distinct entities. _Address_ represents the authentication and control mechanism, while _account_ represents the ownership.

One address can control multiple accounts, which can belong to the same or different investors.

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

These mechanisms are invoked by submitting the requests to the API exposed by the Own public blockchain network node.
