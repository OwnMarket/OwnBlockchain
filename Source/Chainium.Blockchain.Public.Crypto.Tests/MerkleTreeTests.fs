namespace Chainium.Blockchain.Public.Crypto.Tests

open Xunit
open Swensen.Unquote
open Chainium.Blockchain.Public.Crypto.MerkleTree


module MerkleTreeTests =
    open Chainium.Blockchain.Public.Crypto

    let hashingRule
        (left : MerkleNode option) 
        (right : MerkleNode option)
        =
        let nodehash x =
            match x with
            | Some l -> l.Hash
            | None -> Array.zeroCreate 0

        let lefthash = nodehash left
        let righthash = nodehash right
        
        righthash
        |> Array.append lefthash


    [<Fact>]
    let ``MerkleTree.build - check if correct number of nodes has been added`` () =
            let nodes = 
                [for i in 1..8 do yield 
                                        {
                                            Hash = [|byte(i)|]
                                            Left = None
                                            Right = None
                                        } 
                                        |> Some
                ]    
            
            let root = MerkleTree.build hashingRule nodes

            test <@ nodes.Length = root.Value.Hash.Length @>

    [<Fact>]
    let ``MerkleTree.build - check for empty list`` ()=            
            let root = MerkleTree.build hashingRule []
            test <@ root = None @>
                
                
                            

