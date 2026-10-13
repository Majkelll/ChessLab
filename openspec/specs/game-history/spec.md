# game-history Specification

## Purpose
Describe how finished games are archived and how users read their past games.

## Requirements

### Requirement: Archiving finished games
The system SHALL store at most one record for each room code whose game has ended, whatever the end reason, together with one player row per non-empty seat. The save SHALL be attempted after every game broadcast (start, accepted action, bot round, resignation, timeout) and, when it failed, again once a minute for as long as the room exists.

#### Scenario: Game ends
- **WHEN** a game ends
- **THEN** one record is stored with the room code, game kind, the UTC time at which it was saved, end reason, winner (absent for a draw), move count, the move notations joined by spaces and the final position text

#### Scenario: Human seat
- **WHEN** a finished game had a human in a seat
- **THEN** that seat's player row carries the side, role, user identifier and display name

#### Scenario: Repeated end notifications
- **WHEN** the end of the same game is observed more than once
- **THEN** no second record is stored

#### Scenario: Bot seat
- **WHEN** a finished game had a bot in a seat
- **THEN** that seat's player row has no user identifier and no display name and carries the bot's difficulty

#### Scenario: Database unavailable when the game ends
- **WHEN** saving a finished game fails
- **THEN** the failure is logged, the room is kept, and the save is retried about once a minute until it succeeds or the room is 24 hours old

#### Scenario: Game still running
- **WHEN** a game update is broadcast for a game that has not ended
- **THEN** nothing is stored

### Requirement: Listing a user's games
The system SHALL return the caller's finished games from `GetMyGameHistory`, newest first, limited to `limit` entries. `limit` SHALL default to 50 and be clamped to the range 1 to 200. A game belongs to a user when the user occupied a seat in it. There is no paging beyond `limit`.

#### Scenario: Limit above the maximum
- **WHEN** a user requests 1000 entries
- **THEN** at most 200 entries are returned

#### Scenario: Limit below the minimum
- **WHEN** a user requests 0 entries
- **THEN** at most 1 entry is returned

#### Scenario: Player order
- **WHEN** an entry is returned
- **THEN** its players are ordered by side (White, Black) and then by role (Brain, Hand, Player)

### Requirement: Reading one game
The system SHALL return a game's summary, final position text and list of move notations by record identifier (`GetGameHistoryEntry`) to any signed-in user, and by room code (`GetGameHistoryByRoomCode`) only to a user who played in that game. It SHALL return null when no record matches.

#### Scenario: Unknown identifier
- **WHEN** a game is requested by an identifier with no record
- **THEN** null is returned

#### Scenario: Known room code
- **WHEN** a player of an archived game requests it by its room code
- **THEN** the summary, final position and moves are returned

#### Scenario: Room code asked by someone else
- **WHEN** a user who did not play in the game requests it by its room code
- **THEN** null is returned

### Requirement: Opening a retired room
The system SHALL send a user who opens a room or game URL for a code with no live room to that game's history page when an archived record exists for the code and the user played in it. A finished room stays live for 15 minutes, during which its URLs show the retired lobby or the final game state instead.

#### Scenario: Room gone but archived
- **WHEN** a user opens `/room/<code>` after a server restart for a game that was archived
- **THEN** the user is taken to `/history/<record id>`

#### Scenario: Room gone and not archived
- **WHEN** a user opens `/room/<code>` or a game URL for a code with no live room and no record
- **THEN** a notice states that the room was not found and that rooms only live while the server is running
