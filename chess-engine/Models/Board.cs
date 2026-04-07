using chess_engine.Helpers;

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

        public static Board FromStartPosition(string fen)
        {
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
                    board.Squares[FromUiIndex(curIndex)] = pieceDict[symbol];
                    curIndex++;
                }
            }

            MoveGenerator.PrecomputedMoveData();
            MoveGenerator.GenerateMoves(board);

            return board;
        }

        public static Board Update(Board board, int from, int to)
        {
            board.Squares[to] = board.Squares[from];
            board.Squares[from] = Piece.None;

            board.ColorToMove = board.ColorToMove == Piece.White ? Piece.Black : Piece.White;

            MoveGenerator.GenerateMoves(board);

            board.FullMoveNumber++;

            return board;
        }

        // Square index helpers
        public static int IndexOf(int file, int rank) => rank * 8 + file;
        public static int FileOf(int index) => index % 8;
        public static int RankOf(int index) => index / 8;
        public static int ToUiIndex(int rank, int file) => (7 - rank) * 8 + file;
        public static int ToUiIndex(int uiIndex)
        {
            var rank = RankOf(uiIndex);
            var file = FileOf(uiIndex);

            return ToUiIndex(rank, file);
        }
        public static int FromUiIndex(int uiIndex)
        {
            var rank = 7 - RankOf(uiIndex); // flip rank: UI row 0 = engine rank 7
            var file = FileOf(uiIndex);
            return IndexOf(file, rank);
        }
    }
}