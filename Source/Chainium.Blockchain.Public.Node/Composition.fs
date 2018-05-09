namespace Chainium.Blockchain.Public.Node

open System
open Chainium.Blockchain.Public.Core
open Chainium.Blockchain.Public.Crypto
open Chainium.Blockchain.Public.Data

module Composition =

    let saveTx = Raw.saveTx Config.dataDir

    let submitTx = Workflows.submitTx Signing.verifySignature Hashing.hash saveTx
