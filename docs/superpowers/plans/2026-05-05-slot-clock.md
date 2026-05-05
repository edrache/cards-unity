# Slot Clock Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Allow any SlotDefinition to optionally carry a clock that is progressed by a `ProgressSlotClock` slot effect and fires a completion effect (initially `RemoveSlot`) when full.

**Architecture:** A `[Serializable]` `SlotClockConfig` embedded in `SlotDefinition` holds the design-time config (max value, completion effect). `SlotState` creates a `SlotClockState` from it at init time. `TurnController.ExecuteSlotEffect` handles the new `ProgressSlotClock` action, then checks for clock completion. `SlotView` optionally holds a `ClockView` reference; `BoardView.Refresh` pushes the updated `SlotClockState` into it each turn.

**Tech Stack:** C# / Unity 6, NUnit (EditMode tests), UGUI

---

## File Map

| Action | File | Responsibility |
|---|---|---|
| Create | `Assets/Scripts/Data/SlotClockCompletionEffect.cs` | Enum: what happens when clock fills |
| Create | `Assets/Scripts/Data/SlotClockConfig.cs` | `[Serializable]` design-time config (maxValue + completionEffect) |
| Create | `Assets/Scripts/Data/SlotClockState.cs` | Runtime mutable state: currentValue, IsFull, Progress |
| Modify | `Assets/Scripts/Data/SlotDefinition.cs` | Add `public SlotClockConfig clock` field |
| Modify | `Assets/Scripts/Data/SlotState.cs` | Add `ClockState` property; init/reset it in constructors and `SetDefinition` |
| Modify | `Assets/Scripts/Data/SlotEffectAction.cs` | Add `ProgressSlotClock` enum value |
| Modify | `Assets/Scripts/Controllers/TurnController.cs` | Handle `ProgressSlotClock`; call `ExecuteSlotClockCompletion` when full |
| Modify | `Assets/Scripts/UI/ClockView.cs` | Add `Refresh(SlotClockState)` overload |
| Modify | `Assets/Scripts/UI/SlotView.cs` | Add `[SerializeField] ClockView clockView`; add `RefreshClock(SlotClockState)` |
| Modify | `Assets/Scripts/UI/BoardView.cs` | Call `slotView.RefreshClock(slot.ClockState)` in `Refresh` |
| Create | `Assets/Scripts/Tests/SlotClockStateTests.cs` | Unit tests for `SlotClockState` |
| Modify | `Assets/Scripts/Tests/TurnControllerTests.cs` | Tests for `ProgressSlotClock` effect and `RemoveSlot` completion |

---

## Task 1: Tests for SlotClockState (write failing tests first)

**Files:**
- Create: `Assets/Scripts/Tests/SlotClockStateTests.cs`

- [ ] **Step 1: Create the test file**

```csharp
using NUnit.Framework;
using CardsUnity;

namespace CardsUnity.Tests
{
    public class SlotClockStateTests
    {
        [Test]
        public void Increment_increases_current_value()
        {
            var clock = new SlotClockState(maxValue: 5);
            clock.Increment(2);
            Assert.AreEqual(2, clock.CurrentValue);
        }

        [Test]
        public void IsFull_true_when_current_equals_max()
        {
            var clock = new SlotClockState(maxValue: 3);
            clock.Increment(3);
            Assert.IsTrue(clock.IsFull);
        }

        [Test]
        public void IsFull_true_when_current_exceeds_max()
        {
            var clock = new SlotClockState(maxValue: 3);
            clock.Increment(5);
            Assert.IsTrue(clock.IsFull);
        }

        [Test]
        public void IsFull_false_when_below_max()
        {
            var clock = new SlotClockState(maxValue: 3);
            clock.Increment(2);
            Assert.IsFalse(clock.IsFull);
        }

        [Test]
        public void Progress_returns_correct_ratio()
        {
            var clock = new SlotClockState(maxValue: 4);
            clock.Increment(2);
            Assert.AreEqual(0.5f, clock.Progress, 0.001f);
        }

        [Test]
        public void Progress_is_zero_on_fresh_clock()
        {
            var clock = new SlotClockState(maxValue: 5);
            Assert.AreEqual(0f, clock.Progress, 0.001f);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail (SlotClockState doesn't exist yet)**

```bash
/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath . \
  -runTests -testPlatform EditMode \
  -testResults TestResults/EditMode.xml
```

Expected: compilation error — `SlotClockState` not found.

---

## Task 2: Implement data types to make Task 1 tests pass

**Files:**
- Create: `Assets/Scripts/Data/SlotClockCompletionEffect.cs`
- Create: `Assets/Scripts/Data/SlotClockConfig.cs`
- Create: `Assets/Scripts/Data/SlotClockState.cs`
- Modify: `Assets/Scripts/Data/SlotDefinition.cs`
- Modify: `Assets/Scripts/Data/SlotState.cs`

- [ ] **Step 1: Create `SlotClockCompletionEffect.cs`**

```csharp
namespace CardsUnity
{
    public enum SlotClockCompletionEffect
    {
        RemoveSlot
    }
}
```

- [ ] **Step 2: Create `SlotClockConfig.cs`**

```csharp
using System;

namespace CardsUnity
{
    [Serializable]
    public class SlotClockConfig
    {
        public int maxValue = 5;
        public SlotClockCompletionEffect completionEffect = SlotClockCompletionEffect.RemoveSlot;
    }
}
```

- [ ] **Step 3: Create `SlotClockState.cs`**

```csharp
namespace CardsUnity
{
    public class SlotClockState
    {
        public int MaxValue { get; }
        public int CurrentValue { get; private set; }
        public bool IsFull => MaxValue > 0 && CurrentValue >= MaxValue;
        public float Progress => MaxValue > 0 ? (float)CurrentValue / MaxValue : 0f;

        public SlotClockState(int maxValue)
        {
            MaxValue = maxValue;
        }

        public void Increment(int amount = 1) => CurrentValue += amount;
    }
}
```

- [ ] **Step 4: Add `clock` field to `SlotDefinition.cs`**

Current file (`Assets/Scripts/Data/SlotDefinition.cs`):
```csharp
using System.Collections.Generic;
using UnityEngine;

namespace CardsUnity
{
    [CreateAssetMenu(menuName = "CardsUnity/Slot Definition", fileName = "NewSlot")]
    public class SlotDefinition : ScriptableObject
    {
        [Tooltip("If true, an opponent card can occupy this slot and participate in combat.")]
        public bool hasOpponentCardSpot = true;
        [Tooltip("If true, the player can drag a card onto this slot.")]
        public bool hasPlayerCardSpot = true;
        [Tooltip("If true, runtime-spawned instances of this slot are parented under BoardView.passiveSlotContainer instead of the main board container.")]
        public bool spawnInPassiveContainer;
        [Tooltip("If true, cards played into this slot contribute to the global played-card counters.")]
        public bool contributesToGlobalPlayedCardCounts;
        [Tooltip("Controls whether this slot contributes the card's current value or a fixed value to the global played-card counters.")]
        public PlayedCardCountMode playedCardCountMode = PlayedCardCountMode.UseCardValue;
        [Tooltip("When using a fixed contribution mode, every card played into this slot adds this value to the matching global type counter.")]
        public int fixedPlayedCardCountValue = 1;
        public List<SlotEffectDefinition> effects = new List<SlotEffectDefinition>();
    }
}
```

Add one field before `effects`:
```csharp
        [Tooltip("Optional clock attached to this slot. maxValue > 0 activates it. Use a ProgressSlotClock effect to advance it.")]
        public SlotClockConfig clock;
        public List<SlotEffectDefinition> effects = new List<SlotEffectDefinition>();
```

- [ ] **Step 5: Update `SlotState.cs` to initialise `ClockState` from definition**

Replace the entire file:
```csharp
namespace CardsUnity
{
    public class SlotState
    {
        public SlotDefinition Definition { get; private set; }
        public CardInstance OpponentCard { get; set; }
        public CardInstance PlayerCard { get; set; }
        public bool IsActive { get; private set; } = true;
        public SlotClockState ClockState { get; private set; }

        public bool HasOpponentCard => OpponentCard != null;
        public bool HasPlayerCard => PlayerCard != null;

        public SlotState() { }

        public SlotState(SlotDefinition definition)
        {
            Definition = definition;
            ClockState = BuildClockState(definition);
        }

        public SlotState(SlotDefinition definition, bool isActive)
        {
            Definition = definition;
            IsActive = isActive;
            ClockState = BuildClockState(definition);
        }

        public void SetDefinition(SlotDefinition definition)
        {
            Definition = definition;
            ClockState = BuildClockState(definition);
        }

        public void SetActive(bool isActive)
        {
            IsActive = isActive;
        }

        private static SlotClockState BuildClockState(SlotDefinition definition)
        {
            if (definition?.clock == null || definition.clock.maxValue <= 0)
                return null;

            return new SlotClockState(definition.clock.maxValue);
        }
    }
}
```

- [ ] **Step 6: Run tests — expect SlotClockStateTests to pass**

```bash
/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath . \
  -runTests -testPlatform EditMode \
  -testResults TestResults/EditMode.xml
```

Expected: all `SlotClockStateTests` pass; all pre-existing tests still pass.

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/Data/SlotClockCompletionEffect.cs \
        Assets/Scripts/Data/SlotClockCompletionEffect.cs.meta \
        Assets/Scripts/Data/SlotClockConfig.cs \
        Assets/Scripts/Data/SlotClockConfig.cs.meta \
        Assets/Scripts/Data/SlotClockState.cs \
        Assets/Scripts/Data/SlotClockState.cs.meta \
        Assets/Scripts/Data/SlotDefinition.cs \
        Assets/Scripts/Data/SlotState.cs \
        Assets/Scripts/Tests/SlotClockStateTests.cs \
        Assets/Scripts/Tests/SlotClockStateTests.cs.meta
git commit -m "feat: add SlotClockConfig, SlotClockState and wire into SlotState"
```

---

## Task 3: Tests for TurnController — ProgressSlotClock effect (write failing tests first)

**Files:**
- Modify: `Assets/Scripts/Tests/TurnControllerTests.cs`

- [ ] **Step 1: Add two tests to `TurnControllerTests.cs` — append before the last closing brace**

Helper needed in the test class (add alongside the existing private helpers at the bottom of the class):

```csharp
private static SlotDefinition MakeSlotDefWithClock(int maxValue, SlotClockCompletionEffect completion, int progressPerTurn)
{
    var effect = ScriptableObject.CreateInstance<SlotEffectDefinition>();
    effect.context = SlotEffectContext.Passive;
    effect.trigger = SlotEffectTrigger.OnRoundEnd;
    effect.action = SlotEffectAction.ProgressSlotClock;
    effect.actionValue = progressPerTurn;

    var def = ScriptableObject.CreateInstance<SlotDefinition>();
    def.clock = new SlotClockConfig { maxValue = maxValue, completionEffect = completion };
    def.effects = new System.Collections.Generic.List<SlotEffectDefinition> { effect };
    return def;
}
```

Tests to add:

```csharp
[Test]
public void ProgressSlotClock_effect_increments_slot_clock()
{
    var def = MakeSlotDefWithClock(maxValue: 5, SlotClockCompletionEffect.RemoveSlot, progressPerTurn: 2);
    _board.Slots[0].SetDefinition(def);

    _turn.EndTurn();

    Assert.AreEqual(2, _board.Slots[0].ClockState.CurrentValue);
}

[Test]
public void ProgressSlotClock_deactivates_slot_when_clock_reaches_max_with_RemoveSlot()
{
    var def = MakeSlotDefWithClock(maxValue: 1, SlotClockCompletionEffect.RemoveSlot, progressPerTurn: 1);
    _board.Slots[0].SetDefinition(def);

    _turn.EndTurn();

    Assert.IsFalse(_board.Slots[0].IsActive);
}
```

- [ ] **Step 2: Run tests — expect two new failures (ProgressSlotClock not yet handled)**

```bash
/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath . \
  -runTests -testPlatform EditMode \
  -testResults TestResults/EditMode.xml
```

Expected: `ProgressSlotClock_effect_increments_slot_clock` and `ProgressSlotClock_deactivates_slot_when_clock_reaches_max_with_RemoveSlot` both fail.

---

## Task 4: Implement ProgressSlotClock in TurnController

**Files:**
- Modify: `Assets/Scripts/Data/SlotEffectAction.cs`
- Modify: `Assets/Scripts/Controllers/TurnController.cs`

- [ ] **Step 1: Add `ProgressSlotClock` to `SlotEffectAction.cs`**

Replace entire file:
```csharp
namespace CardsUnity
{
    public enum SlotEffectAction
    {
        DrawOpponentCard,
        AddToClock,
        DamageToThreat,
        ReturnCardsFromSlots,
        DrawToHandLimit,
        TurnEnd,
        IncreaseOpponentCardsOfTypeValue,
        IncreaseAppearingOpponentCardOfTypeValue,
        ModifyPlayerCardsOfTypeValue,
        ProgressSlotClock
    }
}
```

- [ ] **Step 2: Handle `ProgressSlotClock` in `TurnController.ExecuteSlotEffect`**

Inside the `switch (effect.action)` block in `ExecuteSlotEffect` (around line 300), add a new case after `ModifyPlayerCardsOfTypeValue`:

```csharp
                case SlotEffectAction.ProgressSlotClock:
                    if (slot.ClockState != null)
                    {
                        slot.ClockState.Increment(effect.actionValue);
                        Debug.Log(
                            $"[TurnController] ProgressSlotClock. Slot={GetSlotIndex(slot)}, " +
                            $"Current={slot.ClockState.CurrentValue}, Max={slot.ClockState.MaxValue}, IsFull={slot.ClockState.IsFull}");
                        if (slot.ClockState.IsFull)
                            ExecuteSlotClockCompletion(slot);
                    }
                    break;
```

- [ ] **Step 3: Add `ExecuteSlotClockCompletion` private method to `TurnController`**

Add this method after `ExecuteSlotEffect` (around line 308):

```csharp
        private void ExecuteSlotClockCompletion(SlotState slot)
        {
            var config = slot.Definition?.clock;
            if (config == null)
                return;

            Debug.Log(
                $"[TurnController] Slot clock full. Slot={GetSlotIndex(slot)}, CompletionEffect={config.completionEffect}");

            switch (config.completionEffect)
            {
                case SlotClockCompletionEffect.RemoveSlot:
                    slot.SetActive(false);
                    break;
            }
        }
```

- [ ] **Step 4: Run tests — expect all tests to pass**

```bash
/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath . \
  -runTests -testPlatform EditMode \
  -testResults TestResults/EditMode.xml
```

Expected: all tests pass including the two new TurnController clock tests.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Data/SlotEffectAction.cs \
        Assets/Scripts/Controllers/TurnController.cs \
        Assets/Scripts/Tests/TurnControllerTests.cs
git commit -m "feat: implement ProgressSlotClock slot effect with RemoveSlot completion"
```

---

## Task 5: View wiring — ClockView, SlotView, BoardView

No automated tests for this task (Unity UI MonoBehaviour code). Verify manually in Play Mode after wiring prefabs.

**Files:**
- Modify: `Assets/Scripts/UI/ClockView.cs`
- Modify: `Assets/Scripts/UI/SlotView.cs`
- Modify: `Assets/Scripts/UI/BoardView.cs`

- [ ] **Step 1: Add `Refresh(SlotClockState)` overload to `ClockView.cs`**

Current `ClockView.cs` has `public void Refresh(ClockState clock)`. Read the file and add the following overload directly after that method:

```csharp
        public void Refresh(SlotClockState clock)
        {
            if (clock == null)
            {
                if (progressBar != null)
                    progressBar.SetBar01(0f);
                if (fillImage != null)
                    fillImage.fillAmount = 0f;
                if (label != null)
                    label.text = string.Empty;
                ClearTicks();
                return;
            }

            SyncTicks(clock.MaxValue);

            if (progressBar != null)
                progressBar.UpdateBar(clock.CurrentValue, 0f, clock.MaxValue);
            else if (fillImage != null)
                fillImage.fillAmount = clock.Progress;

            if (label != null)
                label.text = $"{clock.CurrentValue} / {clock.MaxValue}";
        }
```

- [ ] **Step 2: Add `clockView` field and `RefreshClock` method to `SlotView.cs`**

Add the serialized field after the existing `[SerializeField]` block (after line 22, before `public int SlotIndex`):

```csharp
        [SerializeField] private ClockView clockView;
```

Add the following public method anywhere after `ApplyDefinition`:

```csharp
        public void RefreshClock(SlotClockState clockState)
        {
            if (clockView == null)
                return;

            clockView.gameObject.SetActive(clockState != null);
            clockView.Refresh(clockState);
        }
```

- [ ] **Step 3: Call `RefreshClock` inside `BoardView.Refresh`**

In `BoardView.cs`, inside the `Refresh(BoardState board)` loop, after `slotView.ApplyDefinition(slot.Definition)` (around line 50), add:

```csharp
                slotView.RefreshClock(slot.ClockState);
```

The relevant block should look like this after the edit:

```csharp
                slotView.ApplyDefinition(slot.Definition);
                slotView.RefreshClock(slot.ClockState);
                slotView.SetHighlight(false);
```

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/UI/ClockView.cs \
        Assets/Scripts/UI/SlotView.cs \
        Assets/Scripts/UI/BoardView.cs
git commit -m "feat: wire SlotClockState into SlotView and BoardView refresh"
```

---

## Task 6: Update game design doc

**Files:**
- Modify: `docs/game-design-doc.md`

- [ ] **Step 1: Add slot clock section and changelog entry**

Find the **Board and Slots** section in `docs/game-design-doc.md` and add the following subsection:

```markdown
### Slot Clocks

A slot can optionally carry a clock defined in `SlotClockConfig` (embedded in `SlotDefinition`):

| Field | Meaning |
|---|---|
| `maxValue` | Clock capacity. `0` means no clock. |
| `completionEffect` | What happens when `currentValue >= maxValue`. Currently: `RemoveSlot`. |

The clock is **not** advanced automatically. It is advanced by a `ProgressSlotClock` slot effect:

- **Action:** `ProgressSlotClock`
- **actionValue:** Amount to add per trigger
- **Typical setup:** `context = Passive`, `trigger = OnRoundEnd`, `actionValue = 1`

When the clock reaches `maxValue`, `TurnController` immediately fires the `completionEffect`:

| CompletionEffect | Behaviour |
|---|---|
| `RemoveSlot` | Deactivates the slot (`SlotState.IsActive = false`). |

`SlotView` has an optional `ClockView` child reference. If wired, it shows the clock; if `null`, no clock UI appears.
```

Then add a row to the Changelog table:

```markdown
| 2026-05-05 | Added slot clocks: SlotClockConfig in SlotDefinition, SlotClockState runtime, ProgressSlotClock effect, RemoveSlot completion, ClockView wiring in SlotView |
```

- [ ] **Step 2: Commit**

```bash
git add docs/game-design-doc.md
git commit -m "docs: add slot clock design to game-design-doc"
```

---

## Self-Review

**Spec coverage:**
- ✅ Clock attached to slot via `SlotDefinition.clock` (`SlotClockConfig`)
- ✅ `RemoveSlot` completion effect
- ✅ `ProgressSlotClock` action controls clock advancement
- ✅ No auto-tick — effect drives the clock
- ✅ `SlotView.clockView` optional reference; hidden when `null` or `ClockState == null`
- ✅ `BoardView.Refresh` pushes updated `SlotClockState` every refresh

**Placeholder scan:** No TBDs, no "add error handling" vagueness, all code blocks complete.

**Type consistency:**
- `SlotClockState.Increment(int)` used in Task 2 (implementation) and Task 4 (TurnController) ✅
- `SlotClockState` passed to `ClockView.Refresh(SlotClockState)` in Task 5 ✅
- `SlotState.ClockState` accessed in tests (Task 3) and BoardView (Task 5) ✅
- `SlotClockCompletionEffect.RemoveSlot` used consistently in Tasks 2, 4 ✅
