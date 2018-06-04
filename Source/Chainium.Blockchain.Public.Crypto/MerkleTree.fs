namespace Chainium.Blockchain.Public.Crypto

module MerkleTree =

    type private MerkleNode =
        {
            mutable Parent : MerkleNode option
            Hash : byte[]
            Left : MerkleNode option
            Right : MerkleNode option
        }

    type MerkleProofSegment =
        | LeftHash of byte[]
        | RightHash of byte[]

    type MerkleProof = MerkleProofSegment list

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Build
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let private nodehash x =
        match x with
        | Some l -> l.Hash
        | None -> Array.zeroCreate 0

    let private concatHashes
        left
        right
        =
        Array.append left right

    let private buildNode
        hashFunction
        (left : MerkleNode option)
        (right : MerkleNode option)
        =

        let lefthash = nodehash left
        let righthash = nodehash right

        let setParent node parent =
            match node with
            | Some x ->
                x.Parent <- Option.map id parent
            | None -> ()

        let node =
            {
                Parent = None
                Left = left
                Right = right
                Hash =
                    righthash
                    |> Array.append lefthash
                    |> hashFunction
            }

        let nodeResult = node |> Some

        setParent node.Left nodeResult
        setParent node.Right nodeResult

        nodeResult

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

    let private buildNodes leafHashes =
        leafHashes
        |> List.map
            (
                fun h ->
                    {
                        Parent = None
                        Hash = h
                        Left = None
                        Right = None
                    }
                    |> Some
            )

    let build hashFunc leafHashes =
        leafHashes
        |> buildNodes
        |> buildTree hashFunc
        |> nodehash

    let rec private findLeaf
        (node : MerkleNode option)
        (hash : byte[])
        =
        let searchSubTree x =
            if
                x.Left = None
                && x.Right = None
                && x.Hash = hash
            then
                node
            else
                let left = findLeaf x.Left hash
                let right = findLeaf x.Right hash

                match left with
                | Some x -> left
                | None -> right

        Option.bind (fun n -> searchSubTree(n)) node

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Proof
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let rec private merkleProof node segments hash =
        let otherSubtreeHash parent =

            let childHash c =
                match c with
                | None -> Array.zeroCreate 0
                | Some c -> c.Hash

            let leftHash = childHash parent.Left
            let rightHash = childHash parent.Right

            if leftHash = hash then
                RightHash rightHash
            elif rightHash = hash then
                LeftHash leftHash
            else
                LeftHash (Array.zeroCreate 0)

        match node.Parent with
        | None -> segments

        | Some p ->
            let newSegments =
                [ otherSubtreeHash p ]
                |> List.append segments

            merkleProof p newSegments p.Hash

    let calculateProof
        hashFunc
        leafHashes
        leafHash
        : MerkleProof
        =
            let root =
                leafHashes
                |> buildNodes
                |> buildTree hashFunc

            let leaf = findLeaf root leafHash

            match leaf with
            | None -> []
            | Some l -> merkleProof l [] leafHash

    let verifyProof
        hashFunc
        merkleRoot
        leafHash
        proof
        =

        let hashFromSegment hash segment =
            let concated =
                match segment with
                | LeftHash l -> concatHashes l hash
                | RightHash r -> concatHashes hash r

            hashFunc concated

        let calculatedProof =
            proof
            |> List.fold hashFromSegment leafHash

        merkleRoot = calculatedProof
