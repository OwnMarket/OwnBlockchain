# Own Public Blockchain Node Setup

This document describes the process of setting up an instance of the Own public blockchain network node using provided setup scripts or a simple "xcopy deployment" approach.

Node can be deployed in two ways:

- _Simple deployment_: Deploying using "xcopy deployment" approach (available for for all three OSs and does not require admin permissions). This deployment approach is recommended for non-public nodes running on personal computers.
- _Scripted deployment_: Deploying on a dedicated machine using provided setup scripts (available only for Linux and requires admin permissions). This deployment approach is highly recommended for validator nodes and for mission-critical client nodes in production scenarios.

Please refer to the corresponding section below, depending on your use case.


## Simple deployment

- Go to [GitHub releases](https://github.com/OwnMarket/OwnBlockchain/releases) (e.g. [the latest one](https://github.com/OwnMarket/OwnBlockchain/releases/latest)) and download the package for your operating system (from "Assets" section):
    - [Package for Linux](https://github.com/OwnMarket/OwnBlockchain/releases/latest/download/OwnPublicBlockchainNode_linux-x64.tar.gz)
    - [Package for macOS](https://github.com/OwnMarket/OwnBlockchain/releases/latest/download/OwnPublicBlockchainNode_osx-x64.tar.gz)
    - [Package for Windows](https://github.com/OwnMarket/OwnBlockchain/releases/latest/download/OwnPublicBlockchainNode_win-x64.zip)
- Extract the package to the desired location (e.g. create a directory in your _home_ directory and extract it there)
- Open terminal (command prompt on Windows) and navigate to the directory in which you have extracted the package (e.g. `OwnBlockchainNode` in user's home directory):
    - on Linux and macOS
        ```
        cd ~/OwnBlockchainNode
        ```
    - on Windows
        ```
        cd /d %USERPROFILE%\OwnBlockchainNode
        ```
- Start the node by running one of the two provided scripts (`start_mainnet_node` or `start_testnet_node`), depending on if you want to connect to MainNet or TestNet:
    - on Linux and macOS
        ```
        ./start_mainnet_node.sh
        ```
    - on Windows
        ```
        start_mainnet_node.bat
        ```
- To stop the node, press `Ctrl+C` keyboard combination.


## Scripted deployment

This deployment approach assumes installing the node on a clean machine with `Ubuntu Server 18.04 LTS` or `CentOS 8` operating system installed. Official AWS image identifiers for CentOS 8 are available [here](https://wiki.centos.org/Cloud/AWS).

**NOTE:** On CentOS you will have to install `wget` and `nano` before being able to proceed with the node setup. To install these tools, execute:

```bash
sudo dnf install -y wget nano
```

Login into your Linux machine and execute one of the below two commands in terminal. **Don't execute both commands.**

- If setting up the node for MainNet, execute this command:
    ```bash
    wget -O - https://raw.githubusercontent.com/OwnMarket/OwnBlockchain/master/Docs/Nodes/setup_linux_node.sh | bash
    ```

- If setting up the node for TestNet, execute this command:
    ```bash
    wget -O - https://raw.githubusercontent.com/OwnMarket/OwnBlockchain/master/Docs/Nodes/setup_linux_node_on_testnet.sh | bash
    ```

Some commands in the setup scripts are executed in `sudo` mode and will require entering password.

After installation is done, one instance of the node will be registered as a [systemd](https://en.wikipedia.org/wiki/Systemd) service. Setup script will provide instructions at the end of the execution on how to manage the node service and take a look at its logs. Those are standard systemd commands.

At this point the node can be started

```bash
sudo systemctl start own-blockchain-public-node@ins1
```

Once node is started, it is expected to start synchronizing with other nodes. Depending on the number of blocks node has to download, this process can take significant time to complete.

To view node's logs, use following command

```bash
journalctl -fu own-blockchain-public-node@ins1
```

If you need to stop the node, use following command

```bash
sudo systemctl stop own-blockchain-public-node@ins1
```

Once the node is synchronized, you can proceed with optional steps listed below, if they apply to your use-case.


### Optional configuration steps

#### Expose node to public network

By default, node runs in "pull" mode, which means it is fetching new blocks from peers periodically (by default every minute). If you want the node to participate in node communication (which is a prerequisite for validators as well) and receive blocks and transactions as soon as they're propagated throughout the network, you need to:

- Have a DNS name, as an [A record](https://en.wikipedia.org/wiki/List_of_DNS_record_types), pointing to the IP of the server on which your node is running.

- Configure DNS name and port (default is `25718`) as `PublicAddress` in node configuration file (`Config.json`):
    - Stop the node.
    - Open configuration file in text editor (e.g. `nano` or `vim`).
        ```bash
        sudo nano /var/lib/own/blockchain/public/node/ins1/Config.json
        ```
    - Add an entry with the public DNS name and port pointing to your node.
        ```json
        "PublicAddress": "my-node-name.my-domain.com:25718"
        ```
    - Save the file.
    - Start the node.

- Ensure node is reachable from public network through configured `PublicAddress` (depending on your environment, this might involve configuring DNS entries and firewall ports).
    - Default port node is using to communicate with peers is `25718`.
    - Default port for node API is `10717`.
    - Easiest way to check if your node is accessible from public network is to try to access its API (e.g. http://my-node-name.my-domain.com:10717/stats).

#### Configure node as validator

To configure the node as validator, please refer to the [validator configuration document](ValidatorConfiguration.md).
