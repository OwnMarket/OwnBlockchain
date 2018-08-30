namespace Chainium.Blockchain.Public.Crypto

open System
open System.Security.Cryptography
open Chainium.Blockchain.Public.Core.DomainTypes

module Signing =

    let generateRandomBytes byteCount =
        let bytes = Array.zeroCreate byteCount
        use rngCsp = new RNGCryptoServiceProvider()
        rngCsp.GetBytes(bytes) // Fill the array with a random value.
        bytes

    let generateWallet () : WalletInfo =
        let keyPair = Secp256k1.generateKeyPair ()

        {
            PrivateKey =
                keyPair
                |> fst
                |> Hashing.encode
                |> PrivateKey
            Address =
                keyPair
                |> snd
                |> Hashing.chainiumAddress
        }

    let addressFromPrivateKey (PrivateKey privateKey) =
        let publicKey =
            privateKey
            |> Hashing.decode
            |> Secp256k1.calculatePublicKey
            |> Hashing.chainiumAddress

        publicKey

    let signMessage (PrivateKey privateKey) (message : byte[]) : Signature =
        let privateKey =
            privateKey
            |> Hashing.decode

        let messageHash = Hashing.hashBytes message
        let (recoveryId, signatureSerialized) = Secp256k1.sign messageHash privateKey

        {
            V =
                recoveryId
                |> (fun v -> [| Convert.ToByte v |])
                |> Hashing.encode
            R =
                signatureSerialized
                |> Seq.take 32
                |> Seq.toArray
                |> Hashing.encode
            S =
                signatureSerialized
                |> Seq.skip(32)
                |> Seq.take(32)
                |> Seq.toArray
                |> Hashing.encode
        }

    let verifySignature (signature : Signature) (message : byte[]) : ChainiumAddress option =
        let recoveryId =
            signature.V
            |> Hashing.decode
            |> (fun arr -> arr.[0])
            |> int

        let signature =
            [
                signature.R |> Hashing.decode
                signature.S |> Hashing.decode
            ]
            |> Array.concat
            |> Secp256k1.parseSignature recoveryId

        let messageHash = message |> Hashing.hashBytes
        let publicKey = Secp256k1.recoverPublicKeyFromSignature signature messageHash

        if Secp256k1.verifySignature signature messageHash publicKey then
            Secp256k1.serializePublicKey publicKey
            |> Hashing.chainiumAddress
            |> Some
        else
            None
