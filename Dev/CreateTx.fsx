// This is a utility script used to prepare a Tx for signing when needed for development or manual testing.

open System
open System.Text

"""
{
    SenderAddress: "CHQcJKysWbbqyRm5ho44jexA8radTZzNQQ2",
    Nonce: 1,
    Fee: 0.001,
    Actions: [
        {
            ActionType: "TransferChx",
            ActionData: {
                RecipientAddress: "CHYbsyKS6fdDPRnDNRbEmUQkR3GXzNZmStS",
                Amount: 10
            }
        }
    ]
}
"""
|> Encoding.UTF8.GetBytes
|> Convert.ToBase64String
|> printfn "%s"

(*
// Transfer faucet CHX supply from genesis to faucet supply holder
Private Key: 1EQKWYpFtKZ1rMTqAH8CSLVjE5TN1nPpofzWF68io1HPV
Address: CHQcJKysWbbqyRm5ho44jexA8radTZzNQQ2
{
    Nonce: 1,
    Fee: 0.001,
    Actions: [
        {
            ActionType: "TransferChx",
            ActionData: {
                RecipientAddress: "CHb4ojxy1245voxbSWZDdidRqg8T9L3d2Ts",
                Amount: 100000000
            }
        }
    ]
}
{
    tx: "CnsKICAgIE5vbmNlOiAxLAogICAgRmVlOiAwLjAwMSwKICAgIEFjdGlvbnM6IFsKICAgICAgICB7CiAgICAgICAgICAgIEFjdGlvblR5cGU6ICJUcmFuc2ZlckNoeCIsCiAgICAgICAgICAgIEFjdGlvbkRhdGE6IHsKICAgICAgICAgICAgICAgIFJlY2lwaWVudEFkZHJlc3M6ICJDSGI0b2p4eTEyNDV2b3hiU1daRGRpZFJxZzhUOUwzZDJUcyIsCiAgICAgICAgICAgICAgICBBbW91bnQ6IDEwMDAwMDAwMAogICAgICAgICAgICB9CiAgICAgICAgfQogICAgXQp9Cg==",
    v: "2",
    r: "7ibQNu6d39NMLFzD7dLzRo1AHKrunwYQWzYSx1haPTve",
    s: "35V7Kfx1exDSSF71FyPq6NhJ4fihPx42b27ENHyR95Zt"
}

// Faucet Supply Holder
Private Key: 1HU9REFJ5nKX3Q2bdYFdf4pNmiW4zJFH3zP38ntcBnyCd
Address: CHb4ojxy1245voxbSWZDdidRqg8T9L3d2Ts
{
    Nonce: 1,
    Fee: 0.001,
    Actions: [
        {
            ActionType: "CreateAsset",
            ActionData: {}
        },
        {
            ActionType: "CreateAccount",
            ActionData: {}
        }
    ]
}
{
    tx: "CnsKICAgIE5vbmNlOiAxLAogICAgRmVlOiAwLjAwMSwKICAgIEFjdGlvbnM6IFsKICAgICAgICB7CiAgICAgICAgICAgIEFjdGlvblR5cGU6ICJDcmVhdGVBc3NldCIsCiAgICAgICAgICAgIEFjdGlvbkRhdGE6IHt9CiAgICAgICAgfSwKICAgICAgICB7CiAgICAgICAgICAgIEFjdGlvblR5cGU6ICJDcmVhdGVBY2NvdW50IiwKICAgICAgICAgICAgQWN0aW9uRGF0YToge30KICAgICAgICB9CiAgICBdCn0K",
    v: "1",
    r: "1BNagozWLZorRwEDSt6u9xNqCB1nDBkPLr5AU5Hk9vFft",
    s: "7pWCabj8ZtV5vdwEk41gYfBtPEeLe1jBtHwk8HGp74JJ"
}
{
    Nonce: 2,
    Fee: 0.001,
    Actions: [
        {
            ActionType: "CreateAssetEmission",
            ActionData: {
                EmissionAccountHash: "HD2NGMksGUNMyCLJdkHkfvcZ4PXMMoSh1eLcKxW2M1Zq",
                AssetHash: "2ZK8jQJ7TAoJqAWKPnCUxGU8HTaQ72sdLA96wosZxdZX",
                Amount: 1000000
            }
        }
    ]
}
{
    tx: "CnsKICAgIE5vbmNlOiAyLAogICAgRmVlOiAwLjAwMSwKICAgIEFjdGlvbnM6IFsKICAgICAgICB7CiAgICAgICAgICAgIEFjdGlvblR5cGU6ICJDcmVhdGVBc3NldEVtaXNzaW9uIiwKICAgICAgICAgICAgQWN0aW9uRGF0YTogewogICAgICAgICAgICAgICAgRW1pc3Npb25BY2NvdW50SGFzaDogIkhEMk5HTWtzR1VOTXlDTEpka0hrZnZjWjRQWE1Nb1NoMWVMY0t4VzJNMVpxIiwKICAgICAgICAgICAgICAgIEFzc2V0SGFzaDogIjJaSzhqUUo3VEFvSnFBV0tQbkNVeEdVOEhUYVE3MnNkTEE5Nndvc1p4ZFpYIiwKICAgICAgICAgICAgICAgIEFtb3VudDogMTAwMDAwMAogICAgICAgICAgICB9CiAgICAgICAgfQogICAgXQp9Cg==",
    v: "1",
    r: "1DATQCmcmc2N3YWj5k5PRRBSx7ZNuZqziRumxN3nSUycx",
    s: "73f47MMMSggZDwuayyegk72u4u9rfucvjKvUWuEh3qS5"
}

// Investor
Private Key: 1C7t8nYdKbieSAoYyNbmqhmwkfxjCK2C5ffzoHipRhEbd
Address: CHTvddBEYpCMqfBMhdnoDwbMWNoyBwB1Vya
{
    Nonce: 1,
    Fee: 0.001,
    Actions: [
        {
            ActionType: "CreateAccount",
            ActionData: {}
        }
    ]
}
{
    tx: "CnsKICAgIE5vbmNlOiAxLAogICAgRmVlOiAwLjAwMSwKICAgIEFjdGlvbnM6IFsKICAgICAgICB7CiAgICAgICAgICAgIEFjdGlvblR5cGU6ICJDcmVhdGVBY2NvdW50IiwKICAgICAgICAgICAgQWN0aW9uRGF0YToge30KICAgICAgICB9CiAgICBdCn0K",
    v: "1",
    r: "1BAAwy3pCj9zdUTX2FCi5LGAEZhAKEspP6Fqs9QsTbHm3",
    s: "1G78BPqtFs5xUpEV6urMizHRmqJgXimApemcap9UyBZ8Q"
}

ACCOUNT: 55Nz6e5K6b9jK7fkfPFoDfubgvBLcCfXmyi8xmDEu5k2
*)
