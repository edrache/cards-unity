# Game Design Document
**Project:** Card System (working title)
**Status:** Playable prototype
**Date:** 2026-04-20

---

## 1. Concept

A single-player tactical card combat game. Each round the player builds a formation of three cards and pits it against an enemy row. Fights are resolved column by column using a Rock-Paper-Scissors approach mechanic combined with dice rolls. The tension comes from reading the enemy formation before committing cards, and from managing a small, persistent deck across multiple encounters.

**One-sentence pitch:** "Outwit a row of opponents one column at a time using cards that wear down with every fight."

---

## 2. Pillars

| Pillar | Expression |
|--------|------------|
| **Readable** | Every number on the board has a visible origin; the player can always see where a buff came from and what damage range to expect before committing. |
| **Tactile** | Cards feel physically present — they lift, tilt during drag, snap to slots, and lean when destroyed. |
| **Tight** | Three slots per side. No hidden information on the enemy row. Every decision is clear. |
| **Persistent stakes** | Damage carries over between rounds. A card that survives a tough fight is weaker in the next one. |

---

## 3. Core Loop

```
Draw cards
    ↓
Place cards into slots  ←──────────────────┐
    ↓                                       │
Resolve combat column by column             │
    ↓                                       │
Surviving player cards return to hand       │
Dead cards go to cemetery                   │
Enemy board refills                         │
    ↓                                       │
Round continues? ───────────────────────────┘
    ↓
Enemy encounter cleared?
    ↓
Draft one of three reward cards
    ↓
New encounter begins
```

The loop ends when the player runs out of cards (loss) or when no further encounters remain (not yet implemented).

---

## 4. Board Layout

```
┌────────────────────────────────────┐
│  [Opponent Deck]  [Opponent Grave] │  ← deck panel
│                                    │
│  [Enemy 0]  [Enemy 1]  [Enemy 2]  │  ← enemy row
│     1st        2nd        3rd      │  ← resolution order markers (shown after Resolve)
│  [Slot  0]  [Slot  1]  [Slot  2]  │  ← player row (drop zones)
│                                    │
│  [Card] [Card] [Card]  (hand)      │  ← player hand below slots
│                                    │
│  [Player Deck] [Player Grave]      │  ← deck panel
│                                    │
│  Round N       [Resolve / Continue]│  ← HUD
└────────────────────────────────────┘
```

- Three slots per side, columns are 1:1 — enemy slot 0 always faces player slot 0.
- Enemy cards are always visible; there is no hidden information on the opponent's side.
- The **RPS Approach tracker** panel sits on the right, showing the proportion of each approach type the player has played so far in this encounter.
- The **Rules panel** sits on the left.

---

## 5. Cards

### 5.1 Card Fields

| Field   | Type   | Description |
|---------|--------|-------------|
| `name`  | string | Display name |
| `approach` | enum | Pressure / Appeal / Positioning — determines RPS matchup |
| `value` | number | Serves as both HP and dice ceiling simultaneously |
| `role`  | enum   | Attack / Defense / Support / None — passive combat modifier |
| `emoji` | string | Visual icon displayed on the card face |
| `flavor`| string | Flavour text — thematic description |

> **Key design decision:** `value` is both HP and attack potential in a single number. A card with value 6 rolls 1–6 and has 6 health. Taking 4 damage drops it to value 2, making it roll 1–2 in future fights. Cards weaken as they fight — there is no separate ATK stat.

### 5.2 Approach Types

The three approaches form a closed RPS cycle:

```
🔥 Pressure  →  beats  →  🧭 Positioning
🧭 Positioning →  beats  →  🎭 Appeal
🎭 Appeal    →  beats  →  🔥 Pressure
```

Approach determines the **roll strategy** in combat (see Section 6).

### 5.3 Roles

| Role      | Effect | Timing |
|-----------|--------|--------|
| **Attack**   | +1 added to the rolled value before dealing damage | After rolling |
| **Defense**  | −1 subtracted from incoming damage (minimum 0) | After opponent rolls |
| **Support**  | Each directly adjacent ally gains +1 to its roll ceiling | Before rolling |
| **None**     | No modifier | — |

Support's bonus is positional and temporary — it lasts for the duration of the current round's combat and only applies to the immediate left and right neighbour on the same board.

### 5.4 Player Deck (starting 12 cards, values 3–6)

| Name              | Approach    | Value | Role    |
|-------------------|-------------|-------|---------|
| Break Will        | Pressure    | 5     | Attack  |
| Cut an Opening    | Positioning | 6     | None    |
| Crush Doubt       | Appeal      | 6     | None    |
| All-In Strike     | Pressure    | 3     | None    |
| Hold Composure    | Appeal      | 6     | Defense |
| Stand Unbroken    | Pressure    | 5     | None    |
| Slip the Blow     | Positioning | 4     | None    |
| Guide the Move    | Positioning | 3     | None    |
| Shape Intent      | Appeal      | 5     | Support |
| Lead the Charge   | Pressure    | 6     | None    |
| Mark the Path     | Positioning | 4     | None    |
| Steady Hearts     | Appeal      | 6     | None    |

### 5.5 Enemy Deck (6 cards, values 4–5, all role: None)

Enemies have no roles in the current prototype — they fight purely through approach matchups and value.

| Name               | Approach    | Value |
|--------------------|-------------|-------|
| Kick In the Door   | Pressure    | 5     |
| Cut Off Escape     | Positioning | 5     |
| Demand Valuables   | Appeal      | 5     |
| Call for Cover     | Appeal      | 5     |
| Hold the Breach    | Pressure    | 5     |
| Duck Behind Crates | Positioning | 4     |

---

## 6. Combat System

### 6.1 Effective Range

Before rolling, each card's dice ceiling is computed:

```
effective range = card.value + support bonus
```

Support bonus = number of directly adjacent allies on the **same board** whose role is `support`. Maximum +2 (one from each side).

### 6.2 Roll Strategies

The approach matchup determines how many dice each side rolls:

| Matchup result | Winner | Loser |
|----------------|--------|-------|
| **Advantage** | Rolls 2, keeps the highest | Rolls 1 |
| **Disadvantage** | Rolls 1 | Rolls 2, keeps the highest |
| **Neutral** | Both roll 1 — lowest roll becomes shared damage | (same) |

In the **neutral** case the damage is symmetric: both cards take a value equal to the minimum of the two rolls (before defence reduction).

### 6.3 Damage Calculation

**Advantage / Disadvantage fight:**

```
attacker roll  = best of 1 or 2 rolls within [1 .. effective range]
if attacker.role == Attack:  attacker roll += 1
if defender.role == Defense: attacker roll  = max(0, attacker roll − 1)
defender.value -= attacker roll
```

Both sides attack simultaneously with independent rolls.

**Neutral fight:**

```
p roll = roll within [1 .. player effective range]
e roll = roll within [1 .. enemy effective range]
shared damage = min(p roll, e roll)
player takes: max(0, shared damage − (player.role == Defense ? 1 : 0))
enemy  takes: max(0, shared damage − (enemy.role  == Defense ? 1 : 0))
```

### 6.4 Resolution Order

Slots resolve in the order the player placed their cards — the first card placed fights first. This means the player controls the fight sequence and can use it strategically:

- Place a strong card first to guarantee it fights before it can be weakened.
- Place a Support card last to ensure its neighbours benefit from the buff during their fights.

Order markers (`1st`, `2nd`, `3rd`) appear between the enemy and player rows after Resolve is clicked.

### 6.5 Death and Cleanup

- A card with `value ≤ 0` after a fight is destroyed and sent to the cemetery.
- A dying card **still deals its damage** — both sides resolve simultaneously.
- After all fights: surviving player cards return to hand; enemy board refills from the enemy deck.
- Damage is **persistent** — a player card that survives a fight carries its reduced value into the next round.

---

## 7. Game Flow (Phase State Machine)

```
        ┌──────────────────────────────────────────────────┐
        ↓                                                  │
     [ draw ]                                             │
        │ drawCards() fills hand to match open board slots │
        ↓                                                  │
  [ placement ]                                           │
        │ player drags cards into 1–3 slots               │
        │ canResolve() = all slots filled OR hand empty    │
        ↓                                                  │
    [ combat ]                                            │
        │ fights resolve one column at a time (staged)     │
        │ player clicks Resolve → Continue → Continue...   │
        ↓                                                  │
   finalizeResolveRound()                                 │
        ├── outcome: continue  ────────────────────────────┘
        ├── outcome: win  →  [ reward ]
        │                        │ player picks 1 of 3 cards
        │                        │ deck grows by 1 card
        │                        │ new encounter begins
        │                        └──────────────────────────┐
        │                                                   ↓
        └── outcome: loss  →  [ end ]                  (back to draw)
```

### Staged Combat Flow

Combat does not resolve all at once. Each column fight is shown one at a time:

1. Player clicks **Resolve** — first column fight executes and is shown (dice, result note, column highlight).
2. Resolve button becomes **Continue**.
3. Player clicks Continue — next fight, and so on.
4. After the last fight, Continue triggers cleanup and transitions to draw.

This gives the player time to read each result before seeing the next one.

### Reward Draft

When all enemy cards are cleared (board + deck both empty):

- 3 cards are drawn from a fresh shuffled copy of the player card pool.
- Player picks one — it is added to the bottom of their deck.
- The encounter resets: new enemy deck, enemy board refills, round counter resets to 1.
- The player's deck and hand carry over; damage on surviving cards carries over.

---

## 8. Win and Loss Conditions

| Condition | Trigger | State |
|-----------|---------|-------|
| **Continue** | Neither side is fully out of cards | Next round, same encounter |
| **Win encounter** | Enemy board and deck are both empty after finalize | Reward draft, then new encounter |
| **Loss** | Player hand and deck are both empty after finalize | `phase = 'end'` |

There is currently no meta-win condition — the game does not end after clearing encounters. This is marked as open work.

---

## 9. Approach Tracker (RPS Panel)

A side panel shows the proportion of each approach type played by the player in the current encounter. It provides strategic self-monitoring.

**Resolved bars** (solid fill): cumulative proportion of each approach type across all fights that have completed.

**Projected ghost** (dashed outline): shows what the proportion would become if the cards currently on the board were to be resolved. The ghost appears as cards are placed and disappears when cards are unplaced.

The tracker resets at the start of each new encounter (after a reward is claimed). It does not track enemy cards.

---

## 10. Feedback Systems

### Combat Preview (hover)

Hovering a placed player card shows a tooltip with:
- The enemy card it will face
- RPS outcome label: `ADV` / `DIS` / `EVEN`
- Effective roll range for both sides
- Roll mode (`2× keep best`, `1×`, `1× shared`)
- Projected damage range (min–max) for both sides

If the player card could die at the low end of the range, the preview badge uses red loss styling and shows a skull marker.

### Per-Fight Result Note

After each fight resolves:
- The column gets a highlight.
- Each card shows a short result note (damage dealt/taken, survival or destruction).
- Destroyed cards lean at an angle before being cleaned up.
- Already-resolved columns stop showing projected badges.

### Order Markers

`1st`, `2nd`, `3rd` labels appear between the enemy and player rows once Resolve is clicked, showing the fight sequence before the first fight is seen.

### Deck / Cemetery Counts

Persistent counters for player deck, player cemetery, enemy deck, and enemy cemetery are always visible. Clicking opens an overlay showing the full card list.

---

## 11. Tutorial

A separate standalone page (`tutorial.html`) walks through the game mechanics step by step using guided card placement and pre-scripted scenarios. It reuses the shared card renderer and combat logic but has its own thin renderer and progression system. The tutorial page is independent of `index.html` — no shared state.

Tutorial progression is step-based: each step has a prompt and an auto-advance condition (e.g. "drag a card into slot 1" auto-advances when the card snaps). The player cannot proceed to the next step until the current condition is met.

---

## 12. Visual Design Language

- **Board:** Deep forest green felt (`oklch(27% 0.075 152)`), evoking a card table.
- **Cards:** Warm cream paper (`oklch(96% 0.018 85)`) with dark ink borders. Simple geometric rectangle with rounded corners. Space reserved for card artwork background images.
- **Slots:** Muted gold border at rest; bright gold when a card is nearby and can be dropped.
- **Typography:** Mix of display fonts for card names (Bricolage Grotesque), serif for flavour (EB Garamond), condensed for stats (Barlow Condensed), and small-caps for panel titles (IM Fell English SC).
- **Animations:** Cards lift and enlarge slightly on pick-up, tilt in the drag direction (2D rotation trick), snap to slots with a short elastic settle. Destroyed cards tilt visibly before removal.

---

## 13. Configuration Files

All tuning lives in `config/`. Changing these files is the primary way to balance the game without touching logic.

| File | Controls |
|------|----------|
| `config/game.config.js` | `SLOT_COUNT` (currently 3) |
| `config/card.config.js` | Card dimensions, colors, shadows, typography |
| `config/anim.config.js` | Lift/drop/snap/tilt/wiggle timings and easing |
| `config/layout.config.js` | Board background, row Y positions, slot IDs |

---

## 14. Open Design Questions

| Topic | Question |
|-------|----------|
| **Meta-win** | What ends the run? A fixed number of encounters? A boss encounter? |
| **Enemy roles** | Enemies currently have no roles. Adding Defense/Support enemies would significantly increase difficulty and tactical depth. |
| **Deck size** | The player starts with 12 cards. Should the deck grow or thin across a run? Reward cards add to the deck; there is no removal yet. |
| **Enemy variety** | The enemy pool is 6 cards. Encounters always use the same pool. Distinct encounter sets (different enemy decks) would add progression. |
| **Approach synergies** | Currently each card acts independently. Combos triggered by adjacent cards of the same approach type are unexplored. |
| **Value ceiling** | Player cards cap at value 6; enemy cards at 5. The design doc for the roll system notes values can go up to 10. This headroom is unused. |
| **Reward economy** | The reward draft always offers 3 cards from the same player pool. Distinct reward pools (higher-value cards, unique cards) would make the draft more meaningful. |
| **Per-fight result note weight** | The README flags this as potentially too heavy visually. No resolution yet. |
