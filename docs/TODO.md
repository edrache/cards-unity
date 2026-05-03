# Feature Backlog

> Source: design notes from `TODO_RAW.md`, cross-referenced with current implementation state.
> Current branch: `game/force-wit-presence`

---

## 1. Player Progression System (Single XP Bar)

**Context:** The game already tracks played card counts per type via `PlayedCardCounterState` and resolves type dominance via `PlayedCardCounterDominanceResolver` (same mechanism used by story cards). The progression system reuses this infrastructure: one shared bar fills from all played cards, and the dominant type at fill-time determines how the player develops.

### What to build
- **One persistent XP bar** (`ProgressionState`) that accumulates the base value of every **opponent card the player defeats** — never resetting on turn end.
- Alongside the bar, per-type counters track the accumulated value of defeated cards by type (Force defeated, Wit defeated, Presence defeated) so dominance can be resolved at level-up time.
- When the bar fills:
  - Use `PlayedCardCounterDominanceResolver` to determine the dominant defeated type (Force / Wit / Presence / tie).
  - Trigger the matching **level-up reward** for that type — initially: draw a new card from a type-specific reward deck and add it permanently to the player's deck.
  - Reset the XP bar to 0; overflow carries into the new tier.
  - Reset the per-type defeated counters so the next level-up is scored fresh.
  - Increment the tier counter (Tier 1, Tier 2, …); higher tiers unlock stronger rewards.

### Sub-tasks
- [ ] Add `ProgressionState` data class — single tiered `ClockState`, current tier, per-type defeated-value counters, overflow value.
- [ ] Hook into `CombatResolver` / `TurnController` at the moment an opponent card is destroyed — add the defeated card's base value to the XP bar and to the matching type counter; check for level-up.
- [ ] Implement `LevelUpResolver` — calls `PlayedCardCounterDominanceResolver`, selects the right reward deck, draws a card, resets counters, handles overflow into next tier.
- [ ] Add `ProgressionRewardDeckSet` ScriptableObject — one reward deck per resolution key (Force, Wit, Presence, FullTie).
- [ ] Update `GameConfig` with XP bar cap per tier (flat value or curve).
- [ ] UI: one XP progress bar widget; optionally show small per-type pip counts beneath it.
- [ ] Update `game-design-doc.md`.

---

## 2. Threat System (replaces Player Clock)

**Context:** The existing Player Clock (`ClockState` on the player side, filled when player cards are destroyed) is being removed. The Threat System becomes the **sole loss condition**. It also adds escalating mid-game penalties before the final loss.

### Remove
- `playerClock` field and all references in `ChallengeController`.
- Loss-condition check based on `playerClock.IsFull`.
- Player-side `ClockView` widget from the UI.
- Any `SlotEffectAction.AddToClock` calls that target `ClockTarget.PlayerClock` (repurpose or delete).

### What to build
- Three **Threat Bars**: `ForceThreat`, `WitThreat`, `PresenceThreat` — each one drains downward like an HP bar (damage reduces it, not fills it).
- **Tiers are segments within each bar**, laid out sequentially. Visually each tier is a separate slider widget placed next to the others, together forming one contiguous bar per type. Tier sizes (max values) do not have to be equal and can be changed at runtime.
- Direction of drain: the rightmost / top segment drains first; when it reaches 0 the next segment starts draining; absolute 0 = **game over**.
- Crossing a tier boundary downward triggers a penalty:
  - **Tier N boundary crossed** → spawn a negative passive slot on the board (`SlotDefinition` that harms the player).
  - **Reaching absolute 0** → game over.
- All three bars (Force/Wit/Presence) are independent; any one reaching 0 ends the game.

### Sub-tasks
- [ ] Remove `playerClock` from `ChallengeController` and all downstream references.
- [ ] Replace loss-condition check with `threatState.AnyBarAtFinalTier`.
- [ ] Add `ThreatBarState` data class — current value, ordered list of `ThreatTierState` (each with its own max value and current value); exposes `CurrentActiveSegment`, `TakeDamage(amount)` (drains from active segment down, cascades), `IsAtZero` property.
- [ ] Add `ThreatState` data class — holds three `ThreatBarState`s (Force/Wit/Presence); exposes `AnyAtZero` for loss-condition check.
- [ ] Extend `SlotEffectAction` enum with `DamageToThreat`; use existing `targetCardType` field as the discriminator for which bar to drain; remove or repurpose old `AddToClock` / `PlayerClock` references.
- [ ] Implement tier-crossing logic inside `ThreatBarState.TakeDamage()` — detect segment boundary crossings, fire events; the controller listens and spawns negative slots or triggers game over.
- [ ] Define `ThreatTierDefinition` ScriptableObject — `maxValue` (size of this segment), `SlotDefinition` to spawn when this tier is crossed. Ordered list; crossing the last one's boundary = game over.
- [ ] Update `GameConfig` — remove player clock cap, add `ThreatTierDefinition[]` sets (one list per type, variable length and sizes).
- [ ] UI: remove old player clock widget; for each threat type add a row of N slider widgets (one per tier) placed side by side to form a segmented HP-style bar; segments can have different widths proportional to their `maxValue`.
- [ ] Update `game-design-doc.md`.

---

## 3. NPC Character Deck

**Context:** A special side deck of NPC character cards. When drawn, an NPC appears and produces some effect (narrative, mechanical, or both). Different from the main opponent deck.

### What to build
- `NpcCardDefinition` ScriptableObject — character name, portrait, type, effect(s).
- `NpcDeckDefinition` — ordered or randomised list of NPC cards.
- `NpcDeckState` — runtime state (draw pile, discard pile, active NPC).
- A draw trigger: some event (slot effect, story card outcome, or explicit action) draws an NPC card and resolves its effect.
- NPC effects can be narrative (text shown to player) or mechanical (e.g., add a card to the player's hand, modify a slot, increment a clock).

### Sub-tasks
- [ ] Design `NpcCardDefinition` and `NpcDeckDefinition` SOs (fields TBD with user).
- [ ] Implement `NpcDeckState` runtime with draw/discard logic (can reuse patterns from `DeckState`).
- [ ] Add `DrawNpcCard` as a new `SlotEffectAction` or `StoryEffectAction`.
- [ ] Basic NPC card UI (portrait, name, effect text — can reuse `StoryCardView` patterns).
- [ ] Create at least one sample NPC deck for testing.
- [ ] Update `game-design-doc.md`.

---

## 4. Status / Condition Cards (Negative States)

**Context:** Cards or tokens representing ongoing negative conditions afflicting the player — persistent bad effects that sit on the board or in the hand and do something harmful each turn.

### What to build
- `StatusDefinition` ScriptableObject — name, icon, description, duration (turns or permanent), per-turn effect.
- A `StatusState` runtime object attached to the player (or to a specific slot).
- When a status is active it evaluates each turn (similar to passive slot effects).
- Statuses can be removed by player actions, story card outcomes, or after N turns.

### Sub-tasks
- [ ] Define `StatusDefinition` SO and `StatusState` runtime.
- [ ] Hook status evaluation into the turn loop (`TurnController`).
- [ ] UI: status display area (icons + remaining duration).
- [ ] Integrate with Threat System (tier fill could spawn a status instead of / in addition to a slot).
- [ ] Create sample statuses for playtesting.
- [ ] Update `game-design-doc.md`.

---

## Notes & Dependencies

| Feature | Depends on |
|---|---|
| Player Progression (XP bar) | Existing `PlayedCardCounterState`, `CombatResolver`, `TurnController` |
| Threat System | Replaces `playerClock`; extends `SlotEffectAction`, `TurnController.EvaluateSlotEffects()` |
| NPC Deck | Existing `DeckState` pattern, `SlotEffectAction` / `StoryEffectAction` |
| Status Cards | Threat System (trigger source), `TurnController` (evaluation hook) |

**Suggested implementation order:**
1. Threat System — extends existing slot effects, concrete loss condition, highest gameplay impact.
2. Player Progression — mirrors threat system in structure, adds positive loop.
3. Status Cards — depends on both systems as trigger sources.
4. NPC Deck — most self-contained, can be done in parallel with Status Cards.
