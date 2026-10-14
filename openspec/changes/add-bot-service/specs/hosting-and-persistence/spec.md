# Spec Delta

## MODIFIED Requirements

### Requirement: Optional configuration
The system SHALL read `Bots:ServiceUrl` (no default; in the Development environment `http://localhost:5090`), `Bots:ServiceKey`, `Bots:ServiceCertificateThumbprint` (when set, the bot service's TLS certificate must match it), `Game:InitialClockSeconds` (default 600), `Game:ClockIncrementSeconds` (default 0) and `E2E_TEST_AUTH` (default false, honoured only in the Development environment).

#### Scenario: Defaults
- **WHEN** none of the optional keys is set outside the Development environment
- **THEN** new rooms get a 600 second clock with no increment, bots are unavailable, and the test sign-in endpoint is not mapped

#### Scenario: Bot service key
- **WHEN** `Bots:ServiceUrl` and `Bots:ServiceKey` are set
- **THEN** every request to the bot service's action endpoint carries `Authorization: Bearer` with that key

### Requirement: Container image
The system SHALL be buildable as a container image that listens on HTTP port 8080 and starts with `dotnet ChessLab.Web.dll`. The image SHALL NOT contain Stockfish. The image build SHALL compile the stylesheet in a separate Node 22 stage and SHALL stamp the `APP_VERSION` build argument (default `dev`) as the application's informational version.

#### Scenario: Version stamp
- **WHEN** the image is built with `APP_VERSION=abc1234`
- **THEN** the health report and the page footer show `abc1234`

#### Scenario: Compose stack
- **WHEN** `docker compose up --build` is run with `GOOGLE_CLIENT_ID` and `GOOGLE_CLIENT_SECRET` set
- **THEN** the application starts once the PostgreSQL 18 container reports healthy and is served on host port 8080, with database files kept in the `chesslab-data` volume, and the bot service runs alongside it and is used by the application

#### Scenario: Compose without Google credentials
- **WHEN** `GOOGLE_CLIENT_ID` or `GOOGLE_CLIENT_SECRET` is not set
- **THEN** compose fails with `Set GOOGLE_CLIENT_ID in .env` or `Set GOOGLE_CLIENT_SECRET in .env`

### Requirement: Continuous integration and delivery
The system SHALL check pull requests against `main` by building the solution in Release and running `tests/ChessLab.Core.Tests`, `tests/ChessLab.Web.Tests` and `tests/ChessLab.BotService.Tests` on a self-hosted runner labelled `chesslab`. On every push to `main`, or on manual dispatch, it SHALL run the same build and tests, then publish both images to GHCR tagged `latest` and with the short commit hash, deploy the bot service image to the home server, and deploy the web image to Render.

#### Scenario: Pull request
- **WHEN** a pull request targets `main`
- **THEN** the solution is built and the three test projects run, and an in-progress run for the same pull request is cancelled by a newer one

#### Scenario: End-to-end suite
- **WHEN** either workflow runs
- **THEN** the browser end-to-end suite is built but not run

#### Scenario: Push to main
- **WHEN** a commit is pushed to `main`
- **THEN** both images tagged with the short commit hash are published, the bot service is deployed first and reports that version on `GET /health`, then the web image is deployed and the workflow waits for that deploy to finish

#### Scenario: Unit test fails on main
- **WHEN** a commit pushed to `main` fails one of those tests
- **THEN** no image is published and nothing is deployed

#### Scenario: Bot service deploy fails
- **WHEN** the bot service does not report the new version within 2 minutes of its deploy
- **THEN** the workflow fails and the web image is not deployed
