namespace Own.Blockchain.Public.Crypto.Tests

open Own.Blockchain.Public.Crypto

module Helpers =

    let networkCode = "UNIT_TESTS"

    let getNetworkId () =
        Hashing.networkId networkCode
