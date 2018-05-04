namespace Chainium.Blockchain.Public.Node

open System
open System.IO
open System.Reflection

module Config =

    // TODO: Implement with https://www.demystifyfp.com/FsConfig

    let dataDir =
        let baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
        Path.Combine(baseDir, "Data")
