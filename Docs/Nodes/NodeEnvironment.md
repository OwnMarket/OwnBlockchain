# Own Public Blockchain Node Environment and Configuration

**NOTE:** For instructions how to use the setup scripts to setup a node, please follow [node setup](NodeSetup.md) document!

This document describes the standard operating environment, details of the configuration and the high level process of setting up an instance of the Own public blockchain network node.

While it is important to be aware of the details described in this document, the installation process is mostly automated with [scripts](NodeSetup.md#scripted-deployment).


## Hardware Considerations

To achieve a proper operation of a node, and ensure that the node is not lagging behind the network (in the context of applied blocks / transactions), there are certain hardware requirements that should be met.


### CPU

Cryptography is in the core of the blockchain technology, which makes blockchain software very much dependent on the computational power of the machine it is running on. It is therefore important to make sure the machine has enough computational power for executing cryptographic algorithms during processing of transactions and blocks.

This, of course, highly depends on the network conditions and usage patterns (i.e. transaction submission rate and participation in consensus).

A machine used to host a node serving as the backend for a rarely used wallet will, of course, serve the purpose even with one CPU core, because lagging behind the network during activity bursts might be tolerable in such a scenario.

However, a machine hosting a node used as an entry point to the network for another software system (e.g. share register, bank, exchange, etc.), with frequent interactions with the blockchain, should have 2-4 CPU cores to avoid lagging behind network due to not being able to apply incoming blocks and transactions fast enough.

Validator nodes participate in consensus protocol, with frequent message exchange bursts during which all message signatures must be cryptographically verified. To be able to cope with the amount of messages and create new blocks on time, validator nodes should have "serious" computational power. This heavily depends on the number of validator nodes (min 4, max 100) and on the transaction volume at the point in time. Hence, validators should have **at least** 4 CPU cores, while 8-16 CPU cores is a strongly recommended configuration.


### RAM

The amount of RAM a node is using depends on the node activity.
In idle state node is using ~100 MB. However, during activity bursts usage can significantly increase. To ensure there is enough RAM in such cases and avoid swapping, it is recommended to have at least 1 GB of RAM available to the node at all times.

If the node is configured to use PostgreSQL as a database engine, and if PostgreSQL instance is running on the same machine as the node, in addition to the RAM used by the node, there should be enough RAM available for the PostgreSQL instance itself. Please refer to PostgreSQL documentation for more information about its system requirements.


### Disk

According to the load test results, amount of disk space required per 1 million transactions is ~5 GB. The growth, of course, depends on the usage of the network.


## Operating System Requirements

Own Public Blockchain Node runs on .NET Core runtime and can therefore be used on all three major operating systems (Windows, macOS, Linux).


## Software Prerequisites

Own Public Blockchain Node depends on .NET Core v2.1, which is shipped as part of the self-contained release package used for node installation and does not need to be separately installed.

If the node is configured to use PostgreSQL database engine, corresponding PostgreSQL instance must be installed either on the same or on another machine. Node setup script takes care of this.


## File System

The current working directory, from which the node is invoked, is used as the root directory for the data storage. Once started, node will work with following files in the working directory:

- `Config.json` file
- `Genesis.json` file
- `Data` directory (used for storing raw blockchain data files)
- `State.fdb` database file (if node is configured to use embedded database engine)

**NOTE:** We recommended having working directory different from the directory in which node binaries are stored, to simplify the node update process.

Since node is writing into the working directory, the user, under which identity the node is running, must have permission to write in that directory.


## Network

A node interacts with the network in following ways:

- It is communicating with other nodes over TCP connection. For the node to be able to communicate with other nodes, network port `25718` must be open.
- It is listening on the HTTP (REST) API endpoints for incoming requests (e.g. transaction submissions) from API consumers (wallet, external system, etc.). If node is supposed to be able to receive API requests, network port `10717` must be open.
- If node is configured to use an instance of PostgreSQL database on a different machine, it is communicating with it over the network. In this case, the machine hosting PostgreSQL instance must have the proper network port open (default PostgreSQL port is `5432`). Please refer to the PostgreSQL documentation for more details.

**NOTE:** Default ports can be changed using the configuration file (see further below).


## Node Installation

Node installation consists of following steps:

- If the node is configured to use PostgreSQL, then a corresponding PostgreSQL instance must be installed (if not already available) and an empty database must be created. Node setup script automatically installs PostgreSQL and creates a database.
- Extracting the node package (official packages can be found on [GitHub releases](https://github.com/OwnMarket/OwnBlockchain/releases)).
- Optional steps:
    - Adjusting the configuration file (see below).
    - Copying a different genesis file (e.g. for testnet, instead of the default one or mainnet)
    - Configuring the node to run as a service on the host machine (node setup script takes care of this).


## Configuration File

Configuration file (`Config.json`) provides the execution parameters and environment configuration for the node (e.g. network and database info). Some of the settings that can be configured using the configuration file are listed below:

Setting Name | Description
---  | ---
`DbEngineType` | Database engine used for storing the blockchain state. Possible values are `Firebird` and `Postgres`. If omitted, default value `Firebird` is used.
`DbConnectionString` | Connection string for the database. Depends on the value of `DbEngineType` setting. If `DbEngineType` is `Firebird` (implicit if omitted) then `DbConnectionString` can be omitted as well, in which case it will default to using `State.fdb` name for Firebird database file.
`ApiListeningAddresses` | The addresses on which the node API accepts the requests. Default address is `http://*:10717`.
`ListeningAddress` | The addresses on which the node listens for incoming network messages from the peers. Default address is `*:25718`.
`NetworkBootstrapNodes` | A list of the nodes through which the node is initially entering the network and from which it will get the gossip peers.
`PublicAddress` | If the node has public address, and is reachable through that address from the public network, then it will participate in node gossip, which will enable it to receive transactions and blocks faster than non-public nodes. If the node doesn't have public address, it will work in poll mode, fetching new blocks from the peers periodically (by default every minute).
`ValidatorPrivateKey` | Private key corresponding to the validator address. If the node is supposed to be a validator, it must have this setting configured.
`MinTxActionFee` | Each node can specify the minimum TX action fee (expressed in CHX), which results in the transactions submitted to this node to be rejected if they specify lower fee. Default value is `0.001`.
`MaxBlockFetchQueue` | Node which is lagging behind the network (e.g. because it is just installed, or was offline for some time) will fetch the missing blocks from its peers. This synchronization process can take long time and depends on the network connection speed. This setting specifies how many blocks the node will fetch from its peers in parallel during synchronization. Default value for this setting is `20`. For nodes with fast network connections, this value can be increased to e.g. `100`, which will make the node synchronize faster. However, setting a too high value is counter productive and can result in lots of errors during synchronization.
`MinLogLevel` | Level of details logged in the node output (stdout). Default value is `Info`. Available values are `Verbose`, `Debug`, `Info`, `Success`, `Notice`, `Warning`, `Error`. If, for example `Warning` value is configured, node will log only warnings and errors. WARNING: Running the node with `Verbose` or `Debug` log level for long time can fill the disk space if the system log rotation doesn't clear the old logs fast enough.

### Sample configuration:

Purpose of this example configuration is to show you how to set it **IF needed**, which is not the case for default setup process.

**DO NOT USE THIS EXAMPLE CONFIGURATION IN YOUR NODES! FOLLOW THE [NODE SETUP DOCUMENT](NodeSetup.md) FOR DETAILED INSTRUCTIONS ON HOW TO CONFIGURE THE NODE.**

```json
{
    "DbEngineType": "Postgres",
    "DbConnectionString": "server=db-srvr;database=own_db;user id=own_db_usr;password=XXX;searchpath=own",
    "ApiListeningAddresses": "http://*:10717",
    "ListeningAddress": "*:25718",
    "NetworkBootstrapNodes": [
        "boot1.mainnet.weown.com:25718",
        "boot2.mainnet.weown.com:25718",
        "boot3.mainnet.weown.com:25718"
    ],
    "PublicAddress": "my-node-name.my-domain.com:25718",
    "ValidatorPrivateKey": "XXXXXXXXXX",
    "MinTxActionFee": 0.001,
    "MaxBlockFetchQueue": 20,
    "MinLogLevel": "Info"
}
```

## Genesis File

Genesis file (`Genesis.json`) contains the information about genesis address, genesis validators and the initial blockchain configuration
used to create the first block, also called "genesis block".

Setting Name | Description
---  | ---
`NetworkCode` | Unique identifier of the blockchain network.
`GenesisAddress` | The address holding the total CHX supply in the genesis block.
`GenesisValidators` | A list of the validators which signed the genesis block.
`GenesisSignatures` | A list of the genesis block signatures for each genesis validator.
`ConfigurationBlockDelta` | Number of blocks between two configuration blocks.
`ValidatorDepositLockTime` | Number of configuration blocks to keep the deposit locked.
`ValidatorBlacklistTime` | Number of configuration blocks to keep the validator blacklisted.
`MaxTxCountPerBlock` | Max number of transactions per block.

### Sample genesis file:

```json
{
    "NetworkCode": "OWN_PUBLIC_BLOCKCHAIN_MAINNET",
    "GenesisAddress": "CHXGEzcvxLFDTnS4L5pETLXxLsp3heH6KK1",
    "GenesisValidators": [
        "CHXV1i4NmCENFPFzqEk1ANb7BgBEiF9GHyT@val01.mainnet.weown.com:25718",
        "CHXV2wFEyFie38RUWmEev8UJwhzMj5mPwmn@val02.mainnet.weown.com:25718",
        "CHXV3e3pjQnk2YuCaTt8GYQJgELASiy57kh@val03.mainnet.weown.com:25718",
        "CHXV4vyfhtXjwP9navSG5NVk2mvCZqhnMZa@val04.mainnet.weown.com:25718"
    ],
    "GenesisSignatures": [
        "AWCEFyWYcCTFwwPgUa2DPqAmVyWKEfakx5gDUAQPLnLZUZP7MEgCHte86YhxDAddCiC56biqfUkFXKSSB76GkX2g7",
        "ASvA8F7cuDTVeds5thBJ3uoxXML6E2YEn8xWi8NgGjXCrqnejenrWKbp6M9gGJu1ZPDk8RUD77sCD5p16SbBa1oUT",
        "FWPgPte7dwSGvNdHnRWHFzKZ85JD3AK2MvS2iYJVhJCLXB6y6EmfZCKpJYTep7titkuSHY3j9Uv14jVWJeF66AywE",
        "46ftZ9cFsVm9Lm3qittWE83xzzMGanw9cJ2U2twve4Q3Wz25BmtjTC9qQQPAMPLVrsZqFC6JzMsDTyN96536pH85E"
    ],
    "ConfigurationBlockDelta": 10000,
    "ValidatorDepositLockTime": 10,
    "ValidatorBlacklistTime": 100,
    "MaxTxCountPerBlock": 1000
}
```


## Running the node

Node is started by executing following command:

```bash
$ cd /path/to/the/node/directory
$ ./Own.Blockchain.Public.Node
```

This will run the node by using the configuration file from the node directory and will create the `Data` directory as the subdirectory of the node directory. If, however, data and the configuration should be placed in different directory paths (which is recommended and a common case in Unix/Linux systems), a node can be started by relying on the current working directory and invoking the executable using from a different path.

```bash
$ cd /var/lib/own/blockchain/public/node/ins1
$ /opt/own/blockchain/public/node/Own.Blockchain.Public.Node
```

In this case the node will be using the configuration and genesis file from the `/var/lib/own/blockchain/public/node/ins1` directory, and `Data` directory will be created there as well.
