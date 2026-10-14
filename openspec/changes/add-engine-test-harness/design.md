# Design

## Context

See proposal.md for why. After `add-bot-service`, every bot implements `IGameBot.ChooseActionAsync(GameStateEnvelopeDto, SeatId, BotDifficulty, ct)` and lives in `ChessLab.Bots`; ChessLab.Core sessions build per-seat DTOs and apply actions. Randomness in Core (Card Chess decks, Arcane spell decks, Draft and Martian set-up if any) currently comes from `Random` instances inside the game states. The home server (4 cores) serves live bot traffic; the developer machine hosts the self-hosted GitHub runners, started by hand.

## Goals / Non-Goals

**Goals:**
- One arena that measures any bot of any mode with the same referee the players get.
- Cheap, deterministic signals (bench, perft, tactics) on every pull request; expensive statistical signal (SPRT) only when behaviour changed.
- Variance reduction good enough that SPRT finishes overnight on 4 to 8 cores for typical bounds.

**Non-Goals:**
- Distributed testing across machines (fishtest style); one machine per run.
- Opening book generation tooling beyond a simple script; suites are curated files.

## Decisions

### D1. In-process arena, Core as referee
`tools/ChessLab.Arena` references ChessLab.Core and ChessLab.Bots and calls bots directly, building each request with `session.ToStateDto(seat.Side)` exactly as the backend does. No HTTP, so thousands of games per hour fit on a few cores. Concurrency: N games in parallel, one bot search per core, with `--concurrency` defaulting to processor count minus one.
- Alternative: drive the deployed bot service over HTTP. Rejected for speed and because the service's queue would distort time budgets.
- Alternative: cutechess-cli. It does not know these variants and cannot use Core as referee.

### D2. Deterministic randomness via an injected seed
Core game states gain an optional seed (or `Random` factory) at creation, used for every shuffle and draw. The arena uses the same seed for both games of a pair. Production keeps unseeded randomness. This is a Core change with unit tests showing that equal seeds produce equal decks.
- Bots that use randomness (noise for lower difficulties, Arcane 30 percent spell chance) take their seed from the arena too, so a whole match replays exactly from its seed list.

### D3. SPRT on pentanomial pair results
Results are counted per pair (0, 0.5, 1, 1.5, 2 points) and evaluated with the pentanomial generalized SPRT used by fishtest and OpenBench, with bounds in normalized Elo [0, 5] for gating and alpha = beta = 0.05. Pentanomial statistics account for the correlation that pairing introduces and are what make pairing pay off.
- Default game limit 40000 games; time control per mode expressed as a per-action budget (default 100 ms, engines may scale by phase).

### D4. Bench as fingerprint
Each engine exposes `Bench(depth)` over `tests/engine-data/<mode>/bench.txt` (about 30 positions), single-threaded, transposition table cleared between positions. The expected node count is stored in `tests/engine-data/<mode>/bench.expected`; a pull request that changes behaviour updates that file, which is how CI detects "behaviour changed, SPRT required". Nodes per second is compared against `main` measured on the same runner in the same job to tolerate machine differences.

### D5. Perft references generated from Core
`arena perft --reference` walks Core's `AvailableMoves` with a cloned state; it is slow but authoritative, run once per position and stored in `perft.txt`. This needs a way to copy Core game states: a `Clone()` on each `GameState` (test-only use, so simplicity over speed). Differential fuzzing uses the same clone and random seeds, with failures written as reproducible position text plus seed.

### D6. Gate wiring
- Fast gates run in `tests/ChessLab.Engine.Tests` (xUnit) and the `arena bench`/`arena tactics` commands in the existing PR workflow on the `chesslab` runner.
- A separate workflow `engine-strength.yml` triggers on pull requests touching `src/ChessLab.Bots/**` or `tests/engine-data/**`. It compares each mode's `bench.expected` with `main`; for every changed mode it runs `arena sprt` on the `chesslab-sprt` runner and posts the report as the required check `engine-strength`. Unchanged modes pass immediately. A branch protection rule makes `engine-strength` required.
- Which machine is `chesslab-sprt` is configuration: the developer machine by default (faster turnaround while it is on), the home server as a second runner registered with lowered priority.

### D7. Nightly job on the home server
A systemd timer runs the arena container at night with `nice 19` and `--concurrency 2`. Before each pair it reads the bot service's queue depth from a new internal metrics endpoint and waits while it is non-zero. Blunder mining reads finished games from PostgreSQL (`GameRecord` moves) for full-information modes only (Alice, Absorption, Martian, Draft play phase, Progressive, Hand and Brain), because hidden-information modes cannot be replayed without their seeds. Results go to `~/chesslab-arena/` on the server and the review list is a file the developer turns into tactics positions by hand.

### D8. Anchors
Anchors are bot builds pinned by commit hash, built once into `~/chesslab-arena/anchors/<mode>/<hash>/`. The first anchors are the current greedy and Stockfish bots from `add-bot-service`. An Elo ladder per mode is kept as a CSV that the nightly job appends to.

## Risks / Trade-offs

- [SPRT blocks merges for hours] → Accepted by decision; bounds can be widened per pull request for large expected gains (for example [0, 10]) by a label, and refactors that keep bench need no SPRT.
- [Runners are started by hand] → The strength check stays pending until a `chesslab-sprt` runner is online; the workflow summary says so.
- [Seeded Core randomness leaks into production] → The seed parameter is optional and only the arena sets it; a test asserts two unseeded games differ.
- [Nightly job slows live games] → nice 19, concurrency 2 and the queue-depth wait; the bot service's own workers keep their cores.
- [Blunder threshold in "centipawns" is mode-specific] → Each engine defines its evaluation scale; the threshold is per mode in `tests/engine-data/<mode>/mining.json`.
- [Clone() on Core states drifts from real state] → Covered by the differential fuzz itself: a broken clone produces mismatches.

## Migration Plan

Additive tooling; no production behaviour changes except the optional seed in Core. Register the `chesslab-sprt` runner, add the branch protection rule for `engine-strength` after the first engine change lands, so the rule never blocks with nothing to measure.

## Open Questions

- Exact per-mode time budget for SPRT games; tuned when the first engine lands and recorded in that mode's `tests/engine-data/<mode>/arena.json`.
