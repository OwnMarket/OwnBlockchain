namespace Chainium.Blockchain.Public.Crypto.Tests

open System
open Xunit
open Swensen.Unquote
open Chainium.Common
open Chainium.Blockchain.Public.Crypto
open Chainium.Blockchain.Public.Core.DomainTypes


module SigningTests =
    open System.Text

    [<Fact>]
    let ``Signing.generateRandomSeed generates an array of 64 bytes`` () =
        let seed = Signing.generateRandomSeed ()
        let bytes = Convert.FromBase64String(seed)
        
        test <@ bytes.Length = 64 @>

    [<Fact>]
    let ``Signing.generateRandomSeed always returns a different value`` () =
        let allSeeds =
            [1 .. 10000]
            |> List.map (fun _ -> Signing.generateRandomSeed ())
    
        let distinctSeeds =
            allSeeds
            |> List.distinct
        
        test <@ distinctSeeds.Length = allSeeds.Length @>
    
    [<Fact>]
    let ``Signing.generateWalletInfo using seed`` () =
        let seed = 
            Signing.generateRandomSeed () 
        
        let numOfReps = 1000

        let distinctPairs = 
            [1..numOfReps]
            |> List.map (fun _ -> Some(seed) |> Signing.generateWalletInfo)
            |> List.distinct

        test <@ distinctPairs.Length = numOfReps @>

    [<Fact>]
    let ``Signing.generateWalletInfo without using seed`` () =        
        let numOfReps = 1000

        let walletInfoPairs = 
            [1..numOfReps]
            |> List.map (fun _ -> Signing.generateWalletInfo None)
            |> List.distinct

        test <@ walletInfoPairs.Length = numOfReps @>

    [<Fact>]
    let ``Signing.signMessage same message for multiple users`` () =
        let numOfReps = 100
        let messageToSign = Encoding.UTF8.GetBytes "Chainium"

        let generateSignature () =
            let wallet = Signing.generateWalletInfo None
            Signing.signMessage wallet.PrivateKey messageToSign

            

        let distinctMessages = 
            [1..numOfReps]
            |> List.map (fun _ -> generateSignature ())
            |> List.distinct
         
        test <@ distinctMessages.Length = numOfReps @>

    [<Fact>]
    let ``Signing.verifyMessage sign, verify message and check if resulting adress is same`` () =
        let messageToSign = Encoding.UTF8.GetBytes "Chainium"
        let wallet = Signing.generateWalletInfo None

        let signature = Signing.signMessage wallet.PrivateKey messageToSign
        let address = Signing.verifySignature signature messageToSign
         
        test <@ address <> None @>
        test <@ address.Value = wallet.ChainiumAddress @>
      

    [<Fact>]
    let ``Signing.verifyMessage sign, verify mutiple messages and check if resulting adress is same`` () =
        let messagePrefix = "Chainium"
        
        let wallet = Signing.generateWalletInfo None

        let signAndVerify message=
            let signature = Signing.signMessage wallet.PrivateKey message
            let address = Signing.verifySignature signature message
         
            test <@ address <> None @>
            test <@ address.Value = wallet.ChainiumAddress @>

        [1..100]
        |> List.map(fun i -> sprintf "%s %i" messagePrefix i |> Encoding.UTF8.GetBytes |> signAndVerify)
