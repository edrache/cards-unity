---
name: effect-feasibility
description: Use this skill whenever the user describes a game effect they want to create as a SlotEffectDefinition or StoryEffectDefinition ScriptableObject, OR when they paste/reference one of these files and ask "can I do X?", "how to set up Y effect?", "is this possible?". Also trigger when the user describes something like "I want a slot that does X when Y happens" or "I need an effect that triggers on Z". This skill checks current code, tells you exactly what fields to set (or says what needs implementation), and warns about edge cases.
---

## What this skill does

Analyzes a described effect against the **current** game code and answers:
1. **Can it be done?** — yes / partially / no
2. **If yes** — exact field values to set in the ScriptableObject Inspector
3. **Warnings** — edge cases, interactions, or dangerous side-effects
4. **If no** — minimal implementation needed, or a working alternative

---

## Step 1 — Read current code (always do this, every invocation)

Run these greps to load only the relevant parts. Do NOT read full large files.

```bash
# All tiny enum files — defines what's available in the Inspector dropdowns
cat Assets/Scripts/Data/SlotEffectTrigger.cs
cat Assets/Scripts/Data/SlotEffectContext.cs
cat Assets/Scripts/Data/SlotEffectAction.cs
cat Assets/Scripts/Data/StoryEffectTrigger.cs
cat Assets/Scripts/Data/StoryEffectAction.cs
cat Assets/Scripts/Data/ClockTarget.cs

# SO schemas — shows all fields available to configure
cat Assets/Scripts/Data/SlotEffectDefinition.cs
cat Assets/Scripts/Data/StoryEffectDefinition.cs
cat Assets/Scripts/Data/StatusDefinition.cs

# Only the execution switch — the ground truth of what actually runs
grep -n "case SlotEffectAction\|ExecuteSlotEffect\|ApplyDamageToThreat\|DrawOpponentCard\|ReturnAllPlayer\|DrawPlayerCards\|IncreaseOpponent\|ModifyPlayer\|TurnEnd\|AddToClock" Assets/Scripts/Controllers/TurnController.cs

# Story effect execution
grep -n "case StoryEffectAction\|EvaluateStoryEffects\|ApplyStoryEffectOutcome\|ApplySlotMutation\|IncrementStoryClock\|ExecutePlayedCard" Assets/Scripts/Controllers/ChallengeController.cs

# Status effect execution (subset of SlotEffectAction)
grep -n "ApplyStatusEffect\|case SlotEffectAction" Assets/Scripts/Controllers/TurnController.cs
```

These files are small. The greps pull only the switch statements — the definitive list of what runs.

---

## Step 2 — Analyze the request

Map the described effect to available options:

**For SlotEffectDefinition**, the configurable axes are:
- `trigger` — when does it fire?
- `context` — what slot state is required?
- `action` — what does it do?
- `actionValue` — the numeric parameter
- `valueSource` — fixed number, or the value of a card on the slot?
- `slotCardTarget` — which card's value to use (if CardOnSlotValue)?
- `targetCardType` — which card type / threat bar to target?

**For StoryEffectDefinition**, the configurable axes are:
- `trigger` — when does it fire?
- `action` — what does it do?
- `actionValue` — the numeric parameter
- Outcome branches per resolution key (Force / Presence / Wit / FullTie):
  - `deckMutations` — add cards to a deck
  - `slotMutations` — activate/deactivate/replace slots

**For StatusDefinition** (applied via ThreatTier penalties):
- `action` — only `DamageToThreat` and `AddToClock` are handled
- `targetCardType` — which threat bar
- `actionValue`, `durationType`, `durationTurns`

---

## Step 3 — Respond in this structure

### Feasibility verdict
**✓ Fully supported / ⚠ Partially supported / ✗ Not supported**

One sentence explaining why.

### Inspector setup (if supported)
List every field that needs a non-default value. Be precise:

| Field | Value | Why |
|---|---|---|
| `trigger` | `OnPlayerTurnStart` | fires at turn start |
| `context` | `Passive` | always active |
| ... | ... | ... |

### Risks & edge cases
- Call out any interactions that may surprise: e.g. `TurnEnd` inside `OnCardPlayed` triggers a full `EndTurn()` recursion including opponent refill.
- Note if `valueSource = CardOnSlotValue` silently skips the effect when the referenced card is absent.
- Note if `DiscardPlayerCardsAtOrBelowZero()` runs after every action — any value decrease may cause unexpected card loss.
- Note that `IncreaseOpponentCardsOfTypeValue` and `ModifyPlayerCardsOfTypeValue` only accept **fixed** target types (Force/Presence/Wit); `OpponentCard`/`PlayerCard` dynamic types are silently ignored for these actions.
- Note that StatusDefinition only supports `DamageToThreat` and `AddToClock` — other `SlotEffectAction` values on a Status will do nothing.

### If not supported
Two sub-sections:
1. **Minimal implementation** — the smallest code change needed (which method to extend, what case to add)
2. **Closest working alternative** — an effect achievable right now that gets close to the goal

---

## Notes on code evolution

The enums and switch statements you read in Step 1 are always the ground truth. If a new `SlotEffectAction` case appears in `TurnController.ExecuteSlotEffect`, it's supported. If it's not in the switch, it does nothing regardless of what the enum says.
