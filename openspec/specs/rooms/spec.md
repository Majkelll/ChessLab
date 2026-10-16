# rooms Specification

## Purpose
Describe how rooms are created and joined, how seats are filled with humans or bots, and how the host configures the clock before a game starts.

## Requirements

### Requirement: Room creation
The system SHALL create a room for a chosen game kind when a signed-in user calls `CreateRoom`, make the caller its host, and return the room state. The game kind SHALL default to Hand and Brain. The room code SHALL be six characters drawn from `ABCDEFGHJKMNPQRSTUVWXYZ23456789`, unique among live rooms and not used by any archived game; codes are matched case-sensitively. `CreateRoom` SHALL subscribe the calling connection to the new room's updates.

#### Scenario: Creating a room
- **WHEN** a signed-in user creates a room
- **THEN** the returned room has a six-character code, the caller as host, all seats empty, `HasStarted` false and `IsLocked` false

#### Scenario: Host and seats
- **WHEN** a room has been created
- **THEN** its host holds no seat until they claim one, and the host never changes

#### Scenario: Default clock
- **WHEN** a room is created and no clock is configured on the server
- **THEN** the room's clock is 600 seconds with 0 seconds increment

#### Scenario: Server-configured clock
- **WHEN** `Game:InitialClockSeconds` and `Game:ClockIncrementSeconds` are configured
- **THEN** new rooms start with those values

### Requirement: Room limit and lifetime
The system SHALL let a user host at most 5 rooms whose game has not ended. Rooms and their games live in server memory only and are lost when the server restarts. The system SHALL remove a room 15 minutes after its game ended, and a room whose game never started 2 hours after it was created, together with its bot engine and connection bookkeeping. A room with a game in progress SHALL NOT be removed, and a finished room whose game could not be archived yet SHALL be kept for up to 24 hours.

#### Scenario: Sixth open room
- **WHEN** a user who hosts 5 rooms without a finished game creates another
- **THEN** the call is rejected with `You already host 5 open rooms. Wait for one to finish or expire.`

#### Scenario: Finished room expires
- **WHEN** 15 minutes have passed since a room's game ended
- **THEN** the room is gone within about a minute and its URLs lead to the archived game

#### Scenario: Abandoned lobby expires
- **WHEN** a room has existed for 2 hours without its game starting
- **THEN** the room is removed

### Requirement: Seat layout per game kind
The system SHALL give a Hand and Brain room four seats (White Brain, White Hand, Black Brain, Black Hand) and every other game kind two seats (White Player, Black Player).

#### Scenario: Hand and Brain room
- **WHEN** a Hand and Brain room is created
- **THEN** it has exactly four seats

#### Scenario: Card Chess room
- **WHEN** a Card Chess room is created
- **THEN** it has exactly two seats, both with role Player

### Requirement: Joining a room
The system SHALL let any signed-in user join a room by code with `JoinRoom`, subscribe that connection to the room's updates, and return the room state. Joining SHALL NOT claim a seat. A connection SHALL be subscribed to at most one room. Joining SHALL work for locked and finished rooms too. A seat SHALL stay held when its user's connection drops.

#### Scenario: Unknown code
- **WHEN** a user joins a code that matches no live room
- **THEN** `JoinRoom` returns null

#### Scenario: Reconnecting
- **WHEN** a user's connection drops and a new one calls `JoinRoom`
- **THEN** the user still holds the same seat and receives updates again

#### Scenario: Switching rooms
- **WHEN** a connection that joined room A joins room B
- **THEN** it stops receiving updates for room A

### Requirement: Claiming and leaving seats
The system SHALL let a user claim an empty seat and leave their own seat while the room is unlocked. A user SHALL hold at most one seat per room. A seat that is not part of the room's layout SHALL be rejected. Seat calls SHALL NOT require the caller to have joined the room first.

#### Scenario: Claiming an empty seat
- **WHEN** a user claims an empty seat
- **THEN** the seat shows that user as a human occupant with their display name
- **AND** every connection in the room receives `RoomUpdated`

#### Scenario: Claiming a taken seat
- **WHEN** a user claims a seat held by a human or a bot
- **THEN** the claim is rejected with `Seat is already taken.`

#### Scenario: Claiming the seat already held
- **WHEN** a user claims the seat they already hold
- **THEN** the claim is rejected with `Seat is already taken.`

#### Scenario: Seat outside the layout
- **WHEN** any seat call names White Player in a Hand and Brain room
- **THEN** it is rejected with `Seat White-Player does not exist in this room.` and the room's seats are unchanged

#### Scenario: Claiming a second seat
- **WHEN** a seated user claims a different empty seat
- **THEN** the user's previous seat becomes empty

#### Scenario: Leaving someone else's seat
- **WHEN** a user calls `LeaveSeat` for a seat they do not hold
- **THEN** the seat is unchanged and `RoomUpdated` is still broadcast

#### Scenario: Display name fallback
- **WHEN** the caller's identity has no name
- **THEN** the seat shows the display name `Gracz`

### Requirement: Bot seats
The system SHALL let the host fill a seat with a bot of difficulty Easy, Medium, Hard or Expert, change a bot's difficulty, and clear any seat, including one held by a human, while the room is unlocked.

#### Scenario: Adding a bot to an empty seat
- **WHEN** a bot is set on an empty seat
- **THEN** the seat shows a bot occupant with the chosen difficulty

#### Scenario: Changing a bot's difficulty
- **WHEN** a bot is set on a seat already held by a bot
- **THEN** the seat shows the new difficulty

#### Scenario: Bot on a human seat
- **WHEN** a bot is set on a seat held by a human
- **THEN** the request is rejected with `Seat is occupied by a human player.`

#### Scenario: Non-host sets a bot
- **WHEN** a user who is not the host calls `SetSeatBot`
- **THEN** the request is rejected with `Only the room's host can add or change a bot.`

#### Scenario: Non-host clears a seat
- **WHEN** a user who is not the host calls `ClearSeat`
- **THEN** the request is rejected with `Only the room's host can clear a seat.`

#### Scenario: Clearing a seat
- **WHEN** a seat is cleared
- **THEN** the seat becomes empty

### Requirement: Approximate bot rating
The system SHALL present bot difficulties with approximate ratings of 1320 (Easy), 1700 (Medium), 2100 (Hard) and 2800 (Expert).

#### Scenario: Rating order
- **WHEN** difficulties are compared
- **THEN** the rating increases strictly from Easy to Expert

### Requirement: Clock settings
The system SHALL let only the host change the room's initial time and increment, and only while the room is unlocked. The initial time SHALL be positive and the increment SHALL NOT be negative. Values are whole seconds with no upper bound.

#### Scenario: Host changes the clock
- **WHEN** the host sets 300 seconds initial and 5 seconds increment
- **THEN** the room state shows those values
- **AND** every connection in the room receives `RoomUpdated`

#### Scenario: Non-host changes the clock
- **WHEN** a user who is not the host calls `SetClockSettings`
- **THEN** the request is rejected with `Only the room's host can change the clock settings.`

#### Scenario: Non-positive initial time
- **WHEN** the host sets an initial time of 0 or -1 seconds
- **THEN** the request is rejected with `Initial clock must be positive.`

#### Scenario: Negative increment
- **WHEN** the host sets a negative increment
- **THEN** the request is rejected with `Increment can't be negative.`

### Requirement: Room lock
The system SHALL lock a room when its game starts. A locked room SHALL reject seat changes, bot changes and clock changes with `The room is locked because the game has already started.`

#### Scenario: Seat change after start
- **WHEN** a user claims a seat in a room whose game has started
- **THEN** the claim is rejected

#### Scenario: Room after its game ended
- **WHEN** the game in a room has ended
- **THEN** the room state has `IsLocked` true and `HasStarted` false

### Requirement: Call rate limit
The system SHALL accept at most 120 hub calls per connection in any 10 second window and reject further calls in that window with `Too many requests. Slow down.`

#### Scenario: Flooding connection
- **WHEN** a connection makes its 121st call within 10 seconds
- **THEN** the call is rejected

### Requirement: Unknown room
The system SHALL reject every seat, bot, clock, start, action, state and resign call that names a code with no live room with `Room '<code>' not found.` `JoinRoom` and `GetGameHistoryByRoomCode` return null instead.

#### Scenario: Acting on a missing room
- **WHEN** a user calls `ClaimSeat` with a code that matches no live room
- **THEN** the call fails with a `HubException` naming the code
