# Tasks

## 1. Bot service skeleton

- [ ] 1.1 Create `src/ChessLab.BotService` (ASP.NET Core minimal API, references ChessLab.Core and ChessLab.Bots) and `tests/ChessLab.BotService.Tests`, add both to `ChessLab.slnx`, and verify `dotnet build ChessLab.slnx -c Release` succeeds
- [ ] 1.2 Add `BotService:ApiKey` validation (required, at least 32 characters) and the bearer check on `/v1/action` with constant-time comparison; verify with tests for missing key, wrong key, short key at start-up
- [ ] 1.3 Add `GET /health` reporting `version` (from `APP_VERSION`) and `workers`, 503 while any engine is down; verify with tests using a fake engine

## 2. Worker pool

- [ ] 2.1 Implement the worker pool (`BotService:Workers`, default processor count) with one Stockfish process per worker, fed by an arrival-ordered queue; verify a test with 2 workers and 6 requests processes at most 2 at once in arrival order
- [ ] 2.2 Answer 503 for requests that waited 15 seconds in the queue; verify with a test using a blocked fake engine
- [ ] 2.3 Restart a worker's engine after it exits, answer 500 for the request it was serving, and report 503 on `/health` until restarted; verify with a test that kills the fake engine mid-search
- [ ] 2.4 Make `StockfishEngine` tests skip when `stockfish` is not on `PATH`, matching the E2E convention; verify the suite passes on a runner without Stockfish

## 3. Bots on the seat view

- [ ] 3.1 Change `IGameBot` to take `GameStateEnvelopeDto`, `SeatId`, `BotDifficulty`; port the greedy bots (Alice, Absorption, Martian) and verify existing scoring tests pass against DTO input
- [ ] 3.2 Port the Bidding bot, computing both sides' pseudo-legal moves from `PositionText` with Core rules; verify the bid scenarios in the bots spec with DTO-based tests
- [ ] 3.3 Port the Draft bot (picks from pool and budgets in the section, placement from placeable squares); verify drafting, passing and placement order tests
- [ ] 3.4 Port Card Chess, Progressive and Hand and Brain bots (Hand and Brain derives legal moves per piece kind from the FEN); verify with engine tests that each returns one of the state's available moves
- [ ] 3.5 Port the Arcane bot without instance state (move when `SideToMoveHasCast`, otherwise the spell rules of the bots spec); verify spell selection and target tests from DTO input
- [ ] 3.6 For any information a bot needs that the DTO lacks, add it to the mode's DTO section as public information through the session's viewer hooks; verify with session tests that it is visible exactly to the sides allowed to see it
- [ ] 3.7 Wire `POST /v1/action` to the pool and bots (400 on malformed body or kind mismatch, 422 when the seat has nothing to do); verify with endpoint tests for each status and one success per game kind

## 4. Fairness

- [ ] 4.1 Add Core tests that build two Arcane games differing only in the opponent's hidden hands and assert `ToStateDto(botSide)` serializes identically; same for a Bidding round with different unrevealed opponent bids
- [ ] 4.2 Add a Web test asserting the request body the backend sends equals the JSON of `ToStateDto(seat.Side)` for the bot's seat

## 5. Pause and BotUnavailable in Core

- [ ] 5.1 Add `GameEndReason.BotUnavailable`; verify the history record stores and reads it without a migration (string column of 32 characters)
- [ ] 5.2 Add pause state to `RoomSession` (pause, resume, deadline 5 minutes after pausing, `Apply` rejected with `The game is paused because a bot is unavailable.`, resignation allowed); verify with Core session tests
- [ ] 5.3 Shift turn timers by the paused duration on resume, including Bidding's per-side turn starts and Draft's phases; verify the "3 s + pause + 1 s = 4 s deducted" scenario for each mode with its own timer
- [ ] 5.4 Skip timeout checks while paused; verify a side with 2 seconds left is not flagged during a 1 minute pause
- [ ] 5.5 End a game as BotUnavailable with the winner rule (side without bot seats wins, draw when both sides hold a bot seat); verify human vs bot, Hand and Brain bot partner, and bots-on-both-sides tests
- [ ] 5.6 Publish the pause deadline in `GameStateEnvelopeDto` and `GameUpdateEnvelopeDto`; verify session DTO tests for paused and running games

## 6. Backend integration

- [ ] 6.1 Add `BotServiceClient` (base address `Bots:ServiceUrl`, bearer `Bots:ServiceKey`, optional certificate pinning by `Bots:ServiceCertificateThumbprint`, 20 second timeout); verify with Web tests against an in-process fake service
- [ ] 6.2 Add `BotAvailability` (probe every 10 seconds, updated by action outcomes, unavailable at start and when not configured) and broadcast `RoomUpdated` to unlocked rooms on change; add `BotsAvailable` to `RoomStateDto`; verify with Web tests flipping the fake service
- [ ] 6.3 Rework `BotRunner` to use the client: build the request under the session lock, pause on any failure or 5 rejected rounds, retry every 5 seconds while paused, send the pause and resume `RoomNotice`s; verify with Web hub tests for pause, resume and the human-to-move case
- [ ] 6.4 Make `ClockWatchdog` end games whose pause deadline passed and archive them; verify with a Web test using a controllable time source
- [ ] 6.5 Reject `SetSeatBot` (add or change) and `StartGame` with a bot seat while unavailable with `Bots are unavailable right now. Try again later.`, keep `ClearSeat` allowed; verify with hub tests
- [ ] 6.6 Add the `bots` check to `/hc` (Healthy with `latencyMs` and `serviceVersion`, Degraded `Bot service is unreachable.` or `Bot service is not configured.`); verify with health endpoint tests
- [ ] 6.7 Remove `ChessLab.Bots` and Stockfish from the web host and `Bots:StockfishPath` from its configuration; verify the web project no longer references ChessLab.Bots and all Web tests pass

## 7. Web client

- [ ] 7.1 Show the paused banner with a countdown, stop both local clocks and disable the board while paused, keeping resign; verify with a component or E2E test driven by a paused state
- [ ] 7.2 Show the bots-unavailable notice in the lobby, disable adding and changing bots and the start button when a bot seat exists; verify with an E2E test and `data-testid` attributes
- [ ] 7.3 Label `BotUnavailable` on the result banner (`bot unavailable` plus the explanation line) and on `/history/<id>`; verify with tests on both pages

## 8. Packaging and deployment

- [ ] 8.1 Add the bot service Dockerfile (port 8080, `stockfish` on `PATH`, `APP_VERSION` stamp) and remove Stockfish from the web Dockerfile; verify both images build and `/health` reports the stamped version
- [ ] 8.2 Add the bot service to `docker-compose.yml` with the key from `.env` and point the web container at it; verify `docker compose up --build` plays a bot game locally
- [ ] 8.3 Add `deploy/bot-service/` with a compose template (bot service + Caddy with the self-signed certificate) and a README listing the one-time server steps (directory, API key, certificate, ufw rule, router port forward) and the Render settings; verify by following it on the home server
- [ ] 8.4 Extend CI: run `tests/ChessLab.BotService.Tests` on pull requests and main, publish the bot service image to GHCR, deploy it over SSH from the self-hosted runner, wait up to 2 minutes for `/health` to report the new version, then deploy the web image; verify on a manual dispatch run
- [ ] 8.5 Update `openspec/config.yaml` context (architecture, commands, configuration) for the new service and settings; verify `openspec validate --all --strict --no-interactive` passes

## 9. End-to-end check

- [ ] 9.1 Against the deployed home server, play one game of each of the nine modes against a bot from Render; stop the bot container mid-game and verify the pause banner, then start it within 5 minutes and verify the game resumes with unchanged clocks
- [ ] 9.2 Stop the bot container for more than 5 minutes during a game and verify the game ends as BotUnavailable with the human as winner, the lobby blocks new bot games, and `/hc` reports `bots` Degraded

## Workflow follow-up

- Archive the change with `/opsx:archive` after the PR is merged and the deployment is verified.
