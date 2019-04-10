#!/bin/bash

set -e

# libtommath (needed for Firebird 3.0)
if [ ! -f /usr/lib/x86_64-linux-gnu/libtommath.so.0 ]; then
    if [ ! -f /usr/lib/x86_64-linux-gnu/libtommath.so.1 ]; then
        sudo apt-get update -y
        sudo apt-get install -y libtommath1
    fi
    sudo ln -s /usr/lib/x86_64-linux-gnu/libtommath.so.1 /usr/lib/x86_64-linux-gnu/libtommath.so.0
fi
