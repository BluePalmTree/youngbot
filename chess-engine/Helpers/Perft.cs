using System.Diagnostics;
using chess_engine.Models;

namespace chess_engine.Helpers
{
    public static class Perft
    {
        // Known node counts per test position, keyed by position name → depth → expected nodes.
        // Reference: https://www.chessprogramming.org/Perft_Results
        private static readonly Dictionary<string, Dictionary<int, long>> ExpectedNodes = new()
        {
            ["start"] = new()
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
            },
            ["kiwipete"] = new()
            {
                { 1, 48 },
                { 2, 2039 },
                { 3, 97862 },
                { 4, 4085603 },
                { 5, 193690690 },
            },
            ["position3"] = new()
            {
                { 1, 14 },
                { 2, 191 },
                { 3, 2812 },
                { 4, 43238 },
                { 5, 674624 },
                { 6, 11030083 },
            },
        };

        public static void Divide(string positionKey, string fen, int depth, bool useOracle = false)
        {
            var board = Board.FromStartPosition(fen);

            Action<Board> gen = useOracle ? MoveGenerator.GenerateLegalMovesOracle : MoveGenerator.GenerateLegalMoves;

            gen(board);
            var moves = new List<Move>(MoveGenerator.Moves);
            long total = 0;

            Stopwatch sw = new();
            TimeSpan totalTime = TimeSpan.Zero;

            sw.Start();

            foreach (var move in moves)
            {
                board.MakeMove(move);
                long count = Run(board, depth - 1, useOracle);
                board.UnmakeLastMove();
                total += count;

                Debug.WriteLine($"{move}: {count:N0}");
                Console.WriteLine($"  {move} {Promo(move)}: {count}");
            }

            sw.Stop();
            totalTime = sw.Elapsed;

            string totalElapsed = $"{totalTime.TotalSeconds:F2}s";
            long expected = ExpectedNodes.TryGetValue(positionKey, out var byDepth)
                            && byDepth.TryGetValue(depth, out long v)
                          ? v : -1;
            string status = expected == -1 ? "(no reference)"
                          : total == expected ? "OK" : $"MISMATCH (expected {expected:N0}, diff {total - expected:+#;-#;0})";

            Debug.WriteLine("--------------------------------------");
            Debug.WriteLine($"Perft {positionKey} depth {depth}: {total:N0} in {totalElapsed} — {status}");

            // Mirror the summary to stdout so headless callers (CLI perft harness, piped runs)
            // don't need a debugger attached to see results. Per-move lines stay on Debug.
            Console.WriteLine($"Perft {positionKey} depth {depth}: {total:N0} in {totalElapsed} — {status}");
        }

        private static string Promo(Move m) => m.Flag switch
        {
            MoveFlag.PromoteQueen => "q",
            MoveFlag.PromoteRook => "r",
            MoveFlag.PromoteBishop => "b",
            MoveFlag.PromoteKnight => "n",
            _ => "",
        };

        public static long Run(Board board, int depth, bool useOracle = false)
        {
            if (depth == 0)
                return 1;

            if (useOracle)
                MoveGenerator.GenerateLegalMovesOracle(board);
            else
                MoveGenerator.GenerateLegalMoves(board);

            var moves = new List<Move>(MoveGenerator.Moves);
            long nodes = 0;

            foreach (var move in moves)
            {
                board.MakeMove(move);
                nodes += Run(board, depth - 1, useOracle);
                board.UnmakeLastMove();
            }

            return nodes;
        }
    }
}
