# bots Specification

## Purpose
Describe how bot seats take their turns and how each mode's bot chooses an action.

## Requirements

### Requirement: Bots act when their seat is active
The system SHALL play bot seats automatically after every game broadcast, in rounds, until no active seat is held by a bot or the game ends. Each round SHALL wait a random 400 to 899 milliseconds, let every bot seat that was active before the wait apply one action, then broadcast `GameUpdated` once and archive the game if it has finished. Bot turns for one room SHALL run one at a time.

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

#### Scenario: Rules reject a bot action
- **WHEN** applying a bot's action raises a rule violation
- **THEN** the rejection is logged at debug level, the round still sends `GameUpdated`, and the loop runs again

#### Scenario: Bot keeps being rejected
- **WHEN** 5 rounds in a row apply no bot action
- **THEN** the run is handled as a failure

#### Scenario: Bot fails to choose an action
- **WHEN** choosing an action throws, or any other error occurs during a round
- **THEN** the error is logged, the room's bot and engine are discarded, and the run is tried again after 2 seconds with a fresh engine

#### Scenario: Bot fails three times in a row
- **WHEN** a room's bot run fails for the third time in a row
- **THEN** no further retry is scheduled, the room receives `RoomNotice` saying a bot stopped responding, and the bot stays idle until the next game update in that room

#### Scenario: Bot takes too long
- **WHEN** a bot has not chosen an action within 20 seconds
- **THEN** the choice is cancelled and handled as a failure

### Requirement: Engine-backed bots
The system SHALL use a Stockfish process, one per room, for Hand and Brain, Card Chess, Arcane Chess and Progressive Chess, started on the room's first bot turn from the path in `Bots:StockfishPath` (default `stockfish`) and stopped when the room's game ends. The engine SHALL search only among the moves the mode currently allows. These bots never reroll cards.

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
- **WHEN** the engine cannot be started for a room
- **THEN** the failure is logged, the bot does not act, and the start is attempted again on the next scheduled bot turn

#### Scenario: Engine does not answer
- **WHEN** the engine has not completed its start-up handshake within 20 seconds
- **THEN** the start is cancelled and handled as a failure

### Requirement: Arcane Chess bot spells
The system SHALL let the Arcane Chess bot consider casting one spell before each move. A cast SHALL be the bot's whole action for that round, with the move following in the next round. The bot SHALL never cast Swap, Teleport, Extra Turn, Execution, Mind Swap or Time Freeze.

#### Scenario: Emergency move pending
- **WHEN** the bot can only make an emergency move, holds Deep Breath and can afford it
- **THEN** it casts Deep Breath

#### Scenario: Bot below full HP
- **WHEN** the bot has lost HP and holds an affordable Restoration or Mend
- **THEN** it casts Restoration if possible, otherwise Mend

#### Scenario: Optional spell
- **WHEN** neither case above applies
- **THEN** with a 30 percent chance the bot goes through its spell hand in random order and casts the first affordable spell among Peek, Jam, Feint, Reshuffle, Snap Swap, Shield, Freeze Square, Pin Down, Disarm and Dispel for which it finds a target

#### Scenario: Spell targets
- **WHEN** the bot casts a targeted spell
- **THEN** Snap Swap takes a random card from its own hand, Shield a random own piece other than the king, Freeze Square a random empty square, Pin Down and Disarm a random opposing piece other than the king, and Dispel the square of a random active effect regardless of who owns it

### Requirement: Greedy bots
The system SHALL use a one-move-deep bot for Bidding Chess, Alice Chess, Absorption Chess, Martian Chess and Draft Chess. The bot SHALL score a move as ten times the value of the captured piece plus eight times the value of the promotion piece (pawn 1, knight 3, bishop 3, rook 5, queen 9, king 100) and play a random move among the best scored. Hard and Expert SHALL play identically. The rating shown for a bot's difficulty has no effect on these bots.

#### Scenario: Easy difficulty
- **WHEN** an Easy greedy bot is to move
- **THEN** it plays a uniformly random available move

#### Scenario: Medium difficulty
- **WHEN** a Medium greedy bot is to move
- **THEN** one time in three it plays a random move, otherwise a best-scored move

#### Scenario: Hard or Expert difficulty
- **WHEN** a Hard or Expert greedy bot is to move
- **THEN** it always plays a best-scored move

#### Scenario: Absorption Chess preference
- **WHEN** an Absorption Chess bot scores a capture of a non-king piece
- **THEN** three times the captured piece's value is added to the score

#### Scenario: Martian Chess preference
- **WHEN** a Martian Chess bot scores a non-capturing move that ends outside its own half
- **THEN** six times the moved piece's value (pawn 1, drone 3, queen 9) is subtracted from the score

### Requirement: Bidding Chess bot bids
The system SHALL have the Bidding Chess bot bid according to its chips and the position. Medium, Hard and Expert SHALL bid identically. Whether a king can be taken SHALL be judged on each side's possible moves regardless of whose turn it is.

#### Scenario: No chips
- **WHEN** the bot holds no chips
- **THEN** it bids 0

#### Scenario: Easy bid
- **WHEN** an Easy bot bids
- **THEN** it bids a random amount from 0 to the smaller of its chips and 20

#### Scenario: Bot can take the king
- **WHEN** a non-Easy bot could capture the opposing king with the next move
- **THEN** it bids all of its chips

#### Scenario: Opponent can take the king
- **WHEN** the opponent could capture the bot's king with the next move and the bot cannot capture theirs
- **THEN** it bids three quarters of its chips, at least 1

#### Scenario: Ordinary bid
- **WHEN** neither king can be taken
- **THEN** the bot bids 2 plus three times the value of its best available capture, capped by its chips, minus a random 0 to 2, never below 0

### Requirement: Draft Chess bot drafting and placement
The system SHALL have the Draft Chess bot draft and place its army without input, one pick or one piece per action. Medium, Hard and Expert SHALL draft identically and placement SHALL not depend on difficulty.

#### Scenario: Drafting
- **WHEN** a non-Easy bot is to pick
- **THEN** it takes the most valuable kind it may pick in the order queen, rook, bishop, knight, pawn

#### Scenario: Easy drafting
- **WHEN** an Easy bot is to pick
- **THEN** it takes a random kind it may pick

#### Scenario: Nothing to pick
- **WHEN** the bot may pick no kind
- **THEN** it passes

#### Scenario: Placement
- **WHEN** the bot is placing
- **THEN** it places the king first on the back rank, then queens, rooks, bishops and knights on the back rank and pawns on the pawn rank, filling files in the order e, d, f, c, g, b, h, a
