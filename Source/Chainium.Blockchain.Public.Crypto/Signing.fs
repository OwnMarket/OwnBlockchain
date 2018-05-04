namespace Chainium.Blockchain.Public.Crypto

open System
open System.Text
open System.Security.Cryptography
open Chainium.Common
open Chainium.Blockchain.Public.Core.DomainTypes

module Signing =

    let generateRandomBytes byteCount =
        // TODO: Review
        let bytes = Array.zeroCreate byteCount
        use rngCsp = new RNGCryptoServiceProvider()
        rngCsp.GetBytes(bytes) // Fill the array with a random value.
        bytes

    let generateRandomSeed () =
        // TODO: Implement
        let seed = generateRandomBytes 64;
        Convert.ToBase64String(seed)

    let generatePrivateKey seed : PrivateKey =
        // TODO: Implement
        PrivateKey "DUMMY_PRIVATE_KEY"

    let calculatePublicKey (privateKey : PrivateKey) : PublicKey =
        // TODO: Implement
        PublicKey "DUMMY_PUBLIC_KEY"

    let calculateAddress (publicKey : PublicKey) : ChainiumAddress =
        // TODO: Implement
        ChainiumAddress "ch1234567890"

    let generateKeyPair seed : KeyPair =
        let seed = seed |?> generateRandomSeed
        let privateKey = generatePrivateKey seed
        let publicKey = calculatePublicKey privateKey

        {
            PrivateKey = privateKey
            PublicKey = publicKey
        }

    let signMessage (privateKey : PrivateKey) (data : byte[]) : Signature =
        // TODO: Implement
        {
            R = "AAA"
            S = "BBB"
        }

    let verifySignature (signature : Signature) (data : byte[]) : ChainiumAddress option =
        // TODO: Implement
        Some (ChainiumAddress "ch1234567890")
