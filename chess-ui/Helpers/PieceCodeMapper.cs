using System;
using chess_engine.Models;

namespace chess_ui.Helpers
{
    public static class PieceCodeMapper
    {
        public static string ToCode(int piece)
        {
            if (piece == Piece.None) return string.Empty;

            var color = Piece.ColorOf(piece) == Piece.White ? "w" : "b";
            var type = Piece.TypeOf(piece) switch
            {
                Piece.Pawn => "pa",
                Piece.Knight => "kn",
                Piece.Bishop => "bi",
                Piece.Rook => "ro",
                Piece.Queen => "qu",
                Piece.King => "ki",
                _ => throw new NotImplementedException()
            };

            return color + type;
        }
    }
}