# Spec Delta

## Purpose

Describe the standalone bot service that chooses actions for bot seats from a seat's view of the game, separately from the web host.

## ADDED Requirements

### Requirement: Action endpoint
The bot service SHALL serve `POST /v1/action`, taking a JSON body with the game kind, the bot's seat, its difficulty and the game state built for that seat's side, and SHALL answer 200 with one game action for that seat. It SHALL answer 400 when the body is malformed or the state is for another kind, and 422 when the seat has nothing to do in the given state.

#### Scenario: Bot chooses a move
- **WHEN** the service receives a Card Chess state in which the seat's side is to move
- **THEN** it answers 200 with a Move action that matches one of the state's available moves

#### Scenario: Kind mismatch
- **WHEN** the request names Alice Chess but carries a Martian Chess state
- **THEN** it answers 400

#### Scenario: Seat not active
- **WHEN** the state shows the game is over
- **THEN** it answers 422

### Requirement: Seat view only
The bot service SHALL choose an action using only the request body. A request SHALL carry exactly the state a human in the bot's seat would receive, so two games that differ only in information hidden from that seat produce identical requests.

#### Scenario: Hidden Arcane hand
- **WHEN** two Arcane Chess games differ only in the opponent's spell hand and rank hand, and no Peek is active
- **THEN** the requests for the bot's seat are identical

#### Scenario: Unrevealed bid
- **WHEN** two Bidding Chess games differ only in the amount the opponent has bid this round
- **THEN** the requests for the bot's seat are identical

### Requirement: Authentication
The bot service SHALL accept `POST /v1/action` only with the header `Authorization: Bearer <key>` matching its configured API key, compared in constant time, and SHALL answer 401 otherwise. The service SHALL refuse to start when no API key is configured or it is shorter than 32 characters.

#### Scenario: Missing key
- **WHEN** a request has no `Authorization` header
- **THEN** it answers 401 and does no work

#### Scenario: Short key at start-up
- **WHEN** the configured API key has 16 characters
- **THEN** the service does not start and logs why

### Requirement: Health endpoint
The bot service SHALL serve `GET /health` without authentication, answering 200 with its version and worker count when every worker's engine is ready, and 503 otherwise.

#### Scenario: Healthy service
- **WHEN** all workers have a running engine
- **THEN** `GET /health` answers 200 with `version` and `workers`

#### Scenario: Engine down
- **WHEN** a worker's engine process has exited and not yet been restarted
- **THEN** `GET /health` answers 503

### Requirement: Worker pool
The bot service SHALL handle action requests on a fixed number of workers, by default one per processor, each owning one Stockfish process, and SHALL queue requests beyond that in arrival order. A request still queued 15 seconds after arrival SHALL be answered 503.

#### Scenario: More requests than workers
- **WHEN** six requests arrive at once on a service with four workers
- **THEN** four are processed at once and the other two wait in arrival order

#### Scenario: Queue too slow
- **WHEN** a request has waited 15 seconds without reaching a worker
- **THEN** it is answered 503

#### Scenario: Engine crash
- **WHEN** a worker's Stockfish process exits during a search
- **THEN** that request is answered 500, and the worker starts a new process before taking the next request

### Requirement: Bot service configuration and image
The bot service SHALL be buildable as a container image listening on HTTP port 8080 with `stockfish` on `PATH`, and SHALL read `BotService:ApiKey` (required), `BotService:Workers` (default: processor count) and `BotService:StockfishPath` (default `stockfish`). The image SHALL stamp `APP_VERSION` as the version reported by `/health`.

#### Scenario: Version stamp
- **WHEN** the image is built with `APP_VERSION=abc1234`
- **THEN** `GET /health` reports version `abc1234`

#### Scenario: Worker count
- **WHEN** `BotService:Workers` is 2
- **THEN** the service runs two workers and two Stockfish processes
