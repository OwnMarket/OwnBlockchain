# Node API

Own Public Blockchain Node exposes an HTTP API used for interaction with the node. The API has following endpoints:

Endpoint | Verb | Description
--- | --- | ---
`/tx` | `POST` | Transaction submission
`/tx/{transactionHash}` | `GET` | Transaction info
`/equivocation/{equivocationProofHash}` | `GET` | EquivocationProof info
`/block/{blockNumber}` | `GET` | Block info
`/block/head` | `GET` | Get latest block info
`/block/head/number` | `GET` | Get latest block number
`/address/{blockchainAddress}` | `GET` | Address info
`/address/{blockchainAddress}/accounts` | `GET` | List of accounts controlled by the specified address
`/address/{blockchainAddress}/assets` | `GET` | List of assets controlled by the specified address
`/address/{blockchainAddress}/stakes` | `GET` | Address' stakes
`/account/{accountHash}?asset={assetHash}` | `GET` | Account info with asset balances, optionally filtered for a single asset specified in `asset` query string parameter.
`/account/{accountHash}/votes?asset={assetHash}` | `GET` | List of votes for the account, optionally filtered for a single asset specified in `asset` query string parameter.
`/account/{accountHash}/eligibilities` | `GET` | List of eligibilities for the account.
`/account/{accountHash}/kyc-providers` | `GET` | List of kyc-providers that are controllers for the account.
`/asset/{assetHash}` | `GET` | Asset info
`/asset/{assetHash}/kyc-providers` | `GET` | List of KYC providers for the asset
`/validators?activeOnly={true/false}` | `GET` | List of validators, optionally filtering the active only ones
`/validator/{validatorAddress}` | `GET` | Information about a specific validator
`/validator/{validatorAddress}/stakes` | `GET` | List of stakes for a validator
`/peers` | `GET` | List of peers
`/stats` | `GET` | Various node statistics
`/network` | `GET` | Various network statistics
`/pool` | `GET` | Pending TX count in the pool
`/pool/{blockchainAddress}` | `GET` | Pending TXs in the pool by the specified sender address
`/node` | `GET` | General info about node

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
    "expirationTime": 0,
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
    "consensusStep": "Vote",
    "equivocationValue1": "9VMtBESNLXWFRQXrd2HbXc2CGWUkdyPQjAKP5MciU59k",
    "equivocationValue2": "D8ViZH31RHBYrDfUhUC1DK49pY1dxCvgRMsbnS9Lbn3p",
    "signature1": "E5nmjsHcL1hFmJEjphUhg6DBn6gyxYzrTKKtXvDGB8FhefQZQ6o5QJ1MRgXqqY97YMsCe8cs3muDF524Mq1Q9qTzG",
    "signature2": "M4jAhLWup8fe6NVnUg193uqLzGdgFuo6XFP2pDZFWGNvK6LuwYRqwM8HBADatgTZreXz2oZr5GhA3kZqi2GhaHrZE",
    "status": "Processed",
    "depositTaken": 5000,
    "depositDistribution" : [
        {
            "validatorAddress": "CHc1zbyXodtHMEsixH7ZQEajY2Fun3ab5jy",
            "amount": "1000"
        },
        {
            "validatorAddress": "CHVkbiNDYsZJTcUUtRDEucceRrZ8kbXgFCJ",
            "amount": "1000"
        },
        {
            "validatorAddress": "CHZHzBNVMQweCiqYVueEYpmeJMax8382HFr",
            "amount": "1000"
        },
        {
            "validatorAddress": "CHLwBmcHjN23HCNmLMag3sosxeW13h3cko6",
            "amount": "1000"
        },
        {
            "validatorAddress": "CHNugxKAxMaPbKpx5yraBNoLh63icwqVa5Y",
            "amount": "1000"
        }
    ],
    "includedInBlockNumber": 125
}
```


## `GET /block/{blockNumber}`

Request URL:
```
/block/21
```

Response JSON payload:
```json
{
    "number": 21,
    "hash": "6pYM6BBGyQttPCTVmpqNpH4i4jE5KjgmaXCWuDJcbiLp",
    "previousHash": "EsEYC4f4xvNiSQLRX8P3GNzGeWm1smVvUD5U5oTnDw3b",
    "configurationBlockNumber": 18,
    "timestamp": 1554632225409,
    "proposerAddress": "CHN5FmdEhjKHynhdbzXxsNB35oxL559gRLH",
    "txSetRoot": "9U4jKtneZ5CJ2qA3qzmiCbekz6j5ZaXSjGAPqaADscG1",
    "txResultSetRoot": "6Hb32QtPVJdRiubdGRb73WiExmk75rKP76jDfrBg6R9B",
    "equivocationProofsRoot": "9Vc4dQKFpQ8XP36q5TFAnpChvFmTg8UH6rpu3FqVn268",
    "equivocationProofResultsRoot": "Gtcbiey3WwiRHrYuGc5ytcttEpMq19uY4oA2FAaMXwLc",
    "stateRoot": "C6MYZeTZUKZwikUpTEADCYANRZXvMJquCTMSE2ztz78C",
    "stakingRewardsRoot": "Ey9qZK4J4G2PK68ZFzyteP8dcUWCjcBiMZ46D7nH11pY",
    "configurationRoot": "8J9eC9A3jzmwCxg8VhjT84xAts8tVVm6RwzLKBhMd3D8",
    "txSet": [
        "6XTowWarMR1UjzAVfiMYs7hsKK9hPBagR7JtFn7nxgfK",
        "8ZVF1R9vLkV2QGMJxGGffgPqMKv41kemFfVGTzmPfKyg",
        "J2FsRZcPQKfhsSTamKp525AN2imsyVqU5gwVu1AohvHn",
        "EJd9RbnVBq1yLG4W35FAwAY4Th3w9bhYDTdvJZuWH8TX",
        "BnpoTcYcnm7mNeNF8fonS4GciZvWEEK9ZnRifkNNxWkQ"
    ],
    "equivocationProofs": [
        "6rYDAZNZE5dhii3JNHvcpxk6uiuWfUHr7qwbwN9qYDx4",
        "G9Fz3L8xn7zjyk1ZuHNNnvYeMJFqZpnaELQ95rUGcVNR"
    ],
    "stakingRewards": [
        {
            "stakerAddress": "CHGeQC23WjThKoDoSbKRuUKvq1EGkBaA5Gg",
            "amount": 0.0128125
        },
        {
            "stakerAddress": "CHJQ8noahag1Cwg6tUW6Y9ESdiCFFBwyQ5C",
            "amount": 0.005125
        },
        {
            "stakerAddress": "CHXSesNUw6PdUCY6u3N9B8orHYNQMWHREdZ",
            "amount": 0.0025625
        }
    ],
    "configuration": {
        "configurationBlockDelta": 3,
        "validators": [
            {
                "validatorAddress": "CHMf4inrS8hnPNEgJVZPRHFhsDPCHSw42Q2",
                "networkAddress": "localhost:25701",
                "sharedRewardPercent": 50,
                "totalStake": 800000
            },
            {
                "validatorAddress": "CHN5FmdEhjKHynhdbzXxsNB35oxL559gRLH",
                "networkAddress": "localhost:25703",
                "sharedRewardPercent": 50,
                "totalStake": 800000
            },
            {
                "validatorAddress": "CHStDQ5ZFeFW9rbMhw83f7FXg19okxVVScM",
                "networkAddress": "localhost:25704",
                "sharedRewardPercent": 50,
                "totalStake": 800000
            },
            {
                "validatorAddress": "CHXr1u8DvLmRrnBpVmPcEH43qBhjez6dc4N",
                "networkAddress": "localhost:25702",
                "sharedRewardPercent": 50,
                "totalStake": 800000
            }
        ],
        "validatorsBlacklist": [
            "CHMBuSD7ZxKzLvnBZR5ibnLBM7J8v5YvPmS",
            "CHKtCFeZKE74px2EJS8Sg49j2sdQXpUuKJN"
        ],
        "validatorDepositLockTime": 2,
        "validatorBlacklistTime": 5,
        "maxTxCountPerBlock": 1000
    },
    "consensusRound": 0,
    "signatures": [
        "GBfbNB8xQUaLoZDFDGtpAH3EYks2uFdaBErPJKAzR37DBkKDL4HEFNRYLPapbQuZ5JgDzUSXgU7iGJqy4sSJx4775",
        "64y6JNqpeDqS927rgbCKbdqysAkeEfLC4KS5pAeDpuPL2SR1gsqFFMr5YmrcrPhk75dXeihWovUqctYPrDHjCejiP",
        "664ZaikasKTrtp9BMnoJgEtFuVAgdEq56D9FJKvF1jsq7pFAEXmJu7Nt4FmWjfT8ncXQTCxhakUGgFiDHPjnE4xLG"
    ]
}
```


## `GET /block/head`

Request URL:
```
/block/head
```

Response JSON payload has the same structure as specific block info.


## `GET /block/head/number`

Request URL:
```
/block/head/number
```

Response JSON payload contains only the block number:
```json
123
```

This endpoint is useful for scripting purposes, to avoid fetching the entire block body and parsing it with JSON tools.


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


## `GET /validator/{blockchainAddress}`

Request URL:
```
/validator/CHMf4inrS8hnPNEgJVZPRHFhsDPCHSHZfAJ/stakes
```

Response JSON payload:
```json
{
    "validatorAddress": "CHMf4inrS8hnPNEgJVZPRHFhsDPCHSHZfAJ",
    "networkAddress": "localhost:25701",
    "sharedRewardPercent": 0,
    "isDepositLocked": true,
    "isBlacklisted": false,
    "isEnabled": true,
    "isActive": true
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


## `GET /account/{accountHash}/kyc-providers`

Request URL:
```
/account/4NZXDMd2uKLTmkKVciu84pkSnzUtic6TKxD61grbGcm9/kyc-providers
```

Response JSON payload:
```json
{
    "accountHash": "4NZXDMd2uKLTmkKVciu84pkSnzUtic6TKxD61grbGcm9",
    "kycProviders": [
        "CHVegEXVwUhK2gbrqnMsYyNSVC7CLTM7qmQ"
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


## `GET /peers`

Request URL:
```
/peers
```

Response JSON payload:
```json
{
    "peers": [
        "127.0.0.1:25701",
        "127.0.0.1:25702",
        "127.0.0.1:25703",
        "127.0.0.1:25704"
    ]
}
```


## `GET /stats`

Request URL:
```
/stats
```

Response JSON payload:
```json
{
    "nodeStartTime": "2019-05-02 09:35:16Z",
    "nodeUpTime": "0.00:00:33",
    "nodeCurrentTime": "2019-05-02 09:35:50Z",
    "counters": [
        {
            "counter": "PeerRequests",
            "value": 1
        },
        {
            "counter": "PeerResponses",
            "value": 4
        }
    ]
}
```


## `GET /network`

Request URL:
```
/network
```

Response JSON payload:
```json
{
    "receivesGossip": true
}
```


## `GET /pool`

Request URL:
```
/pool
```

Response JSON payload:
```json
{
    "pendingTxs": 0
}
```


## `GET /pool/{blockchainAddress}`

Request URL:
```
/pool/CHbNPHm1y1pUsGfDv7YhXoXYwfpTsbUP7Z2
```

Response JSON payload:
```json
{
    "senderAddress": "CHbNPHm1y1pUsGfDv7YhXoXYwfpTsbUP7Z2",
    "pendingTxCount": 3,
    "pendingTxs": [
        {
            "txHash": "3GNpManFFB6iQgiqe7EJg2E234Aptc9Ep3tHXv9voMiL",
            "nonce": 4,
            "actionFee": 0.1,
            "actionCount": 1
        },
        {
            "txHash": "6LvLQ8cZsAqoj6BxMwkKjb8fguvjF3h7pPPWuQa6BmDt",
            "nonce": 5,
            "actionFee": 0.1,
            "actionCount": 1
        },
        {
            "txHash": "HTDWREz8Pb5USjdvHY9E2L2uBwCw6PmztADjCFX2S8e9",
            "nonce": 7,
            "actionFee": 0.1,
            "actionCount": 1
        }
    ]
}
```


## `GET /node`

Request URL:
```
/node
```

Response JSON payload:
```json
{
    "versionNumber": "1.3.0",
    "versionHash": "2443c3a4c2c9b6bc17a65bfcc96ddd85ef32225b",
    "networkCode": "OWN_PUBLIC_BLOCKCHAIN_MAINNET",
    "publicAddress": "val01.mainnet.weown.com:25718",
    "validatorAddress": "CHXV1i4NmCENFPFzqEk1ANb7BgBEiF9GHyT"
}
```
