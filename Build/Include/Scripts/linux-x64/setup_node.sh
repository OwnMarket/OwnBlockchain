#!/bin/bash

set -e
cd "${0%/*}"

echo '////////////////////////////////////////////////////////////////////////////////'
echo '// Postgres'
echo '////////////////////////////////////////////////////////////////////////////////'
if grep 'ID_LIKE' /etc/os-release | grep 'rhel'; then
    sudo dnf install -y @postgresql:10 postgresql-contrib
    if [ ! -f /var/lib/pgsql/data/postgresql.conf ]; then
        sudo postgresql-setup --initdb --unit postgresql
    fi
    sudo sed -i -- 's/ident$/md5/g' /var/lib/pgsql/data/pg_hba.conf
    sudo systemctl restart postgresql
    sudo systemctl enable postgresql
else
    sudo sh -c 'echo "deb http://apt.postgresql.org/pub/repos/apt/ $(lsb_release -cs)-pgdg main" >> /etc/apt/sources.list.d/pgdg.list'
    wget -q -O - https://www.postgresql.org/media/keys/ACCC4CF8.asc | sudo apt-key add -
    sudo apt update -y
    sudo apt install -y postgresql-10 postgresql-contrib-10
fi
sudo -u postgres psql -c 'CREATE EXTENSION IF NOT EXISTS adminpack'
sudo -u postgres psql -c 'SELECT version()'

if grep 'ID_LIKE' /etc/os-release | grep 'rhel'; then
    echo '////////////////////////////////////////////////////////////////////////////////'
    echo '// .NET Core dependencies'
    echo '////////////////////////////////////////////////////////////////////////////////'
    sudo dnf install -y krb5-libs libicu openssl-libs
fi

echo '////////////////////////////////////////////////////////////////////////////////'
echo '// Node binaries'
echo '////////////////////////////////////////////////////////////////////////////////'
NODE_DIR=/opt/own/blockchain/public/node
sudo mkdir -p -m +r "$NODE_DIR"
sudo cp -r ./* "$NODE_DIR"

DATA_DIR=/var/lib/own/blockchain/public/node
sudo mkdir -p "$DATA_DIR"

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

echo '////////////////////////////////////////////////////////////////////////////////'
echo '// Instance 1'
echo '////////////////////////////////////////////////////////////////////////////////'
./setup_node_instance.sh ins1
