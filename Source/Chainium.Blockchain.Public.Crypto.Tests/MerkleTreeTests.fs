namespace Chainium.Blockchain.Public.Crypto.Tests

open Xunit
open Swensen.Unquote
open Chainium.Blockchain.Public.Crypto.MerkleTree


module MerkleTreeTests =
    open Chainium.Blockchain.Public.Crypto

    let hashFunc h = h


    [<Fact>]
    let ``MerkleTree.build - check if correct number of nodes has been added`` () =
            let leafs =
                [
                    for i in 1 .. 8 -> [| byte(i) |]
                ]

            let root = MerkleTree.build hashFunc leafs

            test <@ leafs.Length = root.Length @>

    [<Fact>]
    let ``MerkleTree.build - check for empty list`` ()=
            let root = MerkleTree.build hashFunc []
            test <@ root.Length = 0 @>
