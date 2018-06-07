// This is a utility script used to prepare a Tx for signing when needed for development or manual testing.

open System
open System.Text

"""
{
    Nonce: 1,
    Fee: 1,
    Actions: [
        {
            ActionType: "ChxTransfer",
            ActionData: {
                RecipientAddress: "CHKj2rtVz5z6Pyy1bpH8GaEyhTX7eabQzdw",
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
CnsKICAgIE5vbmNlOiAxLAogICAgRmVlOiAxLAogICAgQWN0aW9uczogWwogICAgICAgIHsKICAgICAgICAgICAgQWN0aW9uVHlwZTogIkNoeFRyYW5zZmVyIiwKICAgICAgICAgICAgQWN0aW9uRGF0YTogewogICAgICAgICAgICAgICAgUmVjaXBpZW50QWRkcmVzczogIkNIS2oycnRWejV6NlB5eTFicEg4R2FFeWhUWDdlYWJRemR3IiwKICAgICAgICAgICAgICAgIEFtb3VudDogMTAKICAgICAgICAgICAgfQogICAgICAgIH0KICAgIF0KfQo=

Private Key: 1EQKWYpFtKZ1rMTqAH8CSLVjE5TN1nPpofzWF68io1HPV
Address: CHQcJKysWbbqyRm5ho44jexA8radTZzNQQ2

V: 1
R: 1FdmCDScepMzMbkYwwF6nupAgE5WqhLcWjFUjBm11kjVa
S: bGPah3wtfWmC6RuNN3Ur1MzMNAutYF1ehcq3DoVUBWZ
*)
