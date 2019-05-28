#!/bin/bash

export COPYFILE_DISABLE=true # Prevent packing of resource fork files on macOS (./._xxxxx)

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
for PLATFORM_ID in linux-x64 osx-x64 win-x64; do
    pushd ../Source/Own.Blockchain.Public.Node
    mkdir -p "$TEMP_DIR/Node"
    dotnet publish -c Release -r $PLATFORM_ID -o "$TEMP_DIR/Node"
    popd

    rm "$TEMP_DIR/Node/Genesis.json"
    rm "$TEMP_DIR/Node/Config.json"

    cp -r ~/.nuget/packages/secp256k1.net/0.1.48/content/native "$TEMP_DIR/Node"
    cp -r ./Include/Firebird/$PLATFORM_ID/* "$TEMP_DIR/Node"
    cp -r ./Include/Scripts/$PLATFORM_ID/* "$TEMP_DIR/Node"
    cp -r ./Include/Configs/* "$TEMP_DIR/Node"

    pushd "$TEMP_DIR/Node"
    find . -type f -iname "*.sh" -exec chmod +x {} \;
    git rev-parse HEAD > Version
    if [ $PLATFORM_ID = "win-x64" ]; then
        zip -r "$OUTPUT_DIR/OwnPublicBlockchainNode_$PLATFORM_ID.zip" *
    else
        tar czf "$OUTPUT_DIR/OwnPublicBlockchainNode_$PLATFORM_ID.tar.gz" *
    fi
    popd
    rm -rf "$TEMP_DIR/Node"
done

# Build the Faucet
pushd ../Source/Own.Blockchain.Public.Faucet
mkdir -p "$TEMP_DIR/Faucet"
dotnet publish -c Release -r linux-x64 -o "$TEMP_DIR/Faucet"
popd

cp -r ~/.nuget/packages/secp256k1.net/0.1.48/content/native "$TEMP_DIR/Faucet"

pushd "$TEMP_DIR/Faucet"
git rev-parse HEAD > Version
tar czf "$OUTPUT_DIR/OwnPublicBlockchainFaucet_linux-x64.tar.gz" *
popd
rm -rf "$TEMP_DIR/Faucet"

# Cleanup
rm -rf "$TEMP_DIR"

# Restore calling location
popd

# Show the output files
echo '>>> Packages are in following location:'
echo "$OUTPUT_DIR"
ls "$OUTPUT_DIR"
