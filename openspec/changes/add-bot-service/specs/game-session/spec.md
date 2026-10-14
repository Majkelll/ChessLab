# Spec Delta

## MODIFIED Requirements

### Requirement: End reasons
The system SHALL report a finished game with one of the reasons Checkmate, Stalemate, InsufficientMaterial, FiftyMoveRule, Repetition, Resignation, Timeout, HpDepleted, KingCaptured, BoardHalfEmptied, MoveLimit or BotUnavailable, and a winner that is absent for a draw. Which reasons a mode can produce is defined per mode; BotUnavailable can end a game of any mode. The reason DrawAgreed is defined but nothing produces it.

#### Scenario: Drawn game
- **WHEN** a game ends by stalemate in a standard-rules mode
- **THEN** the reason is Stalemate and no winner is reported

#### Scenario: Bot unavailable
- **WHEN** a game ends because its bot stayed unavailable
- **THEN** the reason is BotUnavailable

## ADDED Requirements

### Requirement: Paused game
The system SHALL let a game be paused, as defined in the bots capability. While a game is paused, the system SHALL reject every game action with `The game is paused because a bot is unavailable.`, SHALL still accept resignation, and SHALL publish the time at which the pause will end the game in every update and full state.

#### Scenario: Human acts during a pause
- **WHEN** a seated human performs an action in a paused game
- **THEN** the action is rejected with `The game is paused because a bot is unavailable.`

#### Scenario: Resigning during a pause
- **WHEN** a seated human resigns a paused game
- **THEN** the game ends with reason Resignation as usual

#### Scenario: Pause deadline published
- **WHEN** a game is paused at 12:00:00 UTC
- **THEN** every update and full state carries a pause deadline of 12:05:00 UTC until the game resumes or ends

#### Scenario: Running game
- **WHEN** a game is not paused
- **THEN** the pause deadline is absent

### Requirement: Clocks stop during a pause
The system SHALL NOT count time spent paused against any side. While a game is paused it SHALL NOT end by timeout, and when it resumes every turn timer SHALL continue from where it stood when the pause began.

#### Scenario: Pause on the bot's turn
- **WHEN** a bot has used 3 seconds of its turn, the game is paused for 2 minutes and the bot then moves 1 second after the resume
- **THEN** 4 seconds are deducted from the bot's clock

#### Scenario: Clock nearly out
- **WHEN** the side on the clock has 2 seconds left and the game is paused for 1 minute
- **THEN** no timeout is declared during the pause
