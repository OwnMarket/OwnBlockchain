# Cryptography

## Private/Public Keys

Private/public key pairs are generated using Elliptic Curve Cryptography. Similar to other major blockchain projects, Own blockchain uses a specific Koblitz curve [secp256k1](https://en.bitcoin.it/wiki/Secp256k1).

Concrete implementation of algorithm used in Own blockchain, comes from [secp256k1 library](https://github.com/bitcoin-core/secp256k1).

## Hashing

Own blockchain uses [SHA256](https://en.wikipedia.org/wiki/SHA-256) as the main hashing algorithm. Implementation of SHA256 is part of .NET standard library.

## Encoding

All cryptographic artifacts (private/public keys, hashes, etc.) are encoded as [Base58](https://en.wikipedia.org/wiki/Base58) strings and used as such throughout the application and external interfaces. Base58 dictionary used is the same as the one used in Bitcoin implementation.

Own blockchain uses the implementation of Base58 encoding from [Multiformats project](https://multiformats.io).

## Own Blockchain Address

Own blockchain address is created using following algorithm:

![Own Blockchain Address Algorithm](OwnBlockchainAddress.png)

Here is a sample address generated from a private key using this algorithm:

Private Key | Address
---|---
`1ApCvGVAk3qZVY3VGLxLVvAw3Azuhf5SXBWYDag3oUie2` | `CHbcUgieDDKQBVzCw2vsKvArNkt6Sr8VdkM`

**DO NOT USE THIS PRIVATE KEY AND ADDRESS ON THE NETWORK!!!**
