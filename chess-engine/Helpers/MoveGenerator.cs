using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using chess_engine.Models;

namespace chess_engine.Helpers
{
    public static class MoveGenerator
    {
        private static readonly bool d = true; // debugging

        /// <summary>
        /// N, S, W, E, NW, SW, NE, SE
        /// </summary>
        public static readonly int[] DirectionOffsets = [8, -8, -1, 1, 7, -7, 9, -9];
        public static readonly int[][] NumSquaresToEdge = new int[64][];


        public static List<Move> Moves { get; private set; } = [];

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

        public static void GenerateMoves(Board board)
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
            }

            Debug.WriteLineIf(d, string.Join(Environment.NewLine, moves));
            Moves = moves;
        }

        public static int[] GetValidMovesForSquare(int square)
        {
            return [.. Moves.Where(m => m.From == square).Select(m => m.To)];
        }

        public static bool IsValidMove(int from, int to)
        {
            foreach (var move in Moves)
            {
                if (move.From != from)
                    continue;

                if (move.To == to)
                    return true;
            }

            return false;
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
                    {
                        break;
                    }

                    moves.Add(new Move(startSquare, targetSquare));

                    // Can't move any furhter in tis direction after capturing opponent's piece
                    if (pieceOnTargetSquare != Piece.None && !Piece.IsColor(pieceOnTargetSquare, board.ColorToMove))
                    {
                        break;
                    }
                }
            }

            return moves;
        }
    }
}