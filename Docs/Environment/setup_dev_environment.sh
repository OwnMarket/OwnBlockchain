#!/bin/bash

set -e

# Activate color prompt in terminal for improved readability
sed -i -- "s/^#force_color_prompt=yes$/force_color_prompt=yes/g" ~/.bashrc
source ~/.bashrc

# Configure a reliable DNS (Google's 8.8.8.8) in /etc/resolv.conf (if it doesn't already exist)
sudo grep "^nameserver.*8.8.8.8.*$" -q /etc/resolv.conf || sudo sed -i 's/nameserver /nameserver 8.8.8.8 /' /etc/resolv.conf

# Update to latest
sudo apt update
sudo apt upgrade -y

# Common tools
sudo apt install -y curl git vim tmux htop mc

# .NET Core
wget -q https://packages.microsoft.com/config/ubuntu/18.04/packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt install -y apt-transport-https
sudo apt update
sudo apt install -y dotnet-sdk-2.1

# Mono (prerequisite for Ionide)
sudo apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF
echo "deb http://download.mono-project.com/repo/ubuntu xenial main" | sudo tee /etc/apt/sources.list.d/mono-official.list
sudo apt update
sudo apt install -y mono-complete fsharp

# VS Code
wget -O ~/Downloads/vscode.deb https://go.microsoft.com/fwlink/?LinkID=760868
sudo apt install -y ~/Downloads/vscode.deb
code --install-extension Ionide.ionide-fsharp
