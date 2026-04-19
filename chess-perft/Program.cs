using System.Diagnostics;
using System.Globalization;
using chess_engine.Engine;

namespace chess_perft;

// Headless perft runner.
//
// Usage:
//   dotnet run --project chess-perft --configuration:Release # default suite
//   dotnet run --project chess-perft -- start 5 kiwipete 3   # specific positions
//   dotnet run --project chess-perft -- --fen "<FEN>" <depth> [--oracle]
//
// Flags (can appear anywhere in args):
//   --oracle   In --fen mode, use the slow reference generator.
//   --record   Append one row per (position, depth) run to chess-perft/benchmarks.md.
//
// Prefix a positionKey with "oracle:" to run that case against the slow reference
// generator (GenerateLegalMovesOracle). Useful for A/B-diffing against the fast path.
//
// positionKey ∈ { start, kiwipete, position3 }
internal class Program
{
    private static readonly Dictionary<string, string> Positions = new()
    {
        ["start"] = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",
        ["kiwipete"] = "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1",
        ["position3"] = "8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1",
    };

    private static readonly (string key, int depth)[] DefaultSuite =
    [
        ("start", 1), ("start", 2), ("start", 3), ("start", 4), ("start", 5), ("start", 6),
        ("kiwipete", 1), ("kiwipete", 2), ("kiwipete", 3),
        ("position3", 1), ("position3", 2), ("position3", 3), ("position3", 4),
    ];

    static void Main(string[] args)
    {
        bool record = args.Contains("--record");
        var filteredArgs = args.Where(a => a != "--record").ToArray();

        var collected = new List<(string position, int depth, Perft.PerftResult result)>();

        // JIT warmup so the first recorded row isn't dominated by cold-start cost.
        // Cheap (<1 ms) but covers the hot paths in MoveGenerator and AttackData.
        if (record)
        {
            var (warmup,_) = chess_engine.Models.Board.FromStartPosition(Positions["start"]);
            Perft.Run(warmup, 2);
        }

        // Special mode: --fen "<FEN>" <depth> [--oracle]
        if (filteredArgs.Length >= 3 && filteredArgs[0] == "--fen")
        {
            string fen = filteredArgs[1];
            int depth = int.Parse(filteredArgs[2]);
            bool oracle = filteredArgs.Length >= 4 && filteredArgs[3] == "--oracle";
            var result = Perft.Divide("custom", fen, depth, oracle);
            if (record)
            {
                collected.Add((oracle ? "custom-oracle" : "custom", depth, result));
                WriteBenchmarks(collected);
            }
            return;
        }

        (string key, int depth)[] suite = filteredArgs.Length == 0
            ? DefaultSuite
            : ParsePairs(filteredArgs);

        foreach (var (key, depth) in suite)
        {
            bool oracle = key.StartsWith("oracle:", StringComparison.Ordinal);
            string lookup = oracle ? key["oracle:".Length..] : key;

            if (!Positions.TryGetValue(lookup, out var fen))
            {
                Console.Error.WriteLine($"Unknown position '{lookup}'. Known: {string.Join(", ", Positions.Keys)}");
                continue;
            }

            var result = Perft.Divide(lookup, fen, depth, oracle);
            if (record)
                collected.Add((oracle ? $"{lookup}-oracle" : lookup, depth, result));
        }

        if (record)
            WriteBenchmarks(collected);
    }

    private static (string, int)[] ParsePairs(string[] args)
    {
        if (args.Length % 2 != 0)
            throw new ArgumentException("Args must be pairs of <positionKey> <depth>.");

        var list = new List<(string, int)>();
        for (int i = 0; i < args.Length; i += 2)
            list.Add((args[i], int.Parse(args[i + 1])));

        return list.ToArray();
    }

    private static void WriteBenchmarks(List<(string position, int depth, Perft.PerftResult result)> rows)
    {
        if (rows.Count == 0)
            return;

        string path = FindBenchmarksPath();
        bool fresh = !File.Exists(path);

        string commit = GetCommitSha();
        string config = GetBuildConfig();
        string date = DateTime.UtcNow.ToString("yyyy-MM-dd");

        var sb = new System.Text.StringBuilder();
        if (fresh)
        {
            sb.AppendLine("# Perft benchmarks");
            sb.AppendLine();
            sb.AppendLine("Each row is one `--record`ed perft run. The commit column carries `-dirty` when the working tree had uncommitted changes.");
            sb.AppendLine();
            sb.AppendLine("| date | commit | position | depth | nodes | time (ms) | nodes/sec | config | status |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|");
        }

        // Force invariant culture so the file is stable across machines/locales —
        // otherwise a German-locale recorder writes "8.902" for 8,902 and a US-locale
        // recorder writes "8,902", and merging or diffing gets confusing fast.
        var inv = CultureInfo.InvariantCulture;
        foreach (var (position, depth, result) in rows)
        {
            double ms = result.Elapsed.TotalMilliseconds;
            long nps = ms > 0 ? (long)(result.Nodes / (ms / 1000.0)) : 0;
            string status = result.Expected == -1 ? "no-ref" : (result.Match ? "OK" : "MISMATCH");
            sb.AppendLine(string.Format(inv,
                "| {0} | {1} | {2} | {3} | {4:N0} | {5:F1} | {6:N0} | {7} | {8} |",
                date, commit, position, depth, result.Nodes, ms, nps, config, status));
        }

        File.AppendAllText(path, sb.ToString());
        Console.WriteLine($"Recorded {rows.Count} row(s) to {path}");
    }

    // Walk up from AppContext.BaseDirectory to locate chess-perft/benchmarks.md.
    // Needed because `dotnet run` puts the CWD somewhere under bin/.
    private static string FindBenchmarksPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "chess-perft.csproj")))
            dir = dir.Parent;

        if (dir == null)
            throw new InvalidOperationException("Could not find chess-perft project root from AppContext.BaseDirectory.");

        return Path.Combine(dir.FullName, "benchmarks.md");
    }

    private static string GetCommitSha()
    {
        try
        {
            string sha = RunGit("rev-parse --short HEAD").Trim();
            string porcelain = RunGit("status --porcelain").Trim();
            return string.IsNullOrEmpty(porcelain) ? sha : $"{sha}-dirty";
        }
        catch
        {
            return "unknown";
        }
    }

    private static string RunGit(string args)
    {
        var psi = new ProcessStartInfo("git", args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var proc = Process.Start(psi)!;
        string stdout = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit();
        if (proc.ExitCode != 0)
            throw new InvalidOperationException("git failed");
        return stdout;
    }

    private static string GetBuildConfig()
    {
#if DEBUG
        return "Debug";
#else
        return "Release";
#endif
    }
}
