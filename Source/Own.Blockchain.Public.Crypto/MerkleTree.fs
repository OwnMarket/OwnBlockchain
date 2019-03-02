namespace Own.Blockchain.Public.Crypto

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

    let private nodeHash node =
        match node with
        | Some n -> n.Hash
        | None -> Array.empty

    let private buildParentNode
        hashFunc
        (leftNode : MerkleNode option)
        (rightNode : MerkleNode option)
        =

        let lefthash = nodeHash leftNode
        let righthash = nodeHash rightNode

        let node =
            {
                Parent = None
                Left = leftNode
                Right = rightNode
                Hash =
                    righthash
                    |> Array.append lefthash
                    |> hashFunc
            }

        let setParent child parent =
            match child with
            | Some n -> n.Parent <- Some parent
            | None -> ()

        setParent node.Left node
        setParent node.Right node

        node |> Some

    // Builds the upper level list of nodes by computing parent nodes from ordered pairs of 2 (child) nodes.
    // The pairs are constructed left-to-right.
    let rec private buildParentLevel hashFunc parentNodes (nodes : MerkleNode option list) =
        let pair, remainingNodes =
            match nodes with
            | [_] -> ([nodes.Head; nodes.Head], List.Empty)
            | [] -> (List.Empty, parentNodes)
            | _ -> nodes |> List.splitAt 2

        if pair.Length < 2 then
            remainingNodes
        else
            let leftNode = pair.Item 0
            let rightNode = pair.Item 1
            let parentNode = buildParentNode hashFunc leftNode rightNode
            buildParentLevel hashFunc (parentNodes @ [parentNode]) remainingNodes

    // Builds tree from bottom-up, level by level.
    let rec private buildRootNode hashFunc nodes =
        match nodes with
        | [] -> None
        | [_] -> nodes.Head
        | _ ->
            nodes
            |> buildParentLevel hashFunc []
            |> buildRootNode hashFunc

    let private buildTree
        hashFunc
        (leafNodes : MerkleNode option list)
        =

        buildRootNode hashFunc leafNodes

    let private toLeafNodes leafHashes =
        leafHashes
        |> List.map (fun h ->
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
        |> toLeafNodes
        |> buildTree hashFunc
        |> nodeHash

    // Find merkle node by hash in the leaf nodes starting from a given node.
    let rec private findLeafNode
        (node : MerkleNode option)
        (hash : byte[])
        =

        let isLeaf n = n.Left = None && n.Right = None
        node |> Option.bind (fun n ->
            if (isLeaf n) && n.Hash = hash then
                Some n
            else
                let leftNode = findLeafNode n.Left hash
                match leftNode with
                | Some _ -> leftNode
                | None -> findLeafNode n.Right hash
        )

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Proof
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    // Finds the sibling node, i.e node with the same parent.
    let private findSiblingHash node =
        node.Parent
        |> Option.bind (fun parent ->
            let leftHash = nodeHash parent.Left
            let rightHash = nodeHash parent.Right

            if leftHash = node.Hash then
                RightHash rightHash
            elif rightHash = node.Hash then
                LeftHash leftHash
            else
                LeftHash Array.empty
            |> Some
        )

    // Builds the merkle proof segments for a node (ordered sequence of hashes that can reconstruct the root hash).
    let rec private merkleProof node segments =
        match findSiblingHash node with
        | None -> segments
        | Some siblingHash ->
            match node.Parent with
            | None -> segments
            | Some parent -> merkleProof parent (segments @ [ siblingHash ])

    // Calculate the merkle proof for a hash starting from the leaf hashes.
    let calculateProof
        hashFunc
        leafHashes
        leafHash
        : MerkleProof
        =

        let rootNode =
            leafHashes
            |> toLeafNodes
            |> buildTree hashFunc

        match findLeafNode rootNode leafHash with
        | None -> []
        | Some leafNode -> merkleProof leafNode []

    // Verify that a hash is in the merkle tree using the proof segments.
    let verifyProof
        hashFunc
        merkleRoot
        leafHash
        proof
        =

        let hashFromSegment hash segment =
            match segment with
            | LeftHash leftHash -> Array.append leftHash hash
            | RightHash rightHash -> Array.append hash rightHash
            |> hashFunc

        let computedRootHash =
            proof
            |> List.fold hashFromSegment leafHash

        computedRootHash = merkleRoot
