namespace Own.Blockchain.Public.Sdk.Tests

open Own.Blockchain.Public.Crypto

module Helpers =

    let networkCode = "UNIT_TESTS"

    let getNetworkId () =
        Hashing.networkId networkCode

    let verifySignature = Signing.verifySignature getNetworkId
