namespace Own.Blockchain.Public.Crypto

open System
open System.Security.Cryptography
open Secp256k1Net
open Own.Common

module internal Secp256k1 =

    let private getSecp256k1 = memoizePerThread (fun () -> new Secp256k1())

    let private secretKeyVerify privateKey =
        let secp256k1 = getSecp256k1 ()
        try
            secp256k1.SecretKeyVerify(Span privateKey)
        with
        | _ -> false

    let generatePrivateKey () =
        let privateKey = Array.zeroCreate<byte> Secp256k1.PRIVKEY_LENGTH
        use rngCsp = new RNGCryptoServiceProvider()
        rngCsp.GetBytes(privateKey)
        while not (secretKeyVerify privateKey) do
            rngCsp.GetBytes(privateKey)
        privateKey

    let serializePublicKey publicKey =
        let serializedPublicKey = Array.zeroCreate<byte> Secp256k1.SERIALIZED_UNCOMPRESSED_PUBKEY_LENGTH
        let secp256k1 = getSecp256k1 ()
        if secp256k1.PublicKeySerialize(Span serializedPublicKey, Span publicKey) then
            serializedPublicKey
        else
            failwith "[Secp256k1] Error serializing public key"

    let calculatePublicKey privateKey =
        let publicKey = Array.zeroCreate<byte> Secp256k1.PUBKEY_LENGTH
        let secp256k1 = getSecp256k1 ()
        if secp256k1.PublicKeyCreate(Span publicKey, Span privateKey) then
            serializePublicKey publicKey
        else
            failwith "[Secp256k1] Error calculating public key"

    let rec generateKeyPair () =
        let privateKey = generatePrivateKey ()
        let publicKey = calculatePublicKey privateKey
        (privateKey, publicKey)

    let signRecoverable messageHash privateKey =
        let signature = Array.zeroCreate<byte> Secp256k1.UNSERIALIZED_SIGNATURE_SIZE
        let secp256k1 = getSecp256k1 ()
        if secp256k1.SignRecoverable(Span signature, Span messageHash, Span privateKey) then
            signature
        else
            failwith "[Secp256k1] Error signing message"

    let serializeSignature signature =
        let serializedSignature = Array.zeroCreate<byte> Secp256k1.SERIALIZED_SIGNATURE_SIZE
        let recoveryId = ref -1
        let secp256k1 = getSecp256k1 ()
        if secp256k1.RecoverableSignatureSerializeCompact(Span serializedSignature, recoveryId, Span signature) then
            (!recoveryId, serializedSignature)
        else
            failwith "[Secp256k1] Error serializing signature"

    let sign messageHash privateKey =
        let signature = signRecoverable messageHash privateKey
        serializeSignature signature

    let parseSignature recoveryId serializedSignature =
        let signature = Array.zeroCreate<byte> Secp256k1.UNSERIALIZED_SIGNATURE_SIZE
        let secp256k1 = getSecp256k1 ()
        if secp256k1.RecoverableSignatureParseCompact(Span signature, Span serializedSignature, recoveryId) then
            signature
        else
            failwith "[Secp256k1] Error parsing signature"

    let recoverPublicKeyFromSignature signature messageHash =
        let publicKey = Array.zeroCreate<byte> (Secp256k1.PUBKEY_LENGTH)
        let secp256k1 = getSecp256k1 ()
        if secp256k1.Recover(Span publicKey, Span signature, Span messageHash) then
            publicKey
        else
            failwith "[Secp256k1] Error recovering publicKey"

    let verifySignature signature messageHash publicKey =
        let secp256k1 = getSecp256k1 ()
        secp256k1.Verify(Span signature, Span messageHash, Span publicKey)
