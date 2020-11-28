#!/bin/bash

set -e
cd "${0%/*}"

NODE_DIR=/opt/own/blockchain/public/node
DATA_DIR=/var/lib/own/blockchain/public/node

if [ -f "$DATA_DIR/ins1/Data/Block_0" ]; then
    echo "Update not possible. Please reinstall the node and perform full synchronization."
    exit 1
fi

echo '////////////////////////////////////////////////////////////////////////////////'
echo '// Node binaries'
echo '////////////////////////////////////////////////////////////////////////////////'
pushd "$NODE_DIR"
sudo rm -rf *
popd
sudo cp -r ./* "$NODE_DIR"

echo '////////////////////////////////////////////////////////////////////////////////'
echo '// Systemd service unit'
echo '////////////////////////////////////////////////////////////////////////////////'
sudo bash -c 'cat > /lib/systemd/system/own-blockchain-public-node@.service' << EOF
[Unit]
Description=Own Public Blockchain Node
After=network.target postgresql.service

[Service]
Environment=DOTNET_CLI_HOME=/tmp
WorkingDirectory=$DATA_DIR/%i
ExecStart="$NODE_DIR/Own.Blockchain.Public.Node"
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
EOF

sudo systemctl daemon-reload
