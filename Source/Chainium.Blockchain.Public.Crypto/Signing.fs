namespace Chainium.Blockchain.Public.Crypto

open System
open System.Security.Cryptography
open Org.BouncyCastle.Asn1.Sec
open Org.BouncyCastle.Crypto.Parameters
open Org.BouncyCastle.Crypto.Generators
open Org.BouncyCastle.Security
open Org.BouncyCastle.Math
open Org.BouncyCastle.Crypto.Digests
open Org.BouncyCastle.Crypto.Signers
open Org.BouncyCastle.Math.EC
open Chainium.Blockchain.Public.Core.DomainTypes

module Signing =

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Private functions and values used only in this module
    // Highly dependent on BouncyCastle library
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    let private curve = SecNamedCurves.GetByName("secp256k1")
    let private domain = ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H)

    let private generateKeyPair () =
        let gen = ECKeyPairGenerator()
        let secureRandom = SecureRandom()

        let keyGenParams = ECKeyGenerationParameters(domain, secureRandom)

        gen.Init(keyGenParams)

        let keyPair = gen.GenerateKeyPair()

        let privateKey = keyPair.Private :?> ECPrivateKeyParameters
        let publicKey = keyPair.Public :?> ECPublicKeyParameters

        (privateKey.D.ToByteArrayUnsigned(), publicKey.Q.GetEncoded())

    let private bytesToBigInteger (bytes : byte[]) =
        BigInteger(1, bytes)

    let private calculatePublicKey (privateKey : BigInteger) =
        curve.G.Multiply(privateKey).GetEncoded()
        |> bytesToBigInteger

    let private getCompressionArrayBasedOnRecId recId (x : BigInteger) =
        let compressionArraySize = 33
        let xUnsigned = x.ToByteArrayUnsigned()

        let compEncoding = Array.zeroCreate compressionArraySize
        let compressionByteVal = byte(0x02 + (recId % 2))
        compEncoding.[0] <- compressionByteVal

        let copyOffset = compEncoding.Length - xUnsigned.Length
        Buffer.BlockCopy(xUnsigned, 0, compEncoding, copyOffset, xUnsigned.Length)

        compEncoding

    let private calculateQPoint
        (message : byte[])
        (order : BigInteger)
        (rComponent : BigInteger)
        (sComponent : BigInteger)
        (r : ECPoint)
        =

        let messageE = message |> bytesToBigInteger
        let e = BigInteger.Zero.Subtract(messageE).Mod(order)
        let rr = rComponent.ModInverse(order)
        let sor = sComponent.Multiply(rr).Mod(order)
        let eor = e.Multiply(rr).Mod(order)

        let q = curve.G.Multiply(eor).Add(r.Multiply(sor))
        q.Normalize().GetEncoded() |> bytesToBigInteger

    let private recoverPublicKeyFromSignature
        (recId : int)
        (rComponent : BigInteger)
        (sComponent : BigInteger)
        (messageHash : byte[])
        =

        let fpCurve = curve.Curve :?> FpCurve
        let order = curve.N

        let item =
            recId / 2
            |> int
            |> string

        let i = BigInteger(item)
        let x = order.Multiply(i).Add(rComponent)

        let compareToPrime = x.CompareTo(fpCurve.Q)

        match compareToPrime >= 0 with
        | true -> None
        | false ->
            let getEncodingArray intValue = getCompressionArrayBasedOnRecId recId intValue

            let r =
                x
                |> getEncodingArray
                |> curve.Curve.DecodePoint

            calculateQPoint messageHash order rComponent sComponent r
            |> Some

    let private verify originalMesageHash messageHash signature (publicKey : BigInteger) =
        let ecPoint = domain.Curve.DecodePoint(publicKey.ToByteArrayUnsigned())
        let publicKeyParameters = ECPublicKeyParameters(ecPoint, domain)

        let ecdsa = ECDsaSigner()
        ecdsa.Init(false, publicKeyParameters)
        ecdsa.VerifySignature(originalMesageHash, messageHash, signature)

    let rec private getRecordId recordId (publicKey : BigInteger) originalMessage (signature : BigInteger[]) =
        match recordId with
        | recordId when recordId >= 4 || recordId < 0 -> -1
        | _ ->
            let rComponent = signature.[0]
            let sComponent = signature.[1]

            let potentialKey = recoverPublicKeyFromSignature recordId rComponent sComponent originalMessage

            let isValidKey =
                match potentialKey with
                | None -> false
                | Some key -> publicKey = key && (verify originalMessage rComponent sComponent key)

            match isValidKey with
            | true -> recordId
            | false -> getRecordId (recordId + 1) publicKey originalMessage signature

    let private calculateVComponent publicKey originalMessage signature =
        getRecordId 0 publicKey originalMessage signature

    let private getBouncyCastleSignatureArray privateKeyInt messageHash =
        let digest = Sha256Digest()
        let calc = HMacDsaKCalculator(digest)
        let ecdsa = ECDsaSigner(calc)

        let privateParams = ECPrivateKeyParameters(privateKeyInt, domain)
        let rnd = SecureRandom()
        let paramsWithRandom = ParametersWithRandom(privateParams, rnd)

        ecdsa.Init(true, paramsWithRandom)
        ecdsa.GenerateSignature(messageHash)

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Public facing functions, operating with domain types instead of raw bytes or BigInteger.
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let generateRandomBytes byteCount =
        let bytes = Array.zeroCreate byteCount
        use rngCsp = new RNGCryptoServiceProvider()
        rngCsp.GetBytes(bytes) // Fill the array with a random value.
        bytes

    let generateWallet () : WalletInfo =
        let keyPair = generateKeyPair ()

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
            |> bytesToBigInteger
            |> calculatePublicKey

        publicKey.ToByteArrayUnsigned()
        |> Hashing.chainiumAddress

    let signMessage (PrivateKey privateKey) (message : byte[]) : Signature =
        let privateKey =
            privateKey
            |> Hashing.decode
            |> bytesToBigInteger

        let messageHash = Hashing.hashBytes message
        let signature = getBouncyCastleSignatureArray privateKey messageHash

        let publicKey = calculatePublicKey privateKey
        let vComponent =
            calculateVComponent publicKey messageHash signature
            |> (fun v -> [| Convert.ToByte v |])

        {
            V = vComponent |> Hashing.encode
            R = signature.[0].ToByteArrayUnsigned() |> Hashing.encode
            S = signature.[1].ToByteArrayUnsigned() |> Hashing.encode
        }

    let verifySignature (signature : Signature) (message : byte[]) : ChainiumAddress option =
        let vComponent =
            signature.V
            |> Hashing.decode
            |> (fun arr -> arr.[0])
            |> int

        let rComponent =
            signature.R
            |> Hashing.decode
            |> bytesToBigInteger

        let sComponent =
            signature.S
            |> Hashing.decode
            |> bytesToBigInteger

        let messageHash = Hashing.hashBytes message
        recoverPublicKeyFromSignature vComponent rComponent sComponent messageHash
        |> Option.bind (fun publicKey ->
            if verify messageHash rComponent sComponent publicKey then
                publicKey.ToByteArrayUnsigned()
                |> Hashing.chainiumAddress
                |> Some
            else
                None
        )
