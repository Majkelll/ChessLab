# ChessLab

Chess variants you play in the browser, against a friend or a bot. Blazor WebAssembly on the front, SignalR and ASP.NET Core behind it.

## Modes

| Mode | The twist |
| --- | --- |
| Hand and Brain | The Brain names a piece type, the Hand has to move one of that kind. |
| Card Chess | You draw a card and it decides which piece you may move. |
| Arcane Chess | Card Chess plus a hand of spells paid for with mana. |
| Bidding Chess | No turns. Both sides bid in secret and the higher bid moves. |
| Progressive Chess | White plays one move, Black two, White three, and so on. |
| Alice Chess | Two boards. Every move sends the piece to the other one. |
| Absorption Chess | A piece gains the moves of whatever it captures. |
| Martian Chess | Pyramids on a 4x8 board. You own whatever stands in your half. |
| Draft Chess | Buy your army from a shared pool, then set it up in secret. |

Every mode has a bot. Stockfish plays the ones a UCI engine can read. The rest use a simple one-move-deep bot. Bots run in their own service, `ChessLab.BotService`, which the web host calls over HTTP. When it is down, bot games pause and new ones cannot start.

## Run it

With Docker, which brings Postgres and the bot service along:

```sh
docker compose up --build
```

Put `GOOGLE_CLIENT_ID` and `GOOGLE_CLIENT_SECRET` in a `.env` file first. The app listens on http://localhost:8080.

Without Docker you need .NET 10 and npm. For bots, also start the bot service with `stockfish` on your `PATH` (it listens on http://localhost:5090, where the web host's Development settings expect it):

```sh
dotnet run --project src/ChessLab.BotService
```

Then the web host:

```sh
cd src/ChessLab.Web
dotnet user-secrets set "Authentication:Google:ClientId" "<id>"
dotnet user-secrets set "Authentication:Google:ClientSecret" "<secret>"
dotnet user-secrets set "ConnectionStrings:Default" "Data Source=chesslab.db"
dotnet run
```

A connection string starting with `Data Source=` uses SQLite. Anything else is treated as Postgres, and the Development default expects one on `localhost` with database `chesslab_dev`.

Sign-in is Google only. Add `http://localhost:5021/signin-google` as a redirect URI in your OAuth client, or the `:8080` one for Docker.

## Tests

```sh
dotnet test tests/ChessLab.Core.Tests
dotnet test tests/ChessLab.Web.Tests
dotnet test tests/ChessLab.BotService.Tests
dotnet test tests/ChessLab.E2E.Tests
```

The end-to-end tests drive a real browser through Playwright and need its Chromium installed. The bot tests that need Stockfish are skipped when it is not installed. CI runs the first three commands. The slow ones, fifty moves of every mode, sit in their own category:

```sh
dotnet test tests/ChessLab.E2E.Tests --filter Category=LongGame
dotnet test tests/ChessLab.E2E.Tests --filter "Category!=LongGame"
```

## Layout

- `src/ChessLab.Core`: rules for every mode, rooms and clocks. No web dependencies.
- `src/ChessLab.Bots`: Stockfish wrapper and the per-mode bots.
- `src/ChessLab.BotService`: the bot service: one action per request from the bot seat's view, a pool of Stockfish workers.
- `src/ChessLab.Data`: EF Core context, users and finished games.
- `src/ChessLab.Web`: host, SignalR hub, auth, `/hc` health report.
- `src/ChessLab.Web.Client`: the WebAssembly UI.

## Deploy

Pushing to `main` runs the tests on a self-hosted runner, builds both images and publishes them to GHCR, deploys the bot service to the home server, waits for it to report the new version, then triggers a deploy on Render. `deploy/bot-service/README.md` lists the one-time server setup and the Render settings.
