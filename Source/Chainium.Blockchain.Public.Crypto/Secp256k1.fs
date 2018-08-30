namespace Chainium.Blockchain.Public.Crypto

open System
open System.Security.Cryptography
open Secp256k1Net

module Secp256k1 =

    let private secp256k1 = new Secp256k1()

    let internal generatePrivateKey () =
        let rnd = RandomNumberGenerator.Create()
        let privateKey = Array.zeroCreate<byte> Secp256k1.PRIVKEY_LENGTH |> Span
        privateKey |> rnd.GetBytes
        while not (secp256k1.SecretKeyVerify(privateKey)) do
            privateKey |> rnd.GetBytes
        privateKey.ToArray()

    let internal serializePublicKey publicKey =
        let serializedPublicKey = Array.zeroCreate<byte> (Secp256k1.SERIALIZED_PUBKEY_LENGTH) |> Span
        if secp256k1.PublicKeySerialize(serializedPublicKey, publicKey |> Span) then
            serializedPublicKey.ToArray()
        else
            failwith "[Secp256k1] Error serializing publicKey"

    let internal calculatePublicKey privateKey =
        let publicKey = Array.zeroCreate<byte> Secp256k1.PUBKEY_LENGTH |> Span
        if secp256k1.PublicKeyCreate(publicKey, privateKey |> Span) then
            serializePublicKey (publicKey.ToArray())
        else
            failwith "[Secp256k1] Error generating publicKey from privateKey"

    let internal generateKeyPair () =
        let privateKey = generatePrivateKey ()
        let publicKey = calculatePublicKey privateKey
        (privateKey, publicKey)

    let internal signRecoverable messageHash privateKey =
        let signature = Array.zeroCreate<byte> Secp256k1.UNSERIALIZED_SIGNATURE_SIZE |> Span
        if secp256k1.SignRecoverable(signature, (messageHash |> Span), privateKey) then
            signature.ToArray()
        else
            failwith "[Secp256k1] Error signing message"

    let internal serializeSignature signature =
        let serializedSignature = Array.zeroCreate<byte> Secp256k1.SERIALIZED_SIGNATURE_SIZE |> Span
        let recoveryId = ref -1
        if secp256k1.RecoverableSignatureSerializeCompact(serializedSignature, recoveryId, signature |> Span) then
            (!recoveryId, serializedSignature.ToArray())
        else
            failwith "[Secp256k1] Error serializing signature"

    let internal sign messageHash privateKey =
        let signature = signRecoverable messageHash (privateKey |> Span)
        serializeSignature signature

    let internal parseSignature recoveryId serializedSignature =
        let signature = Array.zeroCreate<byte> Secp256k1.UNSERIALIZED_SIGNATURE_SIZE |> Span
        if secp256k1.RecoverableSignatureParseCompact(signature, serializedSignature |> Span, recoveryId) then
            signature.ToArray()
        else
            failwith "[Secp256k1] Error parsing signature"

    let internal recoverPublicKeyFromSignature signature messageHash =
        let publicKey = Array.zeroCreate<byte> (Secp256k1.PUBKEY_LENGTH) |> Span
        if secp256k1.Recover(publicKey, signature |> Span, messageHash |> Span) then
            publicKey.ToArray()
        else
            failwith "[Secp256k1] Error recovering publicKey"

    let internal verifySignature signature messageHash publicKey =
        secp256k1.Verify(signature |> Span, messageHash |> Span, publicKey |> Span)
