using System.Diagnostics;
using System.Text;
using chess_engine.Helpers;

namespace chess_engine.Models
{
    public class Board
    {
        // Mailbox: index = rank*8 + file, 0=a1, 63=h8
        public readonly int[] Squares;

        /// <summary>
        /// Castling rights packed as 4 bits: KQkq
        /// </summary>
        public int CastlingRights;

        /// <summary>
        /// En passant target square (-1 = none)
        /// </summary>
        public int EnPassantSquare;

        // Side to move
        public int ColorToMove;

        // Half-move clock (50-move rule)
        public int HalfMoveClock;

        // Full-move number
        public int FullMoveNumber;

        public readonly Stack<GameState> GameStates;

        public Board()
        {
            Squares = new int[64];
            CastlingRights = 0b1111; // KQkq all available
            EnPassantSquare = -1;
            ColorToMove = Piece.White;
            HalfMoveClock = 0;
            FullMoveNumber = 0;
            GameStates = [];
        }

        public static Board FromStartPosition(string fen)
        {
            // https://de.wikipedia.org/wiki/Forsyth-Edwards-Notation

            var pieceDict = new Dictionary<char, int>
            {
                { 'r', Piece.Black | Piece.Rook },
                { 'n', Piece.Black | Piece.Knight },
                { 'b', Piece.Black | Piece.Bishop },
                { 'q', Piece.Black | Piece.Queen },
                { 'k', Piece.Black | Piece.King },
                { 'p', Piece.Black | Piece.Pawn },
                { 'R', Piece.White | Piece.Rook },
                { 'N', Piece.White | Piece.Knight },
                { 'B', Piece.White | Piece.Bishop },
                { 'Q', Piece.White | Piece.Queen },
                { 'K', Piece.White | Piece.King },
                { 'P', Piece.White | Piece.Pawn },
            };

            var board = new Board();
            int curIndex = 0;
            string fenBoard = fen.Split(' ')[0];

            foreach (char symbol in fenBoard)
            {
                if (symbol == '/')
                {
                    continue;
                }
                else if (char.IsNumber(symbol))
                {
                    curIndex += (int)char.GetNumericValue(symbol);
                }
                else if (char.IsLetter(symbol))
                {
                    board.Squares[ToEngineIndex(curIndex)] = pieceDict[symbol];
                    curIndex++;
                }
            }

            MoveGenerator.PrecomputedMoveData();
            MoveGenerator.GenerateLegalMoves(board);

            return board;
        }

        public void MakeMove(Move move, bool uiMove = false)
        {
            var gs = new GameState
            {
                EnPassantSquare = EnPassantSquare,
                EnPassantCaptureSquare = -1,
                CastlingRights = CastlingRights,
                Move = move,
                CapturedPiece = Squares[move.To],
                //FEN = GetFEN(),
                HalfMoveClock = HalfMoveClock,
                FullMoveNumber = FullMoveNumber,
            };

            EnPassantSquare = -1;

            int movePiece = Piece.TypeOf(Squares[move.From]);
            var whiteMoved = ColorToMove == Piece.White;

            // Reset on capture or pawn move, otherwise increment
            bool isCapture = gs.CapturedPiece != Piece.None || move.Flag == MoveFlag.EnPassant;
            bool isPawnMove = movePiece == Piece.Pawn;

            if (isCapture || isPawnMove)
                HalfMoveClock = 0;
            else
                HalfMoveClock++;

            // Update castling rights
            if (CastlingRights > 0 && (movePiece == Piece.King || movePiece == Piece.Rook))
            {
                var kingRights = whiteMoved ? 0b0011 : 0b1100;
                var kingSideRights = whiteMoved ? 0b0111 : 0b1101;
                var queenSideRights = whiteMoved ? 0b1011 : 0b1110;

                if (movePiece == Piece.King)
                {
                    CastlingRights &= kingRights;
                }
                else
                {
                    var fileOf = FileOf(move.From);
                    if (fileOf == 0)
                        CastlingRights &= queenSideRights;
                    else if (fileOf == 7)
                        CastlingRights &= kingSideRights;
                }
            }

            if (move.Flag == MoveFlag.DoublePawnPush)
                EnPassantSquare = whiteMoved ? move.To - 8 : move.To + 8;

            Squares[move.To] = Squares[move.From];
            Squares[move.From] = Piece.None;

            if (move.Flag == MoveFlag.EnPassant)
            {
                var takenPawn = whiteMoved ? move.To - 8 : move.To + 8;
                gs.EnPassantCaptureSquare = takenPawn;
                Squares[takenPawn] = Piece.None;
            }

            // Pawn Promotions
            if (move.IsPromotion)
            {
                if (move.Flag == MoveFlag.PromoteQueen) Squares[move.To] = Piece.ColorOf(Squares[move.To]) | Piece.Queen;
                if (move.Flag == MoveFlag.PromoteRook) Squares[move.To] = Piece.ColorOf(Squares[move.To]) | Piece.Rook;
                if (move.Flag == MoveFlag.PromoteBishop) Squares[move.To] = Piece.ColorOf(Squares[move.To]) | Piece.Bishop;
                if (move.Flag == MoveFlag.PromoteKnight) Squares[move.To] = Piece.ColorOf(Squares[move.To]) | Piece.Knight;
            }

            // Castling
            if (move.Flag == MoveFlag.KingSideCastle)
            {
                Squares[move.To - 1] = Squares[move.To + 1];
                Squares[move.To + 1] = Piece.None;
            }
            else if (move.Flag == MoveFlag.QueenSideCastle)
            {
                Squares[move.To + 1] = Squares[move.To - 2];
                Squares[move.To - 2] = Piece.None;
            }

            ColorToMove = ColorToMove == Piece.White ? Piece.Black : Piece.White;
            if (!whiteMoved)
                FullMoveNumber++;

            GameStates.Push(gs);
        }

        public void UnmakeLastMove()
        {
            if (GameStates.Count < 1)
                return;

            var gs = GameStates.Pop();
            UnmakeMove(gs.Move, gs);
        }

        public void UnmakeMove(Move move, GameState gameState)
        {
            EnPassantSquare = gameState.EnPassantSquare;
            CastlingRights = gameState.CastlingRights;
            Squares[move.From] = Squares[move.To];
            Squares[move.To] = gameState.CapturedPiece;
            HalfMoveClock = gameState.HalfMoveClock;
            FullMoveNumber = gameState.FullMoveNumber;

            // Castling
            if (move.Flag == MoveFlag.KingSideCastle)
            {
                Squares[move.To + 1] = Squares[move.To - 1];
                Squares[move.To - 1] = Piece.None;
            }
            else if (move.Flag == MoveFlag.QueenSideCastle)
            {
                Squares[move.To - 2] = Squares[move.To + 1];
                Squares[move.To + 1] = Piece.None;
            }

            ColorToMove = ColorToMove == Piece.White ? Piece.Black : Piece.White;

            // Promotion
            if (move.IsPromotion)
            {
                if (ColorToMove == Piece.White)
                    Squares[move.From] = Piece.Pawn | Piece.White;
                else
                    Squares[move.From] = Piece.Pawn | Piece.Black;
            }

            // En Passant
            if (move.Flag == MoveFlag.EnPassant)
            {
                if (ColorToMove == Piece.White)
                    Squares[gameState.EnPassantCaptureSquare] = Piece.Pawn | Piece.Black;
                else
                    Squares[gameState.EnPassantCaptureSquare] = Piece.Pawn | Piece.White;
            }
        }

        // Square index helpers
        public static int IndexOf(int file, int rank) => rank * 8 + file;
        public static int FileOf(int index) => index % 8;
        public static int RankOf(int index) => index / 8;
        public static int ToUiIndex(int rank, int file) => (7 - rank) * 8 + file;
        public static int ToUiIndex(int engineIndex)
        {
            var rank = RankOf(engineIndex);
            var file = FileOf(engineIndex);

            return ToUiIndex(rank, file);
        }
        public static int ToEngineIndex(int uiIndex)
        {
            var rank = 7 - RankOf(uiIndex); // flip rank: UI row 0 = engine rank 7
            var file = FileOf(uiIndex);
            return IndexOf(file, rank);
        }

        public int GetKingSquare(int color)
        {
            for (int i = 0; i < 64; i++)
            {
                if (Piece.TypeOf(Squares[i]) == Piece.King)
                {
                    if (Piece.IsColor(Squares[i], color))
                        return i;
                }
            }

            return -1;
        }

        public bool IsInCheck()
        {
            int kingSquare = GetKingSquare(ColorToMove);
            if (kingSquare == -1) return false;

            // Temporarily flip the turn so GenerateMoves produces opponent moves
            ColorToMove = ColorToMove == Piece.White ? Piece.Black : Piece.White;
            var opponentMoves = MoveGenerator.GenerateMoves(this);
            ColorToMove = ColorToMove == Piece.White ? Piece.Black : Piece.White;

            return opponentMoves.Any(m => m.To == kingSquare);
        }

        public string GetFEN()
        {
            var sb = new StringBuilder();
            int emptySquares = 0;

            for (int r = 0; r < 8; r++)
            {
                for (int f = 0; f < 8; f++)
                {
                    var piece = Squares[ToUiIndex(r, f)];
                    bool resetEmptySquares = true;
                    char? pieceToAppend;

                    if (Piece.TypeOf(piece) == Piece.Rook)
                    {
                        if (Piece.IsColor(piece, Piece.White))
                            pieceToAppend = 'R';
                        else
                            pieceToAppend = 'r';
                    }
                    else if (Piece.TypeOf(piece) == Piece.Knight)
                    {
                        if (Piece.IsColor(piece, Piece.White))
                            pieceToAppend = 'N';
                        else
                            pieceToAppend = 'n';
                    }
                    else if (Piece.TypeOf(piece) == Piece.Bishop)
                    {
                        if (Piece.IsColor(piece, Piece.White))
                            pieceToAppend = 'B';
                        else
                            pieceToAppend = 'b';
                    }
                    else if (Piece.TypeOf(piece) == Piece.Queen)
                    {
                        if (Piece.IsColor(piece, Piece.White))
                            pieceToAppend = 'Q';
                        else
                            pieceToAppend = 'q';
                    }
                    else if (Piece.TypeOf(piece) == Piece.King)
                    {
                        if (Piece.IsColor(piece, Piece.White))
                            pieceToAppend = 'K';
                        else
                            pieceToAppend = 'k';
                    }
                    else if (Piece.TypeOf(piece) == Piece.Pawn)
                    {
                        if (Piece.IsColor(piece, Piece.White))
                            pieceToAppend = 'P';
                        else
                            pieceToAppend = 'p';
                    }
                    else if (Piece.TypeOf(piece) == Piece.None)
                    {
                        emptySquares++;
                        resetEmptySquares = false;
                        pieceToAppend = null;
                    }
                    else
                        throw new NotImplementedException("Piece type not implemented");


                    if (emptySquares > 0 && resetEmptySquares)
                    {
                        sb.Append(emptySquares);
                        emptySquares = 0;
                    }

                    if (pieceToAppend is not null)
                        sb.Append(pieceToAppend);
                }

                if (emptySquares > 0)
                    sb.Append(emptySquares);

                if (r < 7)
                    sb.Append('/');

                emptySquares = 0;
            }

            sb.Append(' ');

            if (ColorToMove == Piece.White)
                sb.Append('w');
            else
                sb.Append('b');

            sb.Append(' ');

            if (CastlingRights > 0)
            {
                if ((CastlingRights & 0b1000) != 0) sb.Append('K');
                if ((CastlingRights & 0b0100) != 0) sb.Append('Q');
                if ((CastlingRights & 0b0010) != 0) sb.Append('k');
                if ((CastlingRights & 0b0001) != 0) sb.Append('q');
            }
            else
                sb.Append('-');

            sb.Append(' ');

            if (EnPassantSquare != -1)
                sb.Append($"{(char)('a' + FileOf(EnPassantSquare))}{RankOf(EnPassantSquare) + 1}");
            else
                sb.Append('-');

            sb.Append(' ');
            sb.Append(HalfMoveClock);

            sb.Append(' ');
            sb.Append(FullMoveNumber);

            string fen = sb.ToString();
            return fen;
        }

        public void AssertIntegrity()
        {
            int whitePawns = 0, blackPawns = 0;
            int whiteKings = 0, blackKings = 0;

            for (int i = 0; i < 64; i++)
            {
                int t = Piece.TypeOf(Squares[i]);
                int c = Piece.ColorOf(Squares[i]);
                if (t == Piece.Pawn && c == Piece.White) whitePawns++;
                if (t == Piece.Pawn && c == Piece.Black) blackPawns++;
                if (t == Piece.King && c == Piece.White) whiteKings++;
                if (t == Piece.King && c == Piece.Black) blackKings++;
            }

            Debug.Assert(whitePawns <= 8, $"Too many white pawns: {whitePawns} | FEN: {GetFEN()} | GameStates: {GetGameState()}");
            Debug.Assert(blackPawns <= 8, $"Too many black pawns: {blackPawns} | FEN: {GetFEN()} | GameStates: {GetGameState()}");
            Debug.Assert(whiteKings == 1, $"White king count wrong: {whiteKings} | FEN: {GetFEN()} | GameStates: {GetGameState()}");
            Debug.Assert(blackKings == 1, $"Black king count wrong: {blackKings} | FEN: {GetFEN()} | GameStates: {GetGameState()}");
        }

        public string GetGameState()
        {
            return string.Join(", ", GameStates.Select(x => x.Move));
        }
    }
}