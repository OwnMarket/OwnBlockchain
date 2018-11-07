# Blockchain Data Structures


## Block

A block in Own public blockchain has following structure:

![Block Structure](BlockStructure.png)

Element | Description
---|---
Block Number | 64-bit integer
Block Hash | SHA256 hash of other fields combined together.
Previous Block Hash | SHA256 hash of the previous block in the chain.
Timestamp | 64-bit integer containing UNIX timestamp.
Validator Address | Own blockchain address of the block creator.
Tx Set Merkle Root | Merkle tree root of all Tx hashes from Tx Set
Tx Results Merkle Root | Merkle tree root of all Tx hashes and corresponding processing results.
State Merkle Root | Merkle tree root of all state entries related to Tx Set belonging to the block.
