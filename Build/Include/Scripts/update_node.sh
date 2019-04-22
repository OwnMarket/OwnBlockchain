#!/bin/bash

set -e
cd "${0%/*}"

echo '////////////////////////////////////////////////////////////////////////////////'
echo '// Node binaries'
echo '////////////////////////////////////////////////////////////////////////////////'
NODE_DIR=/opt/own/blockchain/public/node
sudo rm -rf "$NODE_DIR/*"
sudo cp -r ./* "$NODE_DIR"

DATA_DIR=/var/lib/own/blockchain/public/node

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
