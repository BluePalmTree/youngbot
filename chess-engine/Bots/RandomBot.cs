using chess_engine.Helpers;
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

            return moves[rng.Next(moves.Count)];
        }
    }
}