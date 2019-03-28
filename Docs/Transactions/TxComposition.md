# Transaction Composition

This document describes the process of composing and preparing a transaction for submission to the Own Public Blockchain.


## Transaction Structure

Transactions in the Own Public Blockchain are composed in the form of a JSON object. All transactions have same top level structure:

```json
{
    "senderAddress": "CHLsVaYSPJGFi8BNGd6tP1VvB8UdKbVRDKD",
    "nonce": 1,
    "expirationTime": 0,
    "actionFee": 0.001,
    "actions": [
        {
            "actionType": "...",
            "actionData": {
                ...
            }
        },
        {
            "actionType": "...",
            "actionData": {
                ...
            }
        },
        {
            "actionType": "...",
            "actionData": {
                ...
            }
        }
    ]
}
```

- `senderAddress` is the blockchain address corresponding to the private key used to sign the transaction.
- `nonce` is an incrementing number (64-bit integer) representing the number of transactions coming from the `senderAddress`.
- `expirationTime` is the max block timestamp at which this transaction can be successfully processed. If expiration time is set to a number greater than zero and the transaction is included in the block with block timestamp greater than the expiration time, the transaction will fail with `TxExpired` error code.
- `fee` is a decimal number representing the fee **per action**, expressed in CHX (e.g. if the fee is set to 0.001 and the transaction has three actions, total fee paid for transaction will be 0.003 CHX)
- `actions` is an array of action objects, each representing one action that should be performed on the blockchain state.
- `actionType` identifies the type of the action that should be performed against the blockchain state. There are following action types available:
    - [`TransferChx`](TxActions.md#transferchx)
    - [`DelegateStake`](TxActions.md#delegatestake)
    - [`ConfigureValidator`](TxActions.md#configurevalidator)
    - [`RemoveValidator`](TxActions.md#removevalidator)
    - [`TransferAsset`](TxActions.md#transferasset)
    - [`CreateAssetEmission`](TxActions.md#createassetemission)
    - [`CreateAsset`](TxActions.md#createasset)
    - [`SetAssetCode`](TxActions.md#setassetcode)
    - [`SetAssetController`](TxActions.md#setassetcontroller)
    - [`CreateAccount`](TxActions.md#createaccount)
    - [`SetAccountController`](TxActions.md#setaccountcontroller)
    - [`SubmitVote`](TxActions.md#submitvote)
    - [`SubmitVoteWeight`](TxActions.md#submitvoteweight)
    - [`SetAccountEligibility`](TxActions.md#setaccounteligibility)
    - [`SetAssetEligibility`](TxActions.md#setasseteligibility)
    - [`ChangeKycControllerAddress`](TxActions.md#changekyccontrolleraddress)
    - [`AddKycProvider`](TxActions.md#addkycprovider)
    - [`RemoveKycProvider`](TxActions.md#removekycprovider)
- `actionData` contains the details required for the execution of the action and depends on the `actionType`.

For more information about action types and related fields that must be specified in the `actionData` for specific action type, please refer to the [document about action types](TxActions.md).

**NOTE:** It is important to understand that one transaction can have **multiple actions**, which are applied to the blockchain state in atomic way. If any action within the transaction fails, the whole transaction will fail and none of the actions will be applied to the state.

Here is a sample transaction containing two actions:

```json
{
    "senderAddress": "CHGeQC23WjThKoDoSbKRuUKvq1EGkBaA5Gg",
    "nonce": 42,
    "expirationTime": 0,
    "actionFee": 0.001,
    "actions": [
        {
            "actionType": "TransferChx",
            "actionData": {
                "recipientAddress": "CHfDeuB1y1eJnWd6aWfYaRvpS9Qgrh1eqe7",
                "amount": 1234.56789
            }
        },
        {
            "actionType": "TransferAsset",
            "actionData": {
                "fromAccountHash": "wcpUPec7pNUKys9pkvPfhjkezekZ99GHpXavbS6M1R4",
                "toAccountHash": "Fr5HoamTv7W598duwGQT3p9pqK5oHYjxWqWwycaeg1YC",
                "assetHash": "BTXVBwuTXWTpPtJC71FPGaeC17NVhu9mS6JavqZqHbYH",
                "amount": 12345.6789
            }
        }
    ]
}
```


## Transaction Signing

A transaction must be signed using the private key of the sender address, before submitting it to the blockchain.

Here are the necessary steps that need to be performed to produce a transaction envelope containing the transaction itself and corresponding signature:

- Convert JSON body of the transaction to an array of bytes (a.k.a. "raw transaction").
- Produce the signature by [signing the raw transaction](TxSigning.md) with the private key belonging to the sender address.
- Encode raw transaction using Base64 encoding.
- Create a **transaction envelope** as a JSON object containing two fields:
    - `tx` field contains the encoded transaction
    - `signature` field contains the transaction signature

**NOTE:** Own SDKs will provide a convenient way to create a signed envelope programmatically.

Here is a transaction envelope created by signing the transaction from the example above:

```json
{
    "tx":"ewogICAgInNlbmRlckFkZHJlc3MiOiAiQ0hHZVFDMjNXalRoS29Eb1NiS1J1VUt2cTFFR2tCYUE1R2ciLAogICAgIm5vbmNlIjogNDIsCiAgICAiZXhwaXJhdGlvblRpbWUiOiAwLAogICAgImFjdGlvbkZlZSI6IDAuMDAxLAogICAgImFjdGlvbnMiOiBbCiAgICAgICAgewogICAgICAgICAgICAiYWN0aW9uVHlwZSI6ICJUcmFuc2ZlckNoeCIsCiAgICAgICAgICAgICJhY3Rpb25EYXRhIjogewogICAgICAgICAgICAgICAgInJlY2lwaWVudEFkZHJlc3MiOiAiQ0hmRGV1QjF5MWVKbldkNmFXZllhUnZwUzlRZ3JoMWVxZTciLAogICAgICAgICAgICAgICAgImFtb3VudCI6IDEyMzQuNTY3ODkKICAgICAgICAgICAgfQogICAgICAgIH0sCiAgICAgICAgewogICAgICAgICAgICAiYWN0aW9uVHlwZSI6ICJUcmFuc2ZlckFzc2V0IiwKICAgICAgICAgICAgImFjdGlvbkRhdGEiOiB7CiAgICAgICAgICAgICAgICAiZnJvbUFjY291bnRIYXNoIjogIndjcFVQZWM3cE5VS3lzOXBrdlBmaGprZXpla1o5OUdIcFhhdmJTNk0xUjQiLAogICAgICAgICAgICAgICAgInRvQWNjb3VudEhhc2giOiAiRnI1SG9hbVR2N1c1OThkdXdHUVQzcDlwcUs1b0hZanhXcVd3eWNhZWcxWUMiLAogICAgICAgICAgICAgICAgImFzc2V0SGFzaCI6ICJCVFhWQnd1VFhXVHBQdEpDNzFGUEdhZUMxN05WaHU5bVM2SmF2cVpxSGJZSCIsCiAgICAgICAgICAgICAgICAiYW1vdW50IjogMTIzNDUuNjc4OQogICAgICAgICAgICB9CiAgICAgICAgfQogICAgXQp9",
    "signature":"PJD6p2TTcqwu2B6ueMEgkFhiWbSfha88pGnN88hrMoa6Y6fgj5o9rrdhQYVaaq5v8xjYaaEAuPnye8KNLTmzBRSa3"
}
```

This transaction envelope can then be submitted to the blockchain by using the `POST /tx` API endpoint.
