namespace Chainium.Blockchain.Public.Wallet

open System
open Chainium.Blockchain.Public.Core
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Crypto

module Cli =

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Handlers
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let handleGenerateKeyPairCommand seed =
        Signing.generateWalletInfo seed
        |> printfn "Key Pair: %A" // TODO: Decide about the output format

    let handleSignMessageCommand privateKey message =
        let privateKeyToString (PrivateKey k) = k

        let keyBytes = 
            privateKey
            |> privateKeyToString
            |> Convert.FromBase64String

        message
        |> Serialization.stringToBytes // TODO: Provide input as a file path, so the raw data can be read.
        |> Signing.signMessage keyBytes // TODO: Use key file path, to prevent keys being logged in terminal history.
        |> printfn "Signature: %A" // TODO: Decide about the output format

    let handleUnknownCommand args =
        // TODO: Show help
        printfn "TODO: Print short command reference"


    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Configuration
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let handleCommand args =
        match args with
        | ["-g"] -> handleGenerateKeyPairCommand None
        | ["-g"; seed] -> handleGenerateKeyPairCommand (Some seed)
        | ["-s"; privateKey; message] -> handleSignMessageCommand (PrivateKey privateKey) message
        | _ -> handleUnknownCommand args
