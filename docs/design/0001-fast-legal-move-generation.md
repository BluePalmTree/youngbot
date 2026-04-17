# 0001 — Fast legal move generation

## Context

The first-pass legal-move generator in `chess-engine/Helpers/MoveGenerator.cs` had two correctness bugs and one performance problem:

1. **`board.AttackedSquares` held the wrong color's attacks.** It was populated as a side effect of generating the current player's pseudo-legal moves, so it contained *our* attacks. The UI's "highlight attacked squares" debug overlay therefore showed squares *we* threaten instead of squares the *opponent* threatens.
2. **The legality check was a no-op.** `GenerateLegalMoves` made each candidate move, then checked `AttackedSquares.Any(e => e == kingSquare)` — but the list was never regenerated between the make and the check, so it still reflected the pre-move (wrong-color) attacks. The test essentially always passed. Only the slow reference path `GenerateLegalMoves1` (which regenerates opponent moves after each make) was correct.
3. **Performance.** Even the correct path costs O(pseudo-legal-moves × opponent-moves) because of the make/regenerate/unmake loop.

We also had a latent bug in queenside castling: `GenerateKingMoves` checked two empty squares where three are needed (b, c, d on queenside; only c and d need to be unattacked, but all three must be empty). And castling-through-check was never validated at all.

## Decision

### 1. Give attack data its own type (`AttackData`)

**Alternatives considered:** keep scattering attack info across `Board` fields; pass ad-hoc parameters through the move generator.
**Why this won:** the legality check needs *multiple* pieces of information together — the attack map, the list of checkers, the squares a blocker could land on to resolve single check, and the pin constraints for each own piece. Bundling them into one object makes lifecycle explicit ("compute once, consume many times, discard") and keeps the move generator as a pure reader of that data rather than a writer and a reader of overlapping state.

### 2. `class`, not `struct`, for `AttackData`

**Alternatives considered:** `struct` — value semantics, stack allocation, free of heap pressure.
**Why class won:** the type holds mutable reference-type members (`HashSet<int>`, `Dictionary<int, HashSet<int>>`) we want callers to *share*, not copy. A struct would make "did I mutate the caller's copy or my own?" ambiguous, and structs wider than a CPU register get quietly boxed in several places. One heap allocation per legal-move-generation call is rounding error compared to the work of filling those collections.
(Contrast with `Move`, which is already a `readonly struct`: small, value-like, no shared mutation. Same criteria, different answer.)

### 3. `HashSet<int>` for attack / block / pin squares, not `List<int>` and not bitboards

**Alternatives considered:**
- `List<int>` — what the code already used. Good for iteration, bad for membership (`Any(...)` is O(n)). The legality check runs membership thousands of times per call; this was the dominant cost.
- Bitboards (`ulong`, one bit per square) — the industry standard. O(1) membership via bit-test, O(1) union/intersection of whole boards, zero allocation.

**Why `HashSet<int>` won *for now*:** it gives us O(1) membership (correct asymptotic class) with minimal churn to the surrounding codebase (mailbox `int[64]`, square indices everywhere). Bitboards are the right *next* step but require rethinking piece representation, attack-mask tables, and slider generation (magic bitboards). Not worth bundling into this change.

### 4. `Board.AttackData?` nullable, invalidated on make/unmake

**Alternatives considered:** keep it non-nullable and always valid; regenerate inside `MakeMove` so the invariant "AttackData is always current" holds; omit the cache and recompute on every `IsInCheck` call.
**Why nullable won:** the previous bug class was silently-stale data. Making the field nullable and explicitly setting it to `null` at the top of `MakeMove` / `UnmakeMove` converts stale reads into loud `NullReferenceException`s. Regenerating inside `MakeMove` would make `MakeMove` itself expensive and would regenerate data in perft's deep recursion where the result is never used. Recomputing on every `IsInCheck` loses the cache hit when the UI reads it immediately after `GenerateLegalMoves`.

### 5. Inline legality filtering, no make/unmake in the legality path

**Alternatives considered:** keep make/unmake but use the precomputed `AttackMap` instead of regenerating opponent moves after each make.
**Why inline won:** `AttackMap` goes stale the moment our piece moves. Keeping make/unmake means we'd have to re-`Compute` after every make, which is the same asymptotic cost as before. The classical fast pipeline — check-count + check-block-mask + pin-lines — decides legality *without* moving any piece. The only edge case that still requires per-move reasoning is the en-passant horizontal-discovered-pin, and we handle it with a cheap rank scan rather than make/unmake.

### 6. Keep the slow oracle

`GenerateLegalMoves1` gets renamed to `GenerateLegalMovesOracle`. Ray-scan and pin-line code is notoriously easy to get subtly wrong. Having a dead-simple reference to A/B against any position during debugging is worth more than the 40 dead lines it costs.

## Two subtle bugs surfaced during perft verification

Worth recording because they're the kind of thing that's easy to miss the second time:

### En-passant capture resolves check when the checker is the just-pushed pawn

When an opponent pawn double-pushes AND gives check (so the pawn sits next to our king on the 5th/4th rank), our own pawn can resolve check by en-passant. The move's *target* square is the empty EP square, but the move *removes* the checker via the off-target capture. Naive single-check logic — "target must equal checker's square or lie between checker and king" — rejects this legal move.

Fix: when single check comes from a pawn at square `S` and `board.EnPassantSquare` is exactly one rank's worth (`±8`) from `S`, add the EP target to `CheckBlockMask`. See `chess-engine/Helpers/AttackData.cs`. Perft symptom before the fix: Position 3 depth 5 = 674,543 (expected 674,624).

### A captured rook must revoke that side's castling right

`MakeMove` already revokes castling rights when our king or rook moves. It did NOT revoke when the *captured* piece was an enemy rook on its home square — so after (e.g.) a bishop takes black's a8 rook, black still had its queenside castling bit set even though the rook was gone. The pseudo-legal move generator then emits a phantom queenside castle, and `MakeMove`'s castling logic writes a `None` piece into the rook's landing square.

Fix: in `MakeMove`, after tentatively recording the capture, if `move.To ∈ {a1, h1, a8, h8}` and a piece was captured, clear the corresponding castling bit. See `chess-engine/Models/Board.cs`. Perft symptom before the fix: Kiwipete depth 4 = 4,085,659 (expected 4,085,603, +56).

The lesson shared between these two: moves that *reach an empty square but still remove an enemy piece* are a blind spot for "target square" logic. EP is the obvious one; rook-capture-on-corner is the less obvious one because it affects a *rights flag*, not a square.

---

## Consequences

**Enabled:**
- `IsInCheck` becomes a single hashset lookup when the cache is valid.
- The UI's "attacked squares" overlay now semantically means "opponent threats" — matches user intuition.
- Castling-through-check gets fixed as a natural side effect.
- Clean target for the next optimization pass (bitboards) — the seam between `AttackData` and the generator stays the same.
- The perft harness at `chess-perft/` is now the regression test for legal-move correctness. `dotnet run --project chess-perft` with no args runs the default suite; add an `oracle:` prefix to any position to compare against the slow reference generator. Pass `--record` to append a timing row per case to `chess-perft/benchmarks.md` — that file is the long-term history of move-generation performance and is meant to be committed.

**Still open:**
- **Bitboards.** The obvious next win. Converting `AttackMap` to `ulong` and generating slider attacks via magic bitboards is a separate, larger change.
- **Piece lists.** We still scan all 64 squares to find our pieces. A list of each side's piece positions is O(n-pieces) vs O(64) per generation.
- **Perft speed.** With `HashSet` allocations per node, deep perft is still slow. Bitboards + piece lists would remove that cost.
- **Zobrist hashing / transposition table.** Out of scope — this change is about correctness and constant-factor speed, not search.

**Not solved:**
- There's no automated test suite. Correctness still depends on the in-app Perft menu and manual play. A proper test project is a good follow-up (would belong under a `0002-add-test-project.md`).
