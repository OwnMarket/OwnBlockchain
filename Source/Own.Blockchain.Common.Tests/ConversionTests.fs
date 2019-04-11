namespace Own.Blockchain.Common.Tests

open Xunit
open Swensen.Unquote
open Own.Blockchain.Common

module ConversionTests =

    [<Fact>]
    let ``Conversion.decimalToBytes converts zero consistently`` () =
        let expected = Array.zeroCreate 16

        [
            0m
            0.0000000m
            +0m
            +0.0000000m
            -0m
            -0.0000000m
            1m - 1m
            1.0000000m - 1m
        ]
        |> List.iteri (fun i z ->
            test <@ (i, Conversion.decimalToBytes z) = (i, expected) @>
        )

    [<Fact>]
    let ``Conversion.decimalToBytes converts positive number consistently`` () =
        let expected = Array.zeroCreate 16
        expected.[3] <- 1uy

        [
            1m
            1.0000000m
            +1m
            +1.0000000m
            0m + 1m
            0m + 1.0000000m
            1m + 0m
            1m + 0.0000000m
            1.0000000m + 0m
            1.0000000m + 0.0000000m
            1m - 0m
            1m - 0.0000000m
            1.0000000m - 0m
            1.0000000m - 0.0000000m
            2m - 1m
            2m - 1.0000000m
            2.0000000m - 1m
            2.0000000m - 1.0000000m
        ]
        |> List.iteri (fun i z ->
            test <@ (i, Conversion.decimalToBytes z) = (i, expected) @>
        )

    [<Fact>]
    let ``Conversion.decimalToBytes converts negative number consistently`` () =
        let expected = Array.zeroCreate 16
        expected.[3] <- 1uy
        expected.[12] <- 128uy

        [
            -1m
            -1.0000000m
            0m - 1m
            0m - 1.0000000m
            -1m + 0m
            -1m + 0.0000000m
            -1.0000000m + 0m
            -1.0000000m + 0.0000000m
            -1m - 0m
            -1m - 0.0000000m
            -1.0000000m - 0m
            -1.0000000m - 0.0000000m
            -2m + 1m
            -2m + 1.0000000m
            -2.0000000m + 1m
            -2.0000000m + 1.0000000m
        ]
        |> List.iteri (fun i z ->
            test <@ (i, Conversion.decimalToBytes z) = (i, expected) @>
        )
