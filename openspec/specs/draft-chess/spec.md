# draft-chess Specification

## Purpose
Describe Draft Chess: both sides buy an army from a shared pool, lay it out on their own two ranks, and then play chess with what they built.

## Requirements

### Requirement: Three phases
The system SHALL run a game through the phases Drafting, Placing and Playing, in that order.

#### Scenario: Opening state
- **WHEN** a game starts
- **THEN** the phase is Drafting, White is to pick and the pool is full

#### Scenario: Move before play
- **WHEN** a chess move is submitted during Drafting or Placing
- **THEN** the move is rejected with `The armies are still being built.`

### Requirement: Pool, budget and prices
The system SHALL give each side a budget of 24 points and a shared pool of 2 queens, 4 rooks, 4 bishops, 4 knights and 16 pawns. Base prices SHALL be queen 9, rook 5, bishop 3, knight 3, pawn 1. Each time a kind is taken by either side its price SHALL rise for both sides by 4 for a queen, 2 for a rook, 1 for a bishop or knight and 0 for a pawn. Every side SHALL also own one king at no cost.

#### Scenario: First queen
- **WHEN** White takes the first queen
- **THEN** White has 15 points left, one queen remains in the pool and the queen now costs 13

#### Scenario: Price paid is the price shown
- **WHEN** a side takes a kind whose price has risen
- **THEN** it pays the risen price

### Requirement: Pick order
The system SHALL order picks White, Black, Black, White, White, Black, Black and so on. A side that cannot pick anything or has passed SHALL be skipped. A side skipped because it can afford nothing SHALL NOT be marked as passed.

#### Scenario: Snake order
- **WHEN** White picks, then Black picks
- **THEN** Black picks again before White

#### Scenario: Pick out of turn
- **WHEN** a side picks when it is the other side's pick
- **THEN** the pick is rejected with `It is not <side>'s pick.`

### Requirement: Pick limits
The system SHALL let a side take a kind only when the pool holds one, the side can afford its current price, and the side stays within 8 pawns and 7 other pieces.

#### Scenario: Too expensive
- **WHEN** a side with 4 points left picks a rook priced 5
- **THEN** the pick is rejected with `<side> cannot take a Rook right now.`

#### Scenario: King requested
- **WHEN** a side picks a king
- **THEN** the pick is rejected with `<side> cannot take a King right now.`

#### Scenario: Pick after the draft
- **WHEN** a side picks or passes once the draft is over
- **THEN** the action is rejected with `The draft is over.`

### Requirement: Passing and end of the draft
The system SHALL let the side to pick pass, which removes it from the draft for good. The draft SHALL end when neither side can pick, and the phase SHALL become Placing.

#### Scenario: One side passes
- **WHEN** White passes
- **THEN** every remaining pick goes to Black

#### Scenario: Both sides pass
- **WHEN** both sides have passed
- **THEN** the phase is Placing

### Requirement: Placement
The system SHALL let both sides place their pieces at the same time during Placing, each on its own two home ranks, and SHALL let a side take a placed piece back for as long as the phase lasts, also after it has placed everything. A side's layout SHALL be sent only to that side until play starts.

#### Scenario: Where each kind goes
- **WHEN** a side places its pieces
- **THEN** pawns are accepted only on its second rank and other bought pieces only on its back rank
- **AND** the king is accepted on the back rank, or on the second rank when that rank has more free squares than the side has pawns left to place

#### Scenario: Pawn off the second rank
- **WHEN** a side places a pawn on its back rank
- **THEN** the placement is rejected with `<side> cannot put a Pawn on <square>.`

#### Scenario: Officer on the pawn rank
- **WHEN** a side places a rook on its second rank
- **THEN** the placement is rejected

#### Scenario: Outside the home ranks
- **WHEN** White places a piece on rank 3 or above
- **THEN** the placement is rejected

#### Scenario: King on the second rank
- **WHEN** a side with fewer than eight pawns places its king on a free second-rank square
- **THEN** the placement is accepted

#### Scenario: King where the pawns would not fit
- **WHEN** a side that bought eight pawns places its king on the second rank
- **THEN** the placement is rejected

#### Scenario: Nothing left of that kind
- **WHEN** a side places a kind it has already placed all of
- **THEN** the placement is rejected with `<side> has no <kind> left to place.`

#### Scenario: Taking a piece back
- **WHEN** a side unplaces one of its placed pieces
- **THEN** the piece is available to place again

#### Scenario: Taking a piece back after finishing
- **WHEN** a side that has placed everything unplaces a piece while the other side is still placing
- **THEN** the piece returns to its tray and the side is on the clock again from that moment

#### Scenario: Opponent's layout hidden
- **WHEN** an update is built for Black during Placing
- **THEN** it contains Black's placements and placeable squares and none of White's

#### Scenario: Layouts revealed
- **WHEN** play starts
- **THEN** every recipient receives both layouts

#### Scenario: Taking back from an empty square
- **WHEN** a side unplaces a square it has nothing on
- **THEN** the action is rejected with `<side> has nothing on <square>.`

#### Scenario: Placing during the draft
- **WHEN** a side places a piece while the phase is Drafting
- **THEN** the placement is rejected with `Pieces can only be laid out once the draft is over.`

#### Scenario: Placeable squares published
- **WHEN** a side still has pieces to place, in any phase before play
- **THEN** the published state lists, for each remaining kind, the squares it may stand on

### Requirement: Start of play
The system SHALL start play when both sides have placed every bought piece and their king. Each side SHALL receive 20 seconds on its clock for every unspent budget point and castling SHALL NOT be available. White SHALL move first, unless only Black's king is attacked in the starting position, in which case Black SHALL move first. The starting position SHALL be judged at once.

#### Scenario: Unspent points
- **WHEN** play starts and a side has 3 points left
- **THEN** 60 seconds are added to that side's clock

#### Scenario: Black starts in check
- **WHEN** play starts with Black's king attacked and White's king not attacked
- **THEN** Black is to move

#### Scenario: Both kings start attacked
- **WHEN** play starts with both kings attacked
- **THEN** White is to move and must answer its own check

#### Scenario: White checkmated by the layout
- **WHEN** play starts with White in check and without a legal move
- **THEN** the game ends at once by Checkmate with Black as winner

#### Scenario: White stalemated by the layout
- **WHEN** play starts with White not in check and without a legal move
- **THEN** the game ends at once as a draw by Stalemate

#### Scenario: Layout locked
- **WHEN** a side places or unplaces a piece after play has started
- **THEN** the action is rejected with `Pieces can only be laid out once the draft is over.` or `The layout is already locked in.`

### Requirement: Play and game end
The system SHALL play the Playing phase with standard chess movement, offering only moves that do not leave the mover's king attacked. The game SHALL end by Checkmate, and as a draw by Stalemate, by Repetition when the same placement and side to move occurs three times, by FiftyMoveRule after 100 half-moves without a pawn move or capture, and by MoveLimit after 300 moves. There SHALL be no insufficient-material draw. Resignation and timeout SHALL apply as in every mode.

#### Scenario: Checkmate
- **WHEN** a move leaves the opponent in check with no legal move
- **THEN** the game ends by Checkmate with the mover as winner

### Requirement: Clock across phases
The system SHALL run the clock of the side to pick during Drafting, the clocks of the sides that have not finished placing during Placing, and the clock of the side to move during Playing. During Placing each side SHALL be charged only its own time, measured from the start of the phase or from its own last action. The increment SHALL be added only after chess moves.

#### Scenario: Both sides placing
- **WHEN** White places its last piece 5 seconds into Placing and Black its last piece 8 seconds in
- **THEN** White is charged 5 seconds and Black 8 seconds

#### Scenario: Slow drafter
- **WHEN** the side to pick runs out of time during Drafting
- **THEN** the game ends by Timeout with the other side as winner

### Requirement: Position text before play
The system SHALL publish, until play starts, a position text of the phase followed by each side's picks, as `<phase> W:<picks> B:<picks>`. Until play starts no moves SHALL be available and the published side to move SHALL be White.

#### Scenario: During the draft
- **WHEN** White has picked a queen and Black a rook
- **THEN** the position text is `Drafting W:Queen B:Rook`
