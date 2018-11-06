#!/bin/bash

set -e
cd "${0%/*}"

echo '////////////////////////////////////////////////////////////////////////////////'
echo '// Postgres'
echo '////////////////////////////////////////////////////////////////////////////////'
sudo sh -c 'echo "deb http://apt.postgresql.org/pub/repos/apt/ $(lsb_release -cs)-pgdg main" >> /etc/apt/sources.list.d/pgdg.list'
wget -q -O - https://www.postgresql.org/media/keys/ACCC4CF8.asc | sudo apt-key add -
sudo apt update -y
sudo apt install -y postgresql-10 postgresql-contrib-10
sudo -u postgres psql -c 'CREATE EXTENSION adminpack'

echo '////////////////////////////////////////////////////////////////////////////////'
echo '// .NET Core'
echo '////////////////////////////////////////////////////////////////////////////////'
wget -q -O /tmp/packages-microsoft-prod.deb https://packages.microsoft.com/config/ubuntu/16.04/packages-microsoft-prod.deb
sudo dpkg -i /tmp/packages-microsoft-prod.deb
sudo apt install -y apt-transport-https
sudo apt update -y
sudo apt install -y dotnet-runtime-2.1

echo '////////////////////////////////////////////////////////////////////////////////'
echo '// Node binaries'
echo '////////////////////////////////////////////////////////////////////////////////'
NODE_DIR=/opt/own/blockchain/public/node
DATA_DIR=/var/lib/own/blockchain/public/node
sudo mkdir -p -m +r "$NODE_DIR"
sudo mkdir -p "$DATA_DIR"
sudo cp -r ./* "$NODE_DIR"

echo '////////////////////////////////////////////////////////////////////////////////'
echo '// Systemd service unit'
echo '////////////////////////////////////////////////////////////////////////////////'
sudo bash -c 'cat > /lib/systemd/system/own-blockchain-public-node@.service' << EOF
[Unit]
Description=Own Public Blockchain Node
After=network.target postgresql.service

[Service]
WorkingDirectory=$DATA_DIR/%i
ExecStart=/usr/bin/dotnet "$NODE_DIR/Chainium.Blockchain.Public.Node.dll"
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
EOF

sudo systemctl daemon-reload

echo '////////////////////////////////////////////////////////////////////////////////'
echo '// Instance 1'
echo '////////////////////////////////////////////////////////////////////////////////'
./setup_public_node_instance.sh ins1
