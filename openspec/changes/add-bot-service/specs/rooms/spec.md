# Spec Delta

## ADDED Requirements

### Requirement: Bot service availability
The system SHALL treat the bot service as available only after its health endpoint or an action request has succeeded, and as unavailable after a failed health probe or a failed action request, and at start-up until the first probe succeeds. It SHALL probe the health endpoint every 10 seconds. The room state SHALL say whether bots are available, and every unlocked room SHALL receive `RoomUpdated` when availability changes.

#### Scenario: Service goes down
- **WHEN** a health probe fails while bots were available
- **THEN** the room state of every room says bots are unavailable and every unlocked room receives `RoomUpdated`

#### Scenario: No service configured
- **WHEN** the backend has no bot service address configured
- **THEN** bots are unavailable for the whole run

### Requirement: No new bot games while bots are unavailable
The system SHALL reject, while the bot service is unavailable, filling a seat with a bot and changing a bot's difficulty with `Bots are unavailable right now. Try again later.`, and SHALL reject starting a game in which any seat holds a bot with the same message. Clearing a bot seat SHALL stay allowed.

#### Scenario: Adding a bot while unavailable
- **WHEN** the host sets a bot on an empty seat while bots are unavailable
- **THEN** the request is rejected with `Bots are unavailable right now. Try again later.`

#### Scenario: Starting with a bot while unavailable
- **WHEN** the host starts a room with a bot seat while bots are unavailable
- **THEN** the start is rejected with `Bots are unavailable right now. Try again later.` and the room stays unlocked

#### Scenario: Removing a bot while unavailable
- **WHEN** the host clears a bot seat while bots are unavailable
- **THEN** the seat becomes empty

#### Scenario: Humans only
- **WHEN** the host starts a room with no bot seat while bots are unavailable
- **THEN** the game starts
