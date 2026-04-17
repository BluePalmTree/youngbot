using System.Diagnostics;
using chess_engine.Models;

namespace chess_engine.Helpers
{
    public static class MoveGenerator
    {
        // Direction ordering (shared with AttackData): indices 0..7 map to
        //   N(+8), S(-8), W(-1), E(+1), NW(+7), SE(-7), NE(+9), SW(-9)
        // Orthogonal directions are indices 0..3; diagonals are 4..7.
        public static readonly int[] DirectionOffsets = [8, -8, -1, 1, 7, -7, 9, -9];
        public static readonly int[][] NumSquaresToEdge = new int[64][];

        public static List<Move> Moves { get; private set; } = [];


        private static readonly int NorthIndex = 0;
        private static readonly int SouthIndex = 1;
        private static readonly int WestIndex = 2;
        private static readonly int EastIndex = 3;
        private static readonly int NorthWestIndex = 4;
        private static readonly int NorthEastIndex = 6;

        public static void PrecomputedMoveData()
        {
            for (int rank = 0; rank < 8; rank++)
            {
                for (int file = 0; file < 8; file++)
                {
                    int numNorth = 7 - rank;
                    int numSouth = rank;
                    int numWest = file;
                    int numEast = 7 - file;

                    int squareIndex = rank * 8 + file;

                    NumSquaresToEdge[squareIndex] = [
                        numNorth,
                        numSouth,
                        numWest,
                        numEast,
                        Math.Min(numNorth, numWest),
                        Math.Min(numSouth, numEast),
                        Math.Min(numNorth, numEast),
                        Math.Min(numSouth, numWest)
                    ];
                }
            }
        }

        // Fast legal move generation:
        //   1. Precompute opponent attack map, checkers, check-block mask, pin lines.
        //   2. Generate pseudo-legal moves.
        //   3. Filter each move inline against those structures — no make/unmake.
        public static void GenerateLegalMoves(Board board)
        {
            var data = AttackData.Compute(board, board.ColorToMove);
            board.AttackData = data;
            board.AttackedSquares = [.. data.AttackMap];

            int ownKing = board.GetKingSquare(board.ColorToMove);

            var legalMoves = new List<Move>();
            var pseudoLegal = GenerateMoves(board);

            foreach (var move in pseudoLegal)
            {
                if (IsLegal(board, data, move, ownKing))
                    legalMoves.Add(move);
            }

            if (legalMoves.Count == 0)
                Debug.WriteLine($"No legal moves for {Piece.GetColorText(board.ColorToMove)} left.");

            Moves = legalMoves;
        }

        private static bool IsLegal(Board board, AttackData data, Move move, int ownKing)
        {
            bool isKingMove = move.From == ownKing;

            if (isKingMove)
                return IsLegalKingMove(data, move);

            // Only king moves can escape a double check.
            if (data.InDoubleCheck) return false;

            // Under single check: non-king move must land on a blocking or capturing square.
            if (data.CheckBlockMask != null && !data.CheckBlockMask.Contains(move.To))
                return false;

            // Pinned piece: target must lie on the pin line.
            if (data.PinLines.TryGetValue(move.From, out var pinLine) && !pinLine.Contains(move.To))
                return false;

            // En-passant horizontal-discovered-pin: removing BOTH pawns from the 5th/4th rank
            // can uncover a rook/queen attack along the rank. Handled without make/unmake.
            if (move.Flag == MoveFlag.EnPassant && LeavesKingInCheckViaEpPin(board, move, ownKing))
                return false;

            return true;
        }

        private static bool IsLegalKingMove(AttackData data, Move move)
        {
            // A king move that lands on an attacked square is suicide.
            if (data.AttackMap.Contains(move.To)) return false;

            if (move.Flag == MoveFlag.KingSideCastle || move.Flag == MoveFlag.QueenSideCastle)
            {
                // Castling is illegal while in check.
                if (data.InCheck) return false;

                // The square the king *passes through* must not be attacked either.
                // (Starting square is already guaranteed not-attacked by InCheck=false;
                //  landing square was checked above.)
                int step = move.Flag == MoveFlag.KingSideCastle ? +1 : -1;
                int through = move.From + step;
                if (data.AttackMap.Contains(through)) return false;
            }

            return true;
        }

        private static bool LeavesKingInCheckViaEpPin(Board board, Move move, int kingSq)
        {
            // The capturing pawn leaves from move.From; the captured pawn sits on the same
            // rank as move.From (one square ahead of the EP target along the capturer's column).
            bool whiteMoving = Piece.IsColor(board.Squares[move.From], Piece.White);
            int capturedPawnSq = whiteMoving ? move.To - 8 : move.To + 8;

            int kingRank = Board.RankOf(kingSq);
            if (Board.RankOf(move.From) != kingRank) return false;

            int opponent = whiteMoving ? Piece.Black : Piece.White;

            // Scan the king's rank in both directions; with both pawns conceptually removed,
            // does the first piece we see on the rank turn out to be an enemy rook or queen?
            foreach (int dir in new[] { -1, +1 })
            {
                int sq = kingSq;
                while (true)
                {
                    int file = sq % 8;
                    if (dir == -1 && file == 0) break;
                    if (dir == +1 && file == 7) break;
                    sq += dir;

                    if (sq == move.From || sq == capturedPawnSq) continue;

                    int piece = board.Squares[sq];
                    if (piece == Piece.None) continue;

                    if (Piece.IsColor(piece, opponent))
                    {
                        int type = Piece.TypeOf(piece);
                        if (type == Piece.Rook || type == Piece.Queen) return true;
                    }
                    break;
                }
            }
            return false;
        }

        // Slow reference legal-move generator. Kept as an A/B oracle against the fast
        // path — perft disagreements are easiest to narrow down by replaying against this.
        public static void GenerateLegalMovesOracle(Board board)
        {
            Stopwatch sw = new();
            sw.Start();

            List<Move> pseudoLegalMoves = GenerateMoves(board);
            List<Move> legalMoves = [];

            var orgColorToMove = board.ColorToMove;
            var kingSquare = board.GetKingSquare(board.ColorToMove);
            if (kingSquare == -1)
            {
                Moves = legalMoves;
                Debug.WriteLine($"No king found for {(board.ColorToMove == Piece.White ? "White" : "Black")} | FEN: {board.GetFEN()}");
                return;
            }

            foreach (var moveToVerify in pseudoLegalMoves)
            {
                board.MakeMove(moveToVerify);

                kingSquare = board.GetKingSquare(orgColorToMove);
                if (kingSquare == -1)
                    Debug.WriteLine($"FEN: {board.GetFEN()}");

                List<Move> opponentResponses = GenerateMoves(board);

                if (!opponentResponses.Any(r => r.To == kingSquare))
                    legalMoves.Add(moveToVerify);

                board.UnmakeLastMove();
            }

            sw.Stop();

            if (legalMoves.Count == 0)
                Debug.WriteLine($"No legal moves for {(board.ColorToMove == Piece.White ? "White" : "Black")} left. Checkmate!");

            Moves = legalMoves;
        }


        public static List<Move> GenerateMoves(Board board)
        {
            var moves = new List<Move>();

            for (int startSquare = 0; startSquare < 64; startSquare++)
            {
                int piece = board.Squares[startSquare];

                if (!Piece.IsColor(piece, board.ColorToMove))
                    continue;

                if (Piece.IsSlidingPiece(piece))
                {
                    var slidingMoves = GenerateSlidingMoves(board, startSquare, piece);
                    moves.AddRange(slidingMoves);
                }
                else if (Piece.TypeOf(piece) == Piece.King)
                {
                    var kingMoves = GenerateKingMoves(board, startSquare);
                    moves.AddRange(kingMoves);
                }
                else if (Piece.TypeOf(piece) == Piece.Knight)
                {
                    var knightMoves = GenerateKnightMoves(board, startSquare);
                    moves.AddRange(knightMoves);
                }
                else if (Piece.TypeOf(piece) == Piece.Pawn)
                {
                    var pawnMoves = GeneratePawnMoves(board, startSquare);
                    moves.AddRange(pawnMoves);
                }
                else
                {
                    throw new NotImplementedException("Piece type not implemented");
                }
            }

            return moves;
        }

        public static int[] GetValidMovesForSquare(int square)
        {
            var result = new List<int>();
            for (int i = 0; i < Moves.Count; i++)
            {
                if (Moves[i].From == square)
                    result.Add(Moves[i].To);
            }

            return result.ToArray();
        }

        public static Move? GetMove(int from, int to)
        {
            foreach (var move in Moves)
                if (move.From == from && move.To == to)
                    return move;

            return null;
        }

        public static bool IsValidMove(int from, int to)
        {
            return GetMove(from, to) is not null;
        }

        private static List<Move> GenerateKingMoves(Board board, int startSquare)
        {
            var moves = new List<Move>();

            for (int directionIndex = 0; directionIndex < 8; directionIndex++)
            {
                if (NumSquaresToEdge[startSquare][directionIndex] < 1)
                    continue;

                int targetSquare = startSquare + DirectionOffsets[directionIndex];
                int pieceOnTargetSquare = board.Squares[targetSquare];

                // Blocked by friendly piece, so can't move any further in this direction
                if (Piece.IsColor(pieceOnTargetSquare, board.ColorToMove))
                    continue;

                moves.Add(new Move(startSquare, targetSquare));

                // Can't move any furhter in this direction after capturing opponent's piece
                if (pieceOnTargetSquare != Piece.None && !Piece.IsColor(pieceOnTargetSquare, board.ColorToMove))
                    continue;

                if (board.CastlingRights == 0)
                    continue;

                // King side castle
                var kingSideMask = board.ColorToMove == Piece.White ? 0b1000 : 0b0010;
                if (directionIndex == EastIndex && (board.CastlingRights & kingSideMask) != 0)
                {
                    targetSquare = startSquare + DirectionOffsets[directionIndex] * 2;
                    pieceOnTargetSquare = board.Squares[targetSquare];

                    if (pieceOnTargetSquare != Piece.None)
                        continue;

                    moves.Add(new Move(startSquare, targetSquare, MoveFlag.KingSideCastle));
                }

                // Queen side castle
                var queenSideMask = board.ColorToMove == Piece.White ? 0b0100 : 0b0001;
                if (directionIndex == WestIndex && (board.CastlingRights & queenSideMask) != 0)
                {
                    bool canCastleQueenSide = true;

                    for (int m = 2; m < 4; m++)
                    {
                        targetSquare = startSquare + DirectionOffsets[directionIndex] * m;
                        pieceOnTargetSquare = board.Squares[targetSquare];

                        if (pieceOnTargetSquare != Piece.None)
                        {
                            canCastleQueenSide = false;
                            break;
                        }
                    }

                    if (!canCastleQueenSide)
                        continue;

                    targetSquare = startSquare + DirectionOffsets[directionIndex] * 2;
                    moves.Add(new Move(startSquare, targetSquare, MoveFlag.QueenSideCastle));
                }
            }

            return moves;
        }

        private static List<Move> GenerateKnightMoves(Board board, int startSquare)
        {
            var moves = new List<Move>();

            for (int directionIndex = 0; directionIndex < 4; directionIndex++)
            {
                // Needs at least 2 squares in the "main" direction
                if (NumSquaresToEdge[startSquare][directionIndex] < 2)
                    continue;

                int mainDirSquare = startSquare + DirectionOffsets[directionIndex] * 2;

                // if directionIndex is N or S then W/E are the sedony directins; else N/S
                int startDirIndex = directionIndex == NorthIndex || directionIndex == SouthIndex ? 2 : 0;
                int endDirIndex = startDirIndex + 2;

                for (int secondaryDirIndex = startDirIndex; secondaryDirIndex < endDirIndex; secondaryDirIndex++)
                {
                    // Need at least 1 square in the "secondary" direction
                    if (NumSquaresToEdge[mainDirSquare][secondaryDirIndex] < 1)
                        continue;

                    int targetSquare = mainDirSquare + DirectionOffsets[secondaryDirIndex];
                    int pieceOnTargetSquare = board.Squares[targetSquare];

                    // Blockes by friendly piece, so can't move any further in this direction
                    if (Piece.IsColor(pieceOnTargetSquare, board.ColorToMove))
                        continue;

                    moves.Add(new Move(startSquare, targetSquare));

                    // Can't move any furhter in this direction after capturing opponent's piece
                    if (pieceOnTargetSquare != Piece.None && !Piece.IsColor(pieceOnTargetSquare, board.ColorToMove))
                        continue;
                }
            }

            return moves;
        }

        private static List<Move> GenerateSlidingMoves(Board board, int startSquare, int piece)
        {
            var moves = new List<Move>();

            int startDirIndex = Piece.TypeOf(piece) == Piece.Bishop ? 4 : 0;
            int endDirIndex = Piece.TypeOf(piece) == Piece.Rook ? 4 : 8;

            for (int directionIndex = startDirIndex; directionIndex < endDirIndex; directionIndex++)
            {
                for (int n = 0; n < NumSquaresToEdge[startSquare][directionIndex]; n++)
                {
                    int targetSquare = startSquare + DirectionOffsets[directionIndex] * (n + 1);
                    int pieceOnTargetSquare = board.Squares[targetSquare];

                    // Blockes by friendly piece, so can't move any further in this direction
                    if (Piece.IsColor(pieceOnTargetSquare, board.ColorToMove))
                        break;

                    moves.Add(new Move(startSquare, targetSquare));

                    // Can't move any furhter in this direction after capturing opponent's piece
                    if (pieceOnTargetSquare != Piece.None && !Piece.IsColor(pieceOnTargetSquare, board.ColorToMove))
                        break;
                }
            }

            return moves;
        }

        private static List<Move> GeneratePawnMoves(Board board, int startSquare)
        {
            var moves = new List<Move>();

            var whiteIndex = Piece.IsColor(board.Squares[startSquare], Piece.White) ? 0 : 1;

            var north = NumSquaresToEdge[startSquare][NorthIndex + whiteIndex];
            var rank = Board.RankOf(startSquare);
            bool moved = whiteIndex == 0 ? rank != 1 : rank != 6;

            if (north > 0)
            {
                // Pawn hasn't moved yet so can move two squares, if empty
                if (!moved && north > 1)
                {
                    for (int n = 1; n < 3; n++)
                    {
                        int targetSquare = startSquare + DirectionOffsets[NorthIndex + whiteIndex] * n;
                        int pieceOnTargetSquare = board.Squares[targetSquare];

                        if (pieceOnTargetSquare == Piece.None)
                        {
                            var mf = n == 2 ? MoveFlag.DoublePawnPush : MoveFlag.Normal;
                            AddPawnMove(moves, startSquare, targetSquare, mf);
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                else
                {
                    int targetSquare = startSquare + DirectionOffsets[NorthIndex + whiteIndex];
                    int pieceOnTargetSquare = board.Squares[targetSquare];

                    if (pieceOnTargetSquare == Piece.None)
                    {
                        AddPawnMove(moves, startSquare, targetSquare, MoveFlag.Normal);
                    }
                }
            }

            var northWest = NumSquaresToEdge[startSquare][NorthWestIndex + whiteIndex];
            if (northWest > 0)
            {
                int targetSquare = startSquare + DirectionOffsets[NorthWestIndex + whiteIndex];
                int pieceOnTargetSquare = board.Squares[targetSquare];

                if (targetSquare == board.EnPassantSquare)
                {
                    moves.Add(new Move(startSquare, targetSquare, MoveFlag.EnPassant));
                }

                if (pieceOnTargetSquare != Piece.None && !Piece.IsColor(pieceOnTargetSquare, board.ColorToMove))
                {
                    AddPawnMove(moves, startSquare, targetSquare, MoveFlag.Normal);
                }
            }

            var northEast = NumSquaresToEdge[startSquare][NorthEastIndex + whiteIndex];
            if (northEast > 0)
            {
                int targetSquare = startSquare + DirectionOffsets[NorthEastIndex + whiteIndex];
                int pieceOnTargetSquare = board.Squares[targetSquare];

                if (targetSquare == board.EnPassantSquare)
                {
                    moves.Add(new Move(startSquare, targetSquare, MoveFlag.EnPassant));
                }

                if (pieceOnTargetSquare != Piece.None && !Piece.IsColor(pieceOnTargetSquare, board.ColorToMove))
                {
                    AddPawnMove(moves, startSquare, targetSquare, MoveFlag.Normal);
                }
            }

            return moves;
        }

        // Emit a pawn move, expanding into the four concrete promotion variants when the
        // target lands on the back rank. Perft / Make-Move both need concrete Promote*
        // flags to count and apply promotions correctly.
        private static void AddPawnMove(List<Move> moves, int from, int to, MoveFlag baseFlag)
        {
            if (Prom(to))
            {
                moves.Add(new Move(from, to, MoveFlag.PromoteQueen));
                moves.Add(new Move(from, to, MoveFlag.PromoteRook));
                moves.Add(new Move(from, to, MoveFlag.PromoteBishop));
                moves.Add(new Move(from, to, MoveFlag.PromoteKnight));
            }
            else
            {
                moves.Add(new Move(from, to, baseFlag));
            }
        }

        // True when the pawn reaches the back rank and the move needs to promote.
        private static bool Prom(int targetSquare) => Board.RankOf(targetSquare) is 0 or 7;
    }
}
