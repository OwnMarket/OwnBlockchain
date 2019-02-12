# Own Public Blockchain Node Setup

This document describes the process of setting up an instance of the Own public blockchain network node.


## Hardware Considerations

To achieve a proper operation of a node, and ensure that the node is not lagging behind the network (in the context of applied blocks / transactions), there are certain hardware requirements that must be met.


### CPU

Cryptography is in the core of the blockchain technology, which makes blockchain software very much dependent on the computational power of the machine it is running on. It is therefore important to make sure the machine has enough computational power for executing cryptographic algorithms during processing of transactions and blocks.

This of course highly depends on the network conditions and usage patterns (i.e. transaction submission rate and participation in consensus).

A machine used to host a node serving as the backend for a rarely used wallet will of course serve the purpose even with one CPU core, because lagging behind the network during activity bursts might be tolerable in such a scenario.

However, a machine hosting a node used as an entry point to the network for another software system (e.g. share register, bank, exchange, etc.), with frequent interactions with the blockchain, should have at least 4-8 CPU cores to avoid lagging behind network due to not being able to apply incoming blocks and transactions fast enough.

Validator nodes participate in consensus protocol, with frequent message exchange bursts during which all message signatures must be cryptographically verified. To be able to cope with the amount of messages and create new blocks on time, validator nodes should have "serious" computational power. This heavily depends on the number of validator nodes at the point in time (min 20, max 100). But as a rule of thumb there should be at least 16 CPU cores.


### RAM

The amount of RAM a node is using very much depends on the node activity.
In idle state node is using 50-150 MB (depending on OS). However, during activity bursts usage can significantly increase. To ensure there is enough RAM in such cases and avoid swapping, it is recommended to have at least 1 GB of RAM available to the node at all times.

If the node is configured to use PostgreSQL as a database engine, and if PostgreSQL instance is running on the same machine as the node, in addition to the RAM used by the node, there should be enough RAM available for the PostgreSQL instance itself. Please refer to PostgreSQL documentation for more information about its system requirements.


### Disk

According to the load test results, amount of disk space required per 1 million transactions is ~5 GB. The growth of course depends on the usage of the network.


## Operating System Requirements

Own Public Blockchain Node runs on .NET Core runtime and can therefore be used on all three major operating systems (Windows, macOS, Linux).


## Software Prerequisites

A machine hosting Own Public Blockchain Node must have .NET Core v2.1 installed.

If the node is configured to use PostgreSQL database engine, corresponding PostgreSQL instance must be installed either on the same or on another machine.


## File System

The current working directory from which the node is invoked is used as the root directory for the data storage. Once started, node will work with following files in the working directory:

- Configuration file
- `Data` directory (used for storing raw blockchain data files)
- Database file (if node is configured to use embedded database engine)

**NOTE:** Working directory must not be the directory in which node binaries are stored.

Since node is writing into the working directory, the user under which identity the node runs must have permission to write in that directory.


## Network

A node interacts with the network in following ways:

- It is communicating with other nodes over TCP connection. To make sure the node is visible to other nodes, network port `25718` must be open.
- It is listening on the HTTP (REST) API endpoints for incoming requests (e.g. transaction submissions) from API consumers (wallet, external system, etc.). If node is supposed to be able to receive API requests, network port `10717` must be open.
- If node is configured to use an instance of PostgreSQL database on a different machine, it is communicating with it over the network. In this case, the machine hosting PostgreSQL instance must have the proper network port open (default PostgreSQL port is `5432`)

**NOTE:** Default ports can be changed using the configuration file (see further below).


## Node Installation

Node installation consists of following steps:

- If the node is configured to use PostgreSQL, then a corresponding PostgreSQL instance must be installed (if not already available) and an empty database must be created. (database creation scripts will be provided as a part of the release package)
- Installing the prerequisites (see _Software Prerequisites_ above).
- Extracting the node package (official packages will be provided in GitHub releases upon the testnet launch).
- Optional steps:
    - Adjusting the configuration file (see below).
    - Configuring the node to run as a service on the host machine.

**NOTE:** Setup scripts will be available in the official release package.


## Configuration File

Configuration file provides the execution parameters and environment configuration for the node (e.g. network and database info). There are following settings that can be configured using the configuration file:

Setting Name | Description
---  | ---
`DbEngineType` | Database engine used for storing the blockchain state. Possible values are `Firebird` and `Postgres`.
`DbConnectionString` | Connection string for the database. Depends on the value of `DbEngineType` setting.
`ApiListeningAddresses` | The address on which the node API accepts the requests. Default address is `http://*:10717`.
`NetworkAddress` | The address on which the node listens for incoming network messages from the peers. Default address is `*:25718`.
`NetworkBootstrapNodes` | A list of the nodes through which the node is initially entering the network and from which it will get the gossip peers.
`GenesisAddress` | The address holding the total CHX supply in the genesis block.
`GenesisValidators` | A list of the validators which signed the genesis block.

### Sample configuration:

```json
{
    "DbEngineType": "Postgres",
    "DbConnectionString": "server=db-srvr;database=own_db;user id=own_db_usr;password=XXX;searchpath=own",
    "ApiListeningAddresses": "http://*:10717",
    "NetworkAddress": "*:25718",
    "NetworkBootstrapNodes": [
        "some-node1.weown.com:25718",
        "some-node2.weown.com:25718",
        "some-node3.weown.com:25718"
    ],
    "GenesisAddress": "CHggggggggggggggggggggggggggggggggg",
    "GenesisValidators": [
        "CHaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa@validator1.weown.com:25718",
        "CHbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb@validator2.weown.com:25718",
        "CHccccccccccccccccccccccccccccccccc@validator3.weown.com:25718",
        "CHddddddddddddddddddddddddddddddddd@validator4.weown.com:25718"
    ]
}
```


## Running the node

Node is started by executing following command:

```bash
$ cd /path/to/the/node/directory
$ dotnet Own.Blockchain.Public.Node.dll
```

This will run the node by using the configuration file from the node directory and will create the `Data` directory as the subdirectory of the node directory. If, however, data and the configuration should be placed in different directory paths (which is a common case in Unix/Linux systems), a node can be started by relying on the current working directory and invoking the executable using from a different path.

```bash
$ cd /var/lib/own/blockchain/public/node/instance1
$ dotnet /opt/own/blockchain/public/node/Own.Blockchain.Public.Node.dll
```

In this case the node will be using the configuration file from the `/var/lib/own/blockchain/public/node/instance1` directory, and `Data` directory will be created there as well.
