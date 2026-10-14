# Proposal

## Why

Every mode is going to get its own fast search engine, and "the tests pass" says nothing about whether an engine plays better or worse after a change. Without a measuring tool, strength regressions and rule bugs in duplicated move generators would reach players unnoticed. This change builds the tooling and the CI gates first, so each per-mode engine change is accepted only on measured evidence.

## What Changes

- New match runner that plays bot against bot for any mode with ChessLab.Core as the referee, using paired games (same seed for every random draw, colours swapped) and per-mode opening suites to cut variance.
- SPRT (sequential probability ratio test) over match results, reporting Elo difference with error bars and stopping as soon as the result is decided.
- Anchors: frozen bot versions (today's greedy and Stockfish bots first) to track strength over time and to calibrate difficulty levels.
- `bench` command per engine: fixed positions searched to fixed depth, printing total nodes (a behaviour fingerprint) and nodes per second.
- Correctness tooling for engines with their own move generator: perft against reference counts from Core, differential fuzzing (random games, engine moves equal Core moves), and invariant checks (make/unmake restores state and hash, mirrored evaluation).
- Tactics suites per mode in a plain-text format, with the solved count tracked per engine.
- CI gates: fast gates on every pull request; a hard SPRT gate on any pull request that changes an engine's bench fingerprint; difficulty calibration check; allocation-free search check.
- Nightly job on the home server (lowered priority, yields to live bot traffic) that plays anchor matches and mines blunders from finished human games into new tactics positions.

## Capabilities

### New Capabilities
- `engine-testing`: how bot engines are measured and gated: match runner, SPRT, anchors, bench fingerprint, perft and differential checks, tactics suites, difficulty calibration and the CI gates that use them.

### Modified Capabilities
- `hosting-and-persistence`: new requirement for the engine gates and the required strength check in CI (added separately so it does not conflict with `add-bot-service`, which modifies the existing CI requirement).

## Out of scope

- Any engine or bot strength change; this change only measures. Per-mode engines come in later changes, each of which adds its perft references, bench positions, tactics suite and passes the gates.
- A web UI for results; results are CI check output and files.
- Rating games against humans.

## Impact

- Code: new `tools/ChessLab.Arena` (console app: `match`, `sprt`, `bench`, `perft`, `fuzz`, `tactics`, `calibrate`), new test project `tests/ChessLab.Engine.Tests`, test data under `tests/engine-data/<mode>/` (opening suites, perft references, bench positions, tactics).
- Depends on `add-bot-service`: bots take a `GameStateEnvelopeDto`, which the arena builds from Core sessions exactly as the backend does.
- CI: new jobs on the self-hosted runners; SPRT runs take hours and block merging of engine-changing pull requests until they pass.
- Home server: nightly CPU use at lowered priority, results stored on its disk.
