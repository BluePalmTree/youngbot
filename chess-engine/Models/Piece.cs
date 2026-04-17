namespace chess_engine.Models
{
    public static class Piece
    {
        // Types (lower 3 bits)
        public const int None = 0;
        public const int Pawn = 1;
        public const int Knight = 2;
        public const int Bishop = 3;
        public const int Rook = 4;
        public const int Queen = 5;
        public const int King = 6;

        // Colors (upper bits)
        public const int White = 8;
        public const int Black = 16;

        // Masks
        public const int TypeMask = 0b00111;
        public const int ColorMask = 0b11000;

        // Helpers
        public static int TypeOf(int piece) => piece & TypeMask;
        public static int ColorOf(int piece) => piece & ColorMask;
        public static bool IsColor(int piece, int color) => (piece & ColorMask) == color && piece != None;
        public static bool IsSlidingPiece(int piece) => TypeOf(piece) == Bishop || TypeOf(piece) == Rook || TypeOf(piece) == Queen;
        public static string GetColorText(int c) => c == White ? "White" : "Black";

        // Concrete pieces (combine with |)
        // e.g. White | Pawn, Black | King
    }
}