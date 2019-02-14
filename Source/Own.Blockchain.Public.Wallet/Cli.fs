namespace Own.Blockchain.Public.Wallet

open System
open System.Reflection
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Crypto

module Cli =

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Handlers
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let handleShowVersionCommand () =
        let assembly = System.Reflection.Assembly.GetExecutingAssembly()
        assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion
        |> printfn "%s"

    let handleGenerateWalletCommand () =
        let wallet = Signing.generateWallet ()
        printfn "Private Key: %s\nAddress: %s" wallet.PrivateKey.Value wallet.Address.Value

    let handleDeriveAddressCommand privateKey =
        privateKey // TODO: Use key file path, to prevent keys being logged in terminal history.
        |> PrivateKey
        |> Signing.addressFromPrivateKey
        |> fun (BlockchainAddress a) -> printfn "Address: %s" a

    let handleSignMessageCommand privateKey message =
        let privateKey = PrivateKey privateKey

        message
        |> Convert.FromBase64String // TODO: Provide input as a file path, so the raw data can be read.
        |> Signing.signMessage privateKey // TODO: Use key file path, to prevent keys being logged in terminal history.
        |> fun (Signature signature) -> printfn "Signature: %s" signature

    let handleHelpCommand args =
        printfn "TODO: Print short command reference"

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Configuration
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let handleCommand args =
        match args with
        | ["--version"] -> handleShowVersionCommand ()
        | ["--generate"] -> handleGenerateWalletCommand ()
        | ["--address"; privateKey] -> handleDeriveAddressCommand privateKey
        | ["--sign"; privateKey; message] -> handleSignMessageCommand privateKey message
        | ["--help"] | _ -> handleHelpCommand args
