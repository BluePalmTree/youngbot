# 0002 — Path to the first search-based bot

## Context

`RandomBot` picks uniformly from the legal move list. The next bot will be search-based (minimax → alpha-beta → iterative deepening), which changes how the engine is used: instead of one move generation per user click, the engine runs millions of make/unmake cycles per move. A handful of shortcuts in the current code that are invisible under UI usage become correctness bugs or hot-path costs under search.

This doc is a checklist of what to clean up *before* writing the search, in the order that makes each step cheap to do and hard to regret. It bundles the pre-search hygiene list and the "when do I switch to bitboard storage?" question into one sequence so the items can be ticked off in order.

## Checklist

### Pre-search hygiene (items 1–5)

- [x] **1. Remove `MoveGenerator.Moves` (the static list).**
  A recursive search at depth N will overwrite the list while depth N−1 is still iterating it. That's a correctness bug, not a perf nit — and it will only surface as mysteriously wrong play, not a crash. Make `GenerateLegalMoves` *return* a `List<Move>` (or fill a caller-owned buffer). The UI helpers `GetValidMovesForSquare` / `GetMove` / `IsValidMove` become thin wrappers that the view model calls with its own cached list.

- [ ] **2. Buffer-based move generation.**
  `GenerateMoves` currently allocates ~8 `List<Move>` per call (one per piece type plus the outer list). At 10⁶ nodes/s that's tens of millions of GC allocations per search. After (1), thread a `List<Move>` (or `Span<Move>` + count) through `GenerateMoves` / `GenerateKingMoves` / etc. and append into it instead of returning fresh lists. Small diff once (1) is done.

- [ ] **3. Cache king squares on the `Board`.**
  `GetKingSquare` scans all 64 squares; it's called by `IsInCheck` and `GenerateLegalMoves`, i.e. every node of the search. Store `WhiteKingSquare` / `BlackKingSquare` as fields, update them in `MakeMove` / `UnmakeMove` whenever a king (or castling rook) moves. Tiny diff, big hot-path win. Worth doing before search so the first profile run isn't polluted by it.

- [ ] **4. One `GameResult` API on `Board`.**
  Today checkmate/stalemate is inferred by the UI from "move list empty + `IsInCheck`", fifty-move is checked only in `CompleteMove`, and threefold repetition + insufficient material aren't handled at all. A search needs **one pure function** it can call at every leaf to score the position correctly — `Ongoing / WhiteWins / BlackWins / Draw`. Consolidate the rules into `Board.GetResult()` (or similar) and have the UI consume the same API. Repetition depends on (5), so do them together.

- [ ] **5. Zobrist hashing.**
  A `ulong Hash` on `Board`, XOR-updated incrementally in `MakeMove` / `UnmakeMove` (piece-on-square, castling, EP file, side-to-move). Unlocks:
  - Threefold repetition detection (needed by item 4) — keep a small stack/dict of hashes encountered along the current game line.
  - A transposition table later, once search exists.

  Zobrist is dramatically cheaper to add *before* the search than to retrofit afterwards: the XOR sites are the same handful of lines in `MakeMove` that already special-case castling / EP / promotion. Adding it later means revisiting every one of those code paths under the pressure of "something's wrong with my TT."

### First bot

- [ ] **6. First search-based bot.**
  Build up in stages against the current mailbox representation:
  1. Plain minimax at fixed depth, material-only evaluation.
  2. Alpha-beta pruning.
  3. Iterative deepening with a time budget.
  4. Move ordering (captures first via MVV-LVA; hash move from TT).
  5. Transposition table keyed on the Zobrist hash from (5).
  6. Quiescence search (extend captures at the leaves to avoid the horizon effect).

  Each stage is its own commit and its own strength jump against the previous one — a good pedagogical ladder. Even stage 1 with a material-only eval will crush `RandomBot`.

### Post-bot (now driven by benchmarks, not speculation)

- [ ] **7. Bitboard piece storage (`ulong[12]` instead of `int[64]`).**
  Deferred deliberately. Reasons:
  - It's the biggest change by far — rewrites move generation, make/unmake, FEN, perft, the UI bridge, the bot. Weeks of work vs. days for items 1–5.
  - It's orthogonal to the bot's *strength*: a 4–5-ply alpha-beta with a plain material + PST eval is already dramatically better than random, regardless of how fast move generation is.
  - Magic bitboards (or PEXT) are their own multi-week rabbit hole. Better to commit to them with a concrete profiler reading in hand — "move generation is N% of search time" — than speculatively.
  - The pedagogy of "bitboards as a tool" was already covered by the attack/pin mask work (commit `32d8614`); bitboards as *primary storage* is a separate, bigger commitment.

  If profiling after (6) shows move generation is the real bottleneck, this is the next big item. If eval / ordering / TT is the bottleneck, it stays deferred.

## Side quest — not on the critical path

- [ ] **Kiwipete-oracle perft mismatch.** `chess-perft/benchmarks.md` rows 13–14 show the *oracle* (slow make/unmake legality path) disagreeing with the fast path at kiwipete depths 2–3. The fast path is the one tested against known perft numbers and is correct; the oracle has a latent bug. Doesn't block anything, but it neutralises the oracle as an A/B debugger the next time a perft regression shows up. Cheap to fix while the move-gen code is still loaded in working memory.

## Consequences

**Enabled after items 1–5:**
- Search can recurse without stomping module state.
- Per-node cost drops (no king scan, no list allocation per call).
- Leaves can be scored with a single `GetResult()` call — no UI-side duplication of the rules.
- Repetition works; TT is a drop-in after that.

**Explicitly deferred:**
- Bitboard storage. Not because it's wrong — it's the long-term right answer — but because doing it before there's a search to profile means optimising a constant factor of an algorithm that doesn't exist yet.
- Automated tests. Correctness still leans on the perft harness and manual play. A `0003-add-test-project.md` is still the right follow-up.
