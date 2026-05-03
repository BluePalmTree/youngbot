using chess_engine.Models;

namespace chess_engine.Engine
{
    public static class Evaluation
    {
        const int pawnValue = 100;
        const int knightValue = 300;
        const int bishopValue = 300;
        const int rookValue = 500;
        const int queenValue = 900;

        public static int Evaluate(Board board)
        {
            int whiteEval = CountMaterial(board, Piece.White);
            int blackEval = CountMaterial(board, Piece.Black);

            int evaluation = whiteEval - blackEval;

            int perspective = (board.ColorToMove == Piece.White) ? 1 : -1;
            return evaluation * perspective;
        }

        private static int CountMaterial(Board board, int color)
        {
            int material = 0;
            for (int i = 0; i < 64; i++)
            {
                var sq = board.Squares[i];

                if (Piece.TypeOf(sq) == Piece.None)
                    continue;

                if (!Piece.IsColor(sq, color))
                    continue;

                if (Piece.TypeOf(sq) == Piece.Pawn)
                    material += pawnValue;
                else if (Piece.TypeOf(sq) == Piece.Knight)
                    material += knightValue;
                else if (Piece.TypeOf(sq) == Piece.Bishop)
                    material += bishopValue;
                else if (Piece.TypeOf(sq) == Piece.Rook)
                    material += rookValue;
                else if (Piece.TypeOf(sq) == Piece.Queen)
                    material += queenValue;                    
            }

            return material;
        }
    }
}