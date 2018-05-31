namespace Chainium.Blockchain.Public.Wallet

open System
open System.Text
open System.Reflection
open Chainium.Common
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Crypto

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
        let (PrivateKey pk) = wallet.PrivateKey
        let (ChainiumAddress address) = wallet.Address
        printfn "Private Key: %s\nAddress: %s" pk address

    let handleSignMessageCommand privateKey message =
        let privateKey = PrivateKey privateKey

        message
        |> Convert.FromBase64String // TODO: Provide input as a file path, so the raw data can be read.
        |> Signing.signMessage privateKey // TODO: Use key file path, to prevent keys being logged in terminal history.
        |> (fun { V = v; R = r; S = s } -> printfn "V: %s\nR: %s\nS: %s" v r s )

    let handleHelpCommand args =
        printfn "TODO: Print short command reference"


    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Configuration
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let handleCommand args =
        match args with
        | ["-v"] -> handleShowVersionCommand ()
        | ["-g"] -> handleGenerateWalletCommand ()
        | ["-s"; privateKey; message] -> handleSignMessageCommand privateKey message
        | ["--help"] | _ -> handleHelpCommand args
