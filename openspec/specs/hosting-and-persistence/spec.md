# hosting-and-persistence Specification

## Purpose
Describe startup configuration, database provider selection, schema creation, the persisted data model, reverse proxy handling, the container image and the build and delivery pipeline.

## Requirements

### Requirement: Mandatory configuration
The system SHALL refuse to start when `ConnectionStrings:Default` is missing, or when `Authentication:Google:ClientId` or `Authentication:Google:ClientSecret` is missing or blank. The Development environment supplies a default PostgreSQL connection string (`Host=localhost;Database=chesslab_dev;Username=chesslab;Password=chesslab`) but no Google credentials.

#### Scenario: Connection string missing
- **WHEN** the host starts without `ConnectionStrings:Default`
- **THEN** startup fails with `Missing configuration: ConnectionStrings:Default`

#### Scenario: Google credentials left empty
- **WHEN** the host starts with the empty `Authentication:Google:ClientId` shipped in `appsettings.json`
- **THEN** startup fails with `Missing configuration: Authentication:Google:ClientId`

### Requirement: Optional configuration
The system SHALL read `Bots:StockfishPath` (default `stockfish`), `Game:InitialClockSeconds` (default 600), `Game:ClockIncrementSeconds` (default 0) and `E2E_TEST_AUTH` (default false, honoured only in the Development environment).

#### Scenario: Defaults
- **WHEN** none of the optional keys is set
- **THEN** new rooms get a 600 second clock with no increment, bots look for `stockfish` on `PATH`, and the test sign-in endpoint is not mapped

### Requirement: Database provider selection
The system SHALL use SQLite when the connection string starts with `Data Source=` (case-insensitive) and PostgreSQL otherwise.

#### Scenario: SQLite connection string
- **WHEN** the connection string is `Data Source=chesslab.db`
- **THEN** SQLite is used

#### Scenario: Any other connection string
- **WHEN** the connection string is `Host=db;Database=chesslab;Username=chesslab;Password=chesslab`
- **THEN** PostgreSQL is used

### Requirement: Schema setup at startup
The system SHALL apply pending EF Core migrations at startup on PostgreSQL and SHALL create the schema directly from the model on SQLite.

#### Scenario: PostgreSQL start
- **WHEN** the host starts against PostgreSQL with pending migrations
- **THEN** the migrations are applied before requests are served

#### Scenario: SQLite start
- **WHEN** the host starts against a SQLite file that does not exist
- **THEN** the database is created with all tables

#### Scenario: Existing SQLite file
- **WHEN** the host starts against a SQLite file created by an older version of the model
- **THEN** the file is used as it is and is not upgraded

#### Scenario: PostgreSQL unreachable at startup
- **WHEN** the host starts against PostgreSQL and the migration step fails
- **THEN** startup fails and no request is served

### Requirement: Persisted data model
The system SHALL persist users, finished game records, the players of each record, and ASP.NET data protection keys. Game records SHALL be indexed by finish time and player rows by user. Text columns SHALL be limited to 16 characters for room code, seat role and bot difficulty, 32 for game kind and end reason, 8 for winner and side, and 128 for player display name.

#### Scenario: User uniqueness
- **WHEN** two users would share a Google subject identifier
- **THEN** the database rejects the second through a unique index

#### Scenario: Game record uniqueness
- **WHEN** two game records would share a room code
- **THEN** the database rejects the second through a unique index

#### Scenario: Deleting a game record
- **WHEN** a game record is deleted
- **THEN** its player rows are deleted with it

#### Scenario: Enum storage
- **WHEN** a game record or player row is stored
- **THEN** game kind, end reason, winner, side, seat role and bot difficulty are stored as text

### Requirement: Reverse proxy awareness
The system SHALL honour `X-Forwarded-For` and `X-Forwarded-Proto` headers from any source, on the assumption that only a trusted reverse proxy can reach the host, use the HTTPS redirection middleware, and enable HSTS outside the Development environment.

#### Scenario: Request through a TLS-terminating proxy
- **WHEN** a proxy on any address forwards a request with `X-Forwarded-Proto: https`
- **THEN** the request is treated as HTTPS and the session cookie it sets is marked secure

### Requirement: Container image
The system SHALL be buildable as a container image that listens on HTTP port 8080, starts with `dotnet ChessLab.Web.dll`, and has `stockfish` on `PATH`. The image build SHALL compile the stylesheet in a separate Node 22 stage and SHALL stamp the `APP_VERSION` build argument (default `dev`) as the application's informational version.

#### Scenario: Version stamp
- **WHEN** the image is built with `APP_VERSION=abc1234`
- **THEN** the health report and the page footer show `abc1234`

#### Scenario: Compose stack
- **WHEN** `docker compose up --build` is run with `GOOGLE_CLIENT_ID` and `GOOGLE_CLIENT_SECRET` set
- **THEN** the application starts once the PostgreSQL 18 container reports healthy and is served on host port 8080, with database files kept in the `chesslab-data` volume

#### Scenario: Compose without Google credentials
- **WHEN** `GOOGLE_CLIENT_ID` or `GOOGLE_CLIENT_SECRET` is not set
- **THEN** compose fails with `Set GOOGLE_CLIENT_ID in .env` or `Set GOOGLE_CLIENT_SECRET in .env`

### Requirement: Build
The system SHALL build the stylesheet, fonts and chessboard assets through `npm run build:css` before every build of the web host, installing npm packages first when `node_modules` is absent, unless the build passes `-p:SkipTailwindBuild=true`.

#### Scenario: Solution build
- **WHEN** `dotnet build ChessLab.slnx -c Release` runs on a machine with npm
- **THEN** `wwwroot/css/app.css`, `wwwroot/css/files` and `wwwroot/js/cm-chessboard` are generated before the web host compiles

### Requirement: Continuous integration and delivery
The system SHALL check pull requests against `main` by building the solution in Release and running `tests/ChessLab.Core.Tests` and `tests/ChessLab.Web.Tests` on a self-hosted runner labelled `chesslab`. On every push to `main`, or on manual dispatch, the system SHALL build the solution and run the same tests, and only then build the container image, push it to GHCR tagged `latest` and with the short commit hash, and deploy that hash-tagged image to the Render service with the Render CLI.

#### Scenario: Pull request
- **WHEN** a pull request targets `main`
- **THEN** the solution is built and both test projects run, and an in-progress run for the same pull request is cancelled by a newer one

#### Scenario: End-to-end suite
- **WHEN** either workflow runs
- **THEN** the browser end-to-end suite is built but not run

#### Scenario: Push to main
- **WHEN** a commit is pushed to `main`
- **THEN** an image tagged with the short commit hash is published and deployed, and the workflow waits for the deploy to finish

#### Scenario: Unit test fails on main
- **WHEN** a commit pushed to `main` fails one of those tests
- **THEN** no image is published and nothing is deployed
