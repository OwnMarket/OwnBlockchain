# Transaction Actions

In the Chainium blockchain network, a transaction is a command that consists out of one or more action. A transaction is an atomic executable, which means that either all actions within the transaction are executed or they all get rolled back.

An action is a single command that changes the state. The action types supported on the public blockchain are:

- [Transfer CHX](#transfer-chx)
- [Create Asset Emission](#create-asset-emission)
- [Transfer Asset](#transfer-asset)
- [Set Asset Controller](#set-account-controller)
- [Set Account Controller](#set-asset-controller)
- [Set Asset Code Controller](#set-asset-code-controller)

## Transfer CHX

Transfer CHX action type transfers CHX from one Chainium wallet address to another. This transaction updates the &quot;CHX\_Balance&quot; table on the public blockchain nodes.

**Parameters :**

| Parameter | Type | Description |
| --- | --- | --- |
| Recipient Address | Chainium Address Hash | Chainium wallet address on which the CHX token will be transferred |
| Amount | CHX Amount | Number of CHX tokens being transferred |

There is no sender Chainium address in the parameter list since the sender is implicitly known. It is the address submitting the transaction.

## Create Asset Emission

Asset Emission creates the tokenized equity and creates the offer. When a business owner issues equity on Chainium blockchain the following set of operations are executed:

| Private Blockchain | Public Blockchain |
| --- | --- |
| Create corporate action entry |   |
| Create corporate action primary offer (PO) rules |   |
| Create asset entry with its definition |   |
| Create asset &quot;trading/transferability&quot; rules |   |
| Create a technical primary offer investor account |   |
|   | Holding entry gets created with the emitted number of shares for the technical PO investor |

Action on a public blockchain is implemented through the &quot; **Create Asset Emission**&quot; action type.

Parameters:

| Parameter | Type | Description |
| --- | --- | --- |
| Investor Account | AccountHash | A technical shareholder account hash value |
| Asset | AssetHash | Hash Id of the asset |
| Emission Amount | AssetAmount | Total number of shares created with this corporate action |

## Transfer Asset

This action type transfer asset ownership from one investor account to another. It makes the state update on the public blockchain. It basically represents a trading action. We identify two types of the &quot;Asset Transfer&quot; transactions:

- Trading asset against an emission holding
- Trading asset against a regular investor

The differences are that trading asset against emission holding binds transaction to the set of corporate action rules defined during the asset creation. Those are the &quot; **primary offer rules**&quot; stating who and how much shares can buy in the offer phase.

Trading asset on the secondary market can potentially have its own rules that are imposed by the business or regulators. Those are called &quot; **transferability rules**&quot; and they are defining trading of the asset class.

These high-level steps are conducted when executing this transaction type:

1. Transferability or emission rules are being proved
2. Sender holding is reduced for the amount specified in the action parameters
3. Receiver holding is increased for the amount specified in the action parameters
4. CHX Fee gets reduced from the sender address
5. CHX fee gets added to the transaction validator (node)

**Parameters :**

| Parameter | Type | Description |
| --- | --- | --- |
| From Account | AccountHash | An account hah selling/sending shares. If it is a technical PO account then the PO rules apply |
| To Account | AccountHash | An account hash buying/receiving shares |
| Asset | AssetHash | The hash value of the asset |
| Amount | Amount | Number of equities being transferred |

## Set Account Controller

The &quot;Set Account Controller &quot; tx action moves the &quot;control&quot; over the investor account from one Chainium controller address to another one. This transaction exists on both blockchains (private and public).

Holding state on the public blockchain is controlled in the account table. Account hash gives us the information who can change the holding for an account.

Set account controller action basically updates the account hash in the account table specifying a new controller. In case there is the controller entry in the table then it will be updated. If there is no entry a new assignment in the table will be created.

**Parameters:**

| Parameter | Type | Description |
| --- | --- | --- |
| Account Hash | AccountHash | The account hash of the current account controller |
| Controller Address | ChainiumAddress | A Chainium address of the new controller |

## Set Asset Controller

The &quot;Set Asset Controller&quot; transaction action moves the &quot;control&quot; over the asset from one Chainium controller address to another one. This transaction exists on both blockchains (private and public). This is the Chainium address that controls the asset and has rights to execute corporate actions. This Chainium address usually belongs to a business owner or to someone who has the power of attorney from the business owner to run these serves for the company.

The asset rights on the public blockchain are controlled through the entry in the asset table. Asset hash gives us the information who can change the holding for an account.

Account controller change action basically updates the asset hash in the asset table specifying a new controller. The process first checks if there is already an asset with a current controller. In case there is one the controller will be updated. If there is no entry a new assignment will be created.

**Parameters:**

| Parameter | Type | Description |
| --- | --- | --- |
| Asset Hash | AccountHash | The asset hash of the current account controller |
| Controller Address | ChainiumAddress | A Chainium address of the new controller |

## Set Asset Code Controller

In order to make a user experience on the public blockchain more natural and user-friendly, we have introduced Asset Code domains. The &quot;Asset Code&quot; is a user-friendly name to identify an asset hash and it is directly, &quot;one-to-one&quot; connected to the unique asset hash. Each Asset code belongs to only one single asset hash.

The &quot;Set Asset Code Controller&quot; action type sets the Chainium address who controls the domain asset code. Like every &quot;Set&quot; action on the Chainium public blockchain, it creates a new entry if there is no existing &quot;previous&quot; controller. If there is an existing controller it updates the entry to a new controller.

**Parameters:**

| Parameter | Type | Description |
| --- | --- | --- |
| Asset Hash | AccountHash | The account hash of the current account controller |
| Controller Address | ChainiumAddress | A Chainium address of the new controller |

## Useful links

- [Blockchain Domain Types](file:///tmp/d20180616-4-fnhcc/%E2%80%A2%09https:/github.com/Chainium/Chainium/blob/master/Source/Chainium.Blockchain.Public.Core/DomainTypes.fs)
- [Blockchain Transaction Processing Source Code](file:///tmp/d20180616-4-fnhcc/%E2%80%A2%09https:/github.com/Chainium/Chainium/blob/master/Source/Chainium.Blockchain.Public.Core/Processing.fs)

## Phase 2  tx Action types

- Capital Increase
- Capital Decrease
- Address Data Change\*
- Asset Split
- Asset Merge
- Dividend Payment
