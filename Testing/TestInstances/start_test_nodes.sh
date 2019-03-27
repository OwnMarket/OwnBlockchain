#!/bin/bash

set -e

pushd "${0%/*}" # Go to script directory

APP_DIR="../../../Source/Own.Blockchain.Public.Node/bin/Release/netcoreapp2.1"
APP_COMMAND="dotnet $APP_DIR/Own.Blockchain.Public.Node.dll"
FIREBIRD_DIR="$(cd Ins1 && cd $APP_DIR && pwd)"

tmux new-session -s "blockchain-test" -d

tmux split-window -v -d
tmux send-keys "cd Ins1" C-m
tmux send-keys "export FIREBIRD=$FIREBIRD_DIR" C-m
tmux send-keys "$APP_COMMAND" C-m

tmux split-window -h
tmux send-keys "cd Ins2" C-m
tmux send-keys "export FIREBIRD=$FIREBIRD_DIR" C-m
tmux send-keys "$APP_COMMAND" C-m

tmux select-pane -D
tmux send-keys "cd Ins3" C-m
tmux send-keys "export FIREBIRD=$FIREBIRD_DIR" C-m
tmux send-keys "$APP_COMMAND" C-m

tmux split-window -h
tmux send-keys "cd Ins4" C-m
tmux send-keys "export FIREBIRD=$FIREBIRD_DIR" C-m
tmux send-keys "$APP_COMMAND" C-m

tmux -2 -CC attach-session -d

popd # Go back to caller directory
