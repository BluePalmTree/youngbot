using chess_engine.Engine;
using chess_engine.Models;

namespace chess_engine.Bots
{
    public class SimpleBot : IBot
    {
        private readonly Random rng = new();

        private const int MateScore = 1_000_000;
        private const int SearchDepth = 3;

        public IReadOnlyList<(Move Move, int Score)> LastRootScores { get; private set; } = [];

        public Move? PickMove(Board board, List<Move> moves)
        {
            if (moves.Count == 0)
                return null;

            var scored = SearchRoot(board, moves, SearchDepth);
            LastRootScores = scored;

            var bestEval = scored[0].Score;
            var bestCount = 0;
            for (int i = 0; i < scored.Count; i++)
            {
                if (scored[i].Score >= bestEval)
                    bestCount++;
                else
                    break;
            }

            var selectedMove = scored[rng.Next(bestCount)].Move;

            var c = Math.Min(5, scored.Count);
            for (int i = 0; i < c; i++)
                Console.WriteLine($"{scored[i].Move}   {scored[i].Score}");
            Console.WriteLine($"Selected Move: {selectedMove}");
            
            return selectedMove;
        }

        private List<(Move Move, int Score)> SearchRoot(Board board, List<Move> moves, int depth)
        {
            var results = new List<(Move, int)>(moves.Count);

            foreach (var move in moves)
            {
                board.MakeMove(move);
                int evaluation = -Search(board, depth - 1);
                board.UnmakeLastMove();
                results.Add((move, evaluation));
            }

            results.Sort((a, b) => b.Item2.CompareTo(a.Item2));
            return results;
        }

        private static int Search(Board board, int depth)
        {
            if (depth == 0)
                return Evaluation.Evaluate(board);

            var moves = MoveGenerator.GenerateLegalMoves(board);
            if (moves.Count == 0)
            {
                if (board.IsInCheck())
                    return -MateScore;
                return 0;
            }

            var bestEvaluation = int.MinValue;

            foreach (var move in moves)
            {
                board.MakeMove(move);
                int evaluation = -Search(board, depth - 1);
                bestEvaluation = Math.Max(evaluation, bestEvaluation);
                board.UnmakeLastMove();
            }

            return bestEvaluation;
        }
    }
}
