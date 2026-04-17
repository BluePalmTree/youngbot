# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

The solution is `chess-bot.slnx` with two .NET 10 projects.

```bash
dotnet build chess-bot.slnx           # build everything
dotnet run --project chess-ui         # launch the Avalonia UI (runnable entry point)
dotnet build chess-engine             # engine builds as a library only
```

There is no test project. Engine correctness is validated in-app via the **Testing → Perft** menu (depths 1–9), which calls `Perft.Divide` against the known node-count table in `chess-engine/Helpers/Perft.cs`. Output goes to `Debug.WriteLine` — run under a debugger (or use `dotnet run` and watch the console) to see per-move counts and totals vs. expected.

## Architecture

Two projects, one-way dependency: **`chess-ui` → `chess-engine`**. The engine has no knowledge of UI concerns.

### Engine (`chess-engine/`)

- **`Models/Board.cs`** — mailbox representation (`int[64] Squares`, index = `rank*8 + file`, a1=0, h8=63). Owns mutable game state: `ColorToMove`, `CastlingRights` (4 bits `KQkq`), `EnPassantSquare`, `HalfMoveClock`, `FullMoveNumber`, and a `Stack<GameState>` for undo. `FromStartPosition(fen)` parses FEN; `MakeMove` / `UnmakeLastMove` are the state transitions and **must stay symmetric** — every field saved into `GameState` on make must be restored on unmake (this is what Perft verifies).
- **`Models/Piece.cs`** — pieces are `int`: low 3 bits = type, bits 3–4 = color (`White=8`, `Black=16`). Always go through `Piece.TypeOf` / `Piece.ColorOf` / `Piece.IsColor` rather than raw bitmath.
- **`Models/Move.cs`** — `readonly struct` carrying `From`, `To`, `MoveFlag`, and `PromotionNeeded`. `MoveFlag` encodes special moves (castles, en passant, double push, and the four promotion pieces). Promotion moves come out of `MoveGenerator` with `PromotionNeeded=true` and `Flag=Normal`; the caller (UI or bot) then rebuilds the move with the concrete `Promote*` flag before applying it.
- **`Helpers/MoveGenerator.cs`** — static class; **stateful**. Callers must run `PrecomputedMoveData()` once (done by `Board.FromStartPosition`), then `GenerateLegalMoves(board)` after every board change. Results live in the static `MoveGenerator.Moves` list, and `Board.AttackedSquares` is populated as a side effect of `GenerateMoves`. Legality is currently checked by the make/unmake + attacked-squares approach in `GenerateLegalMoves` — note there is also a slower reference implementation `GenerateLegalMoves1` kept for comparison.
- **`Helpers/Perft.cs`** — node-count self-test keyed off `expectedNodes`. Use when changing move generation or make/unmake.
- **`Bots/RandomBot.cs`** — reads `MoveGenerator.Moves`, picks one at random, auto-promotes to queen. Pattern to follow for new bots: consume the already-generated move list; do not regenerate.

### UI (`chess-ui/`)

Avalonia 11.3 + CommunityToolkit.Mvvm (source-gen `[ObservableProperty]` / `[RelayCommand]`). SVG pieces via `Svg.Controls.Skia.Avalonia`.

- **`ViewModels/BoardViewModel.cs`** — single source of UI truth. Owns the `Board`, drives `MoveGenerator.GenerateLegalMoves` after every change, and mirrors engine state into observable properties (`CastlingRights`, `EnPassantSquare`, `AttackedSquares`, etc.). After any move, it must re-generate legal moves AND call `SyncFromBoard` — the existing `CompleteMove` / `UndoMove` / `NewGame` methods show the full sequence.
- **Coordinate systems** — the engine uses rank-0-at-bottom indexing; the UI renders rank-0-at-top. `Board.ToUiIndex` / `Board.ToEngineIndex` convert between them. Any code touching `Squares[...]` must be clear which space it's in — bugs here are silent and look like "wrong square highlighted."
- **Promotion flow** — `TryMovePiece` detects a promoting pawn, stores `_pendingPromotionMove`, and fires `PromotionRequired`. `MainWindow.axaml.cs` shows a `MenuFlyout` anchored to the target square; the user's choice calls `CompletePromotion(flag)`, which rebuilds the move with the concrete `Promote*` flag and applies it.
- **Bot turns** — `MakeBotMoveIfNeeded` posts work to the dispatcher at `Background` priority so the UI paints the player's move before the bot responds. Don't call it synchronously from inside a move handler.
- **Drag & drop** — implemented in `MainWindow.axaml.cs` (not a behavior): threshold-gated, renders an SVG→bitmap ghost into the `AdornerLayer`, hit-tests on release via `BoardItems.GetRealizedContainers()`.

## Conventions

- The codebase mixes English identifiers with occasional German terms in comments/docs (see `README.md` for the glossary: Läufer=Bishop, Springer=Knight, Turm=Rook, etc.). Use English in code.
- `Debug.WriteLine` / `Console.WriteLine` inside move generation and perft are deliberate instrumentation; leave them unless explicitly cleaning up.
- Nullable reference types are enabled in both projects. Implicit usings are on in the engine.
