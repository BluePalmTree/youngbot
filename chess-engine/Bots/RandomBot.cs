using chess_engine.Models;

namespace chess_engine.Bots
{
    public class RandomBot : IBot
    {
        private readonly Random rng = new();

        public Move? PickMove(Board board, List<Move> moves)
        {            
            if (moves.Count == 0)
                return null;

            var move = moves[rng.Next(moves.Count)];
            if (move.PromotionNeeded)
                return new Move(move.From, move.To, MoveFlag.PromoteQueen);

            return move;
        }
    }
}