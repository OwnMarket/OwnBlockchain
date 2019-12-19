namespace Own.Blockchain.Common.Tests

open Xunit
open Swensen.Unquote
open Own.Common.FSharp

module CommonTests =

    [<Fact>]
    let ``Common.retry executes specified number of times`` () =
        // ARRANGE
        let timesToTry = 3
        let mutable counter = 0

        let act () =
            retry timesToTry <| fun iteration ->
                counter <- counter + 1
                failwithf "Failed %i" iteration
            |> ignore

        // ACT
        raisesWith<exn>
            <@ act () @>
            (fun ex -> <@ ex.Message = sprintf "Failed %i" timesToTry @>)

        // ASSERT
        test <@ counter = timesToTry @>

    [<Fact>]
    let ``Common.retry returns on first successful execution`` () =
        // ARRANGE
        let timesToTry = 3
        let successfulIteration = 2
        let mutable counter = 0

        // ACT
        let actual =
            retry timesToTry <| fun iteration ->
                counter <- counter + 1
                if iteration <> successfulIteration then
                    failwithf "Failed %i" iteration
                iteration

        // ASSERT
        test <@ counter = successfulIteration @>
        test <@ actual = successfulIteration @>

    [<Fact>]
    let ``Common.retryWhile executes while condition is true`` () =
        // ARRANGE
        let expectedResult = "XXXXXXXXXX"
        let mutable counter = 0
        let mutable accumulator = ""

        let act () =
            retryWhile (fun _ -> accumulator <> expectedResult) <| fun iteration ->
                counter <- counter + 1
                accumulator <- accumulator + "X"
                failwithf "Failed with %s" accumulator
            |> ignore

        // ACT
        raisesWith<exn>
            <@ act () @>
            (fun ex -> <@ ex.Message = sprintf "Failed with %s" expectedResult @>)

        // ASSERT
        test <@ counter = expectedResult.Length @>
        test <@ accumulator = expectedResult @>
