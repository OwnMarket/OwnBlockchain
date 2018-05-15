namespace Chainium.Blockchain.Public.Crypto

module MerkleTree =
    
    type private MerkleNode =
        {
            Hash : byte[]
            Left : MerkleNode option
            Right : MerkleNode option
        }
    
    let private nodehash x =
        match x with
        | Some l -> l.Hash
        | None -> Array.zeroCreate 0

    let private buildNode
        hashFunction
        (left : MerkleNode option) 
        (right : MerkleNode option)
        =
        let lefthash = nodehash left
        let righthash = nodehash right

        {
            Left = left
            Right = right
            Hash =  
                righthash
                |> Array.append lefthash
                |> hashFunction
        }
        |> Some

    
    let rec private buildTree 
        hashFunction 
        (treeNodes : MerkleNode option list)  
        =
        let treeBuilder nodes = 
            buildTree hashFunction nodes

        let nodeBuilder left right = 
            buildNode 
                hashFunction 
                left 
                right
        
        let buildSubTree subTree =
            match subTree with
            | [] -> None
            
            | [ _ ] -> 
                nodeBuilder 
                    subTree.Head 
                    None

            | [ _; _ ] -> 
                nodeBuilder
                    subTree.[0] 
                    subTree.[1]

            | _ -> treeBuilder subTree
            
            
        match treeNodes.Length with
        | 0 | 1 | 2 -> buildSubTree treeNodes
        | _ -> 
            let subTrees = 
                treeNodes
                |> List.splitInto 2
                
            subTrees
            |> List.map(fun s -> buildSubTree s)
            |> treeBuilder



    let build hashFunc leafHashes =  
        leafHashes
        |> List.map
            (
                fun h -> 
                    {
                        Hash = h
                        Left = None
                        Right = None
                    } 
                    |> Some
            )
        |> buildTree hashFunc
        |> nodehash