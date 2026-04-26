# Game Design Document

*This is the single source of truth for all game design decisions.*
*Last updated: 2026-04-26*

Any change to mechanics — adding, modifying, or removing a rule, system, or data model — must be reflected here. Append an entry to the [Changelog](#changelog) at the bottom.

---

## Vision

The game is a personal experience. Players should feel that the situation on screen is their own — shaped by their decisions, the layout of their table, and how they played their cards. The visual style is inspired by Cultist Simulator: a large open table on which cards appear, with minimal explicit art so that the player's imagination fills in the rest.

A known risk: players who do not project narrative onto the material may play in a purely mechanical mode. This is an open design challenge.

### Three Design Pillars

Every element added to the game must serve at least one of these:

1. **Personalization** — The player's table layout, earned cards, and in-run decisions create a situation unique to them. No two playthroughs look the same.
2. **Combos and Meaningful Decisions** — Placing the right cards on the right slots in the right order triggers satisfying effect chains. The game offers many decisions but not so many that the player feels overwhelmed.
3. **Emotional Response** — The game should provoke real feelings — relief, dread, satisfaction, anger at in-world events. These emotions should be directed at the game's narrative situation, not at the system.

---

## Card Model

### Definition (ScriptableObject: `CardDefinition`)

| Field | Type | Notes |
|---|---|---|
| `title` | `string` | Display name |
| `type` | `CardType` | One of three archetypes |
| `value` | `int` | Base power / health |
| `effects` | `List<CardEffectTag>` | Effect tags (data only, not yet evaluated) |
| `artwork` | `Sprite` | Card illustration |
| `flavorText` | `string` | Narrative text |

### Runtime Instance (`CardInstance`)

| Field | Type | Notes |
|---|---|---|
| `Definition` | `CardDefinition` | Immutable reference |
| `CurrentValue` | `int` | Mutable — reduced by damage |

**Planned addition:** `MaxValue` — the ceiling `CurrentValue` resets to. Upgradable between challenges. When a card enters discard (or returns to deck — open question), `CurrentValue` resets to `MaxValue`.

### Current Sample Cards

| Title | Type | Value |
|---|---|---|
| Bold Move | Pressure | 4 |
| Sweet Talk | Appeal | 3 |
| Maneuver | Positioning | 5 |

These are prototype assets for validating the loop, not final content.

### Card Value Lifecycle

```
deck → hand → board → discard → deck
```

This loop is a mechanical resource. Effects can reference any transition point.

### Starter Cards vs Story Cards

**Starter cards** — general-purpose actions (e.g., Strike, Charm, Scheme). These form the player's initial deck and work in any slot.

**Story cards** *(planned)* — impose a global rule change on the entire table (e.g., "Pressure cards cannot be played until a specific clock fills"). They appear as consequences of game events — things the player failed to prevent or caused to happen. Their narrative justification is tied to those prior events. Each story card has a removal condition.

---

## Card Types and Matchups

Enum: `CardType` — values: `Pressure`, `Appeal`, `Positioning`

Rock-paper-scissors cycle:

| Attacker | Defender | Result |
|---|---|---|
| Pressure | Appeal | Win |
| Appeal | Positioning | Win |
| Positioning | Pressure | Win |
| Any | Same type | Draw |
| Any | Superior type | Lose |

**Planned:** A **typeless** type that always loses matchups. Used for penalty cards or story-driven downgrades.

---

## Combat Rules

Combat resolves when a player card is placed on a slot that already contains an opponent card.

### Damage Formula

Resolved by `CombatResolver.ResolveCombat()`:

- **Win** → damage = attacker's full `CurrentValue`
- **Draw** or **Lose** → damage = `Random(1, CurrentValue)` inclusive

### Exchange Rule

Both cards attack simultaneously (`CombatResolver.ResolveExchange()`):

1. Calculate both damage values before applying either.
2. Apply damage to both cards.
3. Check for destruction (`CurrentValue ≤ 0`).

Mutual destruction is possible.

### Destruction

When a card's `CurrentValue` reaches `0`:
- Card is removed from its slot.
- The appropriate clock is incremented.
- *(Planned: card moves to its owner's discard pile.)*

### Clock Increments on Destruction

| Event | Clock incremented |
|---|---|
| Opponent card destroyed | Opponent clock +1 |
| Player card destroyed | Player clock +1 |
| Both destroyed | Both clocks +1 |

---

## Turn Structure

### Current Implementation

**Prototype loop:** The player may play any number of cards per turn (one per slot), then clicks End Turn.

#### Start of Turn (`TurnController.StartTurn`)
- Draw cards until hand reaches `draftValue` (default 3).
- If draw pile empty, shuffle discard back in automatically.

#### Player Action Phase
- Drag a card from hand onto any empty player slot.
- If the slot has an opponent card → combat resolves immediately.
- If the slot is empty → card sits there until End Turn.

#### End Turn (`TurnController.EndTurn`)
- All surviving player board cards return to hand as `CardDefinition` (full health restored).
- All player slots cleared.
- Opponent draws to fill empty opponent slots.

### Planned: One-Card-Per-Turn Loop

**Turn = one card played.** The player plays a single card, consequences resolve, and then it's the next turn. This is a fundamental change from the prototype.

**Round** = from one draw action to the next.

**Draw slot** — a special functional slot. Playing a card there triggers:
1. *(Optional)* Return all surviving player cards from other slots to hand (cards come back at their damaged `CurrentValue` — weaker than fresh draws).
2. Draw up to hand limit from deck.

The hand limit is a variable tracked on the board state, not a hardcoded constant.

---

## Board and Slots

### Current Implementation

`BoardState` holds a fixed `SlotState[]` array. Each `SlotState` has:
- `PlayerCard` — the player's `CardInstance`
- `OpponentCard` — the opponent's `CardInstance`

Default board: 3 slots.

### Planned: Slot Types

**Standard slot** — has an opponent card position. At the start of a turn, if that position is empty, the opponent draws and places a card there.

**Functional slot** — triggers effects instead of hosting direct combat. Effect timing options:
- On play: fires when the player places a card here.
- End of turn if empty: fires if the player did not place a card here this turn.
- Passive: fires every turn regardless of card presence.
- Affecting opponent: modifies the opponent card in an adjacent linked slot.

**Draw slot** — the special slot described in Turn Structure above.

### Slot Groups

Slots can be organized into named groups that share a zone clock and can be moved freely on the board. Groups represent encounters or thematic zones.

### Slot Effects

Slot effects use the same trigger + action structure as card effects (see Effects System). A card played onto a slot can suppress that slot's effect — "disable this slot's effect" is a valid action.

### Board Aesthetic

The board is a large, freely arrangeable surface (Cultist Simulator style). The player can:
- Reposition card groups, decks, and slot groups anywhere on the table.
- Rotate cards.
- Add cosmetic / organizational elements that do not affect rules.

---

## Fatigue

*(Planned mechanic — not yet implemented)*

When a card placed on a slot remains there when a new opponent card appears (the player did not act on that slot during that turn), the card becomes **fatigued**. A fatigued card does not automatically fight.

To re-engage with a fatigued card, the player must return it to hand and play it again.

**Effects that interact with fatigue:**

| Effect | Description |
|---|---|
| Does not fatigue | Card never becomes fatigued |
| Attacks while fatigued | Effectively same as never fatiguing |
| On fatigue: draw card | Draw a card if below hand limit |
| On fatigue: buff allies | Increase value of other cards of a type, or in the same zone |
| On fatigue: weaken opponent | Decrease opponent card values in zone |

---

## Clocks

### Current Implementation

`ClockState` tracks `CurrentValue` and `MaxValue`. `IsFull` triggers win/loss. Clock max = deck size (player or opponent).

- Destroying an opponent card → `_opponentClock.Increment(1)`
- Losing a player card → `_playerClock.Increment(1)`

### Planned: Multiple Clocks

The game uses multiple clocks rather than a single HP value. A clock is any numeric resource that fills and triggers an event.

**Player clock** — the main losing condition. Slots or opponent cards drain it (e.g., "at end of turn, if this slot is empty, subtract 2 from player clock").

**Zone clocks** — attached to a slot group. If the player does not cover a slot in the group, the opponent card there contributes its `CurrentValue` to the zone clock each turn. When full, an event triggers.

**Type-contribution tracking** *(planned)* — Zone clocks also record which card types were used to resolve them. When a zone clock fills, the dominant type determines the outcome event. This makes *how* the player solved a challenge matter, not just *whether* they solved it.

---

## Effects System

### Current Implementation

`CardEffectTag` (ScriptableObject) fields:
- `tagName` — string
- `trigger` — `EffectTrigger` enum: `OnPlay`, `OnEndTurn`, `AfterAttack`, `OnDestroyed`
- `description` — string

These tags exist on `CardDefinition.effects` but **are not evaluated** by any runtime code. They are data-only placeholders.

### Planned: Trigger + Action Structure

Every effect — on cards, slots, or story cards — follows the same pattern:

```
trigger → action
```

#### Triggers

| Trigger | When |
|---|---|
| `OnPlay` | Card placed on a slot |
| `OnEndTurn` | Turn ends |
| `OnStartTurn` | Turn begins |
| `OnCombatStart` | A fight begins (globally or in a specific zone) |
| `OnSurviveCombat` | Card survives a fight |
| `OnValueDrop` | Card's value decreases |
| `OnValueThreshold` | Value drops to or below a specified number |
| `OnFatigue` | Card becomes fatigued |
| `OnUnfatigue` | Card returns from fatigued to active |
| `OnReturnToHand` | Card moves back to the player's hand |
| `OnDraw` | Card is drawn into hand |
| `OnDiscard` | Card is destroyed and enters discard |
| `OnReturnToDeck` | Card moves from discard back into the deck |

#### Actions

| Action | Description |
|---|---|
| Draw card | Draw one card |
| Draw to limit | Draw up to hand limit |
| Advance clock | Move a zone clock or player clock |
| Change card value | Increase or decrease `CurrentValue` of own or opponent cards |
| Search discard | Look through discard and select a card |
| Search deck | Look through deck and select a card (by type or value threshold) |
| Return cards from slots | Move surviving board cards back to hand |
| Disable slot effect | Suppress the effect of the current slot |
| Block opponent draw | Prevent opponent from drawing to a specific slot |

#### Scope

Effects can target:
- Cards in hand
- Cards on slots
- Cards in a specific zone
- All cards on the board (global)

**Global passives** (from story cards or high-level effects):
- Ban a card type from being played
- Treat one type as another for matchup resolution

#### Composition

Open design question: should each effect be a pre-authored (trigger + action) pair, or should triggers and actions be composed freely? Free composition risks nonsensical combinations.

---

## Opponent Generation

### Current Implementation

The opponent has a `DeckState` (a `List<CardDefinition>` initialized from `GameConfig.opponentDeck`). At end of turn, empty opponent slots are filled by drawing from this deck. When empty, discard shuffles back in.

### Planned: Table-Based Opponent (Alternative)

Opponent cards are generated by rolling on a random table. Each result specifies a type, a value, and an effect+trigger pair.

**Escalation mechanic:** Rolls are made on a 1–20 range using a d10-equivalent die. Zone clocks and player progress add modifiers to the roll, surfacing more difficult table entries over time. At the start of a run, high-difficulty entries are unreachable. As the game escalates, they become possible.

Open question: should the player see and learn the table over time? Possible mechanic: discovered entries are revealed in a log so the player builds knowledge across runs.

---

## Challenges and Encounters

*(Planned — not yet implemented)*

Periodically a slot group appears as a **challenge** (ambush, confrontation, crisis). Opponent cards in the group apply ongoing pressure — draining clocks, triggering effects — until the challenge is resolved.

Because the player plays one card per turn, resolving a challenge requires choosing it over other board activity. This is the core tension-management decision of mid-game play.

Resolving a challenge rewards the player:
- New cards added to the deck.
- Upgrades to existing cards (`maxValue` increase or new effect entries).

---

## Configuration

Managed by `GameConfig` (ScriptableObject in `Assets/Scripts/Config/`):

| Parameter | Default | Description |
|---|---|---|
| `draftValue` | 3 | Hand limit / cards drawn per turn start |
| `boardSlotCount` | 3 | Number of slots on the board |
| `startingDeck` | 9 cards | Player's starting deck (mix of sample cards) |
| `opponentDeck` | 5 cards | Opponent's deck (smaller mixed set) |

---

## Implementation Status

### Implemented and Working

- Single-challenge loop
- Deck, hand, board, and clock state (`DeckState`, `HandState`, `BoardState`, `ClockState`)
- Draw-to-draft at turn start (with auto-reshuffle)
- Drag-and-drop card play (Unity EventSystem)
- Automatic combat on contested slots (simultaneous exchange)
- End turn flow (cards return to hand, opponent refills)
- Win/loss state detection (clock fill check)
- ScriptableObject-based card and config data
- Full test coverage for combat, deck, and turn logic

### Defined but Not Evaluated

- `CardEffectTag` triggers (`OnPlay`, `OnEndTurn`, `AfterAttack`, `OnDestroyed`) — stored in card data, not yet executed at runtime

### Planned but Not Implemented

| Feature | Notes |
|---|---|
| One-card-per-turn loop | Replaces current all-cards-then-resolve flow |
| Fatigue mechanic | Cards stay on slots, become fatigued |
| Draw slot | Special slot to return cards + draw |
| Functional slot effects | Effect evaluation at runtime |
| Zone clocks | Per-group clocks with type tracking |
| Story cards | Global rule-change cards tied to narrative |
| `MaxValue` and upgrades | Separate current vs max value on `CardInstance` |
| Discard for destroyed board cards | Currently only deck-level discard exists |
| Challenge slot groups | Encounter scripting and rewards |
| Table-based opponent | Randomized opponent generation with escalation |
| Free-arrangement board UI | Cultist Simulator-style draggable table |
| Multi-challenge run structure | Campaign / run progression |
| Deck growth between challenges | Adding / upgrading cards as rewards |

---

## Changelog

| Date | Change |
|---|---|
| 2026-04-25 | Initial design: card types, RPS combat, turn structure, clocks documented in `predesign.md` |
| 2026-04-25 | Prototype implemented: single-challenge loop, drag-and-drop, simultaneous combat, win/loss detection |
| 2026-04-25 | Design expanded via voice notes: three pillars, one-card-per-turn, fatigue, zone clocks, story cards, draw slot, table-based opponent, `MaxValue`, free board layout |
| 2026-04-26 | All design consolidated into this document (`game-design-doc.md`) |
