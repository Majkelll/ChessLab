# Spec Delta

## MODIFIED Requirements

### Requirement: Bots act when their seat is active
The system SHALL play bot seats automatically after every game broadcast, in rounds, until no active seat is held by a bot or the game ends. Each round SHALL wait a random 400 to 899 milliseconds, ask the bot service for one action for every bot seat that was active before the wait, apply each, then broadcast `GameUpdated` once and archive the game if it has finished. Bot turns for one room SHALL run one at a time.

#### Scenario: Human moves against a bot
- **WHEN** a human completes a turn and the next active seat is a bot
- **THEN** the bot acts without further input and the room receives `GameUpdated`

#### Scenario: Several bot seats in a row
- **WHEN** three of four Hand and Brain seats are bots
- **THEN** the bots play their announcements and moves until a human seat is active again

#### Scenario: Both sides owe an action
- **WHEN** both sides are active at once and both are bots
- **THEN** each bot acts in the same round

#### Scenario: Room with bots only
- **WHEN** the host starts a room whose seats are all bots
- **THEN** the bots play the game to its end without further input

#### Scenario: What the bot is sent
- **WHEN** the bot service is asked for a seat's action
- **THEN** the request carries the full game state built for that seat's side, exactly as a human in that seat would receive it

#### Scenario: Rules reject a bot action
- **WHEN** applying a bot's action raises a rule violation
- **THEN** the rejection is logged at debug level, the round still sends `GameUpdated`, and the loop runs again

#### Scenario: Bot keeps being rejected
- **WHEN** 5 rounds in a row apply no bot action
- **THEN** the bot is handled as unavailable

#### Scenario: Bot service fails
- **WHEN** the bot service cannot be reached or answers with an error status
- **THEN** the failure is logged and the bot is handled as unavailable

#### Scenario: Bot fails to choose an action
- **WHEN** asking for an action throws, or any other error occurs during a round
- **THEN** the error is logged and the bot is handled as unavailable

#### Scenario: Bot fails three times in a row
- **WHEN** a paused game's bot keeps failing
- **THEN** there is no give-up after a fixed number of failures; the game stays paused and the bot is asked again every 5 seconds until the game resumes or ends

#### Scenario: Bot takes too long
- **WHEN** the bot service has not answered within 20 seconds
- **THEN** the request is cancelled and the bot is handled as unavailable

### Requirement: Engine-backed bots
The system SHALL use Stockfish, run by the bot service, for Hand and Brain, Card Chess, Arcane Chess and Progressive Chess. The engine SHALL search only among the moves the mode currently allows to the bot's seat. These bots never reroll cards.

#### Scenario: Difficulty
- **WHEN** an engine-backed bot of difficulty Easy, Medium, Hard or Expert searches
- **THEN** the engine is strength-limited to a rating of 1320, 1700, 2100 or 2800 and thinks for 200, 500, 800 or 1200 milliseconds

#### Scenario: Card Chess bot
- **WHEN** a Card Chess bot is to move
- **THEN** it plays the engine's best move among the currently available moves

#### Scenario: Hand and Brain bot as Brain
- **WHEN** a bot holds a Brain seat
- **THEN** it probes each announceable piece kind with a quarter of the thinking time, at least 50 milliseconds, and announces the kind with the highest engine score, keeping the earlier kind on a tie

#### Scenario: Hand and Brain bot as Hand
- **WHEN** a bot holds a Hand seat
- **THEN** it plays the engine's best move among moves of the announced piece kind

#### Scenario: Progressive Chess bot
- **WHEN** a Progressive Chess bot has several moves left in its series
- **THEN** it chooses each move with a separate engine search, one move per round

#### Scenario: Stockfish unavailable
- **WHEN** the bot service cannot start an engine process
- **THEN** its health endpoint reports it as unhealthy and the backend handles bots as unavailable

#### Scenario: Engine does not answer
- **WHEN** an engine has not completed its start-up handshake within 20 seconds
- **THEN** the bot service handles it as a crashed engine and starts a new process

## ADDED Requirements

### Requirement: Pause while a bot is unavailable
The system SHALL pause a game when a bot seat must act and is handled as unavailable, unless the game is already paused. While paused, the system SHALL ask the bot service for that seat's action again every 5 seconds. The first action applied from the bot service SHALL resume the game.

#### Scenario: Service down on the bot's turn
- **WHEN** the bot service cannot be reached while a bot is to move
- **THEN** the game is paused and the room receives `RoomNotice` saying the bot is unavailable and the game is paused

#### Scenario: Service comes back
- **WHEN** a paused game's bot action is obtained and applied
- **THEN** the game resumes, the room receives `RoomNotice` saying the bot is back, and `GameUpdated` is sent

#### Scenario: Human to move when the service fails
- **WHEN** the bot service is down while a human seat is to move and no bot seat is active
- **THEN** the game is not paused and the human can play

### Requirement: Ending a game the bot cannot continue
The system SHALL end a game that has stayed paused for 5 minutes with end reason `BotUnavailable`. The side that holds no bot seat SHALL win; when both sides hold a bot seat, the game SHALL be a draw. The game SHALL then be archived like any other finished game.

#### Scenario: Human against an unavailable bot
- **WHEN** a game between a human White and a bot Black has been paused for 5 minutes
- **THEN** the game ends with reason `BotUnavailable` and White wins

#### Scenario: Bot partner in Hand and Brain
- **WHEN** a Hand and Brain game where White's Hand is a bot and both Black seats are humans has been paused for 5 minutes
- **THEN** the game ends with reason `BotUnavailable` and Black wins

#### Scenario: Bots on both sides
- **WHEN** a game in which each side holds at least one bot seat has been paused for 5 minutes
- **THEN** the game ends with reason `BotUnavailable` as a draw

#### Scenario: Pause interrupted
- **WHEN** a game paused for 4 minutes resumes and is paused again later
- **THEN** the new pause may last another 5 minutes before the game ends

