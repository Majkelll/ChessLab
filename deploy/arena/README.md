# Nightly arena on the home server

Every night a systemd timer plays the current bots against the anchors (frozen bot versions
pinned by commit hash) and mines bot blunders from the day's finished games. It runs the
`ghcr.io/majkelll/chesslab-arena` image, which the `Build and deploy` workflow publishes on every
push to `main`, tagged `latest` and with the short commit hash.

The job never slows live games on purpose:

- every arena process runs under `nice -n 19` (the image's entrypoint) and Docker caps it at
  `ARENA_CONCURRENCY` CPUs (2 by default, with 2 games in parallel);
- before each game pair it reads the bot service's queue at `http://127.0.0.1:51822/internal/queue`
  and waits while any request is queued or running (see `deploy/bot-service/README.md`).

Results stay on the server in `~/chesslab-arena/`:

| Path | Content |
| --- | --- |
| `anchors/<mode>/<hash>/` | anchor builds, one per mode and commit |
| `ladder.csv` | one row per match: date, mode, commit, player, opponent, games, score, Elo and its 95% interval |
| `review/<mode>/review.txt` | blunder positions in the tactics format (`<better action> <position>`) to turn into tactics by hand |

Blunder mining only covers modes whose finished games can be replayed from the stored moves:
Hand and Brain, Progressive, Alice, Absorption and Martian Chess. Card, Arcane and Bidding Chess
hide information that is not stored, and Draft Chess records do not keep the drafted layout.
The threshold and search depth per mode are in `tests/engine-data/<mode>/mining.json`.

## One-time setup

1. Copy the templates and the environment file:

   ```sh
   mkdir -p ~/chesslab-arena ~/.config/systemd/user
   cd ~/chesslab-arena
   # copy nightly.sh, build-anchor.sh and .env.example from deploy/arena/ in the repository
   cp .env.example .env
   chmod +x nightly.sh build-anchor.sh
   ```

   Put the PostgreSQL connection string in `CHESSLAB_DB`. Leave it empty to skip mining.

2. Build the first anchor from the commit that is deployed now (the image tag is its short hash):

   ```sh
   ./build-anchor.sh <hash>
   ```

3. Install and start the timer:

   ```sh
   cp chesslab-arena.service chesslab-arena.timer ~/.config/systemd/user/
   systemctl --user daemon-reload
   systemctl --user enable --now chesslab-arena.timer
   loginctl enable-linger "$USER"
   ```

## Manual run

```sh
systemctl --user start chesslab-arena.service
journalctl --user -u chesslab-arena.service -f
```

To check that the job yields to players, start a game against a bot while it runs: the log
prints `Bot service busy (...), waiting` and no new pair starts until the queue is empty.

Add a new anchor after each engine change that passed its SPRT, with `./build-anchor.sh <hash>`;
the next night's ladder includes it.
