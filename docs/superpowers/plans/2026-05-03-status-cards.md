# Status Cards & Progression Reward Deck Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete the level-up reward loop with `ProgressionRewardDeckSet`, then add a Status / Condition Card system — persistent negative effects attached to the player, evaluated each turn, spawned when Threat tier boundaries are crossed.

**Architecture:** `ProgressionRewardDeckSet` is a ScriptableObject with one card list per `StoryEffectResolutionKey`; `ChallengeController.HandleLevelUp` picks a random card and calls `_turn.AddCardsToDeck`. For statuses: `StatusDefinition` (SO) defines one status type; `StatusState` is one runtime instance (remaining turns, expired flag); `StatusCollection` holds the active list and drives evaluate-and-tick. `TurnController` gains a nullable `StatusCollection` parameter and calls `EvaluateAndTick` at the top of `StartTurn`, reusing `TryResolveFixedTargetCardType` for `DamageToThreat`. `ThreatTierDefinition` gains a `penaltyStatus` field; `ChallengeController.HandleThreatTierDepleted` spawns it. `StatusView` MonoBehaviour renders a row of icon + turn-counter widgets.

**Tech Stack:** Unity 6, C#, NUnit (Unity Test Framework), uGUI, existing `SlotEffectAction`, `SlotEffectTargetCardType`, `ThreatState`, `TurnController`.

**Prerequisite:** `game/force-wit-presence` branch; Threat System and Progression already implemented.

---

## File Map

| Action | Path | Responsibility |
|--------|------|----------------|
| CREATE | `Assets/Scripts/Data/ProgressionRewardDeckSet.cs` | SO: four card lists (Force/Wit/Presence/FullTie) + random draw helper |
| CREATE | `Assets/Scripts/Data/StatusDefinition.cs` | SO: name, icon, description, duration type + turns, SlotEffectAction per-turn effect |
| CREATE | `Assets/Scripts/Data/StatusState.cs` | Runtime: one active status instance — remaining turns, expired flag |
| CREATE | `Assets/Scripts/Data/StatusCollection.cs` | Runtime: list of active statuses, evaluate-and-tick, manual remove |
| CREATE | `Assets/Scripts/UI/StatusView.cs` | View: row of icon + counter widgets for each active status |
| CREATE | `Assets/Scripts/Tests/ProgressionRewardDeckSetTests.cs` | Unit tests: card selection per resolution key, empty list, null list |
| CREATE | `Assets/Scripts/Tests/StatusStateTests.cs` | Unit tests: tick/expiry for turn-based and permanent statuses |
| CREATE | `Assets/Scripts/Tests/StatusCollectionTests.cs` | Unit tests: evaluate callback, tick removal, manual remove |
| MODIFY | `Assets/Scripts/Config/GameConfig.cs` | Add `progressionRewardDeckSet` field |
| MODIFY | `Assets/Scripts/Data/ThreatTierDefinition.cs` | Add `penaltyStatus` field |
| MODIFY | `Assets/Scripts/Controllers/TurnController.cs` | Add `StatusCollection` parameter; call `EvaluateStatusEffects` in `StartTurn` |
| MODIFY | `Assets/Scripts/Controllers/ChallengeController.cs` | Wire reward draw in `HandleLevelUp`; wire status add in `HandleThreatTierDepleted`; refresh `StatusView` |
| MODIFY | `Assets/Scripts/Tests/TurnControllerTests.cs` | Update constructor calls; add status DamageToThreat test |
| MODIFY | `docs/game-design-doc.md` | Document reward deck and status system |

---

## Task 1: ProgressionRewardDeckSet ScriptableObject

**Files:**
- Create: `Assets/Scripts/Data/ProgressionRewardDeckSet.cs`
- Create: `Assets/Scripts/Tests/ProgressionRewardDeckSetTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using CardsUnity;
using UnityEngine;

namespace CardsUnity.Tests
{
    public class ProgressionRewardDeckSetTests
    {
        private static CardDefinition MakeCard(CardType type)
        {
            var def = ScriptableObject.CreateInstance<CardDefinition>();
            def.type = type;
            def.value = 1;
            return def;
        }

        [Test]
        public void GetRewardCard_returns_card_from_force_list_when_key_is_Force()
        {
            var deckSet = ScriptableObject.CreateInstance<ProgressionRewardDeckSet>();
            var card = MakeCard(CardType.Force);
            deckSet.forceCards = new List<CardDefinition> { card };

            var result = deckSet.GetRewardCard(StoryEffectResolutionKey.Force, new System.Random(42));

            Assert.AreSame(card, result);
        }

        [Test]
        public void GetRewardCard_returns_card_from_wit_list_when_key_is_Wit()
        {
            var deckSet = ScriptableObject.CreateInstance<ProgressionRewardDeckSet>();
            var card = MakeCard(CardType.Wit);
            deckSet.witCards = new List<CardDefinition> { card };

            var result = deckSet.GetRewardCard(StoryEffectResolutionKey.Wit, new System.Random(42));

            Assert.AreSame(card, result);
        }

        [Test]
        public void GetRewardCard_returns_card_from_presence_list_when_key_is_Presence()
        {
            var deckSet = ScriptableObject.CreateInstance<ProgressionRewardDeckSet>();
            var card = MakeCard(CardType.Presence);
            deckSet.presenceCards = new List<CardDefinition> { card };

            var result = deckSet.GetRewardCard(StoryEffectResolutionKey.Presence, new System.Random(42));

            Assert.AreSame(card, result);
        }

        [Test]
        public void GetRewardCard_returns_card_from_fullTie_list_when_key_is_FullTie()
        {
            var deckSet = ScriptableObject.CreateInstance<ProgressionRewardDeckSet>();
            var card = MakeCard(CardType.Force);
            deckSet.fullTieCards = new List<CardDefinition> { card };

            var result = deckSet.GetRewardCard(StoryEffectResolutionKey.FullTie, new System.Random(42));

            Assert.AreSame(card, result);
        }

        [Test]
        public void GetRewardCard_returns_null_when_list_is_empty()
        {
            var deckSet = ScriptableObject.CreateInstance<ProgressionRewardDeckSet>();
            deckSet.forceCards = new List<CardDefinition>();

            var result = deckSet.GetRewardCard(StoryEffectResolutionKey.Force, new System.Random(42));

            Assert.IsNull(result);
        }

        [Test]
        public void GetRewardCard_returns_null_when_list_is_null()
        {
            var deckSet = ScriptableObject.CreateInstance<ProgressionRewardDeckSet>();
            deckSet.forceCards = null;

            var result = deckSet.GetRewardCard(StoryEffectResolutionKey.Force, new System.Random(42));

            Assert.IsNull(result);
        }
    }
}
```

- [ ] **Step 2: Run tests — expect compile failure**

```bash
/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath . \
  -runTests -testPlatform EditMode \
  -testResults TestResults/EditMode.xml
```

Expected: compile error — `ProgressionRewardDeckSet` does not exist.

- [ ] **Step 3: Implement ProgressionRewardDeckSet**

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace CardsUnity
{
    [CreateAssetMenu(menuName = "CardsUnity/Progression Reward Deck Set", fileName = "ProgressionRewardDeckSet")]
    public class ProgressionRewardDeckSet : ScriptableObject
    {
        public List<CardDefinition> forceCards    = new List<CardDefinition>();
        public List<CardDefinition> witCards      = new List<CardDefinition>();
        public List<CardDefinition> presenceCards = new List<CardDefinition>();
        public List<CardDefinition> fullTieCards  = new List<CardDefinition>();

        public CardDefinition GetRewardCard(StoryEffectResolutionKey key, System.Random rng)
        {
            var list = key switch
            {
                StoryEffectResolutionKey.Force    => forceCards,
                StoryEffectResolutionKey.Wit      => witCards,
                StoryEffectResolutionKey.Presence => presenceCards,
                StoryEffectResolutionKey.FullTie  => fullTieCards,
                _                                 => null
            };

            if (list == null || list.Count == 0)
                return null;

            return list[rng.Next(list.Count)];
        }
    }
}
```

- [ ] **Step 4: Run tests — expect all pass**

```bash
/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath . \
  -runTests -testPlatform EditMode \
  -testResults TestResults/EditMode.xml
```

Expected: all `ProgressionRewardDeckSetTests` pass; no regressions.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Data/ProgressionRewardDeckSet.cs \
        Assets/Scripts/Tests/ProgressionRewardDeckSetTests.cs
git commit -m "feat: add ProgressionRewardDeckSet ScriptableObject"
```

---

## Task 2: Wire reward draw in GameConfig and ChallengeController

**Files:**
- Modify: `Assets/Scripts/Config/GameConfig.cs`
- Modify: `Assets/Scripts/Controllers/ChallengeController.cs`

- [ ] **Step 1: Add field to GameConfig**

Inside `GameConfig.cs`, in the `[Header("Progression")]` block, add after the `progressionXpCap` line:

```csharp
public ProgressionRewardDeckSet progressionRewardDeckSet;
```

The block now reads:

```csharp
[Header("Progression")]
[Min(1)] public int progressionXpCap = 10;
public ProgressionRewardDeckSet progressionRewardDeckSet;
```

- [ ] **Step 2: Replace HandleLevelUp in ChallengeController**

Replace the existing `HandleLevelUp` method:

```csharp
private void HandleLevelUp(StoryEffectResolutionKey dominantType)
{
    if (config.progressionRewardDeckSet != null)
    {
        var rewardCard = config.progressionRewardDeckSet.GetRewardCard(dominantType, _rng);
        if (rewardCard != null)
            _turn.AddCardsToDeck(
                new[] { rewardCard },
                StoryEffectDeckTarget.Player,
                StoryEffectDeckPlacement.DiscardPile);
    }

    Debug.Log($"Level up! Dominant type: {dominantType}, Tier: {_progression.CurrentTier}");
    RefreshUI();
}
```

- [ ] **Step 3: Compile check in Unity — expect no errors**

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Config/GameConfig.cs \
        Assets/Scripts/Controllers/ChallengeController.cs
git commit -m "feat: wire progression reward deck draw on level-up"
```

---

## Task 3: StatusDefinition ScriptableObject

**Files:**
- Create: `Assets/Scripts/Data/StatusDefinition.cs`

- [ ] **Step 1: Create StatusDefinition**

```csharp
using UnityEngine;

namespace CardsUnity
{
    public enum StatusDurationType { Permanent, Turns }

    [CreateAssetMenu(menuName = "CardsUnity/Status Definition", fileName = "NewStatus")]
    public class StatusDefinition : ScriptableObject
    {
        public string statusName;
        public Sprite icon;
        [TextArea] public string description;

        [Header("Duration")]
        public StatusDurationType durationType = StatusDurationType.Turns;
        [Min(1)] public int durationTurns = 3;

        [Header("Per-Turn Effect")]
        public SlotEffectAction action;
        public SlotEffectTargetCardType targetCardType = SlotEffectTargetCardType.Force;
        [Min(0)] public int actionValue;
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Assets/Scripts/Data/StatusDefinition.cs
git commit -m "feat: add StatusDefinition ScriptableObject"
```

---

## Task 4: StatusState runtime + tests

**Files:**
- Create: `Assets/Scripts/Data/StatusState.cs`
- Create: `Assets/Scripts/Tests/StatusStateTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
using NUnit.Framework;
using CardsUnity;
using UnityEngine;

namespace CardsUnity.Tests
{
    public class StatusStateTests
    {
        private static StatusDefinition MakeDef(StatusDurationType duration, int turns = 3)
        {
            var def = ScriptableObject.CreateInstance<StatusDefinition>();
            def.durationType = duration;
            def.durationTurns = turns;
            return def;
        }

        [Test]
        public void New_turn_based_status_has_correct_remaining_turns()
        {
            var state = new StatusState(MakeDef(StatusDurationType.Turns, 3));
            Assert.AreEqual(3, state.RemainingTurns);
        }

        [Test]
        public void New_status_is_not_expired()
        {
            var state = new StatusState(MakeDef(StatusDurationType.Turns, 2));
            Assert.IsFalse(state.IsExpired);
        }

        [Test]
        public void TickTurn_decrements_remaining_turns_by_one()
        {
            var state = new StatusState(MakeDef(StatusDurationType.Turns, 3));
            state.TickTurn();
            Assert.AreEqual(2, state.RemainingTurns);
        }

        [Test]
        public void TickTurn_sets_IsExpired_when_remaining_turns_reach_zero()
        {
            var state = new StatusState(MakeDef(StatusDurationType.Turns, 1));
            state.TickTurn();
            Assert.IsTrue(state.IsExpired);
        }

        [Test]
        public void TickTurn_on_permanent_status_does_not_expire_it()
        {
            var state = new StatusState(MakeDef(StatusDurationType.Permanent));
            state.TickTurn();
            Assert.IsFalse(state.IsExpired);
        }

        [Test]
        public void Remove_sets_IsExpired_immediately()
        {
            var state = new StatusState(MakeDef(StatusDurationType.Turns, 10));
            state.Remove();
            Assert.IsTrue(state.IsExpired);
        }

        [Test]
        public void Definition_is_accessible_on_state()
        {
            var def = MakeDef(StatusDurationType.Permanent);
            var state = new StatusState(def);
            Assert.AreSame(def, state.Definition);
        }
    }
}
```

- [ ] **Step 2: Run tests — expect compile failure**

```bash
/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath . \
  -runTests -testPlatform EditMode \
  -testResults TestResults/EditMode.xml
```

Expected: compile error — `StatusState` does not exist.

- [ ] **Step 3: Implement StatusState**

```csharp
namespace CardsUnity
{
    public class StatusState
    {
        public StatusDefinition Definition  { get; }
        public int              RemainingTurns { get; private set; }
        public bool             IsExpired   { get; private set; }

        public StatusState(StatusDefinition definition)
        {
            Definition     = definition;
            RemainingTurns = definition.durationTurns;
        }

        public void TickTurn()
        {
            if (Definition.durationType == StatusDurationType.Permanent)
                return;

            RemainingTurns--;
            if (RemainingTurns <= 0)
                IsExpired = true;
        }

        public void Remove() => IsExpired = true;
    }
}
```

- [ ] **Step 4: Run tests — expect all pass**

```bash
/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath . \
  -runTests -testPlatform EditMode \
  -testResults TestResults/EditMode.xml
```

Expected: all `StatusStateTests` pass; no regressions.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Data/StatusState.cs \
        Assets/Scripts/Tests/StatusStateTests.cs
git commit -m "feat: add StatusState runtime with tick and expiry logic"
```

---

## Task 5: StatusCollection runtime + tests

**Files:**
- Create: `Assets/Scripts/Data/StatusCollection.cs`
- Create: `Assets/Scripts/Tests/StatusCollectionTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
using System;
using System.Collections.Generic;
using NUnit.Framework;
using CardsUnity;
using UnityEngine;

namespace CardsUnity.Tests
{
    public class StatusCollectionTests
    {
        private static StatusDefinition MakeDef(
            StatusDurationType duration,
            int turns = 3,
            SlotEffectAction action = SlotEffectAction.AddToClock,
            int value = 1)
        {
            var def = ScriptableObject.CreateInstance<StatusDefinition>();
            def.durationType  = duration;
            def.durationTurns = turns;
            def.action        = action;
            def.actionValue   = value;
            return def;
        }

        [Test]
        public void Add_increases_statuses_count()
        {
            var col = new StatusCollection();
            col.Add(MakeDef(StatusDurationType.Turns));
            Assert.AreEqual(1, col.Statuses.Count);
        }

        [Test]
        public void EvaluateAndTick_calls_callback_once_per_active_status()
        {
            var col = new StatusCollection();
            col.Add(MakeDef(StatusDurationType.Turns, 2));
            col.Add(MakeDef(StatusDurationType.Turns, 2));

            int callCount = 0;
            col.EvaluateAndTick(_ => callCount++);

            Assert.AreEqual(2, callCount);
        }

        [Test]
        public void EvaluateAndTick_does_not_call_callback_for_already_expired_status()
        {
            var col = new StatusCollection();
            col.Add(MakeDef(StatusDurationType.Turns, 1));
            col.Statuses[0].Remove();

            int callCount = 0;
            col.EvaluateAndTick(_ => callCount++);

            Assert.AreEqual(0, callCount);
        }

        [Test]
        public void EvaluateAndTick_removes_status_with_one_turn_remaining_after_tick()
        {
            var col = new StatusCollection();
            col.Add(MakeDef(StatusDurationType.Turns, 1));

            col.EvaluateAndTick(_ => { });

            Assert.AreEqual(0, col.Statuses.Count);
        }

        [Test]
        public void EvaluateAndTick_keeps_status_with_multiple_turns_remaining()
        {
            var col = new StatusCollection();
            col.Add(MakeDef(StatusDurationType.Turns, 3));

            col.EvaluateAndTick(_ => { });

            Assert.AreEqual(1, col.Statuses.Count);
        }

        [Test]
        public void EvaluateAndTick_does_not_remove_permanent_status()
        {
            var col = new StatusCollection();
            col.Add(MakeDef(StatusDurationType.Permanent));

            col.EvaluateAndTick(_ => { });
            col.EvaluateAndTick(_ => { });

            Assert.AreEqual(1, col.Statuses.Count);
        }

        [Test]
        public void Remove_immediately_excludes_status_from_subsequent_evaluation()
        {
            var col = new StatusCollection();
            col.Add(MakeDef(StatusDurationType.Turns, 5));
            var status = col.Statuses[0];
            col.Remove(status);

            int callCount = 0;
            col.EvaluateAndTick(_ => callCount++);

            Assert.AreEqual(0, callCount);
        }
    }
}
```

- [ ] **Step 2: Run tests — expect compile failure**

```bash
/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath . \
  -runTests -testPlatform EditMode \
  -testResults TestResults/EditMode.xml
```

Expected: compile error — `StatusCollection` does not exist.

- [ ] **Step 3: Implement StatusCollection**

```csharp
using System;
using System.Collections.Generic;

namespace CardsUnity
{
    public class StatusCollection
    {
        private readonly List<StatusState> _statuses = new List<StatusState>();

        public IReadOnlyList<StatusState> Statuses => _statuses;

        public void Add(StatusDefinition definition)
        {
            _statuses.Add(new StatusState(definition));
        }

        public void Remove(StatusState status)
        {
            status.Remove();
        }

        public void EvaluateAndTick(Action<StatusState> evaluate)
        {
            var snapshot = new List<StatusState>(_statuses);

            foreach (var status in snapshot)
            {
                if (!status.IsExpired)
                    evaluate(status);
            }

            foreach (var status in snapshot)
            {
                if (!status.IsExpired)
                    status.TickTurn();
            }

            _statuses.RemoveAll(s => s.IsExpired);
        }
    }
}
```

- [ ] **Step 4: Run tests — expect all pass**

```bash
/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath . \
  -runTests -testPlatform EditMode \
  -testResults TestResults/EditMode.xml
```

Expected: all `StatusCollectionTests` pass; no regressions.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Data/StatusCollection.cs \
        Assets/Scripts/Tests/StatusCollectionTests.cs
git commit -m "feat: add StatusCollection with EvaluateAndTick and expiry removal"
```

---

## Task 6: ThreatTierDefinition — add penaltyStatus field

**Files:**
- Modify: `Assets/Scripts/Data/ThreatTierDefinition.cs`

- [ ] **Step 1: Add field**

Replace the entire file:

```csharp
using UnityEngine;

namespace CardsUnity
{
    [CreateAssetMenu(menuName = "CardsUnity/Threat Tier Definition", fileName = "NewThreatTier")]
    public class ThreatTierDefinition : ScriptableObject
    {
        [Min(1)] public int maxValue = 5;

        [Tooltip("Slot spawned on the board when this tier is depleted. Leave null for the final tier (game over instead).")]
        public SlotDefinition penaltySlot;

        [Tooltip("Status attached to the player when this tier is depleted. Can combine with penaltySlot.")]
        public StatusDefinition penaltyStatus;
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Assets/Scripts/Data/ThreatTierDefinition.cs
git commit -m "feat: add penaltyStatus field to ThreatTierDefinition"
```

---

## Task 7: TurnController — add StatusCollection, evaluate statuses in StartTurn

**Files:**
- Modify: `Assets/Scripts/Controllers/TurnController.cs`

- [ ] **Step 1: Add `_statusCollection` field**

In the private fields block, add after `_playedCardCounters`:

```csharp
private readonly StatusCollection _statusCollection;
```

- [ ] **Step 2: Update constructor signature and body**

Replace the constructor signature and its assignment block with:

```csharp
public TurnController(
    DeckState playerDeck,
    HandState playerHand,
    DeckState opponentDeck,
    BoardState board,
    ThreatState threatState,
    ClockState opponentClock,
    ProgressionState progression,
    PlayedCardCounterState playedCardCounters,
    StatusCollection statusCollection,
    int draftValue,
    System.Random rng)
{
    _playerDeck          = playerDeck;
    _playerHand          = playerHand;
    _opponentDeck        = opponentDeck;
    _board               = board;
    _opponentClock       = opponentClock;
    _threatState         = threatState;
    _progression         = progression;
    _playedCardCounters  = playedCardCounters;
    _statusCollection    = statusCollection;
    _draftValue          = draftValue;
    _rng = rng ?? throw new ArgumentNullException(nameof(rng));
}
```

- [ ] **Step 3: Call EvaluateStatusEffects inside StartTurn**

In `StartTurn()`, add `EvaluateStatusEffects();` after the draw loop and before `EvaluateSlotEffects`:

```csharp
public void StartTurn()
{
    int cardsToDraw = Math.Max(0, _draftValue - _playerHand.Cards.Count);

    for (int i = 0; i < cardsToDraw; i++)
    {
        if (_playerDeck.DrawCount == 0 && _playerDeck.CanDraw)
            _playerDeck.ShuffleDiscardIntoDrawPile(_rng);

        if (!_playerDeck.CanDraw)
            break;

        var card = _playerDeck.Draw();
        if (card != null)
            _playerHand.Add(new CardInstance(card));
    }

    EvaluateStatusEffects();
    EvaluateSlotEffects(SlotEffectTrigger.OnPlayerTurnStart);
}
```

- [ ] **Step 4: Add EvaluateStatusEffects and ApplyStatusEffect private methods**

Add these two methods after `RegisterPlayedCard`:

```csharp
private void EvaluateStatusEffects()
{
    _statusCollection?.EvaluateAndTick(ApplyStatusEffect);
}

private void ApplyStatusEffect(StatusState status)
{
    var def = status.Definition;

    switch (def.action)
    {
        case SlotEffectAction.DamageToThreat:
            if (TryResolveFixedTargetCardType(def.targetCardType, out CardType threatType))
                _threatState?.GetBar(threatType)?.TakeDamage(def.actionValue);
            break;

        case SlotEffectAction.AddToClock:
            _opponentClock?.Increment(def.actionValue);
            break;
    }
}
```

- [ ] **Step 5: Compile check — expect errors in TurnControllerTests and ChallengeController**

Open Unity. Both files still use the old constructor without `StatusCollection`. Expected: compile errors there.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Controllers/TurnController.cs
git commit -m "feat: add StatusCollection to TurnController; evaluate statuses at start of each turn"
```

---

## Task 8: Update TurnControllerTests

**Files:**
- Modify: `Assets/Scripts/Tests/TurnControllerTests.cs`

- [ ] **Step 1: Add `_statusCollection` field**

In the class-level fields block, add:

```csharp
private StatusCollection _statusCollection;
```

- [ ] **Step 2: Initialize in SetUp**

In `SetUp()`, after `_playedCardCounters = new PlayedCardCounterState();`, add:

```csharp
_statusCollection = new StatusCollection();
```

Update the `TurnController` constructor call in `SetUp()`:

```csharp
_turn = new TurnController(
    _playerDeck, _playerHand,
    _opponentDeck, _board,
    _threatState, _opponentClock, _progression, _playedCardCounters,
    _statusCollection,
    draftValue: 3,
    rng: new System.Random(42));
```

Update `MakeTurnWithBoard()`:

```csharp
private TurnController MakeTurnWithBoard(BoardState board)
{
    return new TurnController(
        _playerDeck, _playerHand,
        _opponentDeck, board,
        _threatState, _opponentClock, _progression, _playedCardCounters,
        _statusCollection,
        draftValue: 3,
        rng: new System.Random(42));
}
```

- [ ] **Step 3: Add a test for status DamageToThreat**

Add this test in the `// ── slot effect tests` section:

```csharp
[Test]
public void StartTurn_active_DamageToThreat_status_drains_matching_threat_bar()
{
    var def = ScriptableObject.CreateInstance<StatusDefinition>();
    def.durationType   = StatusDurationType.Turns;
    def.durationTurns  = 2;
    def.action         = SlotEffectAction.DamageToThreat;
    def.targetCardType = SlotEffectTargetCardType.Force;
    def.actionValue    = 3;

    _statusCollection.Add(def);
    _turn.StartTurn();

    var bar = _threatState.GetBar(CardType.Force);
    Assert.AreEqual(7, bar.Segments[0].CurrentValue); // started at 10, drained 3
}
```

- [ ] **Step 4: Run all tests — expect all pass**

```bash
/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath . \
  -runTests -testPlatform EditMode \
  -testResults TestResults/EditMode.xml
```

Expected: all tests pass including existing suites.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Tests/TurnControllerTests.cs
git commit -m "test: update TurnControllerTests for StatusCollection constructor; add status drain test"
```

---

## Task 9: ChallengeController — wire StatusCollection

**Files:**
- Modify: `Assets/Scripts/Controllers/ChallengeController.cs`

- [ ] **Step 1: Add `_statusCollection` private field and `statusView` serialized field**

In the private fields block, add after `_playedCardCounters`:

```csharp
private StatusCollection _statusCollection;
```

In the serialized fields block, add after `progressionView`:

```csharp
[SerializeField] private StatusView statusView;
```

- [ ] **Step 2: Create StatusCollection and pass to TurnController in Start()**

In `Start()`, after `_playedCardCounters = new PlayedCardCounterState();`:

```csharp
_statusCollection = new StatusCollection();
```

Update the `TurnController` constructor call:

```csharp
_turn = new TurnController(
    playerDeck,
    _hand,
    opponentDeck,
    _board,
    _threatState,
    _opponentClock,
    _progression,
    _playedCardCounters,
    _statusCollection,
    config.draftValue,
    _rng);
```

- [ ] **Step 3: Update HandleThreatTierDepleted to spawn status**

Replace the existing `HandleThreatTierDepleted` method:

```csharp
private void HandleThreatTierDepleted(CardType type, int tierIndex)
{
    var tiers = config.GetThreatTiers(type);
    if (tiers == null || tierIndex < 0 || tierIndex >= tiers.Count)
        return;

    var tierDef = tiers[tierIndex];
    if (tierDef == null)
        return;

    if (tierDef.penaltySlot != null)
        AddRuntimeStorySlot(tierDef.penaltySlot);

    if (tierDef.penaltyStatus != null)
        _statusCollection.Add(tierDef.penaltyStatus);

    RefreshUI();
}
```

- [ ] **Step 4: Add statusView Refresh call to RefreshUI**

In `RefreshUI()`, at the end:

```csharp
statusView?.Refresh(_statusCollection);
```

- [ ] **Step 5: Compile check in Unity — expect no errors**

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Controllers/ChallengeController.cs
git commit -m "feat: wire StatusCollection into ChallengeController; spawn status on threat tier depletion"
```

---

## Task 10: StatusView UI widget

**Files:**
- Create: `Assets/Scripts/UI/StatusView.cs`

- [ ] **Step 1: Create StatusView**

```csharp
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardsUnity.UI
{
    public class StatusView : MonoBehaviour
    {
        [SerializeField] private Transform iconContainer;
        [SerializeField] private GameObject statusIconPrefab; // needs: Image child for icon + TextMeshProUGUI child for counter

        private readonly List<GameObject> _activeIcons = new List<GameObject>();

        public void Refresh(StatusCollection collection)
        {
            foreach (var icon in _activeIcons)
                Destroy(icon);
            _activeIcons.Clear();

            if (collection == null)
                return;

            foreach (var status in collection.Statuses)
            {
                if (status.IsExpired)
                    continue;

                var go = Instantiate(statusIconPrefab, iconContainer);
                _activeIcons.Add(go);

                var img = go.GetComponentInChildren<Image>();
                if (img != null)
                    img.sprite = status.Definition.icon;

                var label = go.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                    label.text = status.Definition.durationType == StatusDurationType.Permanent
                        ? "∞"
                        : status.RemainingTurns.ToString();
            }
        }
    }
}
```

- [ ] **Step 2: Create the status icon prefab in Unity Editor**

In the Unity Editor:
1. Create a new UI Panel child — name it `StatusIconPrefab`.
2. Add an `Image` component (the status icon sprite will be set here).
3. Add a `TextMeshProUGUI` child for the turn counter (positioned bottom-right of the icon).
4. Save as `Assets/Prefabs/UI/StatusIcon.prefab`.

- [ ] **Step 3: Add StatusView to the scene**

In the Unity Editor:
1. Create a horizontal container in the HUD canvas — name it `StatusArea`.
2. Add `StatusView` MonoBehaviour to it.
3. Assign `iconContainer` to the container's own Transform.
4. Assign `statusIconPrefab` to the prefab from Step 2.
5. Assign the `StatusArea` object to `ChallengeController.statusView` in the Inspector.

- [ ] **Step 4: Run all tests — expect no regressions**

```bash
/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath . \
  -runTests -testPlatform EditMode \
  -testResults TestResults/EditMode.xml
```

Expected: all tests pass.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/UI/StatusView.cs
git commit -m "feat: add StatusView rendering active status icons and turn counters"
```

---

## Task 11: Update game-design-doc.md

**Files:**
- Modify: `docs/game-design-doc.md`

- [ ] **Step 1: Update relevant sections**

Open `docs/game-design-doc.md` and:

1. **Add a Progression Reward Deck section** — `ProgressionRewardDeckSet` holds four `CardDefinition` lists keyed by dominant type (Force / Wit / Presence / FullTie). On level-up a random card from the matching list is drawn and added permanently to the player's discard pile. Assigned via `GameConfig.progressionRewardDeckSet`.

2. **Add a Status / Condition Cards section** — `StatusDefinition` defines a status: name, icon, description, duration type (Permanent or N turns), and one per-turn effect (`SlotEffectAction` + target type + value). `StatusCollection` holds active instances. Statuses are evaluated at the start of every player turn before slot effects, then ticked (decrement remaining turns; expired ones removed). Statuses are spawned when Threat tier boundaries are crossed (`ThreatTierDefinition.penaltyStatus`). `StatusView` renders the active status icons.

3. **Append two rows to the Changelog table:**

```
| 2026-05-03 | Added ProgressionRewardDeckSet: level-up draws a card from the dominant-type reward list |
| 2026-05-03 | Added Status / Condition Card system: per-turn effects, turn-based expiry, Threat tier integration |
```

- [ ] **Step 2: Commit**

```bash
git add docs/game-design-doc.md
git commit -m "docs: update game-design-doc with reward deck and status card system"
```

---

## Self-Review Checklist

**Spec coverage:**
- ✅ `ProgressionRewardDeckSet` SO with one deck per resolution key — Task 1
- ✅ Level-up draws card and adds to player deck permanently — Task 2 (`HandleLevelUp` → `AddCardsToDeck`)
- ✅ `StatusDefinition` SO — name, icon, description, duration, per-turn effect — Task 3
- ✅ `StatusState` runtime — remaining turns, expired flag, tick, remove — Task 4
- ✅ Statuses evaluated each turn (similar to passive slot effects) — Task 7 (`EvaluateStatusEffects` in `StartTurn`)
- ✅ Statuses removed after N turns — Task 5 (`EvaluateAndTick` removes after tick)
- ✅ Statuses can be removed manually — Task 5 (`StatusCollection.Remove`)
- ✅ Threat tier crossing spawns a status — Task 6 (`penaltyStatus`) + Task 9 (`HandleThreatTierDepleted`)
- ✅ UI: status icons with remaining duration — Task 10
- ✅ `game-design-doc.md` updated — Task 11

**Type consistency:**
- `StatusCollection` created in `ChallengeController`, passed to `TurnController` constructor ✅
- Constructor order: `...playedCardCounters, statusCollection, draftValue, rng` — consistent across TurnController, ChallengeController, and TurnControllerTests ✅
- `StatusDefinition.targetCardType` is `SlotEffectTargetCardType` — same enum `TryResolveFixedTargetCardType` already handles ✅
- `StatusView.Refresh(StatusCollection)` — matches what `ChallengeController.RefreshUI` passes ✅
- `ProgressionRewardDeckSet.GetRewardCard(StoryEffectResolutionKey, System.Random)` — return type `CardDefinition` — matches `AddCardsToDeck` array parameter ✅
