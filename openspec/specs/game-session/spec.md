# game-session Specification

## Purpose
Describe the behavior shared by every game mode: starting a game, authorising actions, clocks, timeouts, resignation and the messages sent to clients. Mode-specific rules live in the capability named after each mode.

## Requirements

### Requirement: Starting a game
The system SHALL let only the host start the game, and only when every seat is filled by a human or a bot. Starting SHALL lock the room, create the game with the room's clock settings for both sides, send `GameStarted` and `RoomUpdated` to the room and schedule any bot turns. The host need not be seated. There is no ready-up step.

#### Scenario: Host starts a full room
- **WHEN** the host starts a room in which every seat is filled
- **THEN** the room is locked and `HasStarted` becomes true
- **AND** every connection in the room receives `GameStarted` carrying a game update with no latest move and a move count of 0

#### Scenario: Non-host starts
- **WHEN** a user who is not the host calls `StartGame`
- **THEN** the call is rejected with `Only the room's host can start the game.`

#### Scenario: Seats still empty
- **WHEN** the host starts a room with an empty seat
- **THEN** the start is rejected with `All seats must be filled before the room can be locked.`

#### Scenario: Starting twice
- **WHEN** the host starts a room whose game has already started
- **THEN** the start is rejected with `Game has already started.`

### Requirement: Action authorisation
The system SHALL accept `PerformAction` only from a human seated in the room whose seat is currently allowed to act (the active seats, plus any seat a mode names), and SHALL report every rule violation, from any hub method, to the caller as a `HubException` carrying the rule's message. Which seats are active is defined per mode. The acting seat SHALL be derived from the caller's identity; a client cannot act for another seat.

#### Scenario: Game not started
- **WHEN** a user performs an action in a room with no game
- **THEN** the action is rejected with `Game has not started.`

#### Scenario: Spectator acts
- **WHEN** a user without a seat performs an action
- **THEN** the action is rejected with `You are not seated in this room.`

#### Scenario: Acting out of turn
- **WHEN** a seated user whose seat is not active performs an action
- **THEN** the action is rejected with `It's not your turn.`

#### Scenario: Rule violation
- **WHEN** an active seat performs an action the mode's rules reject
- **THEN** the caller receives a `HubException` carrying the rule's message

### Requirement: Action kinds
The system SHALL define nine action kinds: Move (origin, destination, optional promotion piece), SelectPieceKind (piece kind), SelectReroll (cards), CastSpell (spell, target), SubmitBid (amount), DraftPick (piece kind), DraftPass, PlacePiece (piece kind, square) and UnplacePiece (square). Each mode SHALL accept only its own kinds. Resignation is a separate hub call, not an action.

#### Scenario: Action kind foreign to the mode
- **WHEN** an active seat sends an action kind the mode does not define
- **THEN** the action is rejected with `<kind> is not an action in <mode name>.`, where the mode name is `Hand & Brain`, `Card Chess`, `Arcane Chess`, `Bidding Chess`, `Progressive Chess`, `Alice Chess`, `Absorption Chess`, `Martian Chess` or `Draft Chess`

#### Scenario: Missing field
- **WHEN** a Move action arrives without an origin square
- **THEN** it is rejected with the runtime's `Nullable object must have a value.` text

#### Scenario: Reroll without cards
- **WHEN** a SelectReroll action arrives without a card list
- **THEN** it is rejected with `A reroll selection needs a list of cards.`

### Requirement: One change at a time per room
The system SHALL apply changes to a room and its game one at a time, whether they come from a player's call, a bot or the timeout check, and SHALL read the state for an update under the same exclusion. Bots choose their action without holding that exclusion; only applying it is exclusive.

#### Scenario: Bot and player act together
- **WHEN** a bot's action and a player's action for the same game arrive at the same moment
- **THEN** one is applied completely before the other is looked at

#### Scenario: Bot's turn passed while it was choosing
- **WHEN** a bot finishes choosing after its seat stopped being active
- **THEN** its action is dropped

### Requirement: Game update broadcast
The system SHALL send `GameUpdated` to every connection in the room after every accepted action, bot round, resignation and timeout, followed by `RoomUpdated` when the game has ended. A connection receives updates only after it called `JoinRoom` or `CreateRoom` for the room.

#### Scenario: Update contents
- **WHEN** an update is broadcast
- **THEN** it carries the game kind, side to move, position text, available moves, latest move, move count, both clocks in milliseconds, whether the game is over, the end reason, the winner (absent for a draw) and the section for the room's mode; Arcane Chess carries both the Card Chess and the Arcane section

#### Scenario: Move accepted
- **WHEN** a move is accepted
- **THEN** every connection in the room receives `GameUpdated` whose latest move is that move and whose move count increased by one

#### Scenario: Non-move action accepted
- **WHEN** an announcement, reroll selection, spell, bid or draft action is accepted
- **THEN** the update's move count is unchanged and its latest move is still the previous move

### Requirement: State per viewer
The system SHALL build each update and each full state for its recipient: a seated player sees the game as their side, anyone else as a spectator. Information a mode keeps secret from a side SHALL be left out of what that side and spectators receive, and SHALL be included for everyone once the game is over. Which information is secret is defined per mode.

#### Scenario: Two players, one update
- **WHEN** an Arcane Chess update is sent to both players
- **THEN** each player's copy contains their own spell hand and not the opponent's

#### Scenario: Seat taken after joining
- **WHEN** a user's seat changes while their connection stays in the room
- **THEN** the next update is built for the seat the user holds at that moment

### Requirement: Full state on demand
The system SHALL return the game state, including the full move history, from `GetGameState` to any signed-in user, during and after the game, built for that user as a viewer.

#### Scenario: Game not started
- **WHEN** `GetGameState` is called for a room with no game
- **THEN** the call is rejected with `Game has not started.`

#### Scenario: Client missed an update
- **WHEN** a client receives an update whose move count is neither equal to nor exactly one more than the moves it holds
- **THEN** the client fetches the full state and replaces its move history

### Requirement: Chess clock
The system SHALL give each side the room's initial time at the start and, when a side acts, deduct from its clock the wall-clock time since the turn timer was last reset. The timer SHALL be reset at the start, after every move, and as defined per mode for other actions. Published clock values SHALL be the balances as of the last action; clients count down locally.

#### Scenario: Time deducted for a move
- **WHEN** a side moves 5 seconds after its turn began with 600 seconds left and no increment
- **THEN** that side has 595 seconds left

### Requirement: Clock increment
The system SHALL add the room's increment to the mover's clock after a move, as defined per mode. Actions other than moves (bids, Draft Chess picks, passes and placements) SHALL deduct time without adding an increment.

#### Scenario: Hand and Brain, Card Chess and Arcane Chess
- **WHEN** a side moves with a 3 second increment and the move does not end the game through the chess rules
- **THEN** 3 seconds are added to that side's clock

#### Scenario: Bidding Chess
- **WHEN** a side moves without capturing a king
- **THEN** the increment is added

#### Scenario: Progressive Chess
- **WHEN** a series ends and the game continues
- **THEN** the increment is added once for the whole series

#### Scenario: Alice, Absorption, Martian and Draft Chess
- **WHEN** a side moves
- **THEN** the increment is added, including after the move that ends the game

### Requirement: Timeout
The system SHALL check every active game once per second, and again whenever an action arrives, and end a game when the time since the turn began is at least the remaining time of a side that is on the clock. The other side SHALL win with end reason Timeout. When two sides are on the clock and both have run out, White SHALL lose.

#### Scenario: Nobody moves
- **WHEN** a game starts with 2 seconds per side and no one acts
- **THEN** within a few seconds the game ends with reason Timeout and the side that was on the clock loses
- **AND** every connection in the room receives `GameUpdated` without any player action

#### Scenario: Game not started
- **WHEN** the timeout check runs for a room with no game
- **THEN** nothing happens

#### Scenario: Action after the time ran out
- **WHEN** an action arrives after its side's time ran out but before the periodic check noticed
- **THEN** the action is not applied, the game ends by Timeout, and the room receives `GameUpdated`

### Requirement: Resignation
The system SHALL let a seated human resign at any time during a game, regardless of whose turn it is. The opposing side SHALL win with end reason Resignation. In Hand and Brain either seat of a side resigns for the whole side. There is no draw offer and no way to abort a game.

#### Scenario: Resigning
- **WHEN** a seated player resigns
- **THEN** the game ends with reason Resignation and the other side as winner
- **AND** every connection in the room receives `GameUpdated`

#### Scenario: Resigning before the game
- **WHEN** a user calls `Resign` in a room with no game
- **THEN** the call is rejected with `Game has not started.`

#### Scenario: Spectator resigns
- **WHEN** a user without a seat calls `Resign`
- **THEN** the call is rejected with `You are not seated in this room.`

### Requirement: Finished games are final
The system SHALL reject every game action after the game has ended. A room SHALL host at most one game.

#### Scenario: Action after the end
- **WHEN** any user attempts an action in a finished game
- **THEN** the action is rejected with `The game has already ended.`

#### Scenario: Resigning after the end
- **WHEN** a seated player resigns a finished game
- **THEN** the call is rejected with `The game has already ended.`

### Requirement: End reasons
The system SHALL report a finished game with one of the reasons Checkmate, Stalemate, InsufficientMaterial, FiftyMoveRule, Repetition, Resignation, Timeout, HpDepleted, KingCaptured, BoardHalfEmptied or MoveLimit, and a winner that is absent for a draw. Which reasons a mode can produce is defined per mode. The reason DrawAgreed is defined but nothing produces it.

#### Scenario: Drawn game
- **WHEN** a game ends by stalemate in a standard-rules mode
- **THEN** the reason is Stalemate and no winner is reported

### Requirement: Move selection
The system SHALL accept a move only when its origin, destination and promotion piece match one of the currently available moves. A promoting move SHALL name its promotion piece; there is no default. Martian Chess has no promotion and rejects a move that names a promotion piece. The side that moves SHALL be the game's side to move, whichever seat submitted the action.

#### Scenario: Unavailable move
- **WHEN** a move that is not among the available moves is submitted
- **THEN** the move is rejected and the position is unchanged

#### Scenario: Promotion
- **WHEN** a pawn can reach the last rank in any mode other than Martian Chess
- **THEN** the available moves offer promotion to queen, rook, bishop and knight and no move without a promotion piece
