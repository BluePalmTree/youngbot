namespace chess_engine.Models
{
    public struct GameState
    {
        public int EnPassantSquare;
        public int EnPassantCaptureSquare;
        public int CastlingRights;
        public int CapturedPiece;
        public Move Move;
    }
}