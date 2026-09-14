// Blazor JS interop wrapper around cm-chessboard (https://github.com/shaack/cm-chessboard, MIT).
// The board itself never decides legality — the server is authoritative and pushes down the
// current legal-move list via setLegalMoves(); this file's only job is turning that list into
// input validation/markers and reporting finished moves back to .NET.
import {Chessboard, COLOR, INPUT_EVENT_TYPE, BORDER_TYPE} from "./cm-chessboard/src/Chessboard.js"
import {Markers, MARKER_TYPE} from "./cm-chessboard/src/extensions/markers/Markers.js"
import {PromotionDialog, PROMOTION_DIALOG_RESULT_TYPE} from "./cm-chessboard/src/extensions/promotion-dialog/PromotionDialog.js"
import {Accessibility} from "./cm-chessboard/src/extensions/accessibility/Accessibility.js"

class BoardController {
    constructor(element, dotNetRef, fen, orientationIsBlack) {
        this.dotNetRef = dotNetRef
        this.legalMoves = []
        this.currentFen = fen

        this.board = new Chessboard(element, {
            position: fen,
            orientation: orientationIsBlack ? COLOR.black : COLOR.white,
            assetsUrl: "js/cm-chessboard/assets/",
            style: {
                cssClass: "hand-and-brain",
                showCoordinates: true,
                borderType: BORDER_TYPE.none,
                pieces: {file: "pieces/standard.svg"},
                animationDuration: 250
            },
            extensions: [
                {class: Markers, props: {autoMarkers: MARKER_TYPE.square}},
                {class: PromotionDialog},
                {class: Accessibility, props: {visuallyHidden: true}}
            ]
        })

        this.inputHandler = this.inputHandler.bind(this)
    }

    setPosition(fen, animated) {
        this.currentFen = fen
        return this.board.setPosition(fen, animated)
    }

    setOrientation(isBlack) {
        return this.board.setOrientation(isBlack ? COLOR.black : COLOR.white, true)
    }

    // moves: [{from, to, promotion}], promotion is "q"/"r"/"b"/"n" or null — mirrors the server's
    // current AvailableMoves exactly, so validation here is just a lookup, never a rules decision.
    setLegalMoves(moves) {
        this.legalMoves = moves
    }

    setInteractive(interactive) {
        if (interactive) {
            if (!this.board.isMoveInputEnabled()) {
                this.board.enableMoveInput(this.inputHandler)
            }
        } else {
            this.board.disableMoveInput()
        }
    }

    inputHandler(event) {
        switch (event.type) {
            case INPUT_EVENT_TYPE.moveInputStarted: {
                const moves = this.legalMoves.filter(m => m.from === event.squareFrom)
                event.chessboard.addLegalMovesMarkers(moves)
                return moves.length > 0
            }
            case INPUT_EVENT_TYPE.validateMoveInput: {
                const candidates = this.legalMoves.filter(m => m.from === event.squareFrom && m.to === event.squareTo)
                if (candidates.length === 0) {
                    return false
                }
                if (candidates.length === 1 && !candidates[0].promotion) {
                    this.reportMove(event.squareFrom, event.squareTo, null)
                    return true
                }
                // Promotion: several legal moves share this from/to, one per promotion piece —
                // let the player choose, then report whichever one they picked.
                const piece = this.board.getPiece(event.squareFrom)
                const color = piece ? piece.charAt(0) : COLOR.white
                event.chessboard.showPromotionDialog(event.squareTo, color, (result) => {
                    if (result.type === PROMOTION_DIALOG_RESULT_TYPE.pieceSelected) {
                        this.reportMove(event.squareFrom, event.squareTo, result.piece.charAt(1))
                    } else {
                        this.board.setPosition(this.currentFen, true)
                    }
                })
                return true
            }
            case INPUT_EVENT_TYPE.moveInputFinished:
                event.chessboard.removeLegalMovesMarkers()
                break
        }
    }

    reportMove(from, to, promotion) {
        this.dotNetRef.invokeMethodAsync("OnMoveInput", from, to, promotion)
    }

    destroy() {
        this.board.destroy()
    }
}

export function create(element, dotNetRef, fen, orientationIsBlack) {
    return new BoardController(element, dotNetRef, fen, orientationIsBlack)
}
