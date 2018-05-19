namespace Chainium.Blockchain.Public.Core

open System
open Chainium.Common
open Chainium.Blockchain.Public.Core
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Core.Dtos
open System

module Blocks =

    let assembleBlock createHash blockNumber previousBlockHash txSet output : Block =
        (*
        serialize to byte[]
        calculate hashes
        calculate merkle trees
        *)
        failwith "TODO: assembleBlock"
