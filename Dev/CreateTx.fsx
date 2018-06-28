// This is a utility script used to prepare a Tx for signing when needed for development or manual testing.

open System
open System.Text

"""
{
    Nonce: 1,
    Fee: 0.1,
    Actions: [
        {
            ActionType: "TransferChx",
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

// 10 CHX to CHKj2rtVz5z6Pyy1bpH8GaEyhTX7eabQzdw
{
    "tx": "CnsKICAgIE5vbmNlOiAxLAogICAgRmVlOiAwLjEsCiAgICBBY3Rpb25zOiBbCiAgICAgICAgewogICAgICAgICAgICBBY3Rpb25UeXBlOiAiQ2h4VHJhbnNmZXIiLAogICAgICAgICAgICBBY3Rpb25EYXRhOiB7CiAgICAgICAgICAgICAgICBSZWNpcGllbnRBZGRyZXNzOiAiQ0hLajJydFZ6NXo2UHl5MWJwSDhHYUV5aFRYN2VhYlF6ZHciLAogICAgICAgICAgICAgICAgQW1vdW50OiAxMAogICAgICAgICAgICB9CiAgICAgICAgfQogICAgXQp9Cg==",
    "v": "2",
    "r": "6Mi4QHn5BCVeycvPMHMUqP7hEiZeGR76dC4YvPYytGpD",
    "s": "7sQpvc6Awj1XkSG8ppDtFC4qd9wpTG5tCDtfTK8KfH5o"
}

// 5 CHX to CHen2J21nxRj1rQhwQWBXfehs97YJ2TJVoC
{
    "tx": "CnsKICAgIE5vbmNlOiAyLAogICAgRmVlOiAwLjEsCiAgICBBY3Rpb25zOiBbCiAgICAgICAgewogICAgICAgICAgICBBY3Rpb25UeXBlOiAiQ2h4VHJhbnNmZXIiLAogICAgICAgICAgICBBY3Rpb25EYXRhOiB7CiAgICAgICAgICAgICAgICBSZWNpcGllbnRBZGRyZXNzOiAiQ0hlbjJKMjFueFJqMXJRaHdRV0JYZmVoczk3WUoyVEpWb0MiLAogICAgICAgICAgICAgICAgQW1vdW50OiA1CiAgICAgICAgICAgIH0KICAgICAgICB9CiAgICBdCn0K",
    "v": "2",
    "r": "1DaEpxNa3vKqXVUn7e6tWTGLqcVKvqku9KGGffqaLcZtW",
    "s": "1Fcr7Yhc6KZeQhT8DPaKoH4JkupMn2VrPc3gPGw4Vywm4"
}
*)
