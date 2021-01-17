namespace Own.Blockchain.Public.Crypto

open System
open System.Security.Cryptography
open Own.Common.FSharp
open Own.Blockchain.Common
open Own.Blockchain.Public.Core.DomainTypes

module Signing =

    let generateRandomBytes byteCount =
        let bytes = Array.zeroCreate byteCount
        use rngCsp = new RNGCryptoServiceProvider()
        rngCsp.GetBytes bytes // Fill the array with a random value.
        bytes

    let generateWallet () : WalletInfo =
        let privateKey, publicKey = Secp256k1.generateKeyPair ()

        {
            PrivateKey =
                privateKey
                |> Hashing.encode
                |> PrivateKey
            Address =
                publicKey
                |> Hashing.blockchainAddress
        }

    let addressFromPrivateKey (PrivateKey privateKey) =
        privateKey
        |> Hashing.decode
        |> Secp256k1.calculatePublicKey
        |> Hashing.blockchainAddress

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Generic
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let private sign (PrivateKey privateKey) (bytesToSign : byte[]) : Signature =
        let recoveryId, signatureSerialized =
            privateKey
            |> Hashing.decode
            |> Secp256k1.sign bytesToSign

        [
            signatureSerialized
            [| Convert.ToByte recoveryId |]
        ]
        |> Array.concat
        |> Hashing.encode
        |> Signature

    let private verify (Signature signature) bytesToVerify : BlockchainAddress option =
        let signatureBytes = signature |> Hashing.decode
        let recoveryId = signatureBytes.[64] |> int
        let signature =
            [
                signatureBytes |> Seq.take 32 |> Seq.toArray
                signatureBytes |> Seq.skip 32 |> Seq.take 32 |> Seq.toArray
            ]
            |> Array.concat
            |> Secp256k1.parseSignature recoveryId

        let publicKey = Secp256k1.recoverPublicKeyFromSignature signature bytesToVerify

        if Secp256k1.verifySignature signature bytesToVerify publicKey then
            Secp256k1.serializePublicKey publicKey
            |> Hashing.blockchainAddress
            |> Some
        else
            None

    let signPlainText privateKey message =
        message
        |> Conversion.stringToBytes
        |> Hashing.hashBytes
        |> sign privateKey

    let verifyPlainTextSignature signature message =
        message
        |> Conversion.stringToBytes
        |> Hashing.hashBytes
        |> verify signature

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Network-specific
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let private signHashBytes (networkId : NetworkId option) privateKey (hashBytes : byte[]) : Signature =
        if hashBytes.Length <> 32 then
            failwithf "Data to sign is expected to be 32 bytes long (256-bit hash). Actual length: %i" hashBytes.Length

        networkId
        |> Option.map (fun h -> h.Value)
        |? Array.empty
        |> Array.append hashBytes
        |> Hashing.hashBytes
        |> sign privateKey

    let signHash getNetworkId privateKey hash =
        try
            let networkId = getNetworkId ()
            hash
            |> Hashing.decode
            |> signHashBytes (Some networkId) privateKey
        with
        | ex ->
            raise (new Exception(sprintf "Failed to sign the hash %s" hash, ex))

    let verifySignature getNetworkId signature messageHash : BlockchainAddress option =
        let networkId : NetworkId = getNetworkId ()
        let messageHashBytes = messageHash |> Hashing.decode

        networkId.Value
        |> Array.append messageHashBytes
        |> Hashing.hashBytes
        |> verify signature
