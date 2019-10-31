# Own Public Blockchain Node Database

This document describes how to access the database containing the state of the blockchain node.


## PostgreSQL

```bash
sudo -u postgres psql own_public_blockchain_ins1
```


## Firebird

Add `isql` and `firebird.msg` files from the Firebird distribution package to the node software directory, and then:

```bash
cd /path/to/node/software
cp libfbclient.so libfbclient.so.2 # isql needs libfbclient.so.2
export LD_LIBRARY_PATH="$(pwd)"
export FIREBIRD="$(pwd)"
./isql -user SYSDBA Networks/Main/State.fdb
```
