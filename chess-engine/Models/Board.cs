namespace chess_engine.Models
{
    public struct Board
    {
        // Mailbox: index = rank*8 + file, 0=a1, 63=h8
        public readonly int[] Squares;

        // Castling rights packed as 4 bits: KQkq
        public int CastlingRights;

        // En passant target square (-1 = none)
        public int EnPassantSquare;

        // Side to move
        public int ColorToMove;

        // Half-move clock (50-move rule)
        public int HalfMoveClock;

        // Full-move number
        public int FullMoveNumber;

        public Board()
        {
            Squares = new int[64];
            CastlingRights = 0b1111; // KQkq all available
            EnPassantSquare = -1;
            ColorToMove = Piece.White;
            HalfMoveClock = 0;
            FullMoveNumber = 1;
        }

        //public static Board FromStartPosition() { /* FEN parser later */ }

        public static Board DefaultStartPosition()
        {
            var board = new Board();
            var backRank = new[] {
                Piece.Rook, Piece.Knight, Piece.Bishop, Piece.Queen,
                Piece.King, Piece.Bishop, Piece.Knight, Piece.Rook
            };

            for (int file = 0; file < 8; file++)
            {
                board.Squares[IndexOf(file, 0)] = Piece.White | backRank[file];
                board.Squares[IndexOf(file, 1)] = Piece.White | Piece.Pawn;
                board.Squares[IndexOf(file, 6)] = Piece.Black | Piece.Pawn;
                board.Squares[IndexOf(file, 7)] = Piece.Black | backRank[file];
            }

            return board;
        }

        // Square index helpers
        public static int IndexOf(int file, int rank) => rank * 8 + file;
        public static int FileOf(int index) => index % 8;
        public static int RankOf(int index) => index / 8;
    }
}