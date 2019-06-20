# Own Blockchain SDK

SDK for interacting with Own public blockchain.


## Usage

Add [NuGet package](https://www.nuget.org/packages/Own.Blockchain.Sdk) to the .NET project:

```bash
$ dotnet add package Own.Blockchain.Sdk
```

Use the package in code (example in C#):

```csharp
using System;
using Own.Blockchain.Public.Sdk;

class Program
{
    static void Main(string[] args)
    {
        var networkCode = "OWN_PUBLIC_BLOCKCHAIN_TESTNET";

        // Create a new wallet
        var wallet = new Wallet();
        Console.WriteLine("PK: {0}, Address: {1}", wallet.PrivateKey, wallet.Address);

        // Compose a transaction with nonce = 1
        var tx = new Tx(wallet.Address, 1);
        tx.ActionFee = 0.01m; // Set action fee.
        tx.AddTransferChxAction("CHxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx", 100); // Transfer 100 CHX to CHxxx... address.

        // Look at the raw transaction in JSON format
        Console.WriteLine(tx.ToJson(true));

        // Sign the transaction for submission to node API on TestNet
        Console.WriteLine(tx.Sign(networkCode, wallet.PrivateKey).ToJson());
    }
}
```

Run program:

```bash
$ dotnet run
```
