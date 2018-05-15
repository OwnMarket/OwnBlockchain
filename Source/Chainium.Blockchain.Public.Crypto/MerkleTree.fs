namespace Chainium.Blockchain.Public.Crypto

module MerkleTree =
    
    type MerkleNode ={
        Hash : byte[]
        Left : MerkleNode option
        Right : MerkleNode option
    }
    
    let rec build 
        nodeHashingRule 
        (treeNodes : MerkleNode option list)  
        =
        let treeBuilder nodes = 
            build nodeHashingRule nodes

        let buildNode left right = 
            {
                Hash = nodeHashingRule left right
                Left = left
                Right = right
            }
            |> Some
        
        let buildSubTree subTree =
            match subTree with
            | [] -> None
            | [ _ ] -> 
                buildNode subTree.Head None
            | [ _; _] -> 
                buildNode 
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