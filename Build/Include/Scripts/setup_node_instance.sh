#!/bin/bash

set -e

INSTANCE_NAME=$1

if [ -z "$INSTANCE_NAME" ]; then
    echo "ERROR: Node instance name not specified."
    exit 1;
fi

DATA_DIR=/var/lib/own/blockchain/public/node
INSTANCE_DIR="$DATA_DIR/$INSTANCE_NAME"
INSTANCE_DB=own_public_blockchain_$INSTANCE_NAME
INSTANCE_USER=${INSTANCE_DB}_user
INSTANCE_PASS=$(LC_ALL=C tr -dc 'A-Za-z0-9' </dev/urandom | head -c 20 ; echo)

if [ -f "$INSTANCE_DIR/Config.json" ]; then
    echo "ERROR: Node instance configuration already exsits for '$INSTANCE_NAME'."
    exit 1;
fi

echo '////////////////////////////////////////////////////////////////////////////////'
echo '// Database'
echo '////////////////////////////////////////////////////////////////////////////////'
sudo -u postgres psql << EOF
\set ON_ERROR_STOP on

\c postgres

CREATE USER $INSTANCE_USER WITH PASSWORD '$INSTANCE_PASS';

CREATE DATABASE $INSTANCE_DB WITH ENCODING 'UTF8' LC_COLLATE = 'C' LC_CTYPE = 'C' TEMPLATE template0;
\c $INSTANCE_DB

SET search_path TO public;

-- Create extensions
CREATE EXTENSION adminpack;

-- Create schemas
CREATE SCHEMA IF NOT EXISTS own;

-- Set default permissions
ALTER DEFAULT PRIVILEGES
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO $INSTANCE_USER;

ALTER DEFAULT PRIVILEGES
GRANT SELECT, USAGE ON SEQUENCES TO $INSTANCE_USER;

-- Set permissions on schemas
GRANT ALL ON SCHEMA public TO postgres;
GRANT USAGE ON SCHEMA public TO $INSTANCE_USER;

GRANT ALL ON SCHEMA own TO postgres;
GRANT USAGE, CREATE ON SCHEMA own TO $INSTANCE_USER;
EOF

echo '////////////////////////////////////////////////////////////////////////////////'
echo '// Data directory and configuration'
echo '////////////////////////////////////////////////////////////////////////////////'
sudo mkdir -p "$INSTANCE_DIR"
sudo cp Networks/Main/Genesis.json "$INSTANCE_DIR/Genesis.json"
sudo cp Config.json.template "$INSTANCE_DIR/Config.json"

sudo sed -i -- 's/"DbEngineType".*$/"DbEngineType": "Postgres",/g' "$INSTANCE_DIR/Config.json"
sudo sed -i -- "s/\"DbConnectionString\".*$/\"DbConnectionString\": \"server=localhost;database=$INSTANCE_DB;user id=$INSTANCE_USER;password=$INSTANCE_PASS;searchpath=own\",/g" "$INSTANCE_DIR/Config.json"

echo '////////////////////////////////////////////////////////////////////////////////'
echo '// Starting the instance'
echo '////////////////////////////////////////////////////////////////////////////////'
sudo systemctl enable own-blockchain-public-node@$INSTANCE_NAME

echo 'To start the service:'
echo "    sudo systemctl start own-blockchain-public-node@$INSTANCE_NAME"
echo "    sudo systemctl status own-blockchain-public-node@$INSTANCE_NAME"
echo 'To see the service logs:'
echo "    journalctl -fu own-blockchain-public-node@$INSTANCE_NAME"
echo 'To remove the service:'
echo "    sudo systemctl stop own-blockchain-public-node@$INSTANCE_NAME"
echo "    sudo systemctl disable own-blockchain-public-node@$INSTANCE_NAME"
echo "    sudo systemctl daemon-reload"
