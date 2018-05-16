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
                RecipientAddress: "CH89ftLVLHXnhqwrhRmj1vu2oDCofVNUuQd",
                Amount: 10
            }
        }
    ]
}
"""
|> Encoding.UTF8.GetBytes
|> Convert.ToBase64String
|> printfn "%s"
