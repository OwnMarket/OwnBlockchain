namespace Chainium.Blockchain.Public.Crypto

open System
open System.IO
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
open Org.BouncyCastle.Asn1.X9

module Signing =
    open Org.BouncyCastle.Asn1

     //TODO: decide on encoding,for now assume it is bas64
    type String with
        member this.toBytes() = Convert.FromBase64String(this)
    
    let toString (bytes:byte[]) = Convert.ToBase64String(bytes)


    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Private functions and values used only in this module
    // Highly dependent on bouncycastle library
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    let private curve = SecNamedCurves.GetByName("secp256k1")
    let private domain = ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H)

    let private compressionArraySize = 33

    let private getCompressionArrayBasedOnRecId recId (x : BigInteger)=
        let xUnsigned = x.ToByteArrayUnsigned()
        let defaultVal = byte 0
        
        let compEncoding = Array.create<byte> compressionArraySize defaultVal
        let compressionByteVal = byte(0x02 + (recId % 2))
        compEncoding.[0] <- compressionByteVal

        let copyOffset = compEncoding.Length - xUnsigned.Length
        Buffer.BlockCopy(xUnsigned, 0, compEncoding, copyOffset, xUnsigned.Length)

        compEncoding

    let private calculateQPoint (message : byte[]) (order : BigInteger) (rComponent : BigInteger) (sComponent : BigInteger) (R : ECPoint)= 
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

    let private getAsBigInteger (str:string)=
        str.toBytes()
        |> BigInteger
    
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

    let private toDER (vComponent:int) (signature:BigInteger[]) = 
        let bos = new MemoryStream()
        let seq = new DerSequenceGenerator(bos)
        seq.AddObject(DerInteger(vComponent))
        seq.AddObject(DerInteger(signature.[0]))
        seq.AddObject(DerInteger(signature.[1]))
        seq.Close()
            
        bos.ToArray()
    
    let private fromDER (signatureBytes:byte[]) =
        let decoder = new Asn1InputStream(signatureBytes)
        let seq = decoder.ReadObject() :?> DerSequence

        let seqItem iter = (seq.[iter] :?> DerInteger).Value

        let vComponent = (seqItem 0).IntValue
        let signature = 
            [|
                seqItem 1;
                seqItem 2
            |]
        
        (vComponent,signature)

    let private getBouncyCastleSignatureArray privateKeyInt messageBytes= 
        let digest = Sha256Digest()
        let calc = HMacDsaKCalculator(digest)
        let ecdsa = ECDsaSigner(calc)
         
        let privateParams = ECPrivateKeyParameters(privateKeyInt, domain)
        let rnd = SecureRandom()
        let paramsWithRandom = ParametersWithRandom(privateParams, rnd)

        ecdsa.Init(true, paramsWithRandom)
        ecdsa.GenerateSignature(messageBytes)

    let privateKeyString (PrivateKey e) = e

    let private calculatePublicKeyBytes (privateKey : PrivateKey) =
        let keyAsBigInt = 
            privateKey 
            |> privateKeyString 
            |> getAsBigInteger
        
        curve.G.Multiply(keyAsBigInt).GetEncoded()

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
    
    let private calculatePublicKeyBigInt (privateKey : PrivateKey) = 
        privateKey 
        |> calculatePublicKeyBytes 
        |> BigInteger

    let private getKeyPair (seed : string option) = 
        let gen = new ECKeyPairGenerator()
        let secureRandom = SecureRandom()
        
        match seed with
        | None -> ()
        | Some(seedValue) -> 
            seedValue.toBytes()
            |> secureRandom.SetSeed

        let keyGenParams = ECKeyGenerationParameters(domain, secureRandom)
        
        gen.Init(keyGenParams)

        let keyPair=gen.GenerateKeyPair()

        let privateKey=keyPair.Private :?> ECPrivateKeyParameters
        let publicKey=keyPair.Public :?> ECPublicKeyParameters

        (privateKey.D.ToByteArray(), publicKey.Q.GetEncoded())

    let calculatePublicKey (privateKey : PrivateKey) : PublicKey = 
        privateKey 
        |> calculatePublicKeyBytes 
        |> toString 
        |> PublicKey

    let calculateAddress (publicKey : PublicKey) : ChainiumAddress =
        // TODO: Implement
        ChainiumAddress "ch1234567890"

    let generateWalletInfo (seed:string option) : WalletInfo =
        let keyPair = getKeyPair(seed)

        {
            PrivateKey=keyPair
            |> fst
            |> toString 
            |> PrivateKey;
            ChainiumAddress = keyPair
            |> snd
            |> Hashing.addressHash 
            |> toString
            |> ChainiumAddress
        }
    
    let convertMessageToBytes (str:String) = System.Text.Encoding.UTF8.GetBytes(str)

    let signMessage (privateKey : PrivateKey) (message : string) : Signature =
        let privateKeyInt = 
            privateKey 
            |> privateKeyString 
            |> getAsBigInteger

        let messageBytes = convertMessageToBytes message
        let signature = getBouncyCastleSignatureArray privateKeyInt messageBytes

        let publicKey = calculatePublicKeyBigInt privateKey
        let vComponent = calculateVComponent publicKey messageBytes signature

        toDER vComponent signature 
        |> toString 
        |> Signature

    let signatureAsString (Signature s) = s    

    let verifySignature (signature : Signature) (message : string) : ChainiumAddress option =
        let signatureBytes = (signature |> signatureAsString).toBytes()
        let signatureData = fromDER signatureBytes

        let vComponent = fst signatureData
        let signatureInfo = snd signatureData 
        let rComponent = signatureInfo.[0]
        let sComponent = signatureInfo.[1]

        let messageAsBytes = convertMessageToBytes message
        let publicKey = recoverPublicKeyFromSignature vComponent rComponent sComponent messageAsBytes

        match publicKey with
        | None -> None
        | Some key ->
            let isVerified = verify messageAsBytes rComponent sComponent key

            match isVerified with
            | false -> None
            | true -> 
                key.ToByteArray() 
                |> Hashing.addressHash 
                |> toString 
                |> ChainiumAddress 
                |> Some