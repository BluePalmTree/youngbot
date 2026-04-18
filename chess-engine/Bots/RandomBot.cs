using chess_engine.Engine;
using chess_engine.Models;

namespace chess_engine.Bots
{
    public static class RandomBot
    {
        private static readonly Random rng = new();

        public static Move? PickMove()
        {
            var moves = MoveGenerator.Moves;
            if (moves.Count == 0)
                return null;

            var move = moves[rng.Next(moves.Count)];
            if (move.PromotionNeeded)
                return new Move(move.From, move.To, MoveFlag.PromoteQueen);

            return move;
        }
    }
}