namespace Chainium.Blockchain.Public.Crypto.Tests

open System
open Xunit
open Swensen.Unquote
open Chainium.Common
open Chainium.Blockchain.Public.Crypto

module SigningTests =

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
