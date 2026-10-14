# Proposal

## Why

Bots look random: five modes use a one-move-deep greedy bot that picks uniformly among all moves whenever no capture is available, and Easy plays a uniformly random move. Making every mode's bot strong needs dedicated search engines with far more CPU than the Render host should spend, so bots move to their own service on the home server. This change builds that service and the backend's side of it with today's bot behaviour, so later changes can replace each mode's bot without touching the backend again.

## What Changes

- New standalone `ChessLab.BotService` (ASP.NET Core, container image) hosted on the home server next to the PostgreSQL container. It exposes an authenticated HTTP API that receives a seat's view of the game and returns one `GameAction`, and a `/health` endpoint.
- The request carries exactly the state a human in that seat receives (`IRoomSession.ToStateDto(seatSide)`), so a bot can never see hidden information (Arcane hands, an unrevealed Bidding bid).
- The service runs a fixed pool of workers with one Stockfish process each instead of one process per room, and queues requests beyond the pool.
- Today's bot logic (`ChessLab.Bots`) moves into the service and is rewritten to work from the request state only. Bot strength does not change in this change.
- **BREAKING** The backend no longer runs bots or Stockfish: `ChessLab.Bots` and the `stockfish` package leave the web image, `Bots:StockfishPath` moves to the service, and the backend gains `Bots:ServiceUrl` and `Bots:ServiceKey`.
- The backend tracks whether the bot service is available. While it is not, filling a seat with a bot and starting a game that has a bot seat are rejected, and the lobby shows bots as unavailable.
- When a bot seat must act and its action cannot be obtained, the game pauses: both clocks stop, actions other than resignation are rejected, the room gets a notice, and the backend keeps retrying. A successful bot action resumes the game without charging the paused time to any clock.
- **BREAKING** A pause lasting 5 minutes ends the game with the new end reason `BotUnavailable`: the side without any bot seat wins, and the game is a draw when both sides hold a bot seat. This replaces the current "give up after 3 failures and stay idle" handling.
- The game page shows the pause with a countdown, and the result banner and history label the new end reason.
- `/hc` gains a `bots` check that reports the bot service as Degraded when it is unreachable.
- CI builds and publishes the bot service image and deploys it to the home server.

## Capabilities

### New Capabilities
- `bot-service`: the standalone bot service: its API and authentication, what a request may contain, the worker pool and queue, its health endpoint, configuration and container image.

### Modified Capabilities
- `bots`: bots act through the bot service; the failure, retry and give-up handling becomes the pause and `BotUnavailable` ending; engine processes are pooled in the service instead of started per room. The per-mode decision rules (greedy scoring, Arcane spells, Bidding bids, Draft drafting and placement, Hand and Brain probing) keep their current behaviour and move to the service.
- `game-session`: a game can be paused while a bot is unavailable (clocks stop, actions other than resignation are rejected); new end reason `BotUnavailable`.
- `rooms`: bot seats cannot be filled, and games with bot seats cannot be started, while the bot service is unavailable; the room state says whether bots are available.
- `health-report`: new `bots` check.
- `hosting-and-persistence`: new backend configuration `Bots:ServiceUrl` and `Bots:ServiceKey`; the web image no longer contains Stockfish; CI builds, publishes and deploys the bot service image.
- `web-client`: paused-game notice with countdown, lobby state for unavailable bots, label for the `BotUnavailable` result.

## Out of scope

- Stronger bots. Each mode's own engine (search, evaluation, difficulty by search budget and noise) is a separate later change per mode.
- The engine test harness (match runner, SPRT, perft and differential tests, bench, tactics suites and the CI gates built on them); captured in `add-engine-test-harness`.
- Any fallback bot in the backend. When the service is down there are no bots.
- Running more than one bot service instance, and persisting live games across backend restarts.

## Impact

- Code: new `src/ChessLab.BotService` and test project; `src/ChessLab.Bots` reworked to take a `GameStateEnvelopeDto` instead of an `IRoomSession`; `src/ChessLab.Web/Services/BotRunner.cs` replaced by an HTTP client, an availability monitor and pause handling; `ClockWatchdog`, `GameHub` (`SetSeatBot`, `StartGame`, `PerformAction`), `RoomSession` (pause, turn timer shift), `GameEndReason`, `RoomStateDto`, both game envelopes, `GamePage.razor`, lobby and history pages, `/hc`.
- Wire contracts: `GameEndReason.BotUnavailable`, `RoomStateDto.BotsAvailable`, a pause deadline on the game envelopes. Clients and server ship together, so no versioning beyond the bot service API version.
- Infrastructure: a second container on the home server (i5-6500, 4 cores, 15 GB RAM, Docker, ufw, fail2ban) reachable from Render through a forwarded port, as PostgreSQL already is; TLS and an API key on that port; a second image in GHCR; a deploy step from the self-hosted runner to the home server.
- Dockerfile: Stockfish moves from the web image to the bot service image.
