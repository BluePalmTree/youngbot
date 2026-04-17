# Design decisions

This folder holds Architecture Decision Records (ADRs) — short write-ups of non-obvious design choices and the reasoning behind them. They exist to answer "why was this done this way?" months or years later, without relying on memory or git archaeology.

## Convention

- **Filename:** `NNNN-short-kebab-title.md`, four-digit zero-padded sequence number. Numbers are append-only and never reused.
- **Length:** short. A decision that needs ten pages probably isn't one decision.
- **Structure:**
  1. **Context** — what problem we're solving and what constraints matter.
  2. **Decision** — the choices we made, each paired with the *alternatives considered* and *why this option won*. The alternatives matter as much as the decision: a year from now, the reader's question is usually "why not X instead?"
  3. **Consequences** — what this enables, what it rules out, what's still on the wishlist.

## Index

- [0001 — Fast legal move generation](0001-fast-legal-move-generation.md)
