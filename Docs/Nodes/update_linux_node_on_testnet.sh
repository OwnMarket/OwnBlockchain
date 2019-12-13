#!/bin/bash

set -e

SETUP_DIR=/tmp/OwnPublicBlockchainNodeSetup

# Prepare temp dir
rm -rf "$SETUP_DIR"
mkdir -p -m 777 "$SETUP_DIR"
cd "$SETUP_DIR"

# Download the package
wget https://github.com/OwnMarket/OwnBlockchain/releases/download/v1.4.4/OwnPublicBlockchainNode_linux-x64.tar.gz

# Extract the package
mkdir Package
cd Package
tar xzf ../OwnPublicBlockchainNode_linux-x64.tar.gz

# Update the node
./update_node.sh

echo "Version hash: $(cat /opt/own/blockchain/public/node/Version)"
