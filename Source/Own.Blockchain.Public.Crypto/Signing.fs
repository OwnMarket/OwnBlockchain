namespace Own.Blockchain.Public.Crypto

open System
open System.Security.Cryptography
open Own.Common
open Own.Blockchain.Common
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

    let private signHashBytes (networkCode : string option) (PrivateKey privateKey) (hashBytes : byte[]) : Signature =
        if hashBytes.Length <> 32 then
            failwithf "Data to sign is expected to be 32 bytes long (256-bit hash). Actual length: %i" hashBytes.Length

        let dataToSign =
            networkCode
            |> Option.map (Conversion.stringToBytes >> Hashing.hashBytes)
            |? Array.empty
            |> Array.append hashBytes
            |> Hashing.hashBytes

        let recoveryId, signatureSerialized =
            privateKey
            |> Hashing.decode
            |> Secp256k1.sign dataToSign

        [
            signatureSerialized
            [| Convert.ToByte recoveryId |]
        ]
        |> Array.concat
        |> Hashing.encode
        |> Signature

    let signHash networkCode privateKey hash =
        hash
        |> Hashing.decode
        |> signHashBytes (Some networkCode) privateKey

    let verifySignature networkCode (Signature signature) messageHash : BlockchainAddress option =
        let signatureBytes = signature |> Hashing.decode
        let messageHashBytes = messageHash |> Hashing.decode

        let dataToVerify =
            networkCode
            |> Conversion.stringToBytes
            |> Hashing.hashBytes
            |> Array.append messageHashBytes
            |> Hashing.hashBytes

        let recoveryId = signatureBytes.[64] |> int

        let signature =
            [
                signatureBytes |> Seq.take 32 |> Seq.toArray
                signatureBytes |> Seq.skip 32 |> Seq.take 32 |> Seq.toArray
            ]
            |> Array.concat
            |> Secp256k1.parseSignature recoveryId

        let publicKey = Secp256k1.recoverPublicKeyFromSignature signature dataToVerify

        if Secp256k1.verifySignature signature dataToVerify publicKey then
            Secp256k1.serializePublicKey publicKey
            |> Hashing.blockchainAddress
            |> Some
        else
            None
