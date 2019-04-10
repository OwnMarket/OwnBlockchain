#!/bin/bash

set -e

# .NET Core
if [ ! -f /usr/bin/dotnet ]; then
    sudo apt-get install -y wget
    wget -q -O /tmp/packages-microsoft-prod.deb https://packages.microsoft.com/config/ubuntu/18.04/packages-microsoft-prod.deb
    sudo dpkg -i /tmp/packages-microsoft-prod.deb
    sudo apt-get install -y apt-transport-https
    sudo apt-get update -y
    sudo apt-get install -y dotnet-runtime-2.1
fi

# libtommath (needed for Firebird 3.0)
if [ ! -f /usr/lib/x86_64-linux-gnu/libtommath.so.0 ]; then
    if [ ! -f /usr/lib/x86_64-linux-gnu/libtommath.so.1 ]; then
        sudo apt-get update -y
        sudo apt-get install -y libtommath1
    fi
    sudo ln -s /usr/lib/x86_64-linux-gnu/libtommath.so.1 /usr/lib/x86_64-linux-gnu/libtommath.so.0
fi
