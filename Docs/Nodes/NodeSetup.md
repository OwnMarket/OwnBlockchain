# Own Public Blockchain Node Setup

This document describes the process of setting up an instance of the Own public blockchain network node using provided setup scripts or a simple "xcopy deployment" approach.

Node can be deployed in two ways:

- _Scripted deployment_: Deploying on a dedicated machine using provided setup scripts (available only for Linux and requires admin permissions). This deployment approach is highly recommended for validator nodes and for mission-critical client nodes in production scenarios.
- _Simple deployment_: Deploying using "xcopy approach" (available for for all three OSs and does not require admin permissions). This deployment approach is recommended for non-public nodes running on personal computers.

Please refer to the corresponding section below, depending on your use case.

## Scripted deployment (using setup scripts on Linux server)

We recommend installing the node on `Ubuntu Server 18.04 LTS` operating system.

Login into your Linux machine and execute following command in terminal:

```bash
wget -O - https://raw.githubusercontent.com/OwnMarket/OwnBlockchain/master/Docs/Nodes/setup_linux_node.sh | bash
```

Some commands in the setup scripts are executed in `sudo` mode and will require entering password.

After installation is done, one instance of the node will be registered as a [systemd](https://en.wikipedia.org/wiki/Systemd) service. Setup script will provide instructions at the end of the execution on how to manage the node service and take a look at its logs. Those are standard systemd commands.

**NOTICE:** By default, node is configured to connect to MainNet. If, however, you would like to configure the node to connect to TestNet, make sure to do that **before** starting the node for the first time. Otherwise node instance state reset will be needed, because one instance cannot work with both networks at the same time.

To configure the node to connect to TestNet instead of the MainNet, execute following commands:

- Copy TestNet genesis file to instance directory
    ```bash
    sudo cp /opt/own/blockchain/public/node/Networks/Test/Genesis.json /var/lib/own/blockchain/public/node/ins1/Genesis.json
    ```
- Change bootstrap nodes in configuration file
    - Open file in text editor (e.g. `nano` or `vim`)
        ```bash
        sudo nano /var/lib/own/blockchain/public/node/ins1/Config.json
        ```
    - Replace `.mainnet.weown.com` with `.testnet.weown.com` for all bootstrap nodes in the list and save the file.

At this point the node can be started

```bash
sudo systemctl start own-blockchain-public-node@ins1
```

To view node's logs, use following command

```bash
journalctl -fu own-blockchain-public-node@ins1
```

To stop the node, use following command

```bash
sudo systemctl stop own-blockchain-public-node@ins1
```

## Simple deployment

- Go to [GitHub releases](https://github.com/OwnMarket/OwnBlockchain/releases) (e.g. [the latest one](https://github.com/OwnMarket/OwnBlockchain/releases/latest)) and download the package for your operating system (from "Assets" section):
    - [Package for Linux](https://github.com/OwnMarket/OwnBlockchain/releases/latest/download/OwnPublicBlockchainNode_linux-x64.tar.gz)
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
