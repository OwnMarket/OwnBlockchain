# Own Public Blockchain Node Update

This document describes the process of updating an instance of the Own public blockchain network node using provided update script.
The process described here assumes that the node is installed using the setup scripts provided in the package.


## Node Update Process

To update the node software to latest released version, login into your Linux machine and stop all running node instances (by default there's only one: `ins1`) by executing command in terminal for each instance:

```bash
sudo systemctl stop own-blockchain-public-node@ins1
```

Once the instances are stopped, execute one of the below two commands in terminal. **Don't execute both commands.**

- If updating the MainNet node, execute this command:
    ```bash
    wget -O - https://raw.githubusercontent.com/OwnMarket/OwnBlockchain/master/Docs/Nodes/update_linux_node.sh | bash
    ```

- If updating the TestNet node, execute this command:
    ```bash
    wget -O - https://raw.githubusercontent.com/OwnMarket/OwnBlockchain/master/Docs/Nodes/update_linux_node_on_testnet.sh | bash
    ```

Some commands in the update scripts are executed in `sudo` mode and will require entering password.

After update is done, node instance(s) can be started again:

```bash
sudo systemctl start own-blockchain-public-node@ins1
```

Make sure node is running as expected by looking at the logs:

```bash
journalctl -fu own-blockchain-public-node@ins1
```
