namespace Chainium.Blockchain.Public.Data

open System
open Chainium.Common

type DbChange = {
    Number : int
    Script : string
}

module DbChanges =

    let internal sqliteChanges : DbChange list =
        [
            {
                Number = 1
                Script =
                    """
                    CREATE TABLE IF NOT EXISTS db_version (
                        version_number INTEGER NOT NULL,
                        execution_timestamp BIGINT NOT NULL,

                        CONSTRAINT db_version__pk PRIMARY KEY (version_number)
                    );
                    """
            }
            {
                Number = 2
                Script =
                    """
                    CREATE TABLE IF NOT EXISTS tx (
                        tx_id INTEGER NOT NULL,
                        tx_hash TEXT NOT NULL,
                        sender_address TEXT NOT NULL,
                        nonce BIGINT NOT NULL,
                        fee DECIMAL(30, 18) NOT NULL,
                        action_count SMALLINT NOT NULL,

                        CONSTRAINT tx__pk PRIMARY KEY (tx_id),
                        CONSTRAINT tx__uk__tx_hash UNIQUE (tx_hash)
                    );

                    CREATE TABLE IF NOT EXISTS chx_balance (
                        chx_balance_id INTEGER NOT NULL,
                        chainium_address TEXT NOT NULL,
                        amount DECIMAL(30, 18) NOT NULL,
                        nonce BIGINT NOT NULL,

                        CONSTRAINT chx_balance__pk PRIMARY KEY (chx_balance_id),
                        CONSTRAINT chx_balance__uk__chainium_address UNIQUE (chainium_address)
                    );

                    CREATE TABLE IF NOT EXISTS account (
                        account_id INTEGER NOT NULL,
                        account_hash TEXT NOT NULL,
                        controller_address TEXT NOT NULL,

                        CONSTRAINT account__pk PRIMARY KEY (account_id),
                        CONSTRAINT account__uk__account_hash UNIQUE (account_hash)
                    );

                    CREATE TABLE IF NOT EXISTS asset (
                        asset_id INTEGER NOT NULL,
                        asset_hash TEXT NOT NULL,
                        controller_address TEXT NOT NULL,

                        CONSTRAINT asset__pk PRIMARY KEY (asset_id),
                        CONSTRAINT asset__uk__asset_hash UNIQUE (asset_hash)
                    );

                    CREATE TABLE IF NOT EXISTS holding (
                        holding_id INTEGER NOT NULL,
                        account_id BIGINT NOT NULL,
                        asset_hash TEXT NOT NULL,
                        amount DECIMAL(30, 18) NOT NULL,

                        CONSTRAINT holding__pk PRIMARY KEY (holding_id),
                        CONSTRAINT holding__uk__account_id__asset_hash UNIQUE (account_id, asset_hash),
                        CONSTRAINT holding__fk__account FOREIGN KEY (account_id)
                            REFERENCES account (account_id)
                    );

                    CREATE TABLE IF NOT EXISTS block (
                        block_id INTEGER NOT NULL,
                        block_number BIGINT NOT NULL,
                        block_hash TEXT NOT NULL,
                        block_timestamp BIGINT NOT NULL,

                        CONSTRAINT block__pk PRIMARY KEY (block_id),
                        CONSTRAINT block__uk__number UNIQUE (block_number),
                        CONSTRAINT block__uk__hash UNIQUE (block_hash),
                        CONSTRAINT block__uk__timestamp UNIQUE (block_timestamp)
                    );
                    """
            }
        ]

    let internal postgresqlChanges : DbChange list =
        [
            {
                Number = 1
                Script =
                    """
                    CREATE TABLE IF NOT EXISTS db_version (
                        version_number INTEGER NOT NULL,
                        execution_timestamp BIGINT NOT NULL,

                        CONSTRAINT db_version__pk PRIMARY KEY (version_number)
                    );
                    """
            }
            {
                Number = 2
                Script =
                    """
                    CREATE TABLE IF NOT EXISTS tx (
                        tx_id BIGSERIAL NOT NULL,
                        tx_hash TEXT NOT NULL,
                        sender_address TEXT NOT NULL,
                        nonce BIGINT NOT NULL,
                        fee DECIMAL(30, 18) NOT NULL,
                        action_count SMALLINT NOT NULL,

                        CONSTRAINT tx__pk PRIMARY KEY (tx_id),
                        CONSTRAINT tx__uk__tx_hash UNIQUE (tx_hash)
                    );

                    CREATE TABLE IF NOT EXISTS chx_balance (
                        chx_balance_id BIGSERIAL NOT NULL,
                        chainium_address TEXT NOT NULL,
                        amount DECIMAL(30, 18) NOT NULL,
                        nonce BIGINT NOT NULL,

                        CONSTRAINT chx_balance__pk PRIMARY KEY (chx_balance_id),
                        CONSTRAINT chx_balance__uk__chainium_address UNIQUE (chainium_address)
                    );

                    CREATE TABLE IF NOT EXISTS account (
                        account_id BIGSERIAL NOT NULL,
                        account_hash TEXT NOT NULL,
                        controller_address TEXT NOT NULL,

                        CONSTRAINT account__pk PRIMARY KEY (account_id),
                        CONSTRAINT account__uk__account_hash UNIQUE (account_hash)
                    );

                    CREATE TABLE IF NOT EXISTS asset (
                        asset_id BIGSERIAL NOT NULL,
                        asset_hash TEXT NOT NULL,
                        controller_address TEXT NOT NULL,

                        CONSTRAINT asset__pk PRIMARY KEY (asset_id),
                        CONSTRAINT asset__uk__asset_hash UNIQUE (asset_hash)
                    );

                    CREATE TABLE IF NOT EXISTS holding (
                        holding_id BIGSERIAL NOT NULL,
                        account_id BIGINT NOT NULL,
                        asset_hash TEXT NOT NULL,
                        amount DECIMAL(30, 18) NOT NULL,

                        CONSTRAINT holding__pk PRIMARY KEY (holding_id),
                        CONSTRAINT holding__uk__account_id__asset_hash UNIQUE (account_id, asset_hash),
                        CONSTRAINT holding__fk__account FOREIGN KEY (account_id)
                            REFERENCES account (account_id)
                    );

                    CREATE TABLE IF NOT EXISTS block (
                        block_id BIGSERIAL NOT NULL,
                        block_number BIGINT NOT NULL,
                        block_hash TEXT NOT NULL,
                        block_timestamp BIGINT NOT NULL,

                        CONSTRAINT block__pk PRIMARY KEY (block_id),
                        CONSTRAINT block__uk__number UNIQUE (block_number),
                        CONSTRAINT block__uk__hash UNIQUE (block_hash),
                        CONSTRAINT block__uk__timestamp UNIQUE (block_timestamp)
                    );
                    """
            }
        ]
