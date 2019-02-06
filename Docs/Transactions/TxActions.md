# Transaction Actions

In the Own blockchain network, a transaction is a command that consists out of one or more action. A transaction is an atomic executable, which means that either all actions within the transaction are executed or they all get rolled back.

An action is a single command that changes the state. The action types supported on the public blockchain can be divided in two categories:

- Network Management Actions
    - [`TransferChx`](#transferchx)
    - [`DelegateStake`](#delegatestake)
    - [`ConfigureValidator`](#configurevalidator)
- Asset Management Actions
    - [`TransferAsset`](#transferasset)
    - [`CreateAssetEmission`](#createassetemission)
    - [`CreateAsset`](#createasset)
    - [`SetAssetCode`](#setassetcode)
    - [`SetAssetController`](#setassetcontroller)
    - [`CreateAccount`](#createaccount)
    - [`SetAccountController`](#setaccountcontroller)
- Voting Actions
    - [`SubmitVote`](#submitvote)
    - [`SubmitVoteWeight`](#submitvoteweight)
- Eligibility and KYC Actions
    - [`SetAccountEligibility`](#setaccounteligibility)
    - [`SetAssetEligibility`](#setasseteligibility)
    - [`ChangeKycControllerAddress`](#changekyccontrolleraddress)
    - [`AddKycProvider`](#addkycprovider)
    - [`RemoveKycProvider`](#removekycprovider)

## Network Management Actions

Network management actions are actions related to operating the blockchain network.


### `TransferChx`

Transfers specified amount of CHX from transaction sender wallet address to a specified recipient wallet address.

Parameter | Data Type | Description
--- | --- | ---
`RecipientAddress` | string | Wallet address to which the CHX tokens will be transferred.
`Amount` | decimal number | Number of CHX tokens being transferred.

**NOTE:** There is no sender address in the action parameter list of this action type because the sender is implicitly known from the transaction signature.


### `DelegateStake`

Increases/decreases delegation of specified amount of CHX from transaction sender wallet address to the specified validator wallet address.

Parameter | Data Type | Description
--- | --- | ---
`ValidatorAddress` | string | Wallet address of the validator being "voted" for.
`Amount` | decimal number | Number of CHX tokens being delegated.

**NOTE:** Delegated amount of CHX is still in possession of the sender address, but it is not available for transfers, additional delegation or for paying transaction fees.

To change the amount of CHX delegated to a specific validator address (or completely withdraw the delegation and "unlock" the CHX), a new transaction must be submitted containing a `DelegateStake` action with the specified amount to increase/decrease in the `Amount` field. Setting the `Amount` field to a positive number results in the increase of the delegated amount, while setting the field to a negative number effectively withdraws the delegation for specified amount and unlocks the specified amount of CHX previously delegated to the specified validator.

Examples:

Delegated amount to validator X before TX is processed | Amount value in DelegateStake action | Delegated amount to validator X after TX is processed
--- | --- | ---
0 | 100 | 100
100 | 50 | 150
150 | -70 | 80
80 | -100 | 80 (ERROR: `InsufficientStake`)
80 | -80 | 0


### `ConfigureValidator`

Configures operational parameters for a validator.

Parameter | Data Type | Description
--- | --- | ---
`NetworkAddress` | string | Network address of the validator being configured. (e.g. `validator1.weown.com:25718`)
`SharedRewardPercent` | decimal | Percent of the reward shared with stakers. (0 - 100)

**NOTE:** The transaction must be signed using the private key of the validator wallet address with enough stake to participate in consensus.


## Asset Management Actions

Asset management actions are actions related to assets, accounts and holdings.


### `TransferAsset`

This action type transfer asset ownership from one investor account to another. It makes the state update on the public blockchain. It basically represents a trading action. We identify two types of asset transfers:

- Trading asset against an emission holding
- Trading asset against a regular investor

The differences are that trading asset against emission holding binds transaction to the set of corporate action rules defined during the asset creation. Those are the "**primary offer rules**" stating who and how much of the asset can buy in the primary offer phase.

Trading asset on the secondary market can potentially have its own rules that are imposed by the business or regulators. Those are called "**transferability rules**" and they are constraining trading of the asset class.

These high-level steps are conducted when executing this transaction type:

1. Transferability or emission rules are being proved
2. Sender holding is reduced for the amount specified in the action parameters
3. Receiver holding is increased for the amount specified in the action parameters
4. CHX fee gets reduced from the sender address
5. CHX fee gets added to the transaction validator

Parameter | Data Type | Description
--- | --- | ---
`FromAccountHash` | string | Account hash of the account being transferred from.
`ToAccountHash` | string | Account hash of the account being transferred to.
`AssetHash` | string | Asset hash of the asset being transferred.
`Amount` | decimal number | Asset amount being transferred.


### `CreateAssetEmission`

Asset Emission creates the tokenized equity and creates the offer. When a business owner issues equity on Own blockchain the following set of operations are executed:

Private Blockchain | Public Blockchain
--- | ---
Create corporate action entry |
Create corporate action primary offer (PO) rules |
Create asset entry with its definition |
Create asset "trading/transferability" rules |
Create a technical primary offer investor account |
|| Holding entry gets created with the emitted number of shares for the technical PO investor

Action on a public blockchain is implemented through the `CreateAssetEmission` action type, which creates new or increases existing asset supply.

Parameter | Data Type | Description
--- | --- | ---
`EmissionAccountHash` | string | Account hash, asset holding of which will be increased for specified amount.
`AssetHash` | string | Asset hash of the asset being emitted.
`Amount` | decimal number | Asset amount being emitted with this corporate action.

**NOTE:** This action does not implicitly create new asset - asset hash specified in the action must already exist (see `CreateAsset` action).


### `CreateAsset`

Creates new asset hash on the blockchain.

This action has no parameters and is specified as `"actionData": {}` in JSON representation of the transaction.

**NOTE:** Asset hash is generated in a predictable way and can be known even before submitting a transaction to the blockchain. It is calculated as a hash of sender address, its nonce and action number (position of the action within the transaction).


### `SetAssetCode`

Sets an alphanumeric code for an asset.

In order to provide a more user-friendly experience on the public blockchain, an asset can have a short alphanumeric code assigned to it. This code represents an alias for the actual asset hash and it must be unique.

Parameter | Data Type | Description
--- | --- | ---
`AssetHash` | string | Asset hash of the affected asset.
`AssetCode` | string | Asset code being set (e.g. `ABC123`).

**NOTE:** The transaction must be signed using the private key of the address currently set as the asset controller (see `SetAssetController` action). If the specified code is already assigned to some other asset hash, the transaction will fail.


### `SetAssetController`

Sets the new controller address for the asset.

Asset controller has control over the asset by being able to:

- execute corporate actions (e.g. `CreateAssetEmission`)
- change the asset code
- set the new asset controller

Asset controller address usually belongs to a business owner or to someone who has the power of attorney from the business owner to run these services for the company.

Parameter | Data Type | Description
--- | --- | ---
`AssetHash` | string | Asset hash of the affected asset.
`ControllerAddress` | string | Wallet address of the new asset controller.

**NOTE:** The transaction must be signed using the private key of the address currently set as the asset controller. After the transaction is executed, old controller address doesn't have control over the asset anymore.


### `CreateAccount`

Creates new account hash on the blockchain.

This action has no parameters and is specified as `"actionData": {}` in JSON representation of the transaction.

**NOTE:** Account hash is generated in a predictable way and can be known even before submitting a transaction to the blockchain. It is calculated as a hash of sender address, its nonce and action number (position of the action within the transaction).


### `SetAccountController`

Sets the new controller address for the account.

Account controller has control over the account by being able to:

- transfer asset holding from controlled account to another account
- set the new account controller

Parameter | Data Type | Description
--- | --- | ---
`AccountHash` | string | Account hash of the affected account.
`ControllerAddress` | string | Wallet address of the new account controller.

**NOTE:** The transaction must be signed using the private key of the address currently set as the account controller. After the transaction is executed, old controller address doesn't have control over the account anymore.

## Voting Actions

Voting actions are actions related to voting process: vote submission and vote weighting. Vote weighting transaction action should be used to mark the end of the voting event (i.e. when the voting event has been stopped). Once there is a weight on a vote, the vote value cannot be altered anymore.

**NOTE:**
Resolution hash is usually a hash of the related data stored in the application using the blockchain. For example, Own's [FAST Platform](https://platform.weown.com) is generating the resolution hash (before submitting a transaction to the blockchain) by calculating it from following components:

- Asset hash
- Event name
- Resolution text
- End date of voting event


### `SubmitVote`

`SubmitVote` action submits an unweighted vote to the blockchain.
For the action to succeed, there must be a holding on the account and asset in place, prior to the execution of a `SubmitVote` action.

Parameter | Data Type | Description
--- | --- | ---
`AccountHash` | string | Hash of the account submitting the vote.
`AssetHash` | string | Hash of the asset owned by the account represented by `AccountHash` and participating in the voting event.
`ResolutionHash` | string | Hash of the voting resolution.
`VoteHash` | string | Hash of the vote value.

**NOTES:**
- Value for the `VoteHash` is usually a hash of the related data stored in the application using the blockchain. For example, Own's [FAST Platform](https://platform.weown.com) is generating the vote hash as the hash of the voting option ("Yes", "No" or similar "Agree", "Disagree").
- The transaction must be signed using the private key of the address currently set as the account controller (see `SetAccountController` action).


### `SubmitVoteWeight`

`SubmitVoteWeight` action assigns the weight to a vote submitted through `SubmitVote` action. Once weighted, a vote cannot be changed anymore.

Parameter | Data Type | Description
--- | --- | ---
`AccountHash` | string | Hash of the account submitting the vote.
`AssetHash` | string | Hash of the asset owned by account represented by `AccountHash` and participating in the voting event.
`ResolutionHash` | string | Hash of the voting resolution.
`VoteWeight` | decimal number | The weight of the vote.

**NOTE:**
The transaction must be signed using the private key of the address currently set as the asset controller (see `SetAssetController` action).


## Eligibility Actions

### `SetAcccountEligibility`

`SetAccountEligibility` action sets the account eligibility in the context of primary and secondary market for an asset.

Parameter | Data Type | Description
--- | --- | ---
`AccountHash` | string | Hash of the account for which the eligibility is applied.
`AssetHash` | string | Hash of the asset for which the eligibility is applied.
`IsPrimaryEligible` | bool | Is eligible in the primary market, i.e. can the account (`AccountHash`) buy the asset (`AssetHash`) from primary offer.
`IsSecondaryEligible` | bool | Is eligible in the secondary market, i.e. can the account (`AccountHash`) acquire the asset (`AssetHash`) in the secondary market, e.g. by buying it on exchange or getting it transferred from another account.

**NOTE:**
- For new eligibility (i.e. there is no existing eligibility for the (`AccountHash`, `AssetHash`) pair) the transaction must be signed using the private key of the address currently set as the asset controller (see `SetAssetController` action) or one of the addresses from the approved list of KYC providers for the asset.
- If eligibility exists, then the transaction must be signed using the private key of the address currently set as the KYC controller for the account.

### `SetAssetEligibility`

`SetAssetEligibility` action sets the eligibility required flag on the asset

Parameter | Data Type | Description
--- | --- | ---
`AssetHash` | string | Hash of the asset for which the required eligibility flag is set.
`IsEligibilityRequired` | bool | Determine if the eligibility requirement checks are performed on `TransferAsset` action

**NOTE:**
The transaction must be signed using the private key of the address currently set as the asset controller (see `SetAssetController` action).

### `ChangeKycControllerAddress`

`ChangeKycControllerAddress` action changes the KYC controller responsible for maintaining the account eligibility flags in the context of a certain asset.

Parameter | Data Type | Description
--- | --- | ---
`AccountHash` | string | Hash of the account for which the eligibility is applied.
`AssetHash` | string | Hash of the asset for which the eligibility is applied.
`KycControllerAddress` | string | The address of the new KYC controller who will manage the eligibility for the account until changed.

**NOTE:**
The transaction must be signed using the private key of the address currently set as the KYC controller for the account (and must be approved KYC provider) or using the private key of the address currently set as the asset controller (see `SetAssetController` action).

### `AddKycProvider`

`AddKycProvider` action adds a new KYC provider for an existing asset (represented by `AssetHash`).

Parameter | Data Type | Description
--- | --- | ---
`AssetHash` | string | Hash of the asset.
`ProviderAddress` | string | Address of the KYC provider.

**NOTE:**
The transaction must be signed using the private key of the address currently set as the asset controller (see `SetAssetController` action).

### `RemoveKycProvider`

`RemoveKycProvider` action removes the KYC provider for an existing asset (represented by `AssetHash`)

Parameter | Data Type | Description
--- | --- | ---
`AssetHash` | string | Hash of the asset.
`ProviderAddress` | string | Address of the KYC provider.

**NOTE:**
The transaction must be signed using the private key of the address currently set as the asset controller (see `SetAssetController` action).
