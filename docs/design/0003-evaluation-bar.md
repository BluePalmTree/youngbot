# 0003 — Evaluation bar

## Context

`Evaluation.Evaluate` returns a centipawn score from the side-to-move's perspective (positive = side-to-move is better). It is currently consumed only by `SimpleBot` to rank candidate moves. There is no surface in the UI that lets a human observer see how the engine evaluates the live position.

A vertical evaluation bar — the same idiom used by Lichess and Chess.com — closes that gap. For a learning-oriented project it doubles as a debugging aid: when the bar disagrees with intuition, that's a signal that either the position is misjudged by us or the evaluation function is missing a term.

This doc is the design for a v1 bar that reads `Evaluate` directly. It deliberately does **not** wait for search-based scores; the bar can be re-pointed at a search score later without changing its visual contract.

## Decision

### 1. Placement

A thin vertical strip pinned to the **left** of the board, between the rank labels and the 8×8 grid, occupying the full height of the grid (`Grid.Row="1"`, new column to the left of the existing `Grid.Column="0"` rank labels). White fill grows from the bottom, black from the top.

Rationale:

- Vertical orientation matches the board's white-at-bottom convention, so the bar reads in the same direction as the position.
- Same idiom as the major chess sites — no new mental model for the user.
- Lives inside the existing `Grid` in `MainWindow.axaml`, so it scales and aligns with the board automatically; no manual layout math.

### 2. The score the bar consumes

`Evaluate` returns a **side-to-move-relative** centipawn score. The bar needs a **side-independent** score (positive = white better, negative = black better). The conversion is a sign flip when it's black to move.

That flip happens in `BoardViewModel`, **not** in `Evaluation`. The engine's perspective-relative score is the right primitive for negamax search (item 6 in [0002](0002-path-to-first-search-bot.md)) — making `Evaluate` return white-relative scores would force every search node to flip the sign back, which is exactly the kind of UI concern leaking into the engine that we want to avoid.

The view model exposes the raw white-relative centipawn value:

```csharp
[ObservableProperty]
private int _evaluationCentipawns;
```

Recomputed once per board change inside `SetProperties()` — that's already the "board state changed, repaint" hook.

`int` rather than `double`: the evaluator only ever produces integers (sums of integer piece values), and storing the unmapped centipawn value lets the visual mapping live entirely in a converter, where it can be tweaked without touching the VM.

### 3. Mapping centipawns → bar fill

A naive linear mapping looks bad. A queen-up advantage (≈ +900 cp) and a hopeless rout (+2000 cp) both pin the bar to the top, so the bar stops moving exactly when the player most wants to see it move. We want a curve that:

- is monotonic (more white material → more white fill, always),
- is steep near zero (small middlegame swings should be visible),
- saturates smoothly at the extremes (a queen up shouldn't look identical to mate-in-2),
- crosses 0.5 fill at exactly 0 cp (an even position shows an even bar).

The standard solution is to push the centipawn score through a **sigmoid** function. The mapping is:

```
fill = 0.5 + 0.5 * tanh(eval_cp / k)
```

with `k` ≈ 400 cp.

The shape: at `eval = 0` the bar sits exactly at 0.5 (half white, half black). At `eval = +k` the bar is roughly 88% white; at `eval = +2k` it's roughly 98%. Going further pushes it toward 100% but never reaches it, so a +900 and a +2000 evaluation produce visibly different bar positions.

#### What `tanh(eval / k)` actually means

`tanh` is the **hyperbolic tangent**. For our purposes the only fact that matters is its shape on a graph: it passes through `(0, 0)`, rises steeply near the origin, and flattens out asymptotically toward `+1` as the input grows large and toward `-1` as the input grows large negative. It never quite reaches ±1.

So `tanh(x)` is a function that takes any real number and squashes it into the open interval `(-1, +1)`, with most of the action happening near `x = 0`.

The `k` in `tanh(eval / k)` controls **how fast we run out of room**. Dividing by `k` rescales the input: if `k = 400`, then an evaluation of 400 cp goes in as `1.0`, and `tanh(1.0) ≈ 0.76`. An evaluation of 800 cp goes in as `2.0`, and `tanh(2.0) ≈ 0.96`. Larger `k` → flatter curve, the bar moves less per centipawn but stays sensitive further out. Smaller `k` → steeper curve, the bar reacts strongly to small evals but pins to the extremes faster.

Picking `k`: the choice is a UX call, not a mathematical one. `k ≈ 400` (roughly four pawns / one minor piece) means "a one-minor-piece advantage is visually very strong but not yet pegged," which lines up with how human players intuit the score. Higher-end engines like Lichess use a similar logistic curve with a comparable steepness.

Finally `0.5 + 0.5 * tanh(...)` shifts and scales the `(-1, +1)` output of `tanh` into `(0, 1)`, which is what the renderer wants: 0 = full black, 0.5 = even, 1 = full white.

This mapping lives in a value converter (`CentipawnsToFillConverter`) so the curve is a presentation concern. Swapping `tanh` for a clamp-and-linear, or retuning `k`, is then a one-file change with no ripple.

### 4. Mate scores (forward-looking note)

When search lands and starts producing mate scores (typically encoded as `±(MATE_VALUE - plies_to_mate)` — a sentinel range far above any positional score), those should **not** flow through the sigmoid. The bar should peg fully and a separate text label should show "M5" / "M-3". The cleanest way is to reserve a sentinel range in `Evaluate`'s output (e.g. `|score| > 30000` is mate) and have the converter check that before applying `tanh`.

Out of scope for v1 — `Evaluate` doesn't produce mate scores yet — but the converter should be written so adding the check later is a few-line edit, not a redesign.

### 5. View shape

A two-row Avalonia `Grid` whose row heights are bound through the converter:

- Top row: black fill, height = `(1 - fill) *` and the rest is unfilled.
- Bottom row: white fill.
- Both rows use star sizing; the converter returns `GridLength`s that sum to the bar's height. Avalonia's layout system handles the rest.

A centered `TextBlock` over the bar shows the numeric eval as `+0.45` / `−1.20` (centipawns ÷ 100, one decimal). For a learning project the number matters as much as the visual: it is the bridge between "this color is winning" and "by how much, in pawns."

### 6. Cost and threading

`Evaluate` is O(64) — one full board scan. Calling it once per `SetProperties()` (once per UI move, once per bot move) is free; no need to debounce, cache, or move off the UI thread.

When the bar is later re-pointed at a search score, that score will already be computed off-thread by the bot, and `MakeBotMoveIfNeeded` already uses `Dispatcher.UIThread.Post` for the result. The bar just reads whatever value is on the VM at repaint time — same contract, different producer.

## Tradeoffs and known limitations

- **Material-only is misleading.** The bar will misjudge sacrificial attacks, king safety problems, and locked positions until `Evaluation` grows piece-square tables, mobility, and king safety terms. This is acceptable for v1 — the bar is a window onto whatever `Evaluate` currently is, not a claim of objective truth. Worth flagging in a comment near the converter so a future reader doesn't read the bar as ground truth.
- **No mate handling yet.** Per §4, the converter has nowhere to route mate scores because none are produced. Revisit when search lands.
- **`tanh` vs. logistic vs. clamp.** Engines variously use `tanh`, the logistic function, or a hard clamp. They all produce visually similar bars in the centipawn ranges that matter (±2000 cp); picking `tanh` is a coin flip resolved by "it's the shortest expression that has the right shape." If user testing later shows the curve feels wrong, retune `k` first, swap the function second.
