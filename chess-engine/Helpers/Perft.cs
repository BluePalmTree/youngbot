using System.Diagnostics;
using chess_engine.Models;

namespace chess_engine.Helpers
{
    public static class Perft
    {
        private const bool extLog = false; // extended logging

        private static readonly Dictionary<int, long> expectedNodes = new()
        {
            { 0, 1 },
            { 1, 20 },
            { 2, 400 },
            { 3, 8902 },
            { 4, 197281 },
            { 5, 4865609 },
            { 6, 119060324 },
            { 7, 3195901860 },
            { 8, 84998978956 },
            { 9, 2439530234167 }
        };

        public static void Divide(Board board, int depth)
        {
            MoveGenerator.GenerateMoves(board);
            var moves = new List<Move>(MoveGenerator.Moves);
            long total = 0;

            Debug.WriteLineIf(extLog, $"Starting Perft testing with a depth of {depth} | Moves: {moves.Count}");

            foreach (var move in moves)
            {
                Debug.WriteLineIf(extLog, $"Depth: {depth} | {move}");
                board.MakeMove(move);
                MoveGenerator.GenerateMoves(board);
                long count = Run(board, depth - 1);
                board.UnmakeLastMove();
                MoveGenerator.GenerateMoves(board);
                total += count;
                Debug.WriteLine($"Depth: {depth} | {move}: {count:N0}");
                Debug.WriteLineIf(extLog, "------------------------------------");
            }

            Debug.WriteLine($"Depth: {depth} | Total: {total:N0} | Expected: {expectedNodes[depth]:N0}");
        }


        public static long Run(Board board, int depth)
        {
            if (depth == 0)
                return 1;

            MoveGenerator.GenerateMoves(board);
            var moves = new List<Move>(MoveGenerator.Moves);
            long nodes = 0;

            foreach (var move in moves)
            {
                board.MakeMove(move);
                nodes += Run(board, depth - 1);
                board.UnmakeLastMove();
                Debug.WriteLineIf(extLog, $"  {move}: {nodes}");
            }

            return nodes;
        }
    }
}
