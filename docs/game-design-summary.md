# Card Game Design Summary

*Last updated: 2026-04-25*

---

## Vision

The game is intended to be a personal experience. Players should feel that the story unfolding on screen is their own — not someone else's authored narrative but a situation shaped by their specific decisions, the layout of their table, and how they played their cards. The visual style is inspired by Cultist Simulator: a large open table on which cards appear, with minimal explicit visual content so that players project their own imagination onto the material.

A risk acknowledged in the design: players who do not naturally fill in the narrative will play the game in a purely mechanical way. This is an open design challenge.

### Three Design Pillars

Every element added to the game should serve at least one of these three pillars:

1. **Personalization** — The player's table, hand, and decisions create a situation unique to them. The layout of the board, the cards they have earned, and the choices they have made during a run all shape what their game looks like and how it plays.

2. **Combos and Meaningful Decisions** — The player can trigger satisfying effect chains by playing the right cards in the right slots in the right order. The game offers many decisions but not so many that they feel overwhelming. Decision weight matters more than decision volume.

3. **Emotional Response** — The game should make the player feel something — relief, dread, anger at in-world events, or satisfaction. These emotions should be directed at the situation in the game, not at the game as a system.

---

## Card Types and Matchups

Each card belongs to one of three archetypes, which form a rock-paper-scissors cycle:

| Type | Description |
|---|---|
| `Pressure` | Force-based solutions |
| `Appeal` | Charisma, charm, appearance |
| `Positioning` | Planning, thinking, outmaneuvering |

Matchup cycle:
- `Pressure` beats `Appeal`
- `Appeal` beats `Positioning`
- `Positioning` beats `Pressure`
- Matching types result in a draw

A potential fourth type is under consideration: a **typeless** type that always loses matchups. This would serve as a penalty card or story-driven downgrade.

---

## Card Model

Each card has:

- `title`
- `type`
- `currentValue` — mutable; reduced by damage during play
- `maxValue` — the ceiling `currentValue` resets to; upgradable between challenges
- `effects` — a list of effect entries (trigger + action pairs)
- `artwork`
- `flavorText`

### Card Value Lifecycle

A card's `currentValue` is reduced during combat. When the value reaches `0`, the card is destroyed and moves to the discard pile. When the player's draw pile is exhausted, the discard pile is shuffled back in.

**Open question:** does `currentValue` reset to `maxValue` when entering discard, or only when re-entering the deck?

The full card loop — `deck → hand → board → discard → deck` — is itself a mechanical resource. Effects can reference any transition point in this loop.

---

## Combat Rules

Combat resolves when a player card occupies the same slot as an opponent card.

### Matchup Outcomes

- **Win**: the attacker deals damage equal to its full `currentValue`.
- **Draw** or **Lose**: the attacker deals a random value between `1` and its `currentValue`.

### Exchange Rule

Both sides resolve simultaneously:
- Player card deals damage to opponent card.
- Opponent card deals damage back in the same step.
- Both damage values are calculated before either card is removed.

Mutual destruction is possible.

### Destruction

A card whose `currentValue` reaches `0` is destroyed and sent to discard.

---

## Turn Structure

**One turn = one card played.**

The player has a hand of cards. Each turn they drag one card onto a slot. The consequences of that placement resolve immediately (on-play effects, combat if an opponent card is present). Then it is the next turn.

### Drawing Cards

The player's hand is not automatically refilled each turn. A special slot on the board functions as a draw action: playing a card there triggers a draw up to the hand limit. If the player has 1 card in hand and the limit is 5, they draw 4.

Optionally, this same slot can first return all surviving cards from board slots to the player's hand before drawing — so the player can reclaim their placed cards at the cost of receiving weaker, already-damaged cards back instead of fresh draws from the deck.

**Round** = the period from one draw action to the next.

### Hand Limit

A variable on the table determines the current hand limit. This value can be modified by effects.

---

## Board and Slots

The board is a large, freely arrangeable surface. Players can reposition card groups, decks, and decorative elements without affecting gameplay rules. Cards in hand can be placed anywhere. Slot groups can be moved.

### Slot Types

**Standard slot** — has a position for an opponent card. At the start of a turn, if the opponent-card position is empty, an opponent card is drawn from the opponent source and placed there.

**Functional slot** — a position that triggers effects rather than hosting a direct opponent card. Effect timing options:
- **On play**: fires when the player places a card here
- **End of turn if empty**: fires if the player did not place a card here this turn
- **Passive**: fires continuously regardless of card presence
- **Affecting opponent**: modifies the opponent card on an adjacent or linked slot

**Draw slot** — the special slot used to draw cards (described above under Turn Structure).

### Slot Groups

Slots can be organized into groups. Groups share a clock (see below) and can be thematically linked (e.g., a single encounter). Groups are moveable on the board.

### Slot Effects

Slot effects are defined as trigger + action pairs (same structure as card effects). A card played onto a slot can suppress the slot's own effect: one of the possible card actions is "disable the effect of this slot."

---

## Fatigue

When a card placed on a slot remains there when a new opponent card appears (because the player did not act on that slot), the card becomes **fatigued**. A fatigued card does not automatically fight.

To re-engage, the player must return the card to hand and play it again.

Effects that interact with fatigue:
- Card does not fatigue
- Card still attacks while fatigued (effectively the same as not fatiguing)
- When card is fatigued: draw a card if below hand limit
- When card is fatigued: increase the value of other cards of a specific type, or in the same area
- When card is fatigued: weaken opponent cards

---

## Clocks

The game uses multiple **clocks** rather than a single HP value. A clock is any numeric resource that fills up and triggers an event when full.

### Clock Types

**Player clock** — the main losing condition. Certain slots or opponent cards drain it over time (e.g., "at end of turn, if this slot is empty, subtract 2 from player clock").

**Zone clocks** — attached to a slot group. Opponent cards in the group contribute their `currentValue` to the zone clock each turn if the player has not covered their slot. When a zone clock fills, an event triggers.

### Type Contribution Tracking

Zone clocks can also record which card types were used to resolve them. When a zone clock fills, the game checks which type contributed the most. The outcome event can differ based on that dominant type — creating a stylistic consequence for how the player solved the challenge.

---

## Story Cards

Cards are divided into two categories:

**Starter cards** — general-purpose actions that work across most situations (e.g., a generic Strike, Charm, or Scheme). These make up the player's initial deck.

**Story cards** — cards that impose a global rule change on the entire table. Examples:
- Pressure cards cannot be played until a specific clock fills or a specific opponent card is defeated.
- All cards of a given type are treated as a different type for the rest of the run.

Story cards appear as consequences of gameplay events — things the player failed to prevent, or caused to happen. They carry narrative justification tied to those prior events.

---

## Effects System

All effects — on cards, on slots, or from story cards — share the same structure:

```
trigger → action
```

### Triggers

| Trigger | Description |
|---|---|
| On play | Card is placed on a slot |
| On end of turn | Turn ends |
| On start of turn | Turn begins |
| On combat start | A fight begins (globally or in a specific area) |
| On survive combat | Card survives a fight |
| On value drop | Card's value decreases |
| On value threshold | Card's value drops to or below a specified number |
| On fatigue | Card becomes fatigued |
| On un-fatigue | Card returns from fatigued to active state |
| On return to hand | Card returns to the player's hand |
| On draw | Card is drawn into hand |
| On destroy / on discard | Card is destroyed and enters discard |
| On return to deck | Card moves from discard back into the deck |

### Actions

| Action | Description |
|---|---|
| Draw card | Draw one card |
| Draw to limit | Draw up to hand limit |
| Advance clock | Move a zone clock or player clock |
| Change card value | Increase or decrease `currentValue` of own or opponent cards |
| Search discard | Look through discard and select a card |
| Search deck | Look through deck and select a card (by type or value threshold) |
| Return cards from slots | Move surviving board cards back to hand |
| Disable slot effect | Suppress the effect of the current slot |
| Block opponent draw | Prevent opponent from drawing a card to a specific slot |

### Scope

Effects can target:
- Cards in hand
- Cards on slots
- Cards in a specific slot group (area)
- All cards on the board globally

### Composition

Whether effects are authored as fixed pairs (trigger + action as a single entry) or composed from separate trigger and action components is an open design question. Free composition risks creating nonsensical or broken combinations. Pre-authored pairs are safer but less flexible.

### Global passive effects

Some cards or story cards apply persistent rules to the entire table:
- Ban a specific card type from being played
- Treat one type as another type for matchup resolution

---

## Opponent Generation

Two approaches are under consideration:

**Deck-based opponent** — the opponent has a fixed deck. Cards drawn from it appear on the board. Predictable; allows the player to build knowledge of the opponent.

**Table-based opponent** — opponent cards are generated by rolling on a random table. Each result specifies a type, a value, and an effect+trigger pair. The table uses an RPG-style escalation: rolls are made on a 1–20 range with a d10-equivalent die, but modifiers (from zone clocks, player progress, or card effects) push the effective roll higher, surfacing more difficult entries. At the start of a run, high results are effectively inaccessible. The player's choices during the run determine how far the table escalates.

The table-based approach is less predictable but creates a different kind of tension. An open design question is whether the player should be able to see and learn the table over time, and how to display modifiers that affect rolls.

---

## Challenges and Encounters

Periodically, a slot group appears as a **challenge** (e.g., an ambush or confrontation). The opponent cards in the group apply ongoing pressure — draining clocks, triggering effects — until the challenge is resolved.

Because the player plays only one card per turn, resolving a challenge requires prioritizing it over other board activity. This is the core tension-management decision of mid-game play.

Resolving a challenge rewards the player with new cards or upgrades to existing cards (`maxValue` increases or new effect entries).

---

## Deck and Card Progression

Cards that are destroyed enter the discard pile. When the draw pile empties, discard is shuffled back in. Cards return with `currentValue` reset to `maxValue`.

Progression options:
- Earn new cards from resolved challenges
- Upgrade existing cards (increase `maxValue`, add or change effects)

Open question: does upgrading a card mean adding a new trigger+action entry, or replacing an existing one?

---

## Board Configuration (Current Prototype)

| Parameter | Value |
|---|---|
| Draft value (hand limit) | 3 |
| Board slot count | 3 |
| Player starting deck size | 9 |
| Opponent deck size | 5 |

Sample player cards: `Bold Move` (Pressure, 4), `Sweet Talk` (Appeal, 3), `Maneuver` (Positioning, 5).

---

## Implementation Status

### Implemented

- Single-challenge loop
- Deck, hand, board, and clock state
- Draw-to-hand at turn start
- Drag-and-drop card play
- Automatic combat on contested slots
- Simultaneous combat exchange (with mutual destruction)
- End turn flow
- Win/lose state detection
- ScriptableObject-based card and config data
- Effect tag fields on cards (`OnPlay`, `OnEndTurn`, `AfterAttack`, `OnDestroyed`) — data only, not yet evaluated

### Planned

- Fatigue mechanic
- One-card-per-turn loop (replacing current all-cards-then-resolve flow)
- Functional and draw slots with actual effect evaluation
- Zone clocks and type-contribution tracking
- Story cards and global table rules
- Deck/card progression between challenges
- Challenge slot groups and encounter scripting
- Table-based opponent generation
- Free table arrangement (Cultist Simulator-style UI)
- Discard handling for destroyed board cards
- Card `maxValue` and upgrade system
