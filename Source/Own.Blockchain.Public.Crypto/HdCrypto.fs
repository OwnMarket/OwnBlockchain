namespace Own.Blockchain.Public.Crypto

open NBitcoin
open NBitcoin.DataEncoders
open Own.Blockchain.Public.Core.DomainTypes

module HdCrypto =

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // BIP39
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let private getMnemonicFromString (mnemonicPhrase : string) =
        try
            new Mnemonic(mnemonicPhrase, Wordlist.English)
            |> Some
        with
        | e ->
            failwith e.Message

    let private generateMasterExtKeyFromMnemonic (mnemonic : Mnemonic) passphrase =
        mnemonic.DeriveExtKey(passphrase)

    let generateMnemonic (wordCount : WordCount) =
        new Mnemonic(Wordlist.English, wordCount)

    let generateSeedFromMnemonic (mnemonic : Mnemonic) (passphrase : string) =
        mnemonic.DeriveSeed passphrase
        |> Encoders.Hex.EncodeData

    let recoverMasterExtKeyFromMnemonic (mnemonicPhrase : string) passphrase =
        getMnemonicFromString mnemonicPhrase |> Option.map (fun mnemonic ->
            generateMasterExtKeyFromMnemonic mnemonic passphrase
        )

    let recoverMasterExtKeyFromSeed seed =
        try
            seed
            |> Encoders.Hex.DecodeData
            |> ExtKey
            |> Some
        with
        | _ ->
            failwith "Invalid seed"

    let generateMasterExtKeyWithWordcount (wordCount : WordCount) passphrase =
        let mnemonic = generateMnemonic wordCount
        generateMasterExtKeyFromMnemonic mnemonic passphrase

    let generateMasterExtKey passphrase =
        generateMasterExtKeyWithWordcount WordCount.Eighteen passphrase

    let getMasterPrivateKey (masterExtKey : ExtKey) =
        masterExtKey.PrivateKey.ToBytes()
        |> Hashing.encode
        |> PrivateKey

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // BIP44
    ////////////////////////////////////////////////////////////////////////////////////////////////////
