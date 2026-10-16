#!/usr/bin/env bash
set -euo pipefail

if [ $# -ne 1 ]; then
  echo "usage: build-anchor.sh <commit hash used as the arena image tag>" >&2
  exit 2
fi

ARENA_HOME="${ARENA_HOME:-$HOME/chesslab-arena}"
IMAGE="ghcr.io/majkelll/chesslab-arena:$1"
MODES="hand-and-brain card-chess arcane-chess bidding-chess progressive-chess alice-chess absorption-chess martian-chess draft-chess"

docker pull --quiet "$IMAGE"
container="$(docker create "$IMAGE")"
trap 'docker rm -f "$container" >/dev/null' EXIT

for mode in $MODES; do
  target="$ARENA_HOME/anchors/$mode/$1"
  mkdir -p "$target"
  docker cp "$container:/app/." "$target"
done

echo "Anchor $1 is ready under $ARENA_HOME/anchors"
