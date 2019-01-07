namespace Own.Blockchain.Public.Data

open System

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
                    CREATE TABLE db_version (
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
                    CREATE TABLE tx (
                        tx_id INTEGER NOT NULL,
                        tx_hash TEXT NOT NULL,
                        sender_address TEXT NOT NULL,
                        nonce BIGINT NOT NULL,
                        fee DECIMAL(30, 18) NOT NULL,
                        action_count SMALLINT NOT NULL,

                        CONSTRAINT tx__pk PRIMARY KEY (tx_id),
                        CONSTRAINT tx__uk__tx_hash UNIQUE (tx_hash)
                    );
                    CREATE INDEX tx__ix__sender_address ON tx (sender_address);
                    CREATE INDEX tx__ix__fee ON tx (fee DESC);

                    CREATE TABLE chx_balance (
                        chx_balance_id INTEGER NOT NULL,
                        blockchain_address TEXT NOT NULL,
                        amount DECIMAL(30, 18) NOT NULL,
                        nonce BIGINT NOT NULL,

                        CONSTRAINT chx_balance__pk PRIMARY KEY (chx_balance_id),
                        CONSTRAINT chx_balance__uk__blockchain_address UNIQUE (blockchain_address)
                    );

                    CREATE TABLE account (
                        account_id INTEGER NOT NULL,
                        account_hash TEXT NOT NULL,
                        controller_address TEXT NOT NULL,

                        CONSTRAINT account__pk PRIMARY KEY (account_id),
                        CONSTRAINT account__uk__account_hash UNIQUE (account_hash)
                    );

                    CREATE TABLE asset (
                        asset_id INTEGER NOT NULL,
                        asset_hash TEXT NOT NULL,
                        asset_code TEXT,
                        controller_address TEXT NOT NULL,

                        CONSTRAINT asset__pk PRIMARY KEY (asset_id),
                        CONSTRAINT asset__uk__asset_hash UNIQUE (asset_hash),
                        CONSTRAINT asset__uk__asset_code UNIQUE (asset_code)
                    );

                    CREATE TABLE holding (
                        holding_id INTEGER NOT NULL,
                        account_id BIGINT NOT NULL,
                        asset_hash TEXT NOT NULL,
                        amount DECIMAL(30, 18) NOT NULL,

                        CONSTRAINT holding__pk PRIMARY KEY (holding_id),
                        CONSTRAINT holding__uk__account_id__asset_hash UNIQUE (account_id, asset_hash),
                        CONSTRAINT holding__fk__account FOREIGN KEY (account_id)
                            REFERENCES account (account_id)
                    );

                    CREATE TABLE block (
                        block_id INTEGER NOT NULL,
                        block_number BIGINT NOT NULL,
                        block_hash TEXT NOT NULL,
                        block_timestamp BIGINT NOT NULL,
                        is_config_block SMALLINT NOT NULL, -- TODO: Change this to BOOLEAN once supported in both DBs.
                        is_applied SMALLINT NOT NULL, -- TODO: Change this to BOOLEAN once supported in both DBs.

                        CONSTRAINT block__pk PRIMARY KEY (block_id),
                        CONSTRAINT block__uk__number UNIQUE (block_number),
                        CONSTRAINT block__uk__hash UNIQUE (block_hash),
                        CONSTRAINT block__uk__timestamp UNIQUE (block_timestamp)
                    );
                    """
            }
            {
                Number = 3
                Script =
                    """
                    CREATE TABLE validator (
                        validator_id INTEGER NOT NULL,
                        validator_address TEXT NOT NULL,
                        network_address TEXT NOT NULL,

                        CONSTRAINT validator__pk PRIMARY KEY (validator_id),
                        CONSTRAINT validator__uk__validator_address UNIQUE (validator_address)
                    );

                    CREATE TABLE stake (
                        stake_id INTEGER NOT NULL,
                        stakeholder_address TEXT NOT NULL,
                        validator_address TEXT NOT NULL,
                        amount DECIMAL(30, 18) NOT NULL,

                        CONSTRAINT stake__pk PRIMARY KEY (stake_id),
                        CONSTRAINT stake__uk__stakeholder_address__validator_address
                            UNIQUE (stakeholder_address, validator_address)
                    );

                    CREATE TABLE peer (
                        peer_id INTEGER NOT NULL,
                        network_address TEXT NOT NULL,

                        CONSTRAINT peer__pk PRIMARY KEY (peer_id),
                        CONSTRAINT peer__uk__network_address UNIQUE (network_address)
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
                    CREATE TABLE db_version (
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
                    CREATE TABLE tx (
                        tx_id BIGSERIAL NOT NULL,
                        tx_hash TEXT NOT NULL,
                        sender_address TEXT NOT NULL,
                        nonce BIGINT NOT NULL,
                        fee DECIMAL(30, 18) NOT NULL,
                        action_count SMALLINT NOT NULL,

                        CONSTRAINT tx__pk PRIMARY KEY (tx_id),
                        CONSTRAINT tx__uk__tx_hash UNIQUE (tx_hash)
                    );
                    CREATE INDEX tx__ix__sender_address ON tx (sender_address);
                    CREATE INDEX tx__ix__fee ON tx (fee DESC);

                    CREATE TABLE chx_balance (
                        chx_balance_id BIGSERIAL NOT NULL,
                        blockchain_address TEXT NOT NULL,
                        amount DECIMAL(30, 18) NOT NULL,
                        nonce BIGINT NOT NULL,

                        CONSTRAINT chx_balance__pk PRIMARY KEY (chx_balance_id),
                        CONSTRAINT chx_balance__uk__blockchain_address UNIQUE (blockchain_address)
                    );

                    CREATE TABLE account (
                        account_id BIGSERIAL NOT NULL,
                        account_hash TEXT NOT NULL,
                        controller_address TEXT NOT NULL,

                        CONSTRAINT account__pk PRIMARY KEY (account_id),
                        CONSTRAINT account__uk__account_hash UNIQUE (account_hash)
                    );

                    CREATE TABLE asset (
                        asset_id BIGSERIAL NOT NULL,
                        asset_hash TEXT NOT NULL,
                        asset_code TEXT,
                        controller_address TEXT NOT NULL,

                        CONSTRAINT asset__pk PRIMARY KEY (asset_id),
                        CONSTRAINT asset__uk__asset_hash UNIQUE (asset_hash),
                        CONSTRAINT asset__uk__asset_code UNIQUE (asset_code)
                    );

                    CREATE TABLE holding (
                        holding_id BIGSERIAL NOT NULL,
                        account_id BIGINT NOT NULL,
                        asset_hash TEXT NOT NULL,
                        amount DECIMAL(30, 18) NOT NULL,

                        CONSTRAINT holding__pk PRIMARY KEY (holding_id),
                        CONSTRAINT holding__uk__account_id__asset_hash UNIQUE (account_id, asset_hash),
                        CONSTRAINT holding__fk__account FOREIGN KEY (account_id)
                            REFERENCES account (account_id)
                    );

                    CREATE TABLE block (
                        block_id BIGSERIAL NOT NULL,
                        block_number BIGINT NOT NULL,
                        block_hash TEXT NOT NULL,
                        block_timestamp BIGINT NOT NULL,
                        is_config_block SMALLINT NOT NULL, -- TODO: Change this to BOOLEAN once supported in both DBs.
                        is_applied SMALLINT NOT NULL, -- TODO: Change this to BOOLEAN once supported in both DBs.

                        CONSTRAINT block__pk PRIMARY KEY (block_id),
                        CONSTRAINT block__uk__number UNIQUE (block_number),
                        CONSTRAINT block__uk__hash UNIQUE (block_hash),
                        CONSTRAINT block__uk__timestamp UNIQUE (block_timestamp)
                    );
                    """
            }
            {
                Number = 3
                Script =
                    """
                    CREATE TABLE validator (
                        validator_id BIGSERIAL NOT NULL,
                        validator_address TEXT NOT NULL,
                        network_address TEXT NOT NULL,

                        CONSTRAINT validator__pk PRIMARY KEY (validator_id),
                        CONSTRAINT validator__uk__validator_address UNIQUE (validator_address)
                    );

                    CREATE TABLE stake (
                        stake_id BIGSERIAL NOT NULL,
                        stakeholder_address TEXT NOT NULL,
                        validator_address TEXT NOT NULL,
                        amount DECIMAL(30, 18) NOT NULL,

                        CONSTRAINT stake__pk PRIMARY KEY (stake_id),
                        CONSTRAINT stake__uk__stakeholder_address__validator_address
                            UNIQUE (stakeholder_address, validator_address)
                    );

                    CREATE TABLE peer (
                        peer_id BIGSERIAL NOT NULL,
                        network_address TEXT NOT NULL,

                        CONSTRAINT peer__pk PRIMARY KEY (peer_id),
                        CONSTRAINT peer__uk__network_address UNIQUE (network_address)
                    );
                    """
            }
        ]
