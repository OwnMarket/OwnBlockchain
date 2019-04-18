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
                "output member travel bullet track tornado teach kangaroo blue surge modify actor \
                shove valley bronze immense bonus this wise reflect naive behind ensure point"
                "barely rice round clean park either frog thunder certain idea employ cricket \
                empty prosper depth upset sell siren must ice couple game corn title"
                "gas express fall now found crash sock mind purpose flight remind street \
                wear cart apple alone mean enforce body assume much grid metal usual"
                "firm cable silly evidence excite gift daughter wait rail glare stage square \
                enjoy minor opinion broccoli rude rigid toe mushroom parade patrol rebel romance"
                "glass address lesson magic motor liberty monitor essay denial turn engine junior \
                acquire between dish hard shiver skill electric similar hurdle any usual poverty"
                "riot eternal uncover fluid rookie add drum dose hotel limit shift bottom \
                harbor cream virtual depth orient glow mansion van business mansion mom atom"
                "veteran sleep cushion item parent jeans menu romance enough base force road \
                cancel mammal predict mystery toss allow orient soccer main course shock put"
                "pass endorse force share long upon cruel crawl aunt clarify pudding erode \
                stadium fatigue pool fold champion glimpse exact method mirror orchard add vanish"
                "treat west life list lion brush parent palm silver oval enforce carbon \
                pencil grain artefact risk spy exclude main adult stick find grass envelope"
                "bar bracket agree baby perfect radio rather lunch blind cotton original hobby \
                chapter filter wreck degree select cook ensure lounge wool hamster mad blame"
            ]

        let expectedSeeds =
            [
                "6ff9e0add736b505e5315ebf8febe445c7c7508767ff4a119f7f3353334352f5\
                ee84d05b884ae01fd560dee5f8f6b61813567367f1946d14c8accfb7c1ecf98c"
                "0fa915b513a1ce18c0045f8531aed65e675876201bd968bbd6e8227b1c8ae2d3\
                e8377929e5a4e6aa691dddf853ac3ca06d57049ccdc060ce7bbfe26aba305f9a"
                "3a1dede0c331cb2921c45284f9dc0b93af945c4af978455c97e2bfe7b365b09c\
                8a15588a5d8a500e7902c4e223d415b9ea30a8a77055beedf606f9bbf29110af"
                "d4c4ecaccd8e128bccd901759a687c899787612b39f739362d1b4f7315398cf6\
                4bc5850c0471b621809071d6b407d1b93ea836a4c3b7cdedcf84313d38acb300"
                "9004eabdb07455fcdbe5ccd9ba706314c8c8da2a7bc6bf9a1344853fb54d7737\
                11859089c0dfce5a75defc0a704de0e0130fd693e960f23b3542329657320443"
                "dc9c86e147d65aef603bfe1c55e70edbcb21690ce018c9a4ea10a26b8e6f4036\
                595e1107f88b0144e5e05fa18621d1be1c8cada5b7ae12e337e380ad96f311c2"
                "fe9b418f508d43cd1c9de6da1575d2398560d2e02a9610a04925e17607f5f880\
                9474208cbf0c583c38bc50a1a482fcbbb5a230bbd64ea0ae6ebf359af3106df6"
                "a961fef9a55e31b67eecc6dc7b6bde04601eed3f0aa26f95f91f2dc2b8a2b769\
                4052c80a115ee47152db7867782d8ef1e04d63488421a4e125d8a79aa9b8675c"
                "5ec885ab75927ed7a93c52504449fa931182214188b95e824b72e1289a57eb9b\
                c9701c95c26af9b1a6180632a45ec4374caf229bea2669936353987bc704fadf"
                "af1f7388883d5140175cbd4f350dbf207eb74ad4f71cc9e03c604217baf50761\
                191bf4b9dafe42a6db1b68d9faefe8b85f17c0628a1bdc21f8e79b15d1a93f7b"
            ]

        // ACT
        let seeds =
            mnemonicPhrases
            |> List.map HdCrypto.getMnemonicFromString
            |> List.choose (Option.map (fun m ->
                HdCrypto.generateSeedFromMnemonic m "pass"
                )
            )

        // ASSERT
        test <@ seeds = expectedSeeds @>

    [<Fact>]
    let ``HdCrypto.generateWalletsFromSeed from JS`` () =
        // ARRANGE
        let seeds =
            [
                "6ff9e0add736b505e5315ebf8febe445c7c7508767ff4a119f7f3353334352f5\
                ee84d05b884ae01fd560dee5f8f6b61813567367f1946d14c8accfb7c1ecf98c"
                "0fa915b513a1ce18c0045f8531aed65e675876201bd968bbd6e8227b1c8ae2d3\
                e8377929e5a4e6aa691dddf853ac3ca06d57049ccdc060ce7bbfe26aba305f9a"
                "3a1dede0c331cb2921c45284f9dc0b93af945c4af978455c97e2bfe7b365b09c\
                8a15588a5d8a500e7902c4e223d415b9ea30a8a77055beedf606f9bbf29110af"
                "d4c4ecaccd8e128bccd901759a687c899787612b39f739362d1b4f7315398cf6\
                4bc5850c0471b621809071d6b407d1b93ea836a4c3b7cdedcf84313d38acb300"
                "9004eabdb07455fcdbe5ccd9ba706314c8c8da2a7bc6bf9a1344853fb54d7737\
                11859089c0dfce5a75defc0a704de0e0130fd693e960f23b3542329657320443"
                "dc9c86e147d65aef603bfe1c55e70edbcb21690ce018c9a4ea10a26b8e6f4036\
                595e1107f88b0144e5e05fa18621d1be1c8cada5b7ae12e337e380ad96f311c2"
                "fe9b418f508d43cd1c9de6da1575d2398560d2e02a9610a04925e17607f5f880\
                9474208cbf0c583c38bc50a1a482fcbbb5a230bbd64ea0ae6ebf359af3106df6"
                "a961fef9a55e31b67eecc6dc7b6bde04601eed3f0aa26f95f91f2dc2b8a2b769\
                4052c80a115ee47152db7867782d8ef1e04d63488421a4e125d8a79aa9b8675c"
                "5ec885ab75927ed7a93c52504449fa931182214188b95e824b72e1289a57eb9b\
                c9701c95c26af9b1a6180632a45ec4374caf229bea2669936353987bc704fadf"
                "af1f7388883d5140175cbd4f350dbf207eb74ad4f71cc9e03c604217baf50761\
                191bf4b9dafe42a6db1b68d9faefe8b85f17c0628a1bdc21f8e79b15d1a93f7b"
            ]

        let expectedWallets =
            [
                "HU58kMSsmQvPge5vMEMGCgd4fhTqJkY7ToYmPpife8u5", "CHKU3wbL7Q6jnug7eA5pCP7nD66zRBhUL43"
                "FsCXUuE6vTNE1NwdYaMtSn2ZMuoXkDNnibN7HuqnsVwF", "CHY4oAuHVUHdaKNkwB4Vw4Wz1XVy1tZRwn6"
                "B7HwqhiiXesTGxpRFksupu6icX9dNQyqGMcE3R8mTgLL", "CHSkAyZn7MfeMLEtG4Du7WqX9JeeYghob5J"
                "DKCKzypCb45rdp2NcXB8twSgs2BEZ48Zz3vu3GFXbtHv", "CHdX6SYb125dLKn8bGrGV9L5RDYst21aMBo"
                "EhPvgPHhuyRVQAtaTUC3VLJ8v8P75nwXzBFD6TA6gxkD", "CHfhHtzvXr8xL9ZbkHpW2ybzPEgFMBhyvyi"
                "8ewZfkw7qUvTsRBS1TjgKY1UnSe4oTHCrKWjU62eMCTL", "CHXqG1FvoCJwUXkECU2YWxfWktaVpDEZDrQ"
                "6A2STnrjC6dcT5H1bFpQfvNVxxwUuwe2GvvB8VDAfkzn", "CHeCp3pSburKpxJTWYL4uSCi8Cff9AVW2RQ"
                "6fvysUPqwQSvJGiMTpvwG6Gb9LEznGnRpavshUF6Ytt3", "CHbrkULZxAkiMQAaFH6CWdxsw9gtzghma44"
                "D2KmkmpzJonBscMgpPnTevaxDkv2oKbyoVcPKcppSGZ4", "CHXrQaRGvqKXgFqbGTMQ7BG1PyNysXuVh9V"
                "DkimrsSo4iNzqwmBraPEa3T4eBihoorWzUxheXxVZiLg", "CHdCo4GT5HALRcPVTyQZgqYsQz2uxqJq5Ms"
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
