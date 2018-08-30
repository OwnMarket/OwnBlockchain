namespace Chainium.Blockchain.Public.Crypto

open System
open System.Security.Cryptography
open Secp256k1Net

module Secp256k1 =

    let private secp256k1 = new Secp256k1()

    let internal generatePrivateKey () =
        let rnd = RandomNumberGenerator.Create()
        let privateKey = Span (Array.zeroCreate<byte> Secp256k1.PRIVKEY_LENGTH)
        rnd.GetBytes privateKey
        while not (secp256k1.SecretKeyVerify(privateKey)) do
            rnd.GetBytes privateKey
        privateKey.ToArray()

    let internal serializePublicKey publicKey =
        let serializedPublicKey = Span (Array.zeroCreate<byte> (Secp256k1.SERIALIZED_PUBKEY_LENGTH))
        if secp256k1.PublicKeySerialize(serializedPublicKey, Span publicKey) then
            serializedPublicKey.ToArray()
        else
            failwith "[Secp256k1] Error serializing publicKey"

    let internal calculatePublicKey privateKey =
        let publicKey = Span (Array.zeroCreate<byte> Secp256k1.PUBKEY_LENGTH)
        if secp256k1.PublicKeyCreate(publicKey, Span privateKey) then
            serializePublicKey (publicKey.ToArray())
        else
            failwith "[Secp256k1] Error generating publicKey from privateKey"

    let internal generateKeyPair () =
        let privateKey = generatePrivateKey ()
        let publicKey = calculatePublicKey privateKey
        (privateKey, publicKey)

    let internal signRecoverable messageHash privateKey =
        let signature = Span (Array.zeroCreate<byte> Secp256k1.UNSERIALIZED_SIGNATURE_SIZE)
        if secp256k1.SignRecoverable(signature, Span messageHash, privateKey) then
            signature.ToArray()
        else
            failwith "[Secp256k1] Error signing message"

    let internal serializeSignature signature =
        let serializedSignature = Span (Array.zeroCreate<byte> Secp256k1.SERIALIZED_SIGNATURE_SIZE)
        let recoveryId = ref -1
        if secp256k1.RecoverableSignatureSerializeCompact(serializedSignature, recoveryId, Span signature) then
            (!recoveryId, serializedSignature.ToArray())
        else
            failwith "[Secp256k1] Error serializing signature"

    let internal sign messageHash privateKey =
        let signature = signRecoverable messageHash (Span privateKey)
        serializeSignature signature

    let internal parseSignature recoveryId serializedSignature =
        let signature = Span (Array.zeroCreate<byte> Secp256k1.UNSERIALIZED_SIGNATURE_SIZE)
        if secp256k1.RecoverableSignatureParseCompact(signature, Span serializedSignature, recoveryId) then
            signature.ToArray()
        else
            failwith "[Secp256k1] Error parsing signature"

    let internal recoverPublicKeyFromSignature signature messageHash =
        let publicKey = Span (Array.zeroCreate<byte> Secp256k1.PUBKEY_LENGTH)
        if secp256k1.Recover(publicKey, Span signature, Span messageHash) then
            publicKey.ToArray()
        else
            failwith "[Secp256k1] Error recovering publicKey"

    let internal verifySignature signature messageHash publicKey =
        secp256k1.Verify(Span signature, Span messageHash, Span publicKey)
