# Story Cards — Design Spec

*Date: 2026-04-30*
*Status: Approved*

---

## Overview

Story cards impose global rule changes or narrative conditions on the table. They appear as consequences of game events. This spec covers the data model and runtime state for story cards and their deck. UI and clock-wiring to other game systems are deferred.

---

## Data Model

### `StoryCardDefinition` (ScriptableObject)

| Field | Type | Notes |
|---|---|---|
| `title` | `string` | Display name |
| `content` | `string` | Narrative text (long-form, TextArea) |
| `artwork` | `Sprite` | Card illustration |
| `effects` | `List<CardEffectTag>` | Optional effect tags — data-only, not evaluated at runtime |
| `clockMaxValue` | `int` | How many points must fill the card's clock before it advances |

### `StoryDeckDefinition` (ScriptableObject)

| Field | Type | Notes |
|---|---|---|
| `cards` | `List<StoryCardDefinition>` | Ordered list of story cards |
| `drawMode` | `StoryDrawMode` | `Sequential` or `Random` |

### `StoryDrawMode` (enum)

```
Sequential  — cards are revealed in list order
Random      — next card is chosen randomly from remaining cards
```

---

## Runtime State

### `StoryDeckState` (plain class, not ScriptableObject)

Owned and initialized by `ChallengeController` when a `StoryDeckDefinition` is assigned.

| Field | Type | Notes |
|---|---|---|
| `Definition` | `StoryDeckDefinition` | Reference to the SO |
| `ActiveCard` | `StoryCardDefinition` | Currently active card; `null` when deck is exhausted |
| `ActiveCardClock` | `ClockState` | Reuses existing `ClockState`; max = `ActiveCard.clockMaxValue` |
| `CurrentIndex` | `int` | Tracks position in Sequential mode |

### `AdvanceCard()` method

Called when `ActiveCardClock.IsFull`:
- **Sequential:** move to next index; set `ActiveCard = null` when list is exhausted.
- **Random:** pick a random card from the remaining unplayed cards.

Sets `ActiveCardClock` to a fresh `ClockState` with the new card's `clockMaxValue`.

---

## Integration

### `ChallengeController`

- Gets an optional serialized field: `StoryDeckDefinition storyDeck`.
- If set, creates `StoryDeckState` on `Awake`/`Start`. `StoryDeckState` constructor sets `ActiveCard` to the first card directly (index 0 for Sequential, random pick for Random) and initializes `ActiveCardClock`. `AdvanceCard()` is only called later when the clock fills.
- Exposes `public void IncrementStoryClock(int amount)` — the single entry point for other systems to fill the story clock. When the clock becomes full, calls `AdvanceCard()` automatically.

---

## Out of Scope (Deferred)

| Feature | Notes |
|---|---|
| Story card UI | Displaying title, content, artwork, clock bar |
| Clock wiring | Which cards/effects call `IncrementStoryClock` and when |
| Multiple active story cards | Architecture supports one at a time for now |
| Removal condition | Cards currently only advance; removal is a future variant |

---

## Constraints

- One story card active at a time.
- `effects` list is data-only (same pattern as `CardEffectTag` on combat cards) — no runtime evaluation in this spec.
- `StoryDeckState` is not a ScriptableObject; it is transient runtime state created from `StoryDeckDefinition`.
