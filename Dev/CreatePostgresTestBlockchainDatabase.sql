\set ON_ERROR_STOP on

\c postgres

CREATE USER chainium_tests WITH PASSWORD 'testpass1';

CREATE DATABASE chainium_public_blockchain;
\c chainium_public_blockchain

SET search_path TO public;

-- Create extensions
CREATE EXTENSION adminpack;

-- Create schemas
CREATE SCHEMA IF NOT EXISTS chainium;

-- Set default permissions
ALTER DEFAULT PRIVILEGES
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO chainium_tests;

ALTER DEFAULT PRIVILEGES
GRANT SELECT, USAGE ON SEQUENCES TO chainium_tests;

-- Set permissions on schemas
GRANT ALL ON SCHEMA public TO postgres;
GRANT USAGE ON SCHEMA public TO chainium_tests;

GRANT ALL ON SCHEMA chainium TO postgres;
GRANT USAGE, CREATE ON SCHEMA chainium TO chainium_tests;
