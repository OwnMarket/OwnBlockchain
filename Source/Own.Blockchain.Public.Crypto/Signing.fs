namespace Own.Blockchain.Public.Crypto

open System
open System.Security.Cryptography
open Own.Blockchain.Public.Core.DomainTypes

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
                |> Hashing.blockchainAddress
        }

    let addressFromPrivateKey (PrivateKey privateKey) =
        let publicKey =
            privateKey
            |> Hashing.decode
            |> Secp256k1.calculatePublicKey
            |> Hashing.blockchainAddress

        publicKey

    let signMessage (PrivateKey privateKey) (message : byte[]) : Signature =
        let privateKey =
            privateKey
            |> Hashing.decode

        let messageHash = Hashing.hashBytes message
        let (recoveryId, signatureSerialized) = Secp256k1.sign messageHash privateKey

        [
            signatureSerialized
            recoveryId |> (fun v -> [| Convert.ToByte v |])
        ]
        |> Array.concat
        |> Hashing.encode
        |> Signature

    let verifySignature (Signature signature) (message : byte[]) : BlockchainAddress option =
        let signatureBytes = signature |> Hashing.decode

        let recoveryId = signatureBytes.[64] |> int

        let signature =
            [
                signatureBytes |> Seq.take 32 |> Seq.toArray
                signatureBytes |> Seq.skip 32 |> Seq.take 32 |> Seq.toArray
            ]
            |> Array.concat
            |> Secp256k1.parseSignature recoveryId

        let messageHash = message |> Hashing.hashBytes
        let publicKey = Secp256k1.recoverPublicKeyFromSignature signature messageHash

        if Secp256k1.verifySignature signature messageHash publicKey then
            Secp256k1.serializePublicKey publicKey
            |> Hashing.blockchainAddress
            |> Some
        else
            None
