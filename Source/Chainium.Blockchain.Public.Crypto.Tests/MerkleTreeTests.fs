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
    let ``MerkleTree.build - check empty list creation`` () =            
            let root = MerkleTree.build hashFunc []
            test <@ root.Length = 0 @>
                
    [<Fact>]
    let ``MerkleTree.calculateProof - get proof`` () =
        let leafs = 
                [
                    for i in 1 .. 4 do yield [| byte(i) |]
                ]    
        let record = [| byte(2) |]
        let expected = 
            [
                LeftHash [| byte(1) |]
                RightHash [| byte(3); byte(4) |]
            ] 
        
        let actual = MerkleTree.calculateProof hashFunc leafs record

        test <@ expected = actual @>
    
    [<Fact>]
    let ``MerkleTree.calculateProof - uneven nodes get proof`` () =
        let leafs = 
                [
                    for i in 1 .. 5 do yield [| byte(i) |]
                ]    
        let record = [| byte(1) |]
        let expected = 
            [
                RightHash [| byte(2) |]
                RightHash [| byte(3) |]
                RightHash [| byte(4); byte(5) |]
            ] 
        
        let actual = MerkleTree.calculateProof hashFunc leafs record

        test <@ expected = actual @>

    [<Fact>]
    let ``MerkleTree.calculateProof - non existing leaf hash`` () =
        let leafs = 
                [
                    for i in 1 .. 4 do yield [| byte(i) |]
                ]    
        let record = [| byte(0) |]
        
        let expected = List.Empty
        let actual = MerkleTree.calculateProof hashFunc leafs record

        test <@ expected = actual @>
                            
    [<Fact>]
    let ``MerkleTree.verifyProof - leaf hash`` () =
        let root = [| for i in 1 .. 4 do yield byte(i) |]
        let record = [| byte(2) |]
        let proof = 
            [
                LeftHash [| byte(1) |]
                RightHash [| byte(3); byte(4) |]
            ]
        
        let isVerified = MerkleTree.verifyProof hashFunc record proof root

        test <@ isVerified @>

    [<Fact>]
    let ``MerkleTree.verifyProof - invalid leaf hash`` () =
        let root = [| for i in 1 .. 4 do yield byte(i) |]
        let record = [| byte(5) |]
        let proof = 
            [
                LeftHash [| byte(1) |]
                RightHash [| byte(3); byte(4) |]
            ]
        
        let isVerified = MerkleTree.verifyProof hashFunc record proof root

        test <@ isVerified = false @>