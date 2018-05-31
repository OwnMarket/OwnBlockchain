# Cryptography

## Hashing

Chainium blockchain uses [SHA256](https://en.wikipedia.org/wiki/SHA-256) as the main hashing algorithm.

## Encoding

All cryptographic artifacts (private/public keys, hashes, etc...) are encoded as [Base58](https://en.wikipedia.org/wiki/Base58) string and used as such throughout the application and external interfaces.

## Chainium Address

Chainium address is created using following algorithm:

![Chainium Address Algorithm](ChainiumAddress.png)
