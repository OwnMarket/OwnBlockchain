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

    [<Fact>]
    let ``HdCrypto.generateSeedFromMnemonic from JS`` () =
        // ARRANGE
        let mnemonicPhrases =
            [
                "inside employ rabbit mansion lobster tip effort travel lab time fat country \
                purse step submit speed mimic decade odor program picnic frozen brand vessel"
                "cement alert squeeze lottery rigid exhibit code topple ten gesture snow waste \
                fetch engine torch develop about gauge guilt marble miss cactus grab head"
                "lazy waste develop core call cluster provide frequent industry hamster repair tell \
                blanket acoustic direct guard spread equip vanish layer arctic velvet now elder"
                "vendor note left whip plastic session bubble raise tongue gap capital slush \
                bag car merge lady example just ozone sorry narrow expect alone hole"
                "option patient asset truly edit manage knock volume crack hole pitch crater \
                ritual slam deliver wisdom shy welcome cloth tiger tell grid list midnight"
                "cement black hero bird pipe busy maze mixed lunch assume involve phone \
                shop receive own gaze tree keep cave glue crew network motor jealous"
                "scare increase blossom glimpse tree innocent ramp jelly crunch ready merge rally \
                letter lunch case trick release exclude judge pig script rain card decline"
                "taste sun know enter congress village beyond bench region armed neglect vanish \
                collect horror pilot tired cram play few uncle birth abandon grass design"
                "genre joy nest captain emerge again spatial jungle image toss fox next fork \
                ball kit pioneer captain slice clump spot agree borrow mistake start"
                "erosion bubble today note draw ensure dance warfare vessel coral lyrics height bless \
                knife romance chuckle pigeon brass flush script warm reform tilt antenna"
            ]

        let expectedSeeds =
            [
                "739196ef8d7cb1265a1f6ce72e35150098c467ad7d582fa71580c33fccc566e3\
                ed8486000cadef5e1f29dce602d641f4ab755bee9858ee445e2423e93a1cce47"
                "3c17989a0de098bc7960fa5a877e068fb4fe0ed748827cfa843be4139f424f05\
                341381a6476bd5cd8eba9ce343c847ac376b47c9f755ba1c11f3eb3a9cb6bbff"
                "665ddf5c5f9d6450da3e4200d74825c87358412673a2f62a2d85066ee9befeba\
                b748c85718b157289cc4af23d6f298a7ebed5c55a7b87993cae4bf1e4aa6cabc"
                "9b1ff4f5bc0d23a3bbf14a43266fdafaaed7b3d76f6d51abf1d5f897c45f25ef\
                5c546c948a8bb2c0b0ce912333a196649c1f265be4b1a43e41b5c3d10daa8826"
                "06c871ba200cc2efdda03b9719187fffffe5272826d63f709479368f6c07279b\
                28922ed25afb5681c8e6974a81a7f6bd8f78c74565f9b74b4b2a4cea6184de08"
                "2d0418e21ad4f0a477541ede3717fc537bd9f9c0ccbb182d6c84b6afb0a950c8\
                d31832c3fc9a950db5669ce61e14e720647ae55ca43b80a759625991876d15b1"
                "e373ebfa9790fc3744e4e8edf382990ddaed5ef5674bf0aec941b2ac33ef617c\
                ba7a9cc822b7453ef8b1d0145257d52466293a5f6c3793e0891cc5b5aa922d24"
                "6864ba7bee5c9238060d807b0963afab39f6d1f743a75e1dcbe50d3b477119d3\
                4083f3515faf60f4cf3bdc6cc3a91ce5338da7a5d3b28b8cfbc27efd9dbd7e17"
                "2d084d2b396caad05dfd0e53567b04f934ded0a014894ba9e00a2ef61cc58b28\
                030cf987eef355056d5077618d4c65d3539686f048254bb8f22fcf012a6b5928"
                "cc068963d6a5ab539f95c6075554c17c06be4e676cee60a40fd0de4fa206a0b2\
                937336a8c0a6b22ba41c4219192c701848b17fcdfa686fafe156340b1a22626d"
            ]

        // ACT
        let seeds =
            mnemonicPhrases
            |> List.map HdCrypto.getMnemonicFromString
            |> List.choose (fun mnemonic ->
                mnemonic |> Option.map (fun m -> HdCrypto.generateSeedFromMnemonic m "pass")
            )

        // ASSERT
        test <@ seeds = expectedSeeds @>

    [<Fact>]
    let ``HdCrypto.generateWalletsFromSeed from JS`` () =
        // ARRANGE
        let seeds =
            [
                "739196ef8d7cb1265a1f6ce72e35150098c467ad7d582fa71580c33fccc566e3\
                ed8486000cadef5e1f29dce602d641f4ab755bee9858ee445e2423e93a1cce47"
                "3c17989a0de098bc7960fa5a877e068fb4fe0ed748827cfa843be4139f424f05\
                341381a6476bd5cd8eba9ce343c847ac376b47c9f755ba1c11f3eb3a9cb6bbff"
                "665ddf5c5f9d6450da3e4200d74825c87358412673a2f62a2d85066ee9befeba\
                b748c85718b157289cc4af23d6f298a7ebed5c55a7b87993cae4bf1e4aa6cabc"
                "9b1ff4f5bc0d23a3bbf14a43266fdafaaed7b3d76f6d51abf1d5f897c45f25ef\
                5c546c948a8bb2c0b0ce912333a196649c1f265be4b1a43e41b5c3d10daa8826"
                "06c871ba200cc2efdda03b9719187fffffe5272826d63f709479368f6c07279b\
                28922ed25afb5681c8e6974a81a7f6bd8f78c74565f9b74b4b2a4cea6184de08"
                "2d0418e21ad4f0a477541ede3717fc537bd9f9c0ccbb182d6c84b6afb0a950c8\
                d31832c3fc9a950db5669ce61e14e720647ae55ca43b80a759625991876d15b1"
                "e373ebfa9790fc3744e4e8edf382990ddaed5ef5674bf0aec941b2ac33ef617c\
                ba7a9cc822b7453ef8b1d0145257d52466293a5f6c3793e0891cc5b5aa922d24"
                "6864ba7bee5c9238060d807b0963afab39f6d1f743a75e1dcbe50d3b477119d3\
                4083f3515faf60f4cf3bdc6cc3a91ce5338da7a5d3b28b8cfbc27efd9dbd7e17"
                "2d084d2b396caad05dfd0e53567b04f934ded0a014894ba9e00a2ef61cc58b28\
                030cf987eef355056d5077618d4c65d3539686f048254bb8f22fcf012a6b5928"
                "cc068963d6a5ab539f95c6075554c17c06be4e676cee60a40fd0de4fa206a0b2\
                937336a8c0a6b22ba41c4219192c701848b17fcdfa686fafe156340b1a22626d"
            ]

        let expectedWallets =
            [
                "Gvf4Q3aRfMYyBXc5tDzUGzrRGX9qFbz2mWU8zL6oybmm", "CHLuDG2Ns9sh9j2oaQY43RS9Q8SmupEsj6a"
                "6oBWM23RPN1RjKqDvR5iuVdRFWftvZ8Wdc33woZSDNmU", "CHHQKsWMMcQqoY7wbiNixGX2nGrYBjRcJvC"
                "CHTX5DZe4rBGdrfqSFfAAi2EsSiC4tA69Tp9yQUmSjtV", "CHequmAGf9zAntqNBYE7r2vTg6LHBzcbrF8"
                "9jVLMAKaRQpvNTW5jYXJ7g23EujcvGty5ZNwSBij8duc", "CHeGvVGYJ765MHpM895keTTnPpPpX3j2iMD"
                "ErNNmipa6ShFcLJ8AijJt9fKi5H6EhNE8Ho1f8itHUWk", "CHQNhM5d3z7rWJ6GzFiWWGvWMCMv8K15FVs"
                "DKTfd741eci21ekBqsonawXQxR4hUbojvVDf71w8TKPw", "CHQxg37nYtuQnvTmDk4rhu4WzWjyRxA86mc"
                "FQPUTMUeuVpibEvnmpv4VEYMsFxQ3mq1ztAszP9cDG13", "CHY9rM4GLeQMiqfXXbY87mhnYUFK4vZQht5"
                "8vqQph86cPBfd9zBShvki6sVgiUJJRJ5SuyG3RHNc5sa", "CHbBf5jhECUhdGhLmY3y6JJd9giYKJWmWMF"
                "CanHzVrWDjw2gQFu8jonjUBUDbMBB2jZNGCK47ZvfXsv", "CHWbPzgC1ioUAFWJb9RpMEYgPp5g5vvNemZ"
                "G31HhKDn2k1Va9a2ugtVUaVBfhzxdvTpf1GgzaEHTXWd", "CHbiHEVq7Tc3ggx3JuA138LjwzoowLN2yct"
            ]

        // ACT
        let wallets =
            seeds
            |> List.map (fun seed ->
                HdCrypto.generateWallet seed 1u
                |> fun wallet -> wallet.PrivateKey.Value, wallet.Address.Value
            )

        // ASSERT
        test <@ wallets = expectedWallets @>
