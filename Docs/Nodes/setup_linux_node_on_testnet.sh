#!/bin/bash

set -e

SETUP_DIR=/tmp/OwnPublicBlockchainNodeSetup

# Prepare temp dir
rm -rf "$SETUP_DIR"
mkdir -p -m 777 "$SETUP_DIR"
cd "$SETUP_DIR"

# Download the package
wget https://github.com/OwnMarket/OwnBlockchain/releases/download/v1.3.2/OwnPublicBlockchainNode_linux-x64.tar.gz

# Extract the package
mkdir Package
cd Package
tar xzf ../OwnPublicBlockchainNode_linux-x64.tar.gz

# Setup the node
./setup_node.sh

echo "Version hash: $(cat /opt/own/blockchain/public/node/Version)"

sudo cp /opt/own/blockchain/public/node/Networks/Test/Genesis.json /var/lib/own/blockchain/public/node/ins1/Genesis.json
sudo sed -i -- 's/\.mainnet\.weown\.com/\.testnet\.weown\.com/g' /var/lib/own/blockchain/public/node/ins1/Config.json
