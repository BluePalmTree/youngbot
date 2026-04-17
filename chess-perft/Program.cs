using chess_engine.Helpers;

// Headless perft runner. Args: either nothing (runs a default suite) or pairs of
// "<positionKey> <depth>" like: dotnet run --project chess-perft -- start 5 kiwipete 3
//
// Prefix a positionKey with "oracle:" to run against the slow reference generator
// (GenerateLegalMovesOracle). Useful for A/B-diffing against the fast path.
//
// positionKey ∈ { start, kiwipete, position3 }

var positions = new Dictionary<string, string>
{
    ["start"] = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",
    ["kiwipete"] = "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1",
    ["position3"] = "8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1",
};

// Special mode: --fen "<FEN>" <depth> [--oracle]
if (args.Length >= 3 && args[0] == "--fen")
{
    string fen = args[1];
    int depth = int.Parse(args[2]);
    bool oracle = args.Length >= 4 && args[3] == "--oracle";
    Perft.Divide(oracle ? "ORACLE custom" : "custom", fen, depth, oracle);
    return;
}

(string key, int depth)[] suite = args.Length == 0
    ? new[]
    {
        ("start", 1), ("start", 2), ("start", 3), ("start", 4), ("start", 5),
        ("kiwipete", 1), ("kiwipete", 2), ("kiwipete", 3),
        ("position3", 1), ("position3", 2), ("position3", 3), ("position3", 4),
    }
    : ParsePairs(args);

foreach (var (key, depth) in suite)
{
    bool oracle = key.StartsWith("oracle:", StringComparison.Ordinal);
    string lookup = oracle ? key["oracle:".Length..] : key;

    if (!positions.TryGetValue(lookup, out var fen))
    {
        Console.Error.WriteLine($"Unknown position '{lookup}'. Known: {string.Join(", ", positions.Keys)}");
        continue;
    }
    Perft.Divide(oracle ? $"ORACLE {lookup}" : lookup, fen, depth, oracle);
}

static (string, int)[] ParsePairs(string[] args)
{
    if (args.Length % 2 != 0)
        throw new ArgumentException("Args must be pairs of <positionKey> <depth>.");
    var list = new List<(string, int)>();
    for (int i = 0; i < args.Length; i += 2)
        list.Add((args[i], int.Parse(args[i + 1])));
    return list.ToArray();
}
