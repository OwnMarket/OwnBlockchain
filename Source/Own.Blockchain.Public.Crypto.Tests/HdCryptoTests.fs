namespace Own.Blockchain.Public.Crypto.Tests

open Xunit
open Swensen.Unquote
open NBitcoin
open Own.Blockchain.Public.Crypto
open Own.Blockchain.Public.Core.DomainTypes

module HdCryptoTests =

    let passphrase = "my password"
    let mnemonicPhrase = "uphold together jar echo aspect dolphin history fruit carry client strong style"

    [<Fact>]
    let ``HdCrypto.recoverMasterExtKeyFromMnemonic`` () =
        // ARRANGE
        let expectedPrivateKey = "GA1HamBfo4fiBVL2LCHQn3i3tLh3tu4G25NFHFwt8ktQ" |> PrivateKey |> Some

        // ACT
        let privateKey =
            HdCrypto.recoverMasterExtKeyFromMnemonic mnemonicPhrase passphrase
            |> Option.map HdCrypto.toPrivateKey

        let privateKeyWithWrongPassphrase =
            HdCrypto.recoverMasterExtKeyFromMnemonic mnemonicPhrase "wrong password"
            |> Option.map HdCrypto.toPrivateKey

        // ASSERT
        test <@ privateKey = expectedPrivateKey @>
        test <@ privateKeyWithWrongPassphrase <> expectedPrivateKey @>

    [<Fact>]
    let ``HdCrypto.recoverMasterExtKeyFromSeed`` () =
        // ARRANGE
        let mnemonic = HdCrypto.generateMnemonic WordCount.Eighteen
        let seed = HdCrypto.generateSeedFromMnemonic mnemonic passphrase

        // ACT
        let masterPrivateKey =
            HdCrypto.recoverMasterExtKeyFromMnemonic (mnemonic.ToString()) passphrase
            |> Option.map HdCrypto.toPrivateKey

        let masterPrivateKeyFromSeed =
            HdCrypto.recoverMasterExtKeyFromSeed seed
            |> Option.map HdCrypto.toPrivateKey

        // ASSERT
        test <@ masterPrivateKey = masterPrivateKeyFromSeed @>

    [<Fact>]
    let ``HdCrypto.generateWallet and restoreWallet`` () =
        // ARRANGE
        let mnemonic = HdCrypto.generateMnemonic WordCount.Eighteen
        let seed = HdCrypto.generateSeedFromMnemonic mnemonic passphrase

        // ACT
        let wallet0 = HdCrypto.generateWallet seed 3u
        let restoredWallets = HdCrypto.restoreWalletsFromSeed seed 3 1

        // ASSERT
        test <@ wallet0 = restoredWallets.Head @>
