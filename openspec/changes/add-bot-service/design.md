# Design

## Context

See proposal.md for why. Today `BotRunner` (src/ChessLab.Web/Services/BotRunner.cs) runs inside the web host: it creates an `IGameBot` and, for four modes, a Stockfish process per room, hands the bot the live `IRoomSession`, and applies the returned `GameAction` under `lock (session)`. The bots read the live `GameState` directly (`HandBrainBot` calls `LegalMovesFor(kind)`, `BiddingChessBot` calls `MovesFor(opponent)`, `DraftChessBot` calls `CanPick`, `ArcaneChessBot` keeps per-instance state in `spellTriedAtMoveCount`).

Constraints:
- The home server is an i5-6500 (4 cores, 4 threads, no SMT), 15 GB RAM, Ubuntu 24.04, Docker, ufw with default deny, fail2ban, and only the .NET 9 runtime installed. PostgreSQL already runs there in Docker and Render reaches it through a forwarded port (51820) with a TLS certificate.
- Live games exist only in the web host's memory; every change to a session happens under `lock (session)` with no `await` inside.
- Per-viewer DTOs already exist: `IRoomSession.ToStateDto(Side? viewer)` leaves out what a side may not see.

## Goals / Non-Goals

**Goals:**
- One request/response contract that every later per-mode engine plugs into without backend changes.
- Fairness by construction: the service cannot receive hidden information.
- Behaviour parity: every current bot decision rule (see the bots spec) produces the same kind of choice from the request state as it did from the live session.
- Clear failure semantics: unavailability is visible, bounded (5 minutes) and never costs a human the game.

**Non-Goals:**
- Stronger play, new search code, the test harness (separate changes).
- High availability, multiple service instances, request retries across instances.

## Decisions

### D1. Stateless HTTP+JSON service, request = `GameStateEnvelopeDto` for the seat's side
`POST /v1/action` with `{ kind, seat, difficulty, state }`, where `state` is `session.ToStateDto(seat.Side)` built under the session lock. Response is a `GameAction` serialized with System.Text.Json.
- Why: the DTO is already the "what a human may see" boundary and is already tested per mode; reusing it makes the fairness rule a one-liner, and statelessness makes restarts, deploys and crashes harmless.
- Alternatives: gRPC (extra tooling, no real gain at one request per bot turn); a stateful service mirroring rooms (needs sync, breaks on restart); sending the full `GameState` (violates the fairness decision).
- `ToStateDto` is used rather than `ToUpdateDto` because it carries the full move history, which later engines need for repetition and for reconstructing public information (cards played, spells cast).

### D2. Bots rewritten against a read-only view built from the DTO
`IGameBot.ChooseActionAsync(GameStateEnvelopeDto state, SeatId seat, BotDifficulty difficulty, ct)`. Where a bot needs derived public information, it computes it from `PositionText` with ChessLab.Core rules: Hand and Brain legal moves per kind and Bidding pseudo-legal moves for both sides from the FEN, Draft affordability from the section's pool and budgets. Arcane's "consider a spell once per move" becomes stateless: decide from `SideToMoveHasCast` (cast already this turn, so move) and draw the 30 percent chance per request, which yields the same distribution because the backend sends one request per round.
- If a bot needs information that a human could know but the DTO lacks, the DTO section gains it (it is public by definition) and the session's viewer hooks decide visibility, per the project rule for new state.

### D3. Worker pool with one Stockfish per worker; bounded queue
`BotService:Workers` workers (default `Environment.ProcessorCount`, 4 on the home server), each owning one long-lived Stockfish process, fed from a `Channel<T>` in arrival order. Requests older than 15 seconds when dequeued are answered 503, so the backend's 20 second limit is never silently exceeded. A worker whose engine dies restarts it before taking the next item; `/health` is 503 while any engine is down.
- Why: on 4 cores, one process per room oversubscribes the CPU under load and makes thinking time meaningless; a pool keeps each search on its own core.
- Non-engine bots (greedy modes) run on the same workers, so one queue governs all CPU use.

### D4. Backend side: `BotServiceClient`, `BotAvailability`, pause in the session
- `BotServiceClient`: typed `HttpClient` with base address `Bots:ServiceUrl`, bearer key, 20 second timeout.
- `BotAvailability` (singleton + hosted service): probes `GET /health` every 10 seconds, also updated by every action request outcome; on a flip it raises an event that `GameBroadcaster` turns into `RoomUpdated` for unlocked rooms. Starts unavailable.
- Pause lives in `RoomSession` (Core): `PausedAt`, `PauseDeadline`, `Pause(now)`, `Resume(now)`, `EndBecauseBotUnavailable(now)`. `Resume` shifts `TurnStartedAt` (and any per-side turn starts such as Bidding's) forward by the paused duration, so the existing elapsed-time math needs no change. `Apply` throws `InvalidOperationException("The game is paused because a bot is unavailable.")` while paused; `Resign` stays allowed. `DeclareTimeoutIfExpired` returns early while paused.
- `BotRunner` keeps its round loop and room semaphore but replaces engine/bot creation with the client; any failure (HTTP error, timeout, 5 rejected rounds) pauses the game under the lock and schedules a retry every 5 seconds. `ClockWatchdog` ends games whose `PauseDeadline` has passed, since it already ticks every second, then broadcasts and archives as for a timeout.
- The winner rule (side with no bot seat wins, otherwise draw) is computed in the session from `Room.Seats`, so it is unit-testable in Core.
- `GameEndReason.BotUnavailable` is appended to the enum (stored as a string, 14 characters, within the 32-character column), so no migration is needed.

### D5. Exposure: forwarded port, TLS with a pinned self-signed certificate, API key
The service container listens on 8080 inside Docker; a TLS-terminating reverse proxy container (Caddy) on the home server serves a new forwarded port. Without a domain name, Caddy uses a self-signed certificate (as PostgreSQL does), and the backend pins it with `Bots:ServiceCertificateThumbprint`. The API key (`BotService:ApiKey`, at least 32 characters, compared in constant time) authenticates the backend. ufw opens only the new port; fail2ban needs no change because the service has no login.
- Alternatives: plain HTTP (key travels in clear text over the internet, rejected); WireGuard between Render and home (Render cannot run a WireGuard client); Cloudflare Tunnel (adds an external dependency and account, possible later).

### D6. Deploy: self-hosted runner pushes over SSH
After both images are pushed to GHCR, the `chesslab` runner (on the LAN) runs `ssh <user>@<server LAN IP> -p <SSH port> 'docker compose -f ~/chesslab-bots/compose.yml pull && docker compose -f ~/chesslab-bots/compose.yml up -d'` with the image tag passed as an environment variable, then polls `https://<public address>/health` until it reports the new version (2 minute limit) before deploying the web image to Render. Bot service first, because the old backend talks to the new service and not the other way around, as long as `/v1/action` stays compatible.
- The compose file and secrets (`BotService:ApiKey`, Caddy certificate) live on the server, not in the repo; the repo carries a template under `deploy/bot-service/`.

### D7. Local development
`docker-compose.yml` gains the bot service (no TLS, key from `.env`), and Development defaults `Bots:ServiceUrl` to `http://localhost:5090`, where `dotnet run --project src/ChessLab.BotService` also listens. Tests in ChessLab.Web.Tests replace the client with an in-process fake (`WebApplicationFactory` + `TestServer` of the bot service, or a stub `HttpMessageHandler` for failure cases), so no Stockfish is needed in CI except for the bot service's own tests, which skip engine tests when `stockfish` is not on `PATH`, as E2E does today.

## Risks / Trade-offs

- [Home server or home internet goes down] → Bots are unavailable; new bot games are blocked and running ones end in 5 minutes without hurting humans. Accepted by decision; `/hc` shows Degraded.
- [Render to home latency adds to bot think time] → 20 to 150 ms against a 400 to 899 ms artificial delay; negligible.
- [DTO lacks something a current bot reads] → Found while porting each bot (task per mode); fixed by adding public information to the DTO section, guarded by the fairness tests.
- [Self-signed certificate pinning breaks on certificate renewal] → Certificate is long-lived and its thumbprint is configuration; rotating it is a documented two-step change (add new thumbprint, then swap certificate).
- [Public port invites abuse] → API key before any work, 401 is cheap; ufw limits exposure to one port; requests are bounded by the queue's 15 second limit.
- [Pause logic touches every mode's turn timers] → Shift is done in `RoomSession` base for `TurnStartedAt` and in the Bidding override for per-side starts; covered by a Core test per mode that has its own timer.
- [Old web image talking to new service during deploy] → `/v1` path is versioned; breaking contract changes go to `/v2` with both served until the web image catches up.

## Migration Plan

1. Merge with the bot service deployed first (D6). Before the first deploy, set up on the server: `~/chesslab-bots/compose.yml`, API key, Caddy certificate, ufw rule, router port forward; on Render: `Bots:ServiceUrl`, `Bots:ServiceKey`, `Bots:ServiceCertificateThumbprint`.
2. Games running in the old backend during the Render deploy are lost as today (in-memory rooms); no data migration.
3. Rollback: redeploy the previous web image hash on Render; it still contains in-process bots and Stockfish, so it does not need the service. The service can stay running.

## Open Questions

- Public port number for the bot service and whether the router forward is added by hand or with the existing `setup-port-forward-upnp.sh`; does not change the design.
- Whether to replace the self-signed certificate with a domain and Let's Encrypt later; the pinning setting becomes optional then.
