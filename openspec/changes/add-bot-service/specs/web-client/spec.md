# Spec Delta

## MODIFIED Requirements

### Requirement: Game over screen
The system SHALL replace the resign button and the turn status with a result banner when the game ends, for every seat, and SHALL offer a way back to the room.

#### Scenario: Checkmate
- **WHEN** a game ends by checkmate
- **THEN** every seat sees the banner with the winner and the reason

#### Scenario: Bot unavailable
- **WHEN** a game ends with reason BotUnavailable
- **THEN** the banner shows the result with the reason `bot unavailable` and a line saying the game ended because the bot stopped responding

#### Scenario: Back to the room
- **WHEN** a player returns to the room after the game ended
- **THEN** the lobby says the room's game has ended and links to the landing page to start a new room
- **AND** no seats are shown

## ADDED Requirements

### Requirement: Paused game screen
The system SHALL show, on every game page while the game is paused, a banner saying the bot is unavailable and the game is paused, with a countdown to the pause deadline, and SHALL stop counting down both clocks. The board SHALL accept no moves while paused, and the resign button SHALL stay available.

#### Scenario: Game pauses
- **WHEN** the game page receives a state with a pause deadline 5 minutes ahead
- **THEN** the banner shows a countdown from 5:00 and neither clock counts down

#### Scenario: Game resumes
- **WHEN** the next state has no pause deadline
- **THEN** the banner disappears and the clock of the side to move counts down again

### Requirement: Lobby while bots are unavailable
The system SHALL show in the lobby, while the room state says bots are unavailable, a notice that bots are unavailable, and SHALL disable adding a bot and changing a bot's difficulty. When any seat holds a bot, the host's start button SHALL be marked `aria-disabled` and ignore clicks.

#### Scenario: Host adds a bot while unavailable
- **WHEN** the host views a lobby while bots are unavailable
- **THEN** empty seats offer joining but not adding a bot, and the notice is shown

#### Scenario: Bots come back
- **WHEN** the room state changes to say bots are available
- **THEN** the notice disappears and adding a bot is offered again without reloading

### Requirement: History label for an unavailable bot
The system SHALL label a finished game with reason BotUnavailable on the history pages as ended because the bot was unavailable.

#### Scenario: History entry
- **WHEN** a user opens `/history/<id>` for a game that ended with reason BotUnavailable
- **THEN** the result says the game ended because the bot was unavailable
