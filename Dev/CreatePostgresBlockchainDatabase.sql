\set ON_ERROR_STOP on

\c postgres

CREATE USER own_blockchain_node WITH PASSWORD 'testpass1';

CREATE DATABASE own_public_blockchain
    WITH ENCODING 'UTF8'
    LC_COLLATE = 'C'
    LC_CTYPE = 'C'
    TEMPLATE template0;

\c own_public_blockchain

SET search_path TO public;

-- Create extensions
--CREATE EXTENSION adminpack;

-- Create schemas
CREATE SCHEMA IF NOT EXISTS own;

-- Set default permissions
ALTER DEFAULT PRIVILEGES
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO own_blockchain_node;

ALTER DEFAULT PRIVILEGES
GRANT SELECT, USAGE ON SEQUENCES TO own_blockchain_node;

-- Set permissions on schemas
GRANT ALL ON SCHEMA public TO postgres;
GRANT USAGE ON SCHEMA public TO own_blockchain_node;

GRANT ALL ON SCHEMA own TO postgres;
GRANT USAGE, CREATE ON SCHEMA own TO own_blockchain_node;
