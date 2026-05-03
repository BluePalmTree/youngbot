using chess_engine.Models;

namespace chess_engine.Bots
{
    public interface IBot
    {
        Move? PickMove(Board board, List<Move> moves);
    }
}