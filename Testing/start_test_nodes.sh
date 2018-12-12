#!/bin/bash

set -e

pushd "${0%/*}" # Go to script directory
cd TestInstances
APP_COMMAND="dotnet ../../../Source/Own.Blockchain.Public.Node/bin/Release/netcoreapp2.1/Own.Blockchain.Public.Node.dll"

tmux new-session -s "blockchain-test" -d

tmux split-window -v -d
tmux send-keys "cd Ins1" C-m
tmux send-keys "$APP_COMMAND" C-m

tmux split-window -h
tmux send-keys "cd Ins2" C-m
tmux send-keys "$APP_COMMAND" C-m

tmux select-pane -D
tmux send-keys "cd Ins3" C-m
tmux send-keys "$APP_COMMAND" C-m

tmux split-window -h
tmux send-keys "cd Ins4" C-m
tmux send-keys "$APP_COMMAND" C-m

tmux -2 -CC attach-session -d

popd # Go back to caller directory
