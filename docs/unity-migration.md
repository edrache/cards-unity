# Unity Migration Guide

This document describes the complete game logic, data structures, and architecture of the HTML/JS card combat prototype so that a Unity agent can reimplement it faithfully.

---

## 1. Project Overview

A turn-based card combat game. The player builds a hand of cards and places them against an enemy row each round. Combat is resolved column by column using a Rock-Paper-Scissors (RPS) mechanic combined with dice rolls. The goal is to clear the enemy board across one or more encounters before running out of cards.

---

## 2. Core Data Model

### 2.1 Card

Every card is a plain object with these fields:

| Field   | Type     | Description |
|---------|----------|-------------|
| `id`    | string   | Unique identifier (e.g. `'brawler'`) |
| `name`  | string   | Display name (e.g. `'Break Will'`) |
| `rps`   | string   | Approach type: `'pressure'` / `'appeal'` / `'positioning'` |
| `value` | number   | Current HP **and** dice ceiling (starts at 3–10, decreases as damage is taken) |
| `role`  | string   | Combat modifier: `'attack'` / `'defense'` / `'support'` / `'none'` |
| `owner` | string   | `'player'` or `'enemy'` |
| `emoji` | string   | Visual icon for the card face |
| `flavor`| string   | Flavour text shown on the card |

> **Important:** `value` serves a dual purpose — it is both the card's HP and the upper bound of its dice roll. When a card takes damage, `value` decreases. A card is destroyed when `value <= 0`.

### 2.2 GameState

The single source of truth for a running game:

| Field            | Type            | Description |
|------------------|-----------------|-------------|
| `phase`          | string          | Current game phase (see Section 4) |
| `round`          | number          | Current round number (starts at 1) |
| `playerDeck`     | Card[]          | Remaining draw pile |
| `playerHand`     | Card[]          | Cards in hand, not yet placed |
| `playerCemetery` | Card[]          | Destroyed player cards |
| `playerBoard`    | (Card\|null)[]  | 3-slot array; `null` = empty slot |
| `enemyDeck`      | Card[]          | Enemy draw pile |
| `enemyCemetery`  | Card[]          | Destroyed enemy cards |
| `enemyBoard`     | (Card\|null)[]  | 3-slot array; `null` = empty slot |
| `placementOrder` | Card[]          | Order in which player placed cards this round (determines resolution order) |
| `rewardChoices`  | Card[]          | Draft choices shown after clearing an encounter |

---

## 3. Card Definitions

### 3.1 Player Deck (12 cards, value range 3–6)

| id         | name             | rps         | value | role    |
|------------|------------------|-------------|-------|---------|
| brawler    | Break Will       | pressure    | 5     | attack  |
| slasher    | Cut an Opening   | positioning | 6     | none    |
| crusher    | Crush Doubt      | appeal      | 6     | none    |
| lunger     | All-In Strike    | pressure    | 3     | none    |
| bulwark    | Hold Composure   | appeal      | 6     | defense |
| ironclad   | Stand Unbroken   | pressure    | 5     | none    |
| buckler    | Slip the Blow    | positioning | 4     | none    |
| mentor     | Guide the Move   | positioning | 3     | none    |
| tactician  | Shape Intent     | appeal      | 5     | support |
| vanguard   | Lead the Charge  | pressure    | 6     | none    |
| scout      | Mark the Path    | positioning | 4     | none    |
| warden     | Steady Hearts    | appeal      | 6     | none    |

### 3.2 Enemy Deck (6 cards, value range 4–5)

| id        | name               | rps         | value | role |
|-----------|--------------------|-------------|-------|------|
| brawler   | Kick In the Door   | pressure    | 5     | none |
| slasher   | Cut Off Escape     | positioning | 5     | none |
| crusher   | Demand Valuables   | appeal      | 5     | none |
| bulwark   | Call for Cover     | appeal      | 5     | none |
| ironclad  | Hold the Breach    | pressure    | 5     | none |
| buckler   | Duck Behind Crates | positioning | 4     | none |

Both decks are shuffled with Fisher-Yates before use. Cards are **cloned** from definitions — mutations (HP loss) only affect the copy in play, never the definition.

---

## 4. Game Phases

The game cycles through these phases stored in `gameState.phase`:

```
draw → placement → combat → draw → ...
                                 → reward (encounter cleared)
                                 → end (player out of cards)
```

| Phase       | Trigger | What happens |
|-------------|---------|--------------|
| `draw`      | Start of run, or after each round | Player draws cards to fill open slots |
| `placement` | After draw | Player drags cards into the 3 slots |
| `combat`    | Player clicks Resolve (all cards placed, or hand empty) | Fights resolve one column at a time |
| `reward`    | All enemy cards cleared | Player picks 1 of 3 new cards; resets to new encounter |
| `end`       | Player has no cards left | Game over |

---

## 5. Round Flow (detailed)

### 5.1 Draw Phase

```
drawCards():
  targetHandSize = number of null slots on playerBoard
  while hand.length < targetHandSize AND deck is not empty:
    draw top card from playerDeck → add to playerHand
  phase = 'placement'
  placementOrder = []
```

### 5.2 Placement Phase

- Player places cards from hand into slots (drag-and-drop in HTML; any input method in Unity).
- `placeCard(cardId, slotIndex)` — moves card from hand to board; appends to `placementOrder`.
- `unplaceCard(slotIndex)` — returns card from board back to hand; removes from `placementOrder`.
- `canResolve()` returns true when all slots are filled **or** hand is empty (so partially empty boards are allowed when the deck runs dry).

### 5.3 Combat Phase

Triggered by `prepareResolveRound()`:

1. `phase` becomes `'combat'`.
2. `getResolutionOrder()` returns a sorted list of slot indices.
3. Fights are revealed **one at a time** (staged flow): player clicks Resolve → see first fight → click Continue → see second, etc.
4. After all fights: `finalizeResolveRound()` cleans up and transitions to the next phase.

#### Resolution Order

Slots fight in the order the **player placed** their cards. Slots where the player has a card but the enemy slot is empty are skipped. Any remaining filled slots that weren't part of the placement order (edge case) are appended in index order.

#### 5.4 After Each Fight

`resolveCombatStep(slotIndex)` is called per column:
- Calls `resolvePair()` which mutates `card.value` on both sides.
- Returns a result object with rolls, chosen roll, damage dealt, value before/after, and `died` flag.

#### 5.5 Round Cleanup (`finalizeResolveRound`)

1. Any card with `value <= 0` is moved to its cemetery; its slot becomes `null`.
2. Surviving player cards return to `playerHand`; their board slots become `null`.
3. Enemy board refills from `enemyDeck` (left to right).
4. `round += 1`.
5. Determine outcome:
   - **Player has no cards** (hand + deck both empty) → `phase = 'end'`, outcome `'loss'`
   - **Enemy has no cards** (board + deck both empty) → `phase = 'reward'`, generate 3 reward choices
   - Otherwise → `phase = 'draw'`, outcome `'continue'`

---

## 6. Combat System

### 6.1 RPS Matchup

```
pressure    beats  positioning
positioning beats  appeal
appeal      beats  pressure
```

`getRpsResult(attacker, defender)` returns:
- `'advantage'`    — attacker beats defender
- `'disadvantage'` — defender beats attacker
- `'neutral'`      — same type

### 6.2 Effective Range (Dice Ceiling)

```
effectiveRange = card.value + supportBonus
```

`supportBonus` = number of directly adjacent allies (left and right) on the same board whose `role === 'support'`. Each such neighbor contributes `+1`.

### 6.3 Roll Modes

| RPS result      | Player rolls      | Enemy rolls       |
|-----------------|-------------------|-------------------|
| `advantage`     | 2× keep highest   | 1×                |
| `disadvantage`  | 1×                | 2× keep highest   |
| `neutral`       | 1× (shared)       | 1× (shared)       |

For **neutral**: both sides roll once. The **lower** of the two rolls becomes the shared damage amount (both sides take the same value).

### 6.4 Damage Calculation

For **advantage/disadvantage**:

```
attackerDealt = chosenRoll
if attacker.role == 'attack': attackerDealt += 1
if defender.role == 'defense': attackerDealt = max(0, attackerDealt - 1)
defender.value -= attackerDealt
```

Both sides attack simultaneously (calculated independently).

For **neutral**:

```
sharedDamage = min(playerRoll, enemyRoll)
playerTaken  = max(0, sharedDamage - (playerCard.role == 'defense' ? 1 : 0))
enemyTaken   = max(0, sharedDamage - (enemyCard.role == 'defense' ? 1 : 0))
playerCard.value -= playerTaken
enemyCard.value  -= enemyTaken
```

### 6.5 Role Summary

| Role      | Effect |
|-----------|--------|
| `attack`  | `+1` added to the attacker's rolled value before dealing damage |
| `defense` | `-1` subtracted from incoming damage (minimum 0) |
| `support` | `+1` to the **effective range** (dice ceiling) of each directly adjacent ally |
| `none`    | No modifier |

---

## 7. Reward Draft

When the enemy board and deck are both empty after finalizing a round:

1. Create 3 reward choices: shuffle a fresh player deck copy and take the first 3 cards.
2. Player selects 1 card → it is added to `playerDeck`.
3. A new encounter begins: new `enemyDeck` and `enemyBoard`, round counter resets to 1, phase = `'draw'`.

---

## 8. Configuration Constants

All tuning values in one place (map these to Unity ScriptableObjects or constants):

### Combat

| Constant     | Value | Meaning |
|--------------|-------|---------|
| `SLOT_COUNT` | 3     | Number of slots per side |

### Card Visuals (for Unity UI reference)

| Constant       | Value  | Meaning |
|----------------|--------|---------|
| `width`        | 150 px | Card width |
| `height`       | 210 px | Card height |
| `borderRadius` | 10 px  | Corner rounding |

### Animation Timings (for Unity Animator / DOTween reference)

| Constant               | Value    | Meaning |
|------------------------|----------|---------|
| `liftScale`            | 1.15     | Scale when picked up |
| `tiltMax`              | 40°      | Max drag tilt rotation |
| `snapDuration`         | 0.05 s   | Snap-to-slot duration |
| `liftDuration`         | 0.55 s   | Lift/drop animation duration |
| `slotSnapRadius`       | 100 px   | Distance to trigger slot snap |
| `resolveFlashDuration` | 0.45 s   | Single fight reveal duration |
| `resolvePauseDuration` | 0.22 s   | Settle pause before Continue |
| `resolveTiltAngle`     | 30°      | Defeated card lean angle |
| `cleanupDuration`      | 0.55 s   | Board-to-hand return duration |

---

## 9. Suggested Unity Architecture

### 9.1 Data Layer (no MonoBehaviour)

```
CardData              : ScriptableObject  — static card definition (id, name, rps, value, role, owner, sprite, flavor)
CardInstance          : plain C# class   — runtime copy with mutable value field
GameState             : plain C# class   — mirrors the JS gameState object above
CombatResolver        : static class     — getRpsResult, getEffectiveRange, getSupportBonus, resolvePair, getCombatPreview
DeckFactory           : static class     — createPlayerDeck, createEnemyDeck, shuffleDeck (Fisher-Yates)
```

### 9.2 Game Flow (MonoBehaviour)

```
GameManager           : MonoBehaviour    — owns GameState; drives phase transitions; exposes events
RoundController       : MonoBehaviour    — drawCards, placeCard, unplaceCard, canResolve
CombatController      : MonoBehaviour    — prepareResolveRound, resolveCombatStep, finalizeResolveRound
RewardController      : MonoBehaviour    — claimReward, new encounter setup
```

### 9.3 UI Layer

```
CardView              : MonoBehaviour    — renders a single CardInstance; handles drag input
SlotView              : MonoBehaviour    — highlights on hover; receives card drop
BoardView             : MonoBehaviour    — manages 3 player slots + 3 enemy slots
HandView              : MonoBehaviour    — lays out playerHand cards
CombatResultView      : MonoBehaviour    — shows per-fight dice and damage results
HUDView               : MonoBehaviour    — round counter, Resolve/Continue button, phase label
DeckPanelView         : MonoBehaviour    — player/enemy deck counts, cemetery counts
RewardView            : MonoBehaviour    — shows 3 card choices after encounter cleared
TooltipView           : MonoBehaviour    — combat preview on hover (ranges, RPS label, roll mode)
```

### 9.4 Input

Use Unity's EventSystem with `IBeginDragHandler`, `IDragHandler`, `IEndDragHandler` on `CardView`. On drop, call `GameManager.PlaceCard(cardId, slotIndex)` if the drop target is a valid slot.

---

## 10. Combat Preview (Pre-Resolution Tooltip)

Before clicking Resolve, hovering a placed card shows a projected outcome. `getCombatPreview()` is a pure read-only function — it does **not** mutate card values. It returns:

```
{
  rps: 'advantage' | 'disadvantage' | 'neutral',
  label: 'ADV' | 'DIS' | 'EVEN',
  player: {
    range: number,          // effective dice ceiling
    supportBonus: number,   // bonus from adjacent support cards
    rollMode: string,       // '2x keep best' | '1x' | '1x shared'
    damageRange: { min, max }  // projected damage dealt to enemy
  },
  enemy: { ... }            // same shape
}
```

The UI uses `damageRange` to show a badge on each placed card: best-case and worst-case damage output, and whether the player card might die.

---

## 11. File → Unity Module Mapping

| HTML/JS file            | Unity equivalent |
|-------------------------|-----------------|
| `config/game.config.js` | `GameConfig` ScriptableObject |
| `config/card.config.js` | `CardVisualConfig` ScriptableObject |
| `config/anim.config.js` | `AnimConfig` ScriptableObject |
| `config/layout.config.js` | `LayoutConfig` ScriptableObject |
| `js/cards-data.js`      | `CardData` ScriptableObjects + `DeckFactory` |
| `js/buffs.js`           | `CombatResolver.GetSupportBonus()` |
| `js/combat.js`          | `CombatResolver` static class |
| `js/enemy.js`           | `DeckFactory` + `EnemyBoardManager` |
| `js/game.js`            | `GameState` + `GameManager` + `RoundController` |
| `js/interactions.js`    | `CardView` drag handlers |
| `js/card-renderer.js`   | `CardView` + `CardVisuals` prefab |
| `js/main.js`            | `BoardView` + scene bootstrap |
| `js/ui.js`              | `HUDView` |
| `js/rps-tracker.js`     | `TooltipView` + `CombatResultView` |
| `css/style.css`         | Unity UI prefabs + Canvas styling |

---

## 12. Key Invariants to Preserve

1. **`value` is both HP and dice ceiling.** Never separate these into two fields.
2. **Damage is simultaneous.** Both cards in a column take damage at the same time; a dying card still deals its damage.
3. **Placement order = resolution order.** The slot the player filled first fights first.
4. **Support bonus is positional.** It only applies to the immediate left and right neighbors on the same board.
5. **Neutral rolls are shared.** In a neutral matchup, the *lower* of the two rolls is the damage both sides take (before defense reduction).
6. **Card definitions are immutable.** Always clone before creating a runtime instance; never mutate definitions.
7. **Enemy deck refills after every round**, not after each fight.
8. **Reward draft resets the encounter** (new enemy deck + board, round = 1), but the player keeps their modified deck.
