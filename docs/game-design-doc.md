# Game Design Document

*This is the single source of truth for all game design decisions.*
*Last updated: 2026-05-01*

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
| `IsExhausted` | `bool` | True when the card has been used this turn and is shown rotated in UI |

**Planned addition:** `MaxValue` — the ceiling `CurrentValue` resets to. Upgradable between challenges. When a card enters discard (or returns to deck — open question), `CurrentValue` resets to `MaxValue`.

### Current Sample Cards

| Title | Type | Value |
|---|---|---|
| Bold Move | Force | 4 |
| Sweet Talk | Presence | 3 |
| Maneuver | Wit | 5 |

These are prototype assets for validating the loop, not final content.

### Chase Encounter Prototype Cards

`Assets/Resources/Cards/1_Chase/` contains a first pass of encounter card definitions for a contemporary city search scenario. These cards represent universal urban friction rather than named characters or bespoke narrative beats.

| Title | Type | Value |
|---|---|---|
| Crowd | Wit | 3 |
| Noise | Force | 3 |
| Rush | Force | 3 |
| Interruption | Presence | 3 |
| Confusion | Force | 3 |
| Traffic | Wit | 3 |
| Mistake | Force | 3 |
| Distrust | Presence | 3 |
| Distraction | Presence | 3 |
| Blind Spot | Wit | 3 |

### Bar Encounter Prototype Cards

`Assets/Resources/Cards/Bar/` contains a first pass of encounter card definitions for a tavern information-gathering scenario. These cards represent social pressure, physical disruption, and misleading leads that get in the way of finding the truth.

| Title | Type | Value |
|---|---|---|
| Distrust | Presence | 3 |
| Provocation | Presence | 3 |
| Brawl | Force | 3 |
| Rumor | Wit | 3 |
| Confusion | Force | 3 |
| Locked Door | Wit | 3 |
| Crash | Force | 3 |
| Suspicion | Presence | 3 |
| Debt | Wit | 3 |
| Interruption | Presence | 3 |

### Card Value Lifecycle

```
deck → hand → board → discard → deck
```

This loop is a mechanical resource. Effects can reference any transition point.

### Starter Cards vs Story Cards

**Starter cards** — general-purpose actions (e.g., Strike, Charm, Scheme). These form the player's initial deck and work in any slot.

**Story cards** *(planned)* — impose a global rule change on the entire table (e.g., "Force cards cannot be played until a specific clock fills"). They appear as consequences of game events — things the player failed to prevent or caused to happen. Their narrative justification is tied to those prior events. Each story card has a removal condition.

---

## Card Types and Matchups

Enum: `CardType` — values: `Force`, `Presence`, `Wit`

Rock-paper-scissors cycle:

| Attacker | Defender | Result |
|---|---|---|
| Force | Presence | Win |
| Presence | Wit | Win |
| Wit | Force | Win |
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

### Threat and Progression on Destruction

| Event | Result |
|---|---|
| Opponent card destroyed | Opponent clock +1 and player XP increases by the defeated card's base value |
| Player card destroyed | No threat damage is dealt automatically; threat changes come from explicit effects |
| Both destroyed | Opponent clock +1, player XP increases by the defeated opponent card's base value, and the player card is removed |

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
- A player card placed on the board becomes **Exhausted** and is displayed using the prefab-configured exhausted rotation angle.

#### End Turn (`TurnController.EndTurn`)
- All surviving player board cards return to hand as their existing `CardInstance` (current damage is preserved).
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

Each `SlotState` may also hold an optional `SlotDefinition` (ScriptableObject). When a `SlotDefinition` is present:
- `hasOpponentCardSpot` / `hasPlayerCardSpot` declare whether the slot can currently host opponent and player cards.
- `effects` is a list of `SlotEffectDefinition` entries evaluated by `TurnController`.
- `SlotView` exposes up to three effect description labels (one per context: `NoOpponentCard`, `NoPlayerCard`, `Passive`).
- `SlotView` can serialize its own `SlotDefinition` directly on the scene object, and `ChallengeController` will use scene slot definitions before falling back to `GameConfig.slotDefinitions`.

Slots with no `SlotDefinition` use the legacy default behaviour: both anchors are available and opponent cards are auto-placed at end of turn by `PlaceOpponentCards()`.

Slots can also opt into contributing to one shared global tally of played card values. This tally has one running total per card type: `Force`, `Presence`, and `Wit`.

Only cards played directly into a contributing slot affect the tally. Each such slot decides how much value it sends into the global counter:
- `UseCardValue` — add the played card's current runtime `CurrentValue`
- `UseFixedValue` — add one fixed slot-defined value, regardless of the card's own value

The played card's type determines which global bucket is incremented. Example: a `Force` card played into a contributing slot increases only the global `Force` total.

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

`ClockState` tracks `CurrentValue` and `MaxValue`.

Current prototype note: the opponent clock still fills when opponent cards are defeated, but it does not currently end the encounter.

- Destroying an opponent card → `_opponentClock.Increment(1)`
- Story cards use their own per-card `ClockState` to track narrative progress

### Planned: Multiple Clocks

The game uses multiple clocks rather than a single HP value. A clock is any numeric resource that fills and triggers an event.

**Threat bars** — the player-facing losing condition is now split into three typed bars: `Force`, `Wit`, and `Presence`. Each bar is built from ordered segments. Damage drains the first available segment in bar order; when a segment is depleted it may spawn a penalty slot, and when any full bar reaches zero the run ends.

**Zone clocks** — attached to a slot group. If the player does not cover a slot in the group, the opponent card there contributes its `CurrentValue` to the zone clock each turn. When full, an event triggers.

**Type-contribution tracking** *(planned)* — Zone clocks also record which card types were used to resolve them. When a zone clock fills, the dominant type determines the outcome event. This makes *how* the player solved a challenge matter, not just *whether* they solved it.

## Player Progression

The player also has one XP bar represented by `ProgressionState`.

- Defeating an opponent card adds XP equal to that card's base `CardDefinition.value`.
- The same defeated value is also added into per-type counters for `Force`, `Wit`, and `Presence`.
- When XP reaches `XpCap`, the player levels up and the dominant defeated type is resolved via `PlayedCardCounterDominanceResolver`.
- On level-up, the current tier increments, the defeated-type counters reset, and any XP overflow carries into the next tier.
- Reward resolution is currently limited to surfacing the dominant type event; concrete reward content will be wired later.

---

## Effects System

### Current Implementation

**Card effects** — `CardEffectTag` (ScriptableObject) fields:
- `tagName` — string
- `trigger` — `EffectTrigger` enum: `OnPlay`, `OnEndTurn`, `AfterAttack`, `OnDestroyed`
- `description` — string

These tags exist on `CardDefinition.effects` but **are not evaluated** by any runtime code. They are data-only placeholders.

**Slot effects** — `SlotEffectDefinition` (ScriptableObject) fields:
- `context` — `SlotEffectContext`: `NoOpponentCard`, `NoPlayerCard`, `Passive`
- `trigger` — `SlotEffectTrigger`: `OnPlayerTurnStart`, `OnCardPlayed`, `OnOpponentCardPlaced`, `OnRoundEnd`
- `action` — `SlotEffectAction`: `DrawOpponentCard`, `AddToClock`, `DamageToThreat`, `ReturnCardsFromSlots`, `DrawToHandLimit`, `TurnEnd`, `IncreaseOpponentCardsOfTypeValue`, `IncreaseAppearingOpponentCardOfTypeValue`
- `actionValue` — `int` used by numeric effect payloads such as `AddToClock` and `DamageToThreat`
- `targetCardType` — `CardType` used by `DamageToThreat`, `IncreaseOpponentCardsOfTypeValue`, and `IncreaseAppearingOpponentCardOfTypeValue`
- `clockTarget` — `ClockTarget`: currently only `OpponentClock`
- `description` — `string` shown in `SlotView`; multiple effects in the same context are concatenated into one multi-line label

Slot effects **are evaluated at runtime** by `TurnController`.

| Action | Trigger | Context | Behaviour |
|---|---|---|---|
| `DrawOpponentCard` | `OnPlayerTurnStart` | `NoOpponentCard` | Draw from opponent deck and place the card on this slot |
| `AddToClock` | `OnPlayerTurnStart` | `NoPlayerCard` | Increment the opponent clock by `actionValue` |
| `DamageToThreat` | `OnPlayerTurnStart` or `OnCardPlayed` | Any supported slot context | Drain the targeted threat bar by `actionValue` using `targetCardType` |
| `ReturnCardsFromSlots` | `OnCardPlayed` | `Passive` | Return all player cards currently on the board to hand |
| `DrawToHandLimit` | `OnCardPlayed` | `Passive` | Draw from the player deck until hand reaches `draftValue` |
| `TurnEnd` | `OnCardPlayed` | `Passive` | Run the full end-turn sequence immediately: return player cards, clear player slots, and refill opponent slots |
| `IncreaseOpponentCardsOfTypeValue` | `OnPlayerTurnStart` or `OnCardPlayed` | `Passive` | Increase `CurrentValue` by `actionValue` for every opponent card on the board whose `CardType` matches `targetCardType` |
| `IncreaseAppearingOpponentCardOfTypeValue` | `OnOpponentCardPlaced` | `Passive` | Increase `CurrentValue` by `actionValue` only for the opponent card that has just been placed, if its `CardType` matches `targetCardType` |

`OnRoundEnd` fires as soon as the round-end flow is triggered, before player cards return to hand and before empty board slots are refilled with opponent cards. This includes round ends caused by the `TurnEnd` slot action.

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
| Advance clock | Move a zone clock, story clock, or opponent clock |
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
| `slotDefinitions` | Empty list | Optional per-slot definitions used to switch the board into slot-effect mode |

---

## Implementation Status

### Implemented and Working

- Single-challenge loop
- Deck, hand, board, threat, clock, and progression state (`DeckState`, `HandState`, `BoardState`, `ThreatState`, `ClockState`, `ProgressionState`)
- Draw-to-draft at turn start (with auto-reshuffle)
- Drag-and-drop card play (Unity EventSystem)
- Automatic combat on contested slots (simultaneous exchange)
- End turn flow (cards return to hand, opponent refills)
- Win/loss state detection (threat depletion for loss, opponent clock retained as encounter feedback)
- ScriptableObject-based card and config data
- Full test coverage for combat, deck, and turn logic
- Slot effect system: `SlotEffectDefinition` and `SlotDefinition` ScriptableObjects with runtime evaluation in `TurnController`
- Slot effect actions implemented: `DrawOpponentCard`, `AddToClock`, `DamageToThreat`, `ReturnCardsFromSlots`, `DrawToHandLimit`, `TurnEnd`
- UI support for segmented threat bars via `ThreatBarView`, including an aggregated `current/max` label across all threat segments
- UI support for player XP/tier tracking via `ProgressionView`
- Slot UI effect descriptions via `TextMeshProUGUI` labels in `SlotView`
- Reusable UI outline renderer for `Canvas` elements via `RectOutlineGraphic` (`MaskableGraphic` + shader) with configurable thickness, color, rounded corners, solid/dashed mode, dash length, gap length, dash offset, and fixed vs edge-fitted dash distribution
- Story card data model: `StoryCardDefinition`, `StoryDeckDefinition` ScriptableObjects with `StoryDrawMode` (Sequential/Random); `StoryDeckState` runtime class with clock-driven `AdvanceCard()`; `ChallengeController.IncrementStoryClock(int)` entry point
- Story card effects can resolve on story-clock completion from global played-card counters, use RPS to break two-way ties, use a dedicated full-tie branch, optionally hide from the story-card UI, and mutate decks or passive board slots
- Runtime-spawned story slots can now be instantiated directly from story effects; `SlotDefinition.spawnInPassiveContainer` routes them into `BoardView.passiveSlotContainer` for passive-effect layout separation

### Current Story Content

- `Bar` story deck: a three-card sequential investigation arc stored under `Assets/Resources/Story/Bar/`
- `Card_Bar1` — `Smoke and Rumors`: the player enters the bar and must identify where information about the ancient god's idol is being traded
- `Card_Bar2` — `The Informant's Wall`: the player locates the informant, but a protective group blocks access
- `Card_Bar3` — `The Price of a Name`: the player must convince the informant to reveal who currently possesses the figurine of the ancient god
- Current prototype pacing: each bar story card uses a `clockMaxValue` of `3` and advances through the shared `IncrementCardDestroyed` story effect

### Defined but Not Evaluated

- `CardEffectTag` triggers (`OnPlay`, `OnEndTurn`, `AfterAttack`, `OnDestroyed`) — stored in card data, not yet executed at runtime

### Planned but Not Implemented

| Feature | Notes |
|---|---|
| One-card-per-turn loop | Replaces current all-cards-then-resolve flow |
| Fatigue mechanic | Cards stay on slots, become fatigued |
| Draw slot | Special slot to return cards + draw |
| Zone clocks | Per-group clocks with type tracking |
| Story cards — UI | Display active story card (title, content, artwork, clock bar) |
| Story cards — clock wiring | Connect existing cards/effects to IncrementStoryClock |
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
| 2026-04-26 | Added `RectOutlineGraphic` for UI `RectTransform` borders with configurable rounded solid and dashed shader-driven outlines |
| 2026-04-26 | Slot effect system implemented: `SlotEffectDefinition` and `SlotDefinition`, four runtime actions, slot spot gating, and `SlotView` TMP effect descriptions |
| 2026-04-29 | Board slots can now serialize `SlotDefinition` directly in scene `SlotView` objects, with `ChallengeController` preferring scene layout over `GameConfig.slotDefinitions` |
| 2026-04-29 | Added `TurnEnd` slot effect action, which triggers the full end-turn flow and advances play when activated on card play |
| 2026-04-29 | Added `IsExhausted` to `CardInstance`; played player cards are marked Exhausted and `CardView` rotates them using a prefab-configured angle |
| 2026-04-30 | Player cards returning from board to hand now keep their damaged `CurrentValue` instead of resetting to base value |
| 2026-04-30 | Added global played-card counters gated by slot configuration, using either `CardInstance.CurrentValue` or a slot-defined fixed value per played card |
| 2026-04-30 | Added 10 `CardDefinition` prototype assets for the `1_Chase` city-search encounter set under `Assets/Resources/Cards/1_Chase/` |
| 2026-04-30 | Added story card data model: StoryCardDefinition SO, StoryDeckDefinition SO, StoryDeckState runtime class with Sequential/Random draw and per-card ClockState; ChallengeController exposes IncrementStoryClock(int) |
| 2026-05-01 | Renamed the three card archetypes across the project from `Pressure` / `Appeal` / `Positioning` to `Force` / `Presence` / `Wit` |
| 2026-05-01 | Added 10 `CardDefinition` prototype assets for the `Bar` tavern information-gathering encounter set under `Assets/Resources/Cards/Bar/` |
| 2026-05-01 | Added three `StoryCardDefinition` prototype assets for the `Bar` encounter and assigned them sequentially in `Assets/Resources/Story/Bar/Story_Bar.asset` |
| 2026-05-01 | Expanded `StoryEffectDefinition` with story-clock completion resolution, played-card counter dominance, RPS tiebreaks, a full-tie branch, optional story-card visibility, and deck/slot mutations for passive story slots |
| 2026-05-01 | Added runtime story-slot spawning via `StoryEffectSlotMutationMode.Add`, plus `SlotDefinition.spawnInPassiveContainer` and `BoardView.passiveSlotContainer` support for passive-slot placement |
| 2026-05-01 | Added `IncreaseOpponentCardsOfTypeValue` slot effect action with `targetCardType` filtering for buffing matching opponent cards already on the board |
| 2026-05-01 | Added `OnOpponentCardPlaced` slot-effect trigger and `IncreaseAppearingOpponentCardOfTypeValue` for one-time buffs applied only to the newly spawned matching opponent card |
| 2026-05-03 | Added `OnRoundEnd` slot-effect trigger, evaluated immediately when round end begins, including `TurnEnd`-driven round ends |
| 2026-05-03 | Removed the startup `EndTurn()` call from `ChallengeController`, so a new encounter now begins without triggering round-end slot effects or spawning the initial opponent refill automatically |
| 2026-05-03 | Added a `Type current/max` TextMeshPro value label to `ThreatBarView`, showing the summed threat across all bar segments |
| 2026-05-01 | Updated `SlotView` to concatenate multiple effect descriptions from the same context into one multi-line label instead of overwriting earlier text |
| 2026-05-01 | Disabled the prototype win-condition hook for a full opponent clock so only the player loss condition remains active |
| 2026-05-03 | Replaced the player clock with three segmented threat bars and added XP-based player progression with dominant-type level-up resolution |
