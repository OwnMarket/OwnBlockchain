namespace Chainium.Blockchain.Public.Crypto

open System
open System.Text
open System.Security.Cryptography
open Chainium.Common
open Chainium.Blockchain.Public.Core.DomainTypes
open Org.BouncyCastle.Asn1.Sec
open Org.BouncyCastle.Crypto.Parameters
open Org.BouncyCastle.Crypto.Generators
open Org.BouncyCastle.Security
open Org.BouncyCastle.Math
open Org.BouncyCastle.Crypto.Digests
open Org.BouncyCastle.Crypto.Signers
open Org.BouncyCastle.Math.EC

module Signing =

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Private functions and values used only in this module
    // Highly dependent on bouncycastle library
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    let private curve = SecNamedCurves.GetByName("secp256k1")
    let private domain = ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H)

    let private getCompressionArrayBasedOnRecId recId (x : BigInteger)=
        let xUnsigned = x.ToByteArrayUnsigned()
        let defaultVal = byte 0
        let compressionArraySize = 33

        let compEncoding = Array.create<byte> compressionArraySize defaultVal
        let compressionByteVal = byte(0x02 + (recId % 2))
        compEncoding.[0] <- compressionByteVal

        let copyOffset = compEncoding.Length - xUnsigned.Length
        Buffer.BlockCopy(xUnsigned, 0, compEncoding, copyOffset, xUnsigned.Length)

        compEncoding

    let private calculateQPoint (message : byte[]) (order : BigInteger) (rComponent : BigInteger) (sComponent : BigInteger) (R : ECPoint) = 
        let messageE = BigInteger(1, message)
        let e = BigInteger.Zero.Subtract(messageE).Mod(order)
        let rr = rComponent.ModInverse(order)
        let sor = sComponent.Multiply(rr).Mod(order)
        let eor = e.Multiply(rr).Mod(order)

        let q = curve.G.Multiply(eor).Add(R.Multiply(sor))
        q.Normalize().GetEncoded() |> BigInteger

    let private recoverPublicKeyFromSignature (recId : int) (rComponent : BigInteger) (sComponent : BigInteger) (message : byte[]) =
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
            
            let R =  
                x 
                |> getEncodingArray 
                |> curve.Curve.DecodePoint

            calculateQPoint message order rComponent sComponent R 
            |> Some
    
    let private verify message messageHash signature (publicKey:BigInteger)=
        let ecPoint = domain.Curve.DecodePoint(publicKey.ToByteArray())
        let publicKeyParameters = ECPublicKeyParameters(ecPoint, domain)
        
        let ecdsa = ECDsaSigner()
        ecdsa.Init(false, publicKeyParameters)
        ecdsa.VerifySignature(message, messageHash, signature) 
    
    let rec private getRecordId recordId (publicKey:BigInteger) originalMessage (signature:BigInteger[]) =
        match recordId with
        | recordId when recordId >= 4 || recordId<0 -> -1
        | _ ->
            let rComponent = signature.[0]
            let sComponent = signature.[1]

            let potentialKey = recoverPublicKeyFromSignature recordId rComponent sComponent originalMessage

            let isValidKey = match potentialKey with
                             | None -> false
                             | Some key -> publicKey = key && (verify originalMessage rComponent sComponent key)

            match isValidKey with
            | true -> recordId
            | false -> getRecordId (recordId+1) publicKey originalMessage signature

    let private calculateVComponent publicKey originalMessage signature = 
        getRecordId 0 publicKey originalMessage signature

    let private getBouncyCastleSignatureArray privateKeyInt messageBytes= 
        let digest = Sha256Digest()
        let calc = HMacDsaKCalculator(digest)
        let ecdsa = ECDsaSigner(calc)
         
        let privateParams = ECPrivateKeyParameters(privateKeyInt, domain)
        let rnd = SecureRandom()
        let paramsWithRandom = ParametersWithRandom(privateParams, rnd)

        ecdsa.Init(true, paramsWithRandom)
        ecdsa.GenerateSignature(messageBytes)

    let private calculatePublicKeyAsBytes (privateKey : BigInteger) =
        curve.G.Multiply(privateKey).GetEncoded()

    let private getKeyPair (seed : string option) = 
        let gen = ECKeyPairGenerator()
        let secureRandom = SecureRandom()
        
        match seed with
        | None -> ()
        | Some(seedValue) -> 
            seedValue
            |> Convert.FromBase64String
            |> secureRandom.SetSeed

        let keyGenParams = ECKeyGenerationParameters(domain, secureRandom)
        
        gen.Init(keyGenParams)

        let keyPair=gen.GenerateKeyPair()

        let privateKey=keyPair.Private :?> ECPrivateKeyParameters
        let publicKey=keyPair.Public :?> ECPublicKeyParameters

        (privateKey.D.ToByteArray(), publicKey.Q.GetEncoded())

    let generateRandomBytes byteCount =
        // TODO: Review
        let bytes = Array.zeroCreate byteCount
        use rngCsp = new RNGCryptoServiceProvider()
        rngCsp.GetBytes(bytes) // Fill the array with a random value.
        bytes

    let generateRandomSeed () =
        // TODO: Implement
        let seed = generateRandomBytes 64;
        Convert.ToBase64String(seed)

    let calculatePublicKey (privateKey : byte[]) : byte[] =         
        privateKey
        |> BigInteger
        |> calculatePublicKeyAsBytes

    let generateWalletInfo (seed:string option) : WalletInfo =
        let keyPair = getKeyPair(seed)

        {
            PrivateKey =
                keyPair
                |> fst
            ChainiumAddress = 
                keyPair
                |> snd
                |> Hashing.addressHash 
                |> RawChainiumAddress
        }

    let signMessage (privateKey : byte[]) (message : byte[]) : Signature =
        let privateKeyInt = BigInteger privateKey 
        let signature = getBouncyCastleSignatureArray privateKeyInt message

        let publicKey = 
            privateKeyInt 
            |> calculatePublicKeyAsBytes 
            |> BigInteger

        let vComponent = calculateVComponent publicKey message signature

        {
            V = [| (vComponent |> byte) |]
            R = signature.[0].ToByteArray()
            S = signature.[1].ToByteArray()
        }  

    let verifySignature (signature : Signature) (message : byte[]) : RawChainiumAddress option =
        
        let vComponent = 
            signature.V.[0]  
            |> int      
        
        let rComponent = 
            signature.R
            |> BigInteger

        let sComponent = 
            signature.S
            |> BigInteger

        let publicKey = recoverPublicKeyFromSignature vComponent rComponent sComponent message

        match publicKey with
        | None -> None
        | Some key ->
            let isVerified = verify message rComponent sComponent key

            match isVerified with
            | false -> None
            | true -> 
                key.ToByteArray() 
                |> Hashing.addressHash 
                |> RawChainiumAddress 
                |> Some