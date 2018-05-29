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
open Chainium.Common
open Chainium.Blockchain.Public.Core.DomainTypes

module Signing =

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Private functions and values used only in this module
    // Highly dependent on BouncyCastle library
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    let private curve = SecNamedCurves.GetByName("secp256k1")
    let private domain = ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H)

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

        let messageE = BigInteger(1, message)
        let e = BigInteger.Zero.Subtract(messageE).Mod(order)
        let rr = rComponent.ModInverse(order)
        let sor = sComponent.Multiply(rr).Mod(order)
        let eor = e.Multiply(rr).Mod(order)

        let q = curve.G.Multiply(eor).Add(r.Multiply(sor))
        q.Normalize().GetEncoded() |> BigInteger

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
        let ecPoint = domain.Curve.DecodePoint(publicKey.ToByteArray())
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

    let private calculatePublicKey (privateKey : BigInteger) =
        curve.G.Multiply(privateKey).GetEncoded()
        |> BigInteger

    let private generateKeyPair (seed : byte[] option) =
        let gen = ECKeyPairGenerator()
        let secureRandom = SecureRandom()

        match seed with
        | None -> ()
        | Some seedValue -> secureRandom.SetSeed seedValue

        let keyGenParams = ECKeyGenerationParameters(domain, secureRandom)

        gen.Init(keyGenParams)

        let keyPair = gen.GenerateKeyPair()

        let privateKey = keyPair.Private :?> ECPrivateKeyParameters
        let publicKey = keyPair.Public :?> ECPublicKeyParameters

        (privateKey.D.ToByteArray(), publicKey.Q.GetEncoded())


    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Public facing functions, operating with domain types instead of raw bytes or BigInteger.
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let generateRandomBytes byteCount =
        // TODO: Review
        let bytes = Array.zeroCreate byteCount
        use rngCsp = new RNGCryptoServiceProvider()
        rngCsp.GetBytes(bytes) // Fill the array with a random value.
        bytes

    let generateRandomSeed () =
        // TODO: Review
        generateRandomBytes 64

    let generateWallet (seed : byte[] option) : WalletInfo =
        let keyPair = generateKeyPair seed

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

    let signMessage (PrivateKey privateKey) (message : byte[]) : Signature =
        let privateKey =
            privateKey
            |> Hashing.decode
            |> BigInteger

        let messageHash = Hashing.hashBytes message
        let publicKey = calculatePublicKey privateKey
        let signature = getBouncyCastleSignatureArray privateKey messageHash

        let vComponent =
            calculateVComponent publicKey messageHash signature
            |> (fun v -> [| byte v |])
            |> Hashing.encode

        {
            V = vComponent
            R = signature.[0].ToByteArray() |> Hashing.encode
            S = signature.[1].ToByteArray() |> Hashing.encode
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
            |> BigInteger

        let sComponent =
            signature.S
            |> Hashing.decode
            |> BigInteger

        let messageHash = Hashing.hashBytes message
        recoverPublicKeyFromSignature vComponent rComponent sComponent messageHash
        |> Option.bind (fun publicKey ->
            if verify messageHash rComponent sComponent publicKey then
                publicKey.ToByteArray()
                |> Hashing.chainiumAddress
                |> Some
            else
                None
        )
