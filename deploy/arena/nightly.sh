#!/usr/bin/env bash
set -euo pipefail

ARENA_HOME="${ARENA_HOME:-$HOME/chesslab-arena}"
[ -f "$ARENA_HOME/.env" ] && . "$ARENA_HOME/.env"

ARENA_IMAGE="${ARENA_IMAGE:-ghcr.io/majkelll/chesslab-arena:latest}"
ARENA_QUEUE_URL="${ARENA_QUEUE_URL:-http://127.0.0.1:51822/internal/queue}"
ARENA_PAIRS="${ARENA_PAIRS:-50}"
ARENA_CONCURRENCY="${ARENA_CONCURRENCY:-2}"

arena() {
  docker run --rm --network host --cpus "$ARENA_CONCURRENCY" \
    -v "$ARENA_HOME:/data" "$ARENA_IMAGE" "$@"
}

docker pull --quiet "$ARENA_IMAGE" || echo "Could not pull $ARENA_IMAGE, using the local copy"

arena ladder --mode all --root /data --csv /data/ladder.csv \
  --pairs "$ARENA_PAIRS" --concurrency "$ARENA_CONCURRENCY" --yield-to "$ARENA_QUEUE_URL"

if [ -n "${CHESSLAB_DB:-}" ]; then
  arena mine --connection "$CHESSLAB_DB" --days 1 --out /data/review
fi
