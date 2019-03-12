# Node API

Own Public Blockchain Node exposes an HTTP API used for interaction with the node. The API has following endpoints:

Endpoint | Verb | Description
--- | --- | ---
`/tx` | `POST` | Transaction submission
`/tx/{transactionHash}` | `GET` | Transaction info
`/equivocation/{equivocationProofHash}` | `GET` | EquivocationProof info
`/block/{blockNumber}` | `GET` | Block info
`/address/{blockchainAddress}` | `GET` | Address info
`/address/{blockchainAddress}/accounts` | `GET` | List of accounts controlled by the specified address
`/address/{blockchainAddress}/assets` | `GET` | List of assets controlled by the specified address
`/address/{blockchainAddress}/stakes` | `GET` | Address' stakes
`/account/{accountHash}?asset={assetHash}` | `GET` | Account info with asset balances, optionally filtered for a single asset specified in `asset` query string parameter.
`/account/{accountHash}/votes?asset={assetHash}` | `GET` | List of votes for the account, optionally filtered for a single asset specified in `asset` query string parameter.
`/account/{accountHash}/eligibilities` | `GET` | List of eligibilities for the account.
`/asset/{assetHash}` | `GET` | Asset info
`/asset/{assetHash}/kyc-providers` | `GET` | List of KYC providers for the asset
`/validators?activeOnly={true/false}` | `GET` | List of validators, optionally filtering the active only ones
`/validator/{validatorAddress}/stakes` | `GET` | List of stakes for a validator

Below are the detailed specifications of requests and responses with samples for each of the listed endpoints.


## `POST /tx`

Request URL:
```
/tx
```

Request JSON payload:
```json
{
    "tx":"ewogICAgU2VuZGVyQWRkcmVzczogIkNITHNWYVlTUEpHRmk4Qk5HZDZ0UDFWdkI4VWRLYlZSREtEIiwKICAgIE5vbmNlOiAxLAogICAgRmVlOiAwLjAwMSwKICAgIEFjdGlvbnM6IFsKICAgICAgICB7CiAgICAgICAgICAgIEFjdGlvblR5cGU6ICJUcmFuc2ZlckNoeCIsCiAgICAgICAgICAgIEFjdGlvbkRhdGE6IHsKICAgICAgICAgICAgICAgIFJlY2lwaWVudEFkZHJlc3M6ICJDSGZEZXVCMXkxZUpuV2Q2YVdmWWFSdnBTOVFncmgxZXFlNyIsCiAgICAgICAgICAgICAgICBBbW91bnQ6IDEwMAogICAgICAgICAgICB9CiAgICAgICAgfQogICAgXQp9CiAgICA=",
    "signature":"E5nmjsHcL1hFmJEjphUhg6DBn6gyxYzrTKKtXvDGB8FhefQZQ6o5QJ1MRgXqqY97YMsCe8cs3muDF524Mq1Q9qTzG"
}
```

Response JSON payload (success case):
```json
{
    "txHash":"CRjqV3DLh7jyCKZqj2pCdfw3s3ynXxEf5JMVm1rCYjmp"
}
```

Response JSON payload (error case):
```json
{
    "errors": [
        "Tx CRjqV3DLh7jyCKZqj2pCdfw3s3ynXxEf5JMVm1rCYjmp already exists."
    ]
}
```

**NOTE:** Error response has the same structure in all endpoints. It's a JSON object with an array property called `errors` containing one or more error messages.


## `GET /tx/{transactionHash}`

Request URL:
```
/tx/CRjqV3DLh7jyCKZqj2pCdfw3s3ynXxEf5JMVm1rCYjmp
```

Response JSON payload:
```json
{
    "txHash": "CRjqV3DLh7jyCKZqj2pCdfw3s3ynXxEf5JMVm1rCYjmp",
    "senderAddress": "CHLsVaYSPJGFi8BNGd6tP1VvB8UdKbVRDKD",
    "nonce": 1,
    "actionFee": 0.001,
    "actions": [
        {
            "actionType": "TransferChx",
            "actionData": {
                "recipientAddress": "CHfDeuB1y1eJnWd6aWfYaRvpS9Qgrh1eqe7",
                "amount": 100
            }
        }
    ],
    "status": "Success",
    "errorCode": null,
    "failedActionNumber": null,
    "blockNumber": 2
}
```


## `GET /equivocation/{equivocationProofHash}`

Request URL:
```
/equivocation/BTXVBwuTXWTpPtJC71FPGaeC17NVhu9mS6JavqZqHbYH
```

Response JSON payload:
```json
{
    "equivocationProofHash": "BTXVBwuTXWTpPtJC71FPGaeC17NVhu9mS6JavqZqHbYH",
    "validatorAddress": "CHT72YWjChhv5xYeDono6Nn4Z5Qe5Q7aRyq",
    "blockNumber": 123,
    "consensusRound": 0,
    "consensusStep": 1,
    "blockHash1": "9VMtBESNLXWFRQXrd2HbXc2CGWUkdyPQjAKP5MciU59k",
    "blockHash2": "D8ViZH31RHBYrDfUhUC1DK49pY1dxCvgRMsbnS9Lbn3p",
    "signature1": "E5nmjsHcL1hFmJEjphUhg6DBn6gyxYzrTKKtXvDGB8FhefQZQ6o5QJ1MRgXqqY97YMsCe8cs3muDF524Mq1Q9qTzG",
    "signature2": "M4jAhLWup8fe6NVnUg193uqLzGdgFuo6XFP2pDZFWGNvK6LuwYRqwM8HBADatgTZreXz2oZr5GhA3kZqi2GhaHrZE",
    "depositTaken": 1000,
    "includedInBlockNumber": 125
}
```


## `GET /block/{blockNumber}`

Request URL:
```
/block/2
```

Response JSON payload:
```json
{
    "number": 2,
    "hash": "9VMtBESNLXWFRQXrd2HbXc2CGWUkdyPQjAKP5MciU59k",
    "previousHash": "D8ViZH31RHBYrDfUhUC1DK49pY1dxCvgRMsbnS9Lbn3p",
    "timestamp": 1549530022179,
    "validator": "CHT72YWjChhv5xYeDono6Nn4Z5Qe5Q7aRyq",
    "txSetRoot": "CRjqV3DLh7jyCKZqj2pCdfw3s3ynXxEf5JMVm1rCYjmp",
    "txResultSetRoot": "5zZ72DnUkLd5LRMX6hxXuTrxd8trGrNTDasaP51RMWdX",
    "stateRoot": "8R4JYdn24veRSUPmReL6A6fgUFHFmzWhX3fMSWfmTD9a",
    "txSet": [
        "CRjqV3DLh7jyCKZqj2pCdfw3s3ynXxEf5JMVm1rCYjmp"
    ]
}
```


## `GET /address/{blockchainAddress}`

Request URL:
```
/address/CHLsVaYSPJGFi8BNGd6tP1VvB8UdKbVRDKD
```

Response JSON payload:
```json
{
    "blockchainAddress": "CHVegEXVwUhK2gbrqnMsYyNSVC7CLTM7qmQ",
    "balance": {
        "total": 1499.993,
        "staked": 100,
        "deposit": 0,
        "available": 1399.993
    },
    "nonce": 7
}
```


## `GET /address/{blockchainAddress}/accounts`

Request URL:
```
/address/CHLsVaYSPJGFi8BNGd6tP1VvB8UdKbVRDKD/accounts
```

Response JSON payload:
```json
{
    "blockchainAddress": "CHLsVaYSPJGFi8BNGd6tP1VvB8UdKbVRDKD",
    "accounts": [
        "wcpUPec7pNUKys9pkvPfhjkezekZ99GHpXavbS6M1R4",
        "Fr5HoamTv7W598duwGQT3p9pqK5oHYjxWqWwycaeg1YC",
        "CFdgvj8PPkmFHys3ASknhBCvnZBLPZPSBHAuMv6DpdGA"
    ]
}
```


## `GET /address/{blockchainAddress}/assets`

Request URL:
```
/address/CHLsVaYSPJGFi8BNGd6tP1VvB8UdKbVRDKD/assets
```

Response JSON payload:
```json
{
    "blockchainAddress": "CHLsVaYSPJGFi8BNGd6tP1VvB8UdKbVRDKD",
    "assets": [
        "FnrfMcvwghb4qws7evxSTHdJ43aShxdRXWu3hZ8HX9wU"
    ]
}
```


## `GET /address/{blockchainAddress}/stakes`

Request URL:
```
/address/CHLsVaYSPJGFi8BNGd6tP1VvB8UdKbVRDKD/stakes
```

Response JSON payload:
```json
{
    "blockchainAddress": "CHLsVaYSPJGFi8BNGd6tP1VvB8UdKbVRDKD",
    "stakes": [
        {
            "validatorAddress": "CHMf4inrS8hnPNEgJVZPRHFhsDPCHSHZfAJ",
            "amount": 100
        }
    ]
}
```


## `GET /validator/{blockchainAddress}/stakes`

Request URL:
```
/validator/CHMf4inrS8hnPNEgJVZPRHFhsDPCHSHZfAJ/stakes
```

Response JSON payload:
```json
{
    "validatorAddress": "CHMf4inrS8hnPNEgJVZPRHFhsDPCHSHZfAJ",
    "stakes": [
        {
            "stakerAddress": "CHVegEXVwUhK2gbrqnMsYyNSVC7CLTM7qmQ",
            "amount": 100
        }
    ]
}
```


## `GET /account/{accountHash}?asset={assetHash}`

Request URL:
```
/account/wcpUPec7pNUKys9pkvPfhjkezekZ99GHpXavbS6M1R4
```

Response JSON payload:
```json
{
    "accountHash": "wcpUPec7pNUKys9pkvPfhjkezekZ99GHpXavbS6M1R4",
    "controllerAddress": "CHLsVaYSPJGFi8BNGd6tP1VvB8UdKbVRDKD",
    "holdings": [
        {
            "assetHash": "BTXVBwuTXWTpPtJC71FPGaeC17NVhu9mS6JavqZqHbYH",
            "balance": 1000000
        },
        {
            "assetHash": "ETktHKf3kySqS6uTN321y1N5iBf1SYsjuzmE4x8FWS3B",
            "balance": 500000
        }
    ]
}
```

If optional `asset` query string parameter is specified, only the holding for that asset will be returned in the `holdings` array:

Request URL:
```
/account/wcpUPec7pNUKys9pkvPfhjkezekZ99GHpXavbS6M1R4?asset=ETktHKf3kySqS6uTN321y1N5iBf1SYsjuzmE4x8FWS3B
```

Response JSON payload:
```json
{
    "accountHash": "wcpUPec7pNUKys9pkvPfhjkezekZ99GHpXavbS6M1R4",
    "controllerAddress": "CHLsVaYSPJGFi8BNGd6tP1VvB8UdKbVRDKD",
    "holdings": [
        {
            "assetHash": "ETktHKf3kySqS6uTN321y1N5iBf1SYsjuzmE4x8FWS3B",
            "balance": 500000
        }
    ]
}
```


## `GET /account/{accountHash}/votes?asset={assetHash}`

Request URL:
```
/account/4NZXDMd2uKLTmkKVciu84pkSnzUtic6TKxD61grbGcm9/votes
```

Response JSON payload:
```json
{
    "accountHash": "4NZXDMd2uKLTmkKVciu84pkSnzUtic6TKxD61grbGcm9",
    "votes": [
        {
            "assetHash": "FnrfMcvwghb4qws7evxSTHdJ43aShxdRXWu3hZ8HX9wU",
            "resolutionHash": "RSH1",
            "voteHash": "Yes",
            "voteWeight": 0
        },
        {
            "assetHash": "ETrivt162Fao3yEdsE1ZaBAdq9s6iRsGHQEBwDwUQYCd",
            "resolutionHash": "RSH1",
            "voteHash": "No",
            "voteWeight": 0
        }
    ]
}
```

If optional `asset` query string parameter is specified, only the votes for that asset will be returned in the `votes` array:

Request URL:
```
/account/4NZXDMd2uKLTmkKVciu84pkSnzUtic6TKxD61grbGcm9/votes?asset=FnrfMcvwghb4qws7evxSTHdJ43aShxdRXWu3hZ8HX9wU
```

Response JSON payload:
```json
{
    "accountHash": "4NZXDMd2uKLTmkKVciu84pkSnzUtic6TKxD61grbGcm9",  
    "votes": [
        {
            "assetHash": "FnrfMcvwghb4qws7evxSTHdJ43aShxdRXWu3hZ8HX9wU",
            "resolutionHash": "RSH1",
            "voteHash": "Yes",
            "voteWeight": 0
        }
    ]
}
```


## `GET /account/{accountHash}/eligibilities`

Request URL:
```
/account/4NZXDMd2uKLTmkKVciu84pkSnzUtic6TKxD61grbGcm9/eligibilities
```

Response JSON payload:
```json
{
    "accountHash": "4NZXDMd2uKLTmkKVciu84pkSnzUtic6TKxD61grbGcm9",  
    "eligibilities": [
        {
            "assetHash": "FnrfMcvwghb4qws7evxSTHdJ43aShxdRXWu3hZ8HX9wU",
            "isPrimaryEligible": true,
            "isSecondaryEligible": true,
            "kycControllerAddress": "CHVegEXVwUhK2gbrqnMsYyNSVC7CLTM7qmQ"
        }
    ]
}
```


## `GET /asset/{assetHash}`

Request URL:
```
/asset/FnrfMcvwghb4qws7evxSTHdJ43aShxdRXWu3hZ8HX9wU
```

Response JSON payload:
```json
{
    "assetHash": "FnrfMcvwghb4qws7evxSTHdJ43aShxdRXWu3hZ8HX9wU",
    "assetCode": "ATP",
    "controllerAddress": "CHVegEXVwUhK2gbrqnMsYyNSVC7CLTM7qmQ",
    "isEligibilityRequired": false
}
```


## `GET /asset/{assetHash}/kyc-providers`

Request URL:
```
/asset/FnrfMcvwghb4qws7evxSTHdJ43aShxdRXWu3hZ8HX9wU/kyc-providers
```

Response JSON payload:
```json
{
    "assetHash": "FnrfMcvwghb4qws7evxSTHdJ43aShxdRXWu3hZ8HX9wU",    
    "kycProviders": [
        "CHVegEXVwUhK2gbrqnMsYyNSVC7CLTM7qmQ"
    ]
}
```


## `GET /validators`

Request URL:
```
/validators
```

Response JSON payload:
```json
{
    "validators": [
        {
            "validatorAddress": "CHMf4inrS8hnPNEgJVZPRHFhsDPCHSHZfAJ",
            "networkAddress": "localhost:25701",
            "sharedRewardPercent": 0,
            "isActive": true
        },
        {
            "validatorAddress": "CHN5FmdEhjKHynhdbzXxsNB35oxL5195XE5",
            "networkAddress": "localhost:25703",
            "sharedRewardPercent": 0,
            "isActive": true
        },
        {
            "validatorAddress": "CHStDQ5ZFeFW9rbMhw83f7FXg19okxQD9E7",
            "networkAddress": "localhost:25704",
            "sharedRewardPercent": 0,
            "isActive": true
        },
        {
            "validatorAddress": "CHVegEXVwUhK2gbrqnMsYyNSVC7CLTM7qmQ",
            "networkAddress": "localhost:25705",
            "sharedRewardPercent": 0,
            "isActive": false
        },
        {
            "validatorAddress": "CHXr1u8DvLmRrnBpVmPcEH43qBhjezuRRtq",
            "networkAddress": "localhost:25702",
            "sharedRewardPercent": 0,
            "isActive": true
        }
    ]
}
```

If optional `activeOnly` query string parameter is specified and is has value `true` (i.e `activeOnly=true`), only the current validators will be returned in the `validators` array:

Request URL:
```
/validators?activeOnly=true
```

Response JSON payload:
```json
{
    "validators": [
        {
            "validatorAddress": "CHMf4inrS8hnPNEgJVZPRHFhsDPCHSHZfAJ",
            "networkAddress": "localhost:25701",
            "sharedRewardPercent": 0,
            "isActive": true
        },
        {
            "validatorAddress": "CHN5FmdEhjKHynhdbzXxsNB35oxL5195XE5",
            "networkAddress": "localhost:25703",
            "sharedRewardPercent": 0,
            "isActive": true
        },
        {
            "validatorAddress": "CHStDQ5ZFeFW9rbMhw83f7FXg19okxQD9E7",
            "networkAddress": "localhost:25704",
            "sharedRewardPercent": 0,
            "isActive": true
        },
        {
            "validatorAddress": "CHXr1u8DvLmRrnBpVmPcEH43qBhjezuRRtq",
            "networkAddress": "localhost:25702",
            "sharedRewardPercent": 0,
            "isActive": true
        }
    ]
}
```


## `GET /validator/{validatorAddress}/stakes`

Request URL:
```
/validator/CHStDQ5ZFeFW9rbMhw83f7FXg19okxQD9E7/stakes
```

Response JSON payload:
```json
{
    "validatorAddress" : "CHMf4inrS8hnPNEgJVZPRHFhsDPCHSHZfAJ",
    "stakes": [
        {
            "stakerAddress": "CHLsVaYSPJGFi8BNGd6tP1VvB8UdKbVRDKD",
            "amount": 100,
        },
        {
            "stakerAddress": "CHVegEXVwUhK2gbrqnMsYyNSVC7CLTM7qmQ",
            "amount": 50
        }     
    ]
}
```