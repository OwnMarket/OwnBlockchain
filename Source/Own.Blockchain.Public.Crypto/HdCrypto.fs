namespace Own.Blockchain.Public.Crypto

open NBitcoin
open NBitcoin.DataEncoders
open Own.Blockchain.Public.Core.DomainTypes

module HdCrypto =

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // BIP39
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let getMnemonicFromString (mnemonicPhrase : string) =
        try
            new Mnemonic(mnemonicPhrase, Wordlist.English)
            |> Some
        with
        | _ -> None

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

    let recoverMasterExtKeyFromSeed bip39Seed =
        try
            bip39Seed
            |> Encoders.Hex.DecodeData
            |> ExtKey
            |> Some
        with
        | _ -> None

    let generateMasterExtKeyWithWordcount (wordCount : WordCount) passphrase =
        let mnemonic = generateMnemonic wordCount
        generateMasterExtKeyFromMnemonic mnemonic passphrase

    let generateMasterExtKey passphrase =
        generateMasterExtKeyWithWordcount WordCount.TwentyFour passphrase

    let toPrivateKey (extKey : ExtKey) =
        extKey.PrivateKey.ToBytes()
        |> Hashing.encode
        |> PrivateKey

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // BIP44
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let bip44RegistrationIndex = 25718

    let generateWallet bip39Seed (walletIndex : uint32) =
        let privateKey =
            try
                match recoverMasterExtKeyFromSeed bip39Seed with
                | Some masterExtKey ->
                    sprintf "m/44'/%i'/0'/0/%i" bip44RegistrationIndex walletIndex
                    |> KeyPath.Parse
                    |> masterExtKey.Derive
                    |> toPrivateKey
                | _ ->
                    failwith "Error generating wallet, invalid seed."
            with
            | e -> failwithf "Error generating wallet, %s" e.Message

        {
            PrivateKey = privateKey
            Address =
                privateKey
                |> Signing.addressFromPrivateKey
        }

    let restoreWalletsFromSeed bip39Seed startIndex walletCount =
        let generate = generateWallet bip39Seed
        [startIndex .. (startIndex + walletCount - 1)] |> List.map (uint32 >> generate)
