# Transaction Processing

Transaction which is submitted into the transaction pool, eventually gets processed. However, for transaction to be included in the block and processed, there are certain conditions to be met. This process and related conditions are explained below.


## Fetching transactions from the pool

Transaction pool contains pending transactions waiting to be included into the block and processed. When fetching transactions from the pool, priority is given to transactions with higher fee. If transactions have same fee, then transactions which came earlier in the pool are taken first.

### Excluding unprocessable transactions from the set

Once transactions are fetched into the set, we exclude those which cannot be processed due to following reasons:

- Sender's CHX balance is too low to cover the execution fee.
- There is a _nonce gap_ in the sequence of transactions belonging to the same sender.

Nonce gap is identified as difference between nonces of two subsequent transactions being greater than `1`.
To detect transactions with the nonce gap, following steps are performed:

1. Group transactions in the set by sender address.
2. Order transactions in each subset by nonce.
3. For each transaction in the subset calculate the difference between its nonce and the nonce of the preceding transaction. Sender's address nonce is used as preceding nonce when calculating the difference for first transaction in the subset.
4. Keep including transactions from the subset into the final set until the nonce difference greater than `1` is detected - skip the rest.

After all unprocessable transactions are excluded from the set, fetch is performed again until the set is filled with processable transactions, or there are no transactions left in transaction pool.


## Ordering the transaction set

Once transaction set is fetched, transactions have to be ordered for processing. Ordering of the transaction set is performed as follows:

1. Order all transactions in the set by order of appearance in the transaction pool.
2. For each transaction in the set following check is performed:
    - If there are other transactions later in the set coming from the same sender, but having nonce smaller than the nonce of the currently checked transaction, or same nonce but higher fee, then such transactions are moved into position immediately before currently checked transaction.


## Processing of the transaction set

After the transaction set is ordered, transactions are being processed one by one. For each transaction in the set, following steps are performed:

1. Update sender's address nonce.
2. Process validator reward (move CHX fee from sender address to validator address).
3. Process actions.

If processing of any action belonging to a transaction fails, due to conditions defined in the action, then the whole transaction fails and the changes performed by the transaction are rolled back.


## Applying new state

After the transaction set is processed, new [block](DataStructures.md#block) is assembled and new state is atomically applied to the persistent storage.
