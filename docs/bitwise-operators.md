# Bitwise operators in C# — visual reference

A quick-lookup cheat sheet for the six bitwise operators, plus how they apply
to chess bitboards. Examples use `byte` (8 bits) for readability; real engine
code typically uses `int`/`uint` (32 bits) or `ulong` (64 bits, one bit per
square).

> **Binary literals:** `0b_1100_1010` is C# syntax for writing a number in
> binary. Underscores are ignored by the compiler and just help you read
> nibbles (4-bit groups). `0b_1100_1010 == 202`.

## AND `&` — "keep bits set in BOTH"

```
  0b_1100_1010   (a = 202)
& 0b_1010_1100   (b = 172)
  ─────────────
  0b_1000_1000   (result = 136)
```

Each column: `1 & 1 = 1`, everything else `0`.

```csharp
byte a = 0b_1100_1010;   // 202
byte b = 0b_1010_1100;   // 172
byte r = (byte)(a & b);  // 0b_1000_1000 = 136

// Masking: keep only the low 4 bits of a byte.
byte value = 0b_1101_0110;                   // 214
byte lowNibble = (byte)(value & 0b_0000_1111);   // 0b_0000_0110 = 6

// Testing a single bit — is bit 3 set in `flags`?
int flags = 0b_0000_1010;
bool bit3Set = (flags & (1 << 3)) != 0;   // true (bit 3 == 8 is present)
```

**Uses:**

- **Masking** — isolate a range of bits (`x & 0x0F` keeps the low nibble).
- **Testing** — `(x & bitMask) != 0` asks "is this bit on?". Non-zero means
  yes, which is why the explicit `!= 0` is conventional (in C# `int` doesn't
  implicitly convert to `bool`).
- **Clearing with `~`** — `x & ~mask` turns off exactly the bits in `mask`.

## OR `|` — "set if EITHER is set"

```
  0b_1100_1010
| 0b_1010_1100
  ─────────────
  0b_1110_1110
```

```csharp
byte a = 0b_1100_1010;
byte b = 0b_1010_1100;
byte r = (byte)(a | b);   // 0b_1110_1110 = 238

// Combining flags.
[Flags]
enum CastleRights { None = 0, WhiteKing = 1, WhiteQueen = 2, BlackKing = 4, BlackQueen = 8 }

var rights = CastleRights.WhiteKing | CastleRights.BlackQueen;  // 0b_1001 = 9

// Turning a flag ON without touching the others.
rights |= CastleRights.WhiteQueen;   // adds bit 1, leaves the rest alone
```

**Uses:**

- **Building flag sets** — combining multiple single-bit values into one
  packed integer.
- **Turning bits ON** — `x |= mask` guarantees every bit in `mask` is set in
  `x` afterward, regardless of prior state.
- **Merging bitboards** — `whiteAttacks | blackAttacks` gives "all squares
  attacked by anyone".

## XOR `^` — "set if DIFFERENT"

```
  0b_1100_1010
^ 0b_1010_1100
  ─────────────
  0b_0110_0110
```

Same bit = 0, different = 1. XOR has a magic property: `x ^ y ^ y == x`.
Applying the same XOR twice cancels out.

```csharp
byte a = 0b_1100_1010;
byte b = 0b_1010_1100;
byte r = (byte)(a ^ b);   // 0b_0110_0110 = 102

// Toggling specific bits (flip bit 2 and bit 5).
byte state = 0b_0000_0000;
byte mask  = 0b_0010_0100;
state ^= mask;            // 0b_0010_0100
state ^= mask;            // 0b_0000_0000  — back to the start

// Zobrist-style incremental hash update:
ulong hash = 0;
hash ^= zobristKeys[whitePawn, e2];   // place pawn on e2
hash ^= zobristKeys[whitePawn, e2];   // remove it (same XOR undoes)
hash ^= zobristKeys[whitePawn, e4];   // place it on e4
```

**Uses:**

- **Toggling** — `x ^= mask` flips exactly the bits in `mask`. Unlike `|`
  (which only sets) or `&` (which only clears), XOR can do both depending on
  the current state.
- **Incremental hashing** — chess engines use XOR to update a 64-bit Zobrist
  hash when a piece moves, without recomputing from scratch. The same key
  XORed in to add a piece XORs back out to remove it.
- **Swap without a temp** — `a ^= b; b ^= a; a ^= b;` swaps two integers.
  Cute, rarely worth it in practice.

## NOT `~` — "flip every bit"

```
~ 0b_1100_1010
  ─────────────
  0b_0011_0101
```

```csharp
byte x = 0b_1100_1010;
byte n = (byte)~x;        // 0b_0011_0101 = 53

// Inverse mask: "everything EXCEPT the low 4 bits".
byte keepHigh = (byte)~0b_0000_1111;                   // 0b_1111_0000 = 240
byte cleared  = (byte)(0b_1111_1111 & keepHigh);       // 0b_1111_0000 = 240

// Clearing a specific bit — turn bit 3 OFF in `flags`.
int flags = 0b_0000_1010;
flags &= ~(1 << 3);       // 0b_0000_0010  (bit 3 cleared)
```

**Uses:**

- **Building "everything except" masks** — `~mask` gives the complement, so
  `x & ~mask` clears exactly those bits.
- **Signed caveat** — on signed types (`int`, `sbyte`, …) `~x == -x - 1`
  because the top bit is the sign. This rarely matters for bitboard work if
  you stick to `uint`/`ulong`, but it's why masks in chess engines are
  usually unsigned.

## Left shift `<<` — "slide bits toward higher values"

```
  0b_0000_1011   (= 11)
<< 2
  ─────────────
  0b_0010_1100   (= 44)     ← same as × 4
```

Zeros fill in on the right. Every left-shift of 1 is a multiplication by 2.

```csharp
int x = 0b_0000_1011;     // 11
int y = x << 2;           // 0b_0010_1100 = 44

// Building a single-bit mask at position n.
int bit5 = 1 << 5;        // 0b_0010_0000 = 32

// Packing two 4-bit values into one byte.
int hi = 0b_1010, lo = 0b_0011;
int packed = (hi << 4) | lo;   // 0b_1010_0011 = 163
```

### What happens when bits shift off the top?

```csharp
// On a byte, C# PROMOTES to int before shifting, so high bits survive.
byte b = 0b_1000_0000;               // 128, bit 7 set
int  promoted  = b << 2;             // 0b_10_0000_0000 = 512
byte truncated = (byte)(b << 2);     // 0b_0000_0000 = 0
// The cast back to byte discards everything above bit 7.

// On a uint, the top bits really do fall off the end:
uint u = 0b_1100_0000_0000_0000_0000_0000_0000_0000;   // bits 31 and 30 set
uint v = u << 2;                                        // 0  — both shifted off

// Gotcha: C# MASKS the shift count, it doesn't saturate.
int w = 1 << 32;    // 1,  NOT 0.  Count 32 is masked to (32 & 31) == 0.
int z = 1 << 33;    // 2.           Count 33 is masked to (33 & 31) == 1.
// The shift-count mask is 31 for 32-bit types and 63 for 64-bit types.
// This is why `1 << 40` silently gives you junk on an `int` — and why
// bitboard code uses `1UL << n` (see below).
```

**Uses:**

- **Building masks** — `1 << n` is the universal "single bit at position n"
  idiom used in set/clear/test bit.
- **Fast multiply** — `x << n` == `x * (1 << n)`. Modern compilers do this
  for you with constant multipliers, but shifts still read as "I care about
  the bit pattern, not the numeric value".
- **Packing fields** — encoding a chess move as `(from << 10) | (to << 4) | flags`
  stores from-square, to-square, and a 4-bit flag into one 16-bit value.

## Right shift `>>` — "slide bits toward lower values"

```
  0b_1011_0000   (= 176)
>> 2
  ─────────────
  0b_0010_1100   (= 44)     ← same as ÷ 4
```

```csharp
uint x = 0b_1011_0000;    // 176
uint y = x >> 2;          // 0b_0010_1100 = 44

// Unpacking a move back into its fields.
int packedMove = 0b_1010_0011_0000_0101;
int from  = (packedMove >> 10) & 0b_0011_1111;   // top 6 bits
int to    = (packedMove >> 4)  & 0b_0011_1111;   // middle 6 bits
int flags = packedMove & 0b_0000_1111;           // low 4 bits
```

### What happens when bits shift off the bottom?

The low bits are simply discarded — no underflow, no wraparound.

```csharp
uint u = 0b_0000_0000_0000_0000_0000_0000_0000_0111;   // 7
uint v = u >> 2;                                        // 0b_..._0001 = 1
// The trailing `11` was shifted off the right edge and discarded.

// Signed right shift preserves the sign bit ("arithmetic shift").
int neg = -4;        // 0b_1111_..._1111_1100 in two's complement
int r1 = neg >> 1;   // -2 — sign bit replicated, still negative
int r2 = neg >>> 1;  // 2_147_483_646 — zero-fill, becomes a large positive

// Same shift-count mask as `<<`:
int w = 1 >> 32;     // 1,  NOT 0.  Count 32 is masked to (32 & 31) == 0.
```

For unsigned types zeros fill in on the left. For signed negative numbers
the sign bit is replicated — use `uint`/`ulong` if you want predictable
zero-fill behavior, or use `>>>` (unsigned right shift, C# 11+) which
always zero-fills regardless of signedness.

**Uses:**

- **Unpacking fields** — combined with `&` masks to pull a packed field
  back out of an integer.
- **Fast divide by power of 2** — `x >> n` for unsigned `x`.
- **Iterating set bits** — repeatedly shifting right and testing the low bit
  walks through every bit in a value.

---

## Applying this to chess: bitboards

A **bitboard** represents the whole 64-square board in one `ulong`
(64-bit unsigned integer), one bit per square. Instead of
`HashSet<int> AttackMap`, you could write:

```csharp
ulong attackMap = 0;

// Mark square `target` as attacked:
attackMap |= 1UL << target;                 // OR in a single bit

// Is square `sq` attacked?
bool attacked = (attackMap & (1UL << sq)) != 0;   // AND-test

// Squares attacked by EITHER side:
ulong contested = whiteAttacks | blackAttacks;

// Squares attacked by white but NOT black:
ulong whiteOnly  = whiteAttacks & ~blackAttacks;

// Toggle a piece on/off a bitboard (Zobrist-style):
pieces ^= 1UL << from;   // remove from source
pieces ^= 1UL << to;     // add at destination
```

### What does `1UL` mean?

`1UL` is the literal `1` typed as `ulong` (**U**nsigned **L**ong — 64-bit
unsigned integer). The suffix matters because of how C# picks the type of
a literal and how shifts interact with it:

| Literal | Type    | Bits |
| ------- | ------- | ---- |
| `1`     | `int`   | 32   |
| `1U`    | `uint`  | 32   |
| `1L`    | `long`  | 64   |
| `1UL`   | `ulong` | 64   |

If you write `1 << 40`, the `1` is an `int` (32-bit), so shifting by 40
overflows and you get `0` — a silent bug. `1UL << 40` gives you the bit at
position 40 of a 64-bit value, which is what you want for a bitboard.

```csharp
ulong bad  = (ulong)(1 << 40);    // 0 — the count is masked to (40 & 31) == 8,
                                  //     so you get `1 << 8 == 256` in an int,
                                  //     NOT bit 40. A silent lie.
ulong good = 1UL << 40;           // bit 40 set, all others 0:
                                  // 0b_0000_0000__0000_0000__0000_0001__0000_0000
                                  //   __0000_0000__0000_0000__0000_0000__0000_0000
```

(The result of `good` is a 64-bit value, so its binary form is 64 digits
long. The only "1" sits at position 40 counting from the right.)

Rule of thumb: **when touching a `ulong` bitboard, always write `1UL`** —
it makes the bit width explicit and avoids the 32-bit overflow trap.

### Visualizing a bitboard

If a knight sits on e4 (square 28), the squares it attacks form this
pattern — all stored in a single `ulong`:

```
 8 . . . . . . . .
 7 . . . . . . . .
 6 . . . 1 . 1 . .     ← d6, f6
 5 . . 1 . . . 1 .     ← c5, g5
 4 . . . . N . . .     ← knight (not part of attack set)
 3 . . 1 . . . 1 .     ← c3, g3
 2 . . . 1 . 1 . .     ← d2, f2
 1 . . . . . . . .
   a b c d e f g h
```

Why this matters: "does this knight give check?" becomes
`(knightAttacks & kingBB) != 0` — one AND, one compare, no loops, no
allocations. Bitboards are the standard representation for fast chess
engines because the per-square loops and set operations in a
`HashSet`-based attack map collapse into a handful of bitwise ops over
64-bit integers.

## Common idioms cheat sheet

| Goal                     | Expression                           |
| ------------------------ | ------------------------------------ |
| Set bit `n`              | `x \|= 1UL << n`                     |
| Clear bit `n`            | `x &= ~(1UL << n)`                   |
| Toggle bit `n`           | `x ^= 1UL << n`                      |
| Test bit `n`             | `(x & (1UL << n)) != 0`              |
| Low `k` bits only        | `x & ((1UL << k) - 1)`               |
| Clear low `k` bits       | `x & ~((1UL << k) - 1)`              |
| Multiply by 2ⁿ           | `x << n`                             |
| Divide by 2ⁿ (unsigned)  | `x >> n`                             |
| Popcount (# of 1-bits)   | `BitOperations.PopCount(x)`          |
| Index of lowest set bit  | `BitOperations.TrailingZeroCount(x)` |
| Index of highest set bit | `BitOperations.Log2(x)`              |

`System.Numerics.BitOperations` wraps hardware intrinsics (e.g. `POPCNT`,
`BMI1`) so these are effectively free on modern CPUs.
