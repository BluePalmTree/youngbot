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
            FullMoveNumber = 1;
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
            MoveGenerator.GenerateMoves(board);

            return board;
        }

        public void MakeMove(Move move)
        {
            var gs = new GameState
            {
                EnPassantSquare = EnPassantSquare,
                EnPassantCaptureSquare = -1,
                CastlingRights = CastlingRights,
                Move = move,
                CapturedPiece = Squares[move.To]
            };

            EnPassantSquare = -1;

            int movePiece = Piece.TypeOf(Squares[move.From]);
            var whiteMoved = ColorToMove == Piece.White;

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
            if (move.Flag == MoveFlag.PromoteQueen) Squares[move.To] = Piece.ColorOf(Squares[move.To]) | Piece.Queen;
            if (move.Flag == MoveFlag.PromoteRook) Squares[move.To] = Piece.ColorOf(Squares[move.To]) | Piece.Rook;
            if (move.Flag == MoveFlag.PromoteBishop) Squares[move.To] = Piece.ColorOf(Squares[move.To]) | Piece.Bishop;
            if (move.Flag == MoveFlag.PromoteKnight) Squares[move.To] = Piece.ColorOf(Squares[move.To]) | Piece.Knight;

            // Castling
            if (move.Flag == MoveFlag.KingSideCastle)
            {
                Squares[move.From + 1] = Squares[move.To + 1];
                Squares[move.To + 1] = Piece.None;
            }
            else if (move.Flag == MoveFlag.QueenSideCastle)
            {
                Squares[move.From - 1] = Squares[move.To - 2];
                Squares[move.To - 2] = Piece.None;
            }

            ColorToMove = ColorToMove == Piece.White ? Piece.Black : Piece.White;
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

            if (move.Flag == MoveFlag.EnPassant)
            {
                if (ColorToMove == Piece.White)
                    Squares[gameState.EnPassantCaptureSquare] = Piece.Pawn | Piece.White;
                else
                    Squares[gameState.EnPassantCaptureSquare] = Piece.Pawn | Piece.Black;
            }

            ColorToMove = ColorToMove == Piece.White ? Piece.Black : Piece.White;

            FullMoveNumber--;
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
    }
}