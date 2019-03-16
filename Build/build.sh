#!/bin/bash

# Prevent packing of resource fork files on macOS (./._xxxxx)
export COPYFILE_DISABLE=true

set -e

pushd "${0%/*}" # Go to script directory
BUILD_DIR="$(pwd)"
TEMP_DIR="$BUILD_DIR/Temp"
OUTPUT_DIR="$BUILD_DIR/Output"

rm -rf "$TEMP_DIR"
mkdir -p -m 777 "$TEMP_DIR"

rm -rf "$OUTPUT_DIR"
mkdir -p -m 777 "$OUTPUT_DIR"

# Build the Node
pushd ../Source/Own.Blockchain.Public.Node
mkdir -p "$TEMP_DIR/Node"
dotnet publish -c Release -o "$TEMP_DIR/Node"
popd

rm "$TEMP_DIR/Node/Genesis.json"
rm "$TEMP_DIR/Node/Config.json"

cp -r ~/.nuget/packages/secp256k1.net/0.1.48/content/native "$TEMP_DIR/Node"
cp -r ../Docs/Deployment/setup_public_node.sh "$TEMP_DIR/Node"
cp -r ../Docs/Deployment/setup_public_node_instance.sh "$TEMP_DIR/Node"

pushd "$TEMP_DIR/Node"
chmod +x *.sh
git rev-parse HEAD > Version
tar czf "$OUTPUT_DIR/Node.tar.gz" *
popd

# Build the Faucet
pushd ../Source/Own.Blockchain.Public.Faucet
mkdir -p "$TEMP_DIR/Faucet"
dotnet publish -c Release -o "$TEMP_DIR/Faucet"
popd

cp -r ~/.nuget/packages/secp256k1.net/0.1.48/content/native "$TEMP_DIR/Faucet"

pushd "$TEMP_DIR/Faucet"
git rev-parse HEAD > Version
tar czf "$OUTPUT_DIR/Faucet.tar.gz" *
popd

# Cleanup
rm -rf "$TEMP_DIR"

# Restore calling location
popd

# Show the output files
echo '>>> Packages are in following location:'
echo "$OUTPUT_DIR"
ls "$OUTPUT_DIR"
