# Own Public Blockchain Node Removal

This document describes how to remove Own public blockchain network node from the system, using provided removal script.
The process described here assumes that the node is installed using the setup scripts provided in the package.


## Node Removal Process

To remove the node software from the system, login into your Linux machine and stop all running node instances (by default there's only one: `ins1`) by executing command in terminal for each instance:

```bash
sudo systemctl stop own-blockchain-public-node@ins1
```

Once the instances are stopped, execute following command in terminal:

```bash
wget -O - https://raw.githubusercontent.com/OwnMarket/OwnBlockchain/master/Docs/Nodes/remove_linux_node.sh | bash
```

Some commands in the removal scripts are executed in `sudo` mode and will require entering password.

This process will remove:
- systemd service definition
- node software binaries
- blockchain database and related database user
- raw blockchain data (i.e. blocks, transactions)
- configuration file
- genesis file

This process will NOT remove the installed PostgreSQL database server.
