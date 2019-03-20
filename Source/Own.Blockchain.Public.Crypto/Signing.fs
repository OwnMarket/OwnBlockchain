namespace Own.Blockchain.Public.Crypto

open System
open System.Security.Cryptography
open Own.Common.FSharp
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

    let private signHashBytes (networkId : NetworkId option) (PrivateKey privateKey) (hashBytes : byte[]) : Signature =
        if hashBytes.Length <> 32 then
            failwithf "Data to sign is expected to be 32 bytes long (256-bit hash). Actual length: %i" hashBytes.Length

        let dataToSign =
            networkId
            |> Option.map (fun h -> h.Value)
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

    let signHash getNetworkId privateKey hash =
        try
            let networkId = getNetworkId ()
            hash
            |> Hashing.decode
            |> signHashBytes (Some networkId) privateKey
        with
        | ex ->
            raise (new Exception(sprintf "Failed to sign the hash %s" hash, ex))

    let verifySignature getNetworkId (Signature signature) messageHash : BlockchainAddress option =
        let networkId : NetworkId = getNetworkId ()
        let signatureBytes = signature |> Hashing.decode
        let messageHashBytes = messageHash |> Hashing.decode

        let dataToVerify =
            networkId.Value
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
