# web-client Specification

## Purpose
Describe the browser experience: the landing page, the lobby, navigation into a game, the game screen frame, the per-mode panels and the history pages. Rules enforced by the server are specified in the rooms, game-session and per-mode capabilities.

## Requirements

### Requirement: Landing page
The system SHALL show signed-in users a way to create a room for each of the nine modes and a field to join a room by code. The join field SHALL accept at most six characters, and the code SHALL be trimmed and upper-cased before navigation. The landing page SHALL NOT check the code; an unknown code is reported by the lobby. Signed-in users SHALL also see a link to their history.

#### Scenario: Creating a room
- **WHEN** a signed-in user creates a room
- **THEN** the browser navigates to `/room/<code>` with a six-character code

#### Scenario: Joining by code
- **WHEN** a second user enters the code `abc123` and joins
- **THEN** the browser navigates to `/room/ABC123`

#### Scenario: Empty code
- **WHEN** a user submits the join form with an empty or blank code
- **THEN** nothing happens

#### Scenario: Room creation fails
- **WHEN** creating a room fails
- **THEN** the error message is shown on the landing page

#### Scenario: Joining an unknown code
- **WHEN** a user joins a code with no live room and no archived game
- **THEN** the lobby shows a not-found error with a link back to the landing page

### Requirement: Lobby
The system SHALL show the lobby at `/room/<code>` with the room code, a button that copies the room link and confirms it, and one card per seat, live for every user in the room. The system SHALL offer the host the clock inputs (minutes and increment seconds) and the start button.

#### Scenario: Seat controls
- **WHEN** any user views an unlocked lobby
- **THEN** an empty seat offers joining, a bot seat shows its difficulty labelled with the approximate rating in a disabled selector, and a human seat offers leaving only to its occupant

#### Scenario: Host's seat controls
- **WHEN** the host views an unlocked lobby
- **THEN** empty seats also offer adding a bot, bot seats let the host change the difficulty or remove the bot, and seats held by other players offer removing them

#### Scenario: Failed lobby action
- **WHEN** a lobby action is rejected by the server
- **THEN** the message is shown in a dismissable notice and the lobby stays in place

#### Scenario: Live seat changes
- **WHEN** one user claims or leaves a seat
- **THEN** every other user in the room sees the change without reloading

#### Scenario: Adding a bot
- **WHEN** a user adds a bot to a seat
- **THEN** the bot starts at Medium difficulty

#### Scenario: Changing a bot
- **WHEN** a user changes a bot's difficulty or removes it
- **THEN** every user in the room sees the change

#### Scenario: Seats not filled
- **WHEN** at least one seat is empty
- **THEN** a hint says to fill all 4 seats (Hand and Brain) or both seats (other modes)
- **AND** the host's start button is marked `aria-disabled` and ignores clicks

#### Scenario: Non-host view
- **WHEN** a user who is not the host views a full lobby
- **THEN** no start button is shown and a message says the host must start the game

#### Scenario: Host changes the clock
- **WHEN** the host edits the minutes or the increment
- **THEN** every other user sees the new clock setting as read-only text

#### Scenario: Invalid clock input
- **WHEN** the host enters minutes of 0 or less, a negative increment, or a value that is not a whole number
- **THEN** the change is not sent

### Requirement: Navigation into the game
The system SHALL send every user in the lobby to the mode's game page when the game starts, and SHALL send a user who opens the lobby of a started room straight to the game page. The game pages SHALL be `/game/<code>` (Hand and Brain) and `/cardchess`, `/arcanechess`, `/bidding`, `/progressive`, `/alice`, `/absorption`, `/martian` and `/draft`, each followed by `/<code>`.

#### Scenario: Game starts
- **WHEN** the host starts a Card Chess game
- **THEN** every user in the lobby lands on `/cardchess/<code>`

#### Scenario: Late arrival
- **WHEN** a user opens `/room/<code>` after the game started
- **THEN** the user is taken to the game page

### Requirement: Game screen frame
The system SHALL show, on every game page, both clocks in `mm:ss`, the move history in order, a status for whose turn it is, and a resign button for seated players. Only the seat whose turn it is SHALL be able to move on the board, and only among the moves the server published. A player seated as Black SHALL see the board from Black's side. The clock of the side to move SHALL count down locally between updates.

#### Scenario: Spectator
- **WHEN** a signed-in user without a seat opens a game page
- **THEN** the game is shown read-only, without a resign button

#### Scenario: Picking up a piece
- **WHEN** a player starts a move from a square
- **THEN** that piece's available destinations are marked, and a piece with none cannot be picked up

#### Scenario: Resigning
- **WHEN** a seated player presses the resign button
- **THEN** the resignation is sent without asking for confirmation

#### Scenario: Another mode's URL
- **WHEN** a user opens `/game/<code>` for a room that is playing Card Chess
- **THEN** the browser is sent to `/cardchess/<code>`

#### Scenario: Game URL before the game starts
- **WHEN** a user opens a game URL for a room whose game has not started
- **THEN** the browser is sent to `/room/<code>`

#### Scenario: Reloading a game page
- **WHEN** a user reloads or directly opens the game page of a running game
- **THEN** the page joins the room and loads the full state, including the move history

#### Scenario: Clock from the lobby
- **WHEN** the host set the clock to 3 minutes and started the game
- **THEN** both clocks start at `03:00`

#### Scenario: Not your turn
- **WHEN** it is another seat's turn
- **THEN** the board does not accept moves from this user

#### Scenario: Two ways to move
- **WHEN** a player moves by clicking origin and destination, or by dragging the piece, on any board other than the Martian Chess grid
- **THEN** the move is submitted either way

#### Scenario: Promotion
- **WHEN** a pawn is moved to the last rank
- **THEN** a dialog asks for the promotion piece and the pawn promotes to the chosen piece

### Requirement: Mode panels
The system SHALL show each mode's own state next to the board and submit that mode's actions.

#### Scenario: Hand and Brain
- **WHEN** a Hand and Brain game is shown to a player on the side to move
- **THEN** six piece-kind cards are shown to the Brain, selectable among the announceable kinds, and to the Hand once a kind is announced
- **AND** seated opponents see the announced kind as read-only text

#### Scenario: Card Chess
- **WHEN** a Card Chess game is shown to a player
- **THEN** the player's hand, the opponent's hand with its reroll marks and both HP counters are shown, with banners for an emergency move, a hand with no playable card, and a hand whose playable cards are all marked
- **AND** clicking a hand card on the player's own turn toggles its reroll mark

#### Scenario: Arcane Chess
- **WHEN** an Arcane Chess game is shown to a player
- **THEN** the Card Chess panel is shown together with mana, the player's own spell hand, the active effects and a target form
- **AND** the opponent's hand is replaced by a `hidden` notice unless the server sent it, and spells are disabled once the server reports a cast this turn
- **AND** while a spell's targets are being chosen the board selects squares instead of moving pieces

#### Scenario: Bidding Chess
- **WHEN** a Bidding Chess game is in its bidding phase
- **THEN** both chip counts with the tie marker and the last bids are shown, and each seated player who has not bid sees a bid field

#### Scenario: Progressive Chess
- **WHEN** a Progressive Chess game is shown
- **THEN** the series number and the moves played and left in it are shown

#### Scenario: Alice Chess
- **WHEN** an Alice Chess game is shown
- **THEN** two boards are shown, each offering the moves of the pieces standing on it, with a notice when the side to move is in check

#### Scenario: Absorption Chess
- **WHEN** an Absorption Chess game is shown
- **THEN** the pieces with extra powers are listed, with a notice when the side to move is in check

#### Scenario: Martian Chess
- **WHEN** a Martian Chess game is shown
- **THEN** a 4 by 8 grid operated by clicking origin then destination and both scores are shown

#### Scenario: Draft Chess
- **WHEN** a Draft Chess game is drafting or placing
- **THEN** both budgets are shown, with the pool, its prices and a pass button during the draft, and a placement tray with a square-select board during placement
- **AND** clicking a square the player already placed on removes that piece

#### Scenario: Marking a third card
- **WHEN** a Card Chess player with two cards marked clicks a third
- **THEN** nothing is sent

#### Scenario: Bid above the chips held
- **WHEN** a Bidding Chess player with 40 chips enters 70 and submits
- **THEN** a bid of 40 is sent

### Requirement: Game over screen
The system SHALL replace the resign button and the turn status with a result banner when the game ends, for every seat, and SHALL offer a way back to the room.

#### Scenario: Checkmate
- **WHEN** a game ends by checkmate
- **THEN** every seat sees the banner with the winner and the reason

#### Scenario: Back to the room
- **WHEN** a player returns to the room after the game ended
- **THEN** the lobby says the room's game has ended and links to the landing page to start a new room
- **AND** no seats are shown

### Requirement: Missing room on a game page
The system SHALL show a notice that the room was not found, with a link back to the landing page, on a game page whose code has no live room and no archived game.

#### Scenario: Room gone
- **WHEN** a user opens `/game/<code>` for a code with no live room and no record
- **THEN** the notice is shown instead of a board

### Requirement: History pages
The system SHALL list the signed-in user's 50 most recent finished games at `/history` and show one game at `/history/<id>` with its result, players, final position and moves. The result SHALL be shown from the viewer's side (won, lost, draw) when the viewer played, and as the winning colour otherwise. Boards SHALL be shown from White's side and there is no move replay.

#### Scenario: Final position by mode
- **WHEN** a finished game is shown
- **THEN** the final position is one board, two boards for Alice Chess, a board with a powers line for Absorption Chess, or a text grid with the score for Martian Chess

#### Scenario: Unknown game
- **WHEN** a user opens `/history/<id>` for an identifier with no record
- **THEN** a not-found message is shown

#### Scenario: Identifier is not a GUID
- **WHEN** a user opens `/history/abc`
- **THEN** the not-found page is shown

#### Scenario: No finished games
- **WHEN** a user with no finished games opens `/history`
- **THEN** an empty-list message is shown

### Requirement: Connection handling
The system SHALL connect to the game hub with automatic reconnection, SHALL rejoin the room and reload its state after a reconnect, and SHALL show the server's message for a failed hub call, and any `RoomNotice`, in a dismissable notice without removing the page content.

#### Scenario: Rejected action
- **WHEN** the server rejects a move on a game page
- **THEN** the message is shown over the page, the board stays in place, and clicking the message dismisses it

#### Scenario: Bot stopped responding
- **WHEN** the server sends `RoomNotice`
- **THEN** its text is shown in the same notice

#### Scenario: Page cannot load
- **WHEN** joining the room or loading the game state fails while a page opens
- **THEN** the page shows only the error message

#### Scenario: Connection restored
- **WHEN** the hub connection reconnects while a lobby or game page is open
- **THEN** the page rejoins the room and shows the current state without a reload

#### Scenario: Rejected spell
- **WHEN** the server rejects an Arcane Chess spell
- **THEN** the error is shown in the spell form and the board stays in place

### Requirement: Version label
The system SHALL display the application version in the bottom corner of every page on large screens, falling back to `dev` when no version is embedded.

#### Scenario: Container build without a version argument
- **WHEN** the image is built without `APP_VERSION`
- **THEN** the label reads `vdev`

#### Scenario: Deployed build
- **WHEN** the image is built by the deploy workflow
- **THEN** the label shows the short commit hash
