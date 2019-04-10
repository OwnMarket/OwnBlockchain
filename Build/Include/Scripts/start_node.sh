#!/bin/bash

set -e

WORKING_DIR="$(pwd)"

cd "${0%/*}" # Go to script directory
SCRIPT_DIR="$(pwd)"

./install_prerequisites.sh # Idempotent

cd "$WORKING_DIR"
export FIREBIRD="$SCRIPT_DIR"
"$SCRIPT_DIR/Own.Blockchain.Public.Node"
