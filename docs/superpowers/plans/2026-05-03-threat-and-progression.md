# Threat System & Player Progression Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the Player Clock with a segmented HP-style Threat System (three draining bars — Force/Wit/Presence — with tier penalties and game over at 0), and add a single XP bar that fills from defeated opponent card values and resolves a level-up reward by dominant defeated type.

**Architecture:** `ThreatBarState` holds an ordered list of `ThreatTierState` segments that drain right-to-left; events fire on tier crossings and on reaching zero. `ProgressionState` accumulates defeated-card values and reuses `PlayedCardCounterDominanceResolver` at level-up. Both are owned by `ChallengeController`, passed into `TurnController`, and rendered by new `ThreatBarView` / `ProgressionView` MonoBehaviours.

**Tech Stack:** Unity 6, C#, NUnit (Unity Test Framework), uGUI / MMProgressBar (MoreMountains Feel), existing `PlayedCardCounterDominanceResolver`, `ClockState` patterns.

---

## File Map

| Action | Path | Responsibility |
|--------|------|----------------|
| CREATE | `Assets/Scripts/Data/ThreatTierDefinition.cs` | SO: one bar segment — max HP and penalty slot |
| CREATE | `Assets/Scripts/Data/ThreatBarState.cs` | Runtime: one typed HP bar, segments + drain logic + events |
| CREATE | `Assets/Scripts/Data/ThreatState.cs` | Runtime: three `ThreatBarState`s, loss check, event aggregation |
| CREATE | `Assets/Scripts/Data/ProgressionState.cs` | Runtime: XP bar, per-type defeated counters, level-up logic |
| CREATE | `Assets/Scripts/UI/ThreatBarView.cs` | View: renders one segmented HP bar |
| CREATE | `Assets/Scripts/UI/ProgressionView.cs` | View: renders XP bar + pip counts |
| CREATE | `Assets/Scripts/Tests/ThreatBarStateTests.cs` | Unit tests for ThreatBarState drain & events |
| CREATE | `Assets/Scripts/Tests/ProgressionStateTests.cs` | Unit tests for ProgressionState XP & level-up |
| MODIFY | `Assets/Scripts/Data/SlotEffectAction.cs` | Add `DamageToThreat` |
| MODIFY | `Assets/Scripts/Data/ClockTarget.cs` | Remove `PlayerClock` |
| MODIFY | `Assets/Scripts/Config/GameConfig.cs` | Add threat tier SO lists + XP cap |
| MODIFY | `Assets/Scripts/Controllers/TurnController.cs` | Replace `_playerClock` with `ThreatState`; add progression hook |
| MODIFY | `Assets/Scripts/Controllers/ChallengeController.cs` | Wire threat & progression; update UI refresh |
| MODIFY | `Assets/Scripts/Tests/TurnControllerTests.cs` | Replace player clock setup with ThreatState |

---

## Task 1: ThreatTierDefinition ScriptableObject

**Files:**
- Create: `Assets/Scripts/Data/ThreatTierDefinition.cs`

- [ ] **Step 1: Create the script**

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
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Assets/Scripts/Data/ThreatTierDefinition.cs
git commit -m "feat: add ThreatTierDefinition ScriptableObject"
```

---

## Task 2: ThreatBarState — drain logic and events

**Files:**
- Create: `Assets/Scripts/Data/ThreatBarState.cs`
- Create: `Assets/Scripts/Tests/ThreatBarStateTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using CardsUnity;
using UnityEngine;

namespace CardsUnity.Tests
{
    public class ThreatBarStateTests
    {
        private static ThreatTierDefinition MakeTier(int maxValue)
        {
            var t = ScriptableObject.CreateInstance<ThreatTierDefinition>();
            t.maxValue = maxValue;
            return t;
        }

        private static ThreatBarState MakeBar(params int[] tierMaxValues)
        {
            var defs = new List<ThreatTierDefinition>();
            foreach (var v in tierMaxValues)
                defs.Add(MakeTier(v));
            return new ThreatBarState(defs);
        }

        [Test]
        public void New_bar_starts_fully_filled()
        {
            var bar = MakeBar(5, 3);
            Assert.AreEqual(5, bar.Segments[0].CurrentValue);
            Assert.AreEqual(3, bar.Segments[1].CurrentValue);
        }

        [Test]
        public void TakeDamage_drains_outermost_segment_first()
        {
            var bar = MakeBar(10, 5);
            bar.TakeDamage(4);
            Assert.AreEqual(6, bar.Segments[0].CurrentValue);
            Assert.AreEqual(5, bar.Segments[1].CurrentValue);
        }

        [Test]
        public void TakeDamage_overflow_cascades_into_next_segment()
        {
            var bar = MakeBar(3, 10);
            bar.TakeDamage(5); // 3 drains first segment, 2 spills into second
            Assert.AreEqual(0, bar.Segments[0].CurrentValue);
            Assert.AreEqual(8, bar.Segments[1].CurrentValue);
        }

        [Test]
        public void TakeDamage_fires_OnTierDepleted_when_segment_reaches_zero()
        {
            var bar = MakeBar(3, 10);
            int depletedIndex = -1;
            bar.OnTierDepleted += i => depletedIndex = i;
            bar.TakeDamage(3);
            Assert.AreEqual(0, depletedIndex);
        }

        [Test]
        public void TakeDamage_does_not_fire_OnTierDepleted_for_partial_drain()
        {
            var bar = MakeBar(5, 10);
            int callCount = 0;
            bar.OnTierDepleted += _ => callCount++;
            bar.TakeDamage(2);
            Assert.AreEqual(0, callCount);
        }

        [Test]
        public void TakeDamage_fires_OnReachedZero_when_all_segments_empty()
        {
            var bar = MakeBar(2, 3);
            bool fired = false;
            bar.OnReachedZero += () => fired = true;
            bar.TakeDamage(5);
            Assert.IsTrue(fired);
        }

        [Test]
        public void TakeDamage_does_not_fire_OnReachedZero_when_segments_remain()
        {
            var bar = MakeBar(2, 3);
            bool fired = false;
            bar.OnReachedZero += () => fired = true;
            bar.TakeDamage(2);
            Assert.IsFalse(fired);
        }

        [Test]
        public void IsAtZero_true_when_all_segments_depleted()
        {
            var bar = MakeBar(2, 3);
            bar.TakeDamage(5);
            Assert.IsTrue(bar.IsAtZero);
        }

        [Test]
        public void IsAtZero_false_when_segments_remain()
        {
            var bar = MakeBar(2, 3);
            bar.TakeDamage(1);
            Assert.IsFalse(bar.IsAtZero);
        }

        [Test]
        public void TakeDamage_excess_beyond_all_segments_is_clamped_at_zero()
        {
            var bar = MakeBar(3);
            bar.TakeDamage(999);
            Assert.AreEqual(0, bar.Segments[0].CurrentValue);
        }

        [Test]
        public void TakeDamage_fires_OnTierDepleted_once_per_crossed_segment()
        {
            var bar = MakeBar(2, 3, 5);
            int callCount = 0;
            bar.OnTierDepleted += _ => callCount++;
            bar.TakeDamage(5); // depletes seg0 (2) and seg1 (3), seg2 has 5 remaining
            Assert.AreEqual(2, callCount);
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

Expected: compile error — `ThreatBarState` does not exist.

- [ ] **Step 3: Implement ThreatBarState**

```csharp
using System;
using System.Collections.Generic;

namespace CardsUnity
{
    public class ThreatTierState
    {
        public int MaxValue { get; }
        public int CurrentValue { get; private set; }
        public float Progress => MaxValue > 0 ? (float)CurrentValue / MaxValue : 0f;

        public ThreatTierState(int maxValue)
        {
            MaxValue = maxValue;
            CurrentValue = maxValue;
        }

        // Returns the overflow that could not be absorbed.
        public int Drain(int amount)
        {
            int taken = Math.Min(amount, CurrentValue);
            CurrentValue -= taken;
            return amount - taken;
        }
    }

    public class ThreatBarState
    {
        private readonly List<ThreatTierState> _segments;

        public event Action<int> OnTierDepleted;
        public event Action OnReachedZero;

        public IReadOnlyList<ThreatTierState> Segments => _segments;
        public bool IsAtZero => _segments.TrueForAll(s => s.CurrentValue == 0);

        public ThreatBarState(IReadOnlyList<ThreatTierDefinition> tierDefs)
        {
            _segments = new List<ThreatTierState>(tierDefs.Count);
            foreach (var def in tierDefs)
                _segments.Add(new ThreatTierState(def.maxValue));
        }

        public void TakeDamage(int amount)
        {
            if (amount <= 0)
                return;

            int remaining = amount;
            for (int i = 0; i < _segments.Count && remaining > 0; i++)
            {
                var seg = _segments[i];
                if (seg.CurrentValue == 0)
                    continue;

                remaining = seg.Drain(remaining);

                if (seg.CurrentValue == 0)
                    OnTierDepleted?.Invoke(i);
            }

            if (IsAtZero)
                OnReachedZero?.Invoke();
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

Expected: all `ThreatBarStateTests` pass; no regressions.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Data/ThreatBarState.cs Assets/Scripts/Tests/ThreatBarStateTests.cs
git commit -m "feat: add ThreatBarState with segmented drain logic and events"
```

---

## Task 3: ThreatState — three bars, event aggregation

**Files:**
- Create: `Assets/Scripts/Data/ThreatState.cs`

- [ ] **Step 1: Implement ThreatState**

```csharp
using System;
using System.Collections.Generic;

namespace CardsUnity
{
    public class ThreatState
    {
        private readonly ThreatBarState _force;
        private readonly ThreatBarState _wit;
        private readonly ThreatBarState _presence;

        // cardType = which bar; tierIndex = which segment was depleted
        public event Action<CardType, int> OnTierDepleted;
        public event Action<CardType> OnBarAtZero;

        public bool AnyAtZero => _force.IsAtZero || _wit.IsAtZero || _presence.IsAtZero;

        public ThreatState(
            IReadOnlyList<ThreatTierDefinition> forceTiers,
            IReadOnlyList<ThreatTierDefinition> witTiers,
            IReadOnlyList<ThreatTierDefinition> presenceTiers)
        {
            _force    = new ThreatBarState(forceTiers);
            _wit      = new ThreatBarState(witTiers);
            _presence = new ThreatBarState(presenceTiers);

            Wire(_force,    CardType.Force);
            Wire(_wit,      CardType.Wit);
            Wire(_presence, CardType.Presence);
        }

        public ThreatBarState GetBar(CardType type) => type switch
        {
            CardType.Force    => _force,
            CardType.Wit      => _wit,
            CardType.Presence => _presence,
            _                 => null
        };

        private void Wire(ThreatBarState bar, CardType type)
        {
            bar.OnTierDepleted += i => OnTierDepleted?.Invoke(type, i);
            bar.OnReachedZero  += () => OnBarAtZero?.Invoke(type);
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Assets/Scripts/Data/ThreatState.cs
git commit -m "feat: add ThreatState aggregating three typed threat bars"
```

---

## Task 4: ProgressionState — XP bar and level-up

**Files:**
- Create: `Assets/Scripts/Data/ProgressionState.cs`
- Create: `Assets/Scripts/Tests/ProgressionStateTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using NUnit.Framework;
using CardsUnity;

namespace CardsUnity.Tests
{
    public class ProgressionStateTests
    {
        [Test]
        public void AddDefeatedCard_increases_CurrentXp_by_card_value()
        {
            var p = new ProgressionState(xpCap: 10);
            p.AddDefeatedCard(CardType.Force, 4);
            Assert.AreEqual(4, p.CurrentXp);
        }

        [Test]
        public void AddDefeatedCard_tracks_per_type_totals()
        {
            var p = new ProgressionState(xpCap: 100);
            p.AddDefeatedCard(CardType.Force, 3);
            p.AddDefeatedCard(CardType.Wit, 5);
            Assert.AreEqual(3, p.DefeatedCounters.Force);
            Assert.AreEqual(5, p.DefeatedCounters.Wit);
            Assert.AreEqual(0, p.DefeatedCounters.Presence);
        }

        [Test]
        public void AddDefeatedCard_fires_OnLevelUp_when_xp_reaches_cap()
        {
            var p = new ProgressionState(xpCap: 5);
            StoryEffectResolutionKey? result = null;
            p.OnLevelUp += key => result = key;
            p.AddDefeatedCard(CardType.Force, 3);
            p.AddDefeatedCard(CardType.Force, 2); // total = 5 = cap
            Assert.IsNotNull(result);
        }

        [Test]
        public void AddDefeatedCard_resolves_dominant_type_at_level_up()
        {
            var p = new ProgressionState(xpCap: 5);
            StoryEffectResolutionKey? result = null;
            p.OnLevelUp += key => result = key;
            p.AddDefeatedCard(CardType.Force, 3);
            p.AddDefeatedCard(CardType.Wit, 1);
            p.AddDefeatedCard(CardType.Force, 1); // Force=4 > Wit=1 → Force wins
            Assert.AreEqual(StoryEffectResolutionKey.Force, result);
        }

        [Test]
        public void AddDefeatedCard_resets_xp_and_counters_after_level_up()
        {
            var p = new ProgressionState(xpCap: 5);
            p.AddDefeatedCard(CardType.Force, 5);
            Assert.AreEqual(0, p.CurrentXp);
            Assert.AreEqual(0, p.DefeatedCounters.Force);
        }

        [Test]
        public void AddDefeatedCard_carries_overflow_into_next_tier()
        {
            var p = new ProgressionState(xpCap: 5);
            p.AddDefeatedCard(CardType.Force, 7); // 5 fills bar, 2 overflow
            Assert.AreEqual(2, p.CurrentXp);
        }

        [Test]
        public void AddDefeatedCard_increments_tier_after_level_up()
        {
            var p = new ProgressionState(xpCap: 5);
            p.AddDefeatedCard(CardType.Force, 5);
            Assert.AreEqual(1, p.CurrentTier);
        }

        [Test]
        public void Progress_is_fraction_of_CurrentXp_over_cap()
        {
            var p = new ProgressionState(xpCap: 10);
            p.AddDefeatedCard(CardType.Force, 4);
            Assert.AreEqual(0.4f, p.Progress, 0.001f);
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

Expected: compile error — `ProgressionState` does not exist.

- [ ] **Step 3: Implement ProgressionState**

```csharp
using System;

namespace CardsUnity
{
    public class ProgressionState
    {
        public int CurrentXp { get; private set; }
        public int XpCap { get; }
        public int CurrentTier { get; private set; }
        public float Progress => XpCap > 0 ? (float)CurrentXp / XpCap : 0f;

        public PlayedCardCounterState DefeatedCounters { get; } = new PlayedCardCounterState();

        public event Action<StoryEffectResolutionKey> OnLevelUp;

        public ProgressionState(int xpCap)
        {
            XpCap = xpCap;
        }

        public void AddDefeatedCard(CardType type, int value)
        {
            if (value <= 0)
                return;

            DefeatedCounters.Add(type, value);
            CurrentXp += value;

            if (CurrentXp >= XpCap)
            {
                int overflow = CurrentXp - XpCap;
                var dominant = PlayedCardCounterDominanceResolver.Resolve(DefeatedCounters);
                CurrentTier++;
                DefeatedCounters.Reset();
                CurrentXp = overflow;
                OnLevelUp?.Invoke(dominant);
            }
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

Expected: all `ProgressionStateTests` pass; no regressions.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Data/ProgressionState.cs Assets/Scripts/Tests/ProgressionStateTests.cs
git commit -m "feat: add ProgressionState with XP accumulation and level-up via dominant type"
```

---

## Task 5: SlotEffectAction + ClockTarget — remove PlayerClock, add DamageToThreat

**Files:**
- Modify: `Assets/Scripts/Data/SlotEffectAction.cs`
- Modify: `Assets/Scripts/Data/ClockTarget.cs`

- [ ] **Step 1: Update SlotEffectAction**

Replace the entire file:

```csharp
namespace CardsUnity
{
    public enum SlotEffectAction
    {
        DrawOpponentCard,
        AddToClock,
        ReturnCardsFromSlots,
        DrawToHandLimit,
        TurnEnd,
        IncreaseOpponentCardsOfTypeValue,
        IncreaseAppearingOpponentCardOfTypeValue,
        DamageToThreat
    }
}
```

- [ ] **Step 2: Update ClockTarget**

Replace the entire file:

```csharp
namespace CardsUnity
{
    public enum ClockTarget
    {
        OpponentClock
    }
}
```

- [ ] **Step 3: Compile check**

In Unity Editor, check the console for errors. `SlotEffectDefinition` still has a `clockTarget` field but since `OpponentClock` is the only remaining value, existing SOs referencing `PlayerClock` will silently default to `OpponentClock` (Unity's enum serialisation fallback). Verify there are no compilation errors.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Data/SlotEffectAction.cs Assets/Scripts/Data/ClockTarget.cs
git commit -m "feat: add DamageToThreat slot effect action; remove PlayerClock target"
```

---

## Task 6: GameConfig — add threat tiers and XP cap

**Files:**
- Modify: `Assets/Scripts/Config/GameConfig.cs`

- [ ] **Step 1: Update GameConfig**

Replace the entire file:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace CardsUnity
{
    [CreateAssetMenu(menuName = "CardsUnity/Game Config", fileName = "GameConfig")]
    public class GameConfig : ScriptableObject
    {
        public int draftValue = 3;
        public int boardSlotCount = 3;
        public List<CardDefinition> startingDeck = new List<CardDefinition>();
        public List<CardDefinition> opponentDeck = new List<CardDefinition>();
        public List<SlotDefinition> slotDefinitions = new List<SlotDefinition>();

        [Header("Threat System")]
        public List<ThreatTierDefinition> forceThreatTiers = new List<ThreatTierDefinition>();
        public List<ThreatTierDefinition> witThreatTiers = new List<ThreatTierDefinition>();
        public List<ThreatTierDefinition> presenceThreatTiers = new List<ThreatTierDefinition>();

        [Header("Progression")]
        [Min(1)] public int progressionXpCap = 10;
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Assets/Scripts/Config/GameConfig.cs
git commit -m "feat: add threat tier lists and XP cap to GameConfig"
```

---

## Task 7: TurnController — replace player clock with ThreatState, add progression hook

**Files:**
- Modify: `Assets/Scripts/Controllers/TurnController.cs`

- [ ] **Step 1: Replace the constructor signature and fields**

Remove `ClockState playerClock` parameter, add `ThreatState threatState` and `ProgressionState progression`. Full updated file:

```csharp
using System;

namespace CardsUnity
{
    public class TurnController
    {
        private readonly DeckState _playerDeck;
        private readonly HandState _playerHand;
        private readonly DeckState _opponentDeck;
        private readonly BoardState _board;
        private readonly ClockState _opponentClock;
        private readonly ThreatState _threatState;
        private readonly ProgressionState _progression;
        private readonly PlayedCardCounterState _playedCardCounters;
        private readonly int _draftValue;
        private readonly Random _rng;
        private bool _turnEndedByEffect;

        public event Action OnOpponentCardDestroyed;
        public event Action OnPlayerCardDestroyed;

        public TurnController(
            DeckState playerDeck,
            HandState playerHand,
            DeckState opponentDeck,
            BoardState board,
            ThreatState threatState,
            ClockState opponentClock,
            ProgressionState progression,
            PlayedCardCounterState playedCardCounters,
            int draftValue,
            Random rng)
        {
            _playerDeck = playerDeck;
            _playerHand = playerHand;
            _opponentDeck = opponentDeck;
            _board = board;
            _threatState = threatState;
            _opponentClock = opponentClock;
            _progression = progression;
            _playedCardCounters = playedCardCounters;
            _draftValue = draftValue;
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        }

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

            EvaluateSlotEffects(SlotEffectTrigger.OnPlayerTurnStart);
        }

        public void PlayCard(CardInstance card, int slotIndex)
        {
            _turnEndedByEffect = false;

            if (card == null)
                throw new ArgumentNullException(nameof(card));

            if (slotIndex < 0 || slotIndex >= _board.Slots.Length)
                throw new ArgumentOutOfRangeException(nameof(slotIndex));

            var slot = _board.Slots[slotIndex];
            if (!slot.IsActive || slot.PlayerCard != null)
                return;

            if (!CanHostPlayerCard(slot))
                return;

            if (!_playerHand.Remove(card))
                return;

            slot.PlayerCard = card;
            slot.PlayerCard.IsExhausted = true;
            RegisterPlayedCard(slot, card);

            if (slot.HasOpponentCard)
                ResolveSlotCombat(slot);

            EvaluateSlotEffectsForSlot(slot, SlotEffectTrigger.OnCardPlayed);
        }

        public void EndTurn()
        {
            foreach (var slot in _board.Slots)
            {
                if (!slot.IsActive)
                    continue;

                if (slot.PlayerCard == null)
                    continue;

                slot.PlayerCard.IsExhausted = false;
                _playerHand.Add(slot.PlayerCard);
                slot.PlayerCard = null;
            }

            PlaceOpponentCards();
        }

        public bool ConsumeTurnEndedByEffect()
        {
            bool result = _turnEndedByEffect;
            _turnEndedByEffect = false;
            return result;
        }

        public void AddCardsToDeck(
            System.Collections.Generic.IEnumerable<CardDefinition> cards,
            StoryEffectDeckTarget targetDeck,
            StoryEffectDeckPlacement placement)
        {
            var deck = targetDeck == StoryEffectDeckTarget.Player ? _playerDeck : _opponentDeck;
            if (deck == null || cards == null)
                return;

            foreach (var card in cards)
            {
                if (card == null)
                    continue;

                if (placement == StoryEffectDeckPlacement.DrawPile)
                    deck.AddToDrawPile(card);
                else
                    deck.AddToDiscardPile(card);
            }
        }

        private void ResolveSlotCombat(SlotState slot)
        {
            var result = CombatResolver.ResolveExchange(slot.PlayerCard, slot.OpponentCard, _rng);

            if (result.FirstResult.Destroyed)
            {
                var defeatedCard = slot.OpponentCard;
                _opponentClock.Increment();
                slot.OpponentCard = null;
                _progression?.AddDefeatedCard(defeatedCard.Definition.type, defeatedCard.Definition.value);
                OnOpponentCardDestroyed?.Invoke();
            }

            if (result.SecondResult.Destroyed)
            {
                slot.PlayerCard = null;
                OnPlayerCardDestroyed?.Invoke();
            }
        }

        private void PlaceOpponentCards()
        {
            foreach (var slot in _board.Slots)
            {
                if (!slot.IsActive)
                    continue;

                if (slot.HasOpponentCard)
                    continue;

                if (slot.Definition != null)
                    continue;

                if (_opponentDeck.DrawCount == 0 && _opponentDeck.CanDraw)
                    _opponentDeck.ShuffleDiscardIntoDrawPile(_rng);

                if (!_opponentDeck.CanDraw)
                    break;

                var card = _opponentDeck.Draw();
                if (card != null)
                {
                    slot.OpponentCard = new CardInstance(card);
                    EvaluateSlotEffects(SlotEffectTrigger.OnOpponentCardPlaced, slot, slot.OpponentCard);
                }
            }
        }

        private void EvaluateSlotEffects(SlotEffectTrigger trigger, SlotState triggeredSlot = null, CardInstance triggeringOpponentCard = null)
        {
            foreach (var slot in _board.Slots)
            {
                if (!slot.IsActive)
                    continue;

                EvaluateSlotEffectsForSlot(slot, trigger, triggeredSlot, triggeringOpponentCard);
            }
        }

        private void EvaluateSlotEffectsForSlot(
            SlotState slot,
            SlotEffectTrigger trigger,
            SlotState triggeredSlot = null,
            CardInstance triggeringOpponentCard = null)
        {
            if (slot.Definition?.effects == null)
                return;

            foreach (var effect in slot.Definition.effects)
            {
                if (effect == null || effect.trigger != trigger)
                    continue;

                if (!IsContextConditionMet(effect.context, slot))
                    continue;

                ExecuteSlotEffect(effect, slot, triggeredSlot, triggeringOpponentCard);
            }
        }

        private static bool IsContextConditionMet(SlotEffectContext context, SlotState slot)
        {
            return context switch
            {
                SlotEffectContext.NoOpponentCard => !slot.HasOpponentCard,
                SlotEffectContext.NoPlayerCard => !slot.HasPlayerCard,
                SlotEffectContext.Passive => true,
                _ => false
            };
        }

        private void ExecuteSlotEffect(
            SlotEffectDefinition effect,
            SlotState slot,
            SlotState triggeredSlot = null,
            CardInstance triggeringOpponentCard = null)
        {
            switch (effect.action)
            {
                case SlotEffectAction.DrawOpponentCard:
                    DrawOpponentCardToSlot(slot);
                    break;
                case SlotEffectAction.AddToClock:
                    _opponentClock.Increment(effect.actionValue);
                    break;
                case SlotEffectAction.ReturnCardsFromSlots:
                    ReturnAllPlayerCardsToHand();
                    break;
                case SlotEffectAction.DrawToHandLimit:
                    DrawPlayerCardsToHandLimit();
                    break;
                case SlotEffectAction.TurnEnd:
                    EndTurn();
                    _turnEndedByEffect = true;
                    break;
                case SlotEffectAction.IncreaseOpponentCardsOfTypeValue:
                    IncreaseOpponentCardsOfTypeValue(effect.targetCardType, effect.actionValue);
                    break;
                case SlotEffectAction.IncreaseAppearingOpponentCardOfTypeValue:
                    IncreaseAppearingOpponentCardOfTypeValue(triggeringOpponentCard, effect.targetCardType, effect.actionValue);
                    break;
                case SlotEffectAction.DamageToThreat:
                    _threatState?.GetBar(effect.targetCardType)?.TakeDamage(effect.actionValue);
                    break;
            }
        }

        private void DrawOpponentCardToSlot(SlotState slot)
        {
            if (!CanHostOpponentCard(slot))
                return;

            if (slot.HasOpponentCard)
                return;

            if (_opponentDeck.DrawCount == 0 && _opponentDeck.CanDraw)
                _opponentDeck.ShuffleDiscardIntoDrawPile(_rng);

            if (!_opponentDeck.CanDraw)
                return;

            var card = _opponentDeck.Draw();
            if (card != null)
            {
                slot.OpponentCard = new CardInstance(card);
                EvaluateSlotEffects(SlotEffectTrigger.OnOpponentCardPlaced, slot, slot.OpponentCard);
            }
        }

        private void ReturnAllPlayerCardsToHand()
        {
            foreach (var slot in _board.Slots)
            {
                if (!slot.IsActive)
                    continue;

                if (slot.PlayerCard == null)
                    continue;

                slot.PlayerCard.IsExhausted = false;
                _playerHand.Add(slot.PlayerCard);
                slot.PlayerCard = null;
            }
        }

        private void DrawPlayerCardsToHandLimit()
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
        }

        private void IncreaseOpponentCardsOfTypeValue(CardType targetCardType, int amount)
        {
            if (amount == 0)
                return;

            foreach (var boardSlot in _board.Slots)
            {
                if (!boardSlot.IsActive || boardSlot.OpponentCard?.Definition == null)
                    continue;

                if (boardSlot.OpponentCard.Definition.type != targetCardType)
                    continue;

                boardSlot.OpponentCard.CurrentValue += amount;
            }
        }

        private static void IncreaseAppearingOpponentCardOfTypeValue(CardInstance card, CardType targetCardType, int amount)
        {
            if (card?.Definition == null || amount == 0)
                return;

            if (card.Definition.type != targetCardType)
                return;

            card.CurrentValue += amount;
        }

        private static bool CanHostPlayerCard(SlotState slot)
        {
            return slot?.Definition == null || slot.Definition.hasPlayerCardSpot;
        }

        private static bool CanHostOpponentCard(SlotState slot)
        {
            return slot?.Definition == null || slot.Definition.hasOpponentCardSpot;
        }

        private void RegisterPlayedCard(SlotState slot, CardInstance card)
        {
            if (_playedCardCounters == null || slot?.Definition == null || card?.Definition == null)
                return;

            if (!slot.Definition.contributesToGlobalPlayedCardCounts)
                return;

            int amount = slot.Definition.playedCardCountMode == PlayedCardCountMode.UseFixedValue
                ? slot.Definition.fixedPlayedCardCountValue
                : card.CurrentValue;

            _playedCardCounters.Add(card.Definition.type, amount);
        }
    }
}
```

- [ ] **Step 2: Compile check — expect errors in TurnControllerTests**

Open Unity, check console. `TurnControllerTests` still passes `_playerClock` to the constructor. Expected: compile errors there.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Controllers/TurnController.cs
git commit -m "feat: replace playerClock with ThreatState in TurnController; hook progression on opponent card destroy"
```

---

## Task 8: Update TurnControllerTests — replace player clock with ThreatState

**Files:**
- Modify: `Assets/Scripts/Tests/TurnControllerTests.cs`

- [ ] **Step 1: Update SetUp and all helper methods**

Replace `_playerClock : ClockState` with `_threatState : ThreatState` throughout the test file. Replace the full file:

```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using CardsUnity;
using UnityEngine;

namespace CardsUnity.Tests
{
    public class TurnControllerTests
    {
        private TurnController _turn;
        private DeckState _playerDeck;
        private HandState _playerHand;
        private DeckState _opponentDeck;
        private BoardState _board;
        private ThreatState _threatState;
        private ClockState _opponentClock;
        private ProgressionState _progression;
        private PlayedCardCounterState _playedCardCounters;

        [SetUp]
        public void SetUp()
        {
            _playerDeck = MakeDeck(6, CardType.Force, value: 3);
            _playerHand = new HandState();
            _opponentDeck = MakeDeck(4, CardType.Presence, value: 4);
            _board = new BoardState(slotCount: 3);
            _threatState = MakeThreatState(maxPerBar: 10);
            _opponentClock = new ClockState(maxValue: 4);
            _progression = new ProgressionState(xpCap: 100);
            _playedCardCounters = new PlayedCardCounterState();

            _turn = new TurnController(
                _playerDeck, _playerHand,
                _opponentDeck, _board,
                _threatState, _opponentClock, _progression, _playedCardCounters,
                draftValue: 3,
                rng: new System.Random(42));
        }

        // ── draw tests ────────────────────────────────────────────────────────

        [Test]
        public void StartTurn_draws_up_to_draft_value()
        {
            _turn.StartTurn();
            Assert.AreEqual(3, _playerHand.Cards.Count);
        }

        [Test]
        public void StartTurn_does_not_exceed_draft_value_when_hand_already_has_cards()
        {
            _playerHand.Add(new CardInstance(ScriptableObject.CreateInstance<CardDefinition>()));
            _turn.StartTurn();
            Assert.AreEqual(3, _playerHand.Cards.Count);
        }

        [Test]
        public void StartTurn_shuffles_discard_when_draw_pile_empty()
        {
            for (int i = 0; i < 6; i++)
                _playerDeck.Discard(_playerDeck.Draw());

            _turn.StartTurn();
            Assert.AreEqual(3, _playerHand.Cards.Count);
        }

        // ── play card tests ───────────────────────────────────────────────────

        [Test]
        public void PlayCard_places_card_in_slot()
        {
            _turn.StartTurn();
            var card = _playerHand.Cards[0];
            _turn.PlayCard(card, slotIndex: 0);
            Assert.AreSame(card, _board.Slots[0].PlayerCard);
        }

        [Test]
        public void PlayCard_removes_card_from_hand()
        {
            _turn.StartTurn();
            var card = _playerHand.Cards[0];
            _turn.PlayCard(card, slotIndex: 0);
            Assert.IsFalse(_playerHand.Cards.Contains(card));
        }

        [Test]
        public void PlayCard_marks_player_card_as_exhausted()
        {
            _turn.StartTurn();
            var card = _playerHand.Cards[0];
            _turn.PlayCard(card, slotIndex: 0);
            Assert.IsTrue(_board.Slots[0].PlayerCard.IsExhausted);
        }

        [Test]
        public void PlayCard_to_occupied_slot_deals_full_damage_on_win()
        {
            _turn.StartTurn();
            var opponentCard = new CardInstance(MakeCardDef(CardType.Presence, value: 10));
            _board.Slots[1].OpponentCard = opponentCard;

            var playerCard = _playerHand.Cards.First(c => c.Definition.type == CardType.Force);
            _turn.PlayCard(playerCard, slotIndex: 1);

            Assert.AreEqual(7, opponentCard.CurrentValue);
        }

        [Test]
        public void PlayCard_increments_opponent_clock_when_card_destroyed()
        {
            _turn.StartTurn();
            _board.Slots[0].OpponentCard = new CardInstance(MakeCardDef(CardType.Presence, value: 1));

            var playerCard = _playerHand.Cards.First(c => c.Definition.type == CardType.Force);
            _turn.PlayCard(playerCard, slotIndex: 0);

            Assert.AreEqual(1, _opponentClock.CurrentValue);
        }

        [Test]
        public void PlayCard_applies_counter_damage_to_player_card()
        {
            _turn.StartTurn();
            _board.Slots[0].OpponentCard = new CardInstance(MakeCardDef(CardType.Wit, value: 1));

            var playerCard = _playerHand.Cards.First(c => c.Definition.type == CardType.Force);
            _turn.PlayCard(playerCard, slotIndex: 0);

            Assert.AreEqual(2, _board.Slots[0].PlayerCard?.CurrentValue);
        }

        [Test]
        public void PlayCard_destroying_opponent_card_adds_to_progression()
        {
            _turn.StartTurn();
            _board.Slots[0].OpponentCard = new CardInstance(MakeCardDef(CardType.Presence, value: 1));

            var playerCard = _playerHand.Cards.First(c => c.Definition.type == CardType.Force);
            _turn.PlayCard(playerCard, slotIndex: 0);

            // opponent card value=1, type=Presence → _progression.DefeatedCounters.Presence == 1
            Assert.AreEqual(1, _progression.DefeatedCounters.Presence);
            Assert.AreEqual(1, _progression.CurrentXp);
        }

        [Test]
        public void PlayCard_player_card_destroyed_does_not_affect_threat_bars()
        {
            _turn.StartTurn();
            _board.Slots[0].OpponentCard = new CardInstance(MakeCardDef(CardType.Wit, value: 10));

            var playerCard = _playerHand.Cards.First(c => c.Definition.type == CardType.Force);
            _turn.PlayCard(playerCard, slotIndex: 0);

            // Threat bars untouched (no slot effect fired DamageToThreat)
            Assert.IsFalse(_threatState.AnyAtZero);
            Assert.IsNull(_board.Slots[0].PlayerCard);
        }

        // ── end turn tests ────────────────────────────────────────────────────

        [Test]
        public void EndTurn_returns_player_cards_to_hand()
        {
            _turn.StartTurn();
            var card = _playerHand.Cards[0];
            _turn.PlayCard(card, slotIndex: 0);
            _turn.EndTurn();
            Assert.IsTrue(_playerHand.Cards.Contains(card));
        }

        [Test]
        public void EndTurn_preserves_current_value_for_returned_player_cards()
        {
            _turn.StartTurn();
            _board.Slots[0].OpponentCard = new CardInstance(MakeCardDef(CardType.Wit, value: 1));

            var card = _playerHand.Cards.First(c => c.Definition.type == CardType.Force);
            _turn.PlayCard(card, slotIndex: 0);

            Assert.AreEqual(2, card.CurrentValue);
            _turn.EndTurn();

            Assert.That(_playerHand.Cards, Does.Contain(card));
            Assert.AreEqual(2, card.CurrentValue);
        }

        [Test]
        public void EndTurn_clears_player_cards_from_slots()
        {
            _turn.StartTurn();
            _turn.PlayCard(_playerHand.Cards[0], slotIndex: 0);
            _turn.EndTurn();
            Assert.IsNull(_board.Slots[0].PlayerCard);
        }

        [Test]
        public void EndTurn_places_opponent_cards_in_empty_slots()
        {
            _turn.StartTurn();
            _turn.EndTurn();
            Assert.Greater(_board.Slots.Count(s => s.HasOpponentCard), 0);
        }

        // ── slot effect tests ─────────────────────────────────────────────────

        [Test]
        public void PlayCard_adds_current_value_to_global_counter_when_slot_uses_card_value_mode()
        {
            var def = ScriptableObject.CreateInstance<SlotDefinition>();
            def.contributesToGlobalPlayedCardCounts = true;
            def.playedCardCountMode = PlayedCardCountMode.UseCardValue;
            var board = new BoardState(new[] { def, null, null });
            var turn = MakeTurnWithBoard(board);

            turn.StartTurn();
            var card = _playerHand.Cards.First();
            card.CurrentValue = 7;

            turn.PlayCard(card, slotIndex: 0);

            Assert.AreEqual(7, _playedCardCounters.Force);
            Assert.AreEqual(0, _playedCardCounters.Presence);
            Assert.AreEqual(0, _playedCardCounters.Wit);
        }

        [Test]
        public void PlayCard_adds_fixed_value_to_matching_global_counter_when_slot_uses_fixed_value_mode()
        {
            var def = ScriptableObject.CreateInstance<SlotDefinition>();
            def.contributesToGlobalPlayedCardCounts = true;
            def.playedCardCountMode = PlayedCardCountMode.UseFixedValue;
            def.fixedPlayedCardCountValue = 3;
            var board = new BoardState(new[] { def, null, null });
            var turn = MakeTurnWithBoard(board);

            turn.StartTurn();
            var presenceCard = new CardInstance(MakeCardDef(CardType.Presence, 5));
            _playerHand.Add(presenceCard);

            turn.PlayCard(presenceCard, slotIndex: 0);

            Assert.AreEqual(3, _playedCardCounters.Presence);
            Assert.AreEqual(0, _playedCardCounters.Force);
            Assert.AreEqual(0, _playedCardCounters.Wit);
        }

        [Test]
        public void PlayCard_does_not_add_to_global_counter_when_slot_does_not_contribute()
        {
            var def = ScriptableObject.CreateInstance<SlotDefinition>();
            def.contributesToGlobalPlayedCardCounts = false;
            var board = new BoardState(new[] { def, null, null });
            var turn = MakeTurnWithBoard(board);

            turn.StartTurn();
            var card = _playerHand.Cards.First();

            turn.PlayCard(card, slotIndex: 0);

            Assert.AreEqual(0, _playedCardCounters.Force);
            Assert.AreEqual(0, _playedCardCounters.Presence);
            Assert.AreEqual(0, _playedCardCounters.Wit);
        }

        [Test]
        public void StartTurn_DrawOpponentCard_fills_empty_slot_with_opponent_card()
        {
            var def = MakeSlotDefinition(
                SlotEffectContext.NoOpponentCard,
                SlotEffectTrigger.OnPlayerTurnStart,
                SlotEffectAction.DrawOpponentCard);
            var board = new BoardState(new SlotDefinition[] { def, null, null });
            var turn = MakeTurnWithBoard(board);

            turn.StartTurn();

            Assert.IsNotNull(board.Slots[0].OpponentCard);
        }

        [Test]
        public void StartTurn_DrawOpponentCard_does_not_replace_existing_opponent_card()
        {
            var def = MakeSlotDefinition(
                SlotEffectContext.NoOpponentCard,
                SlotEffectTrigger.OnPlayerTurnStart,
                SlotEffectAction.DrawOpponentCard);
            var board = new BoardState(new SlotDefinition[] { def, null, null });
            var existing = new CardInstance(MakeCardDef(CardType.Presence, 5));
            board.Slots[0].OpponentCard = existing;
            var turn = MakeTurnWithBoard(board);

            turn.StartTurn();

            Assert.AreSame(existing, board.Slots[0].OpponentCard);
        }

        [Test]
        public void StartTurn_DrawOpponentCard_does_not_fill_slot_without_opponent_spot()
        {
            var def = MakeSlotDefinition(
                SlotEffectContext.NoOpponentCard,
                SlotEffectTrigger.OnPlayerTurnStart,
                SlotEffectAction.DrawOpponentCard);
            def.hasOpponentCardSpot = false;
            var board = new BoardState(new SlotDefinition[] { def, null, null });
            var turn = MakeTurnWithBoard(board);

            turn.StartTurn();

            Assert.IsNull(board.Slots[0].OpponentCard);
        }

        [Test]
        public void StartTurn_DamageToThreat_drains_correct_bar()
        {
            var def = MakeSlotDefinition(
                SlotEffectContext.NoPlayerCard,
                SlotEffectTrigger.OnPlayerTurnStart,
                SlotEffectAction.DamageToThreat,
                actionValue: 3);
            def.effects[0].targetCardType = CardType.Force;

            var board = new BoardState(new SlotDefinition[] { def, null, null });
            var turn = MakeTurnWithBoard(board);

            turn.StartTurn();

            var bar = _threatState.GetBar(CardType.Force);
            Assert.AreEqual(7, bar.Segments[0].CurrentValue); // started at 10, drained 3
        }

        [Test]
        public void StartTurn_AddToClock_increments_opponent_clock()
        {
            var def = MakeSlotDefinition(
                SlotEffectContext.NoPlayerCard,
                SlotEffectTrigger.OnPlayerTurnStart,
                SlotEffectAction.AddToClock,
                actionValue: 2);
            var board = new BoardState(new SlotDefinition[] { def, null, null });
            var turn = MakeTurnWithBoard(board);

            turn.StartTurn();

            Assert.AreEqual(2, _opponentClock.CurrentValue);
        }

        [Test]
        public void PlayCard_ReturnCardsFromSlots_returns_all_board_cards_to_hand()
        {
            var drawSlotDef = MakeSlotDefinition(
                SlotEffectContext.Passive,
                SlotEffectTrigger.OnCardPlayed,
                SlotEffectAction.ReturnCardsFromSlots);
            var board = new BoardState(new SlotDefinition[] { null, null, drawSlotDef });
            board.Slots[0].PlayerCard = new CardInstance(MakeCardDef(CardType.Force, 3));
            var turn = MakeTurnWithBoard(board);

            turn.StartTurn();
            var handCard = _playerHand.Cards[0];
            turn.PlayCard(handCard, slotIndex: 2);

            Assert.IsNull(board.Slots[0].PlayerCard);
            Assert.IsNull(board.Slots[2].PlayerCard);
            Assert.AreEqual(4, _playerHand.Cards.Count);
        }

        [Test]
        public void PlayCard_DrawToHandLimit_draws_up_to_draft_value()
        {
            var drawSlotDef = MakeSlotDefinition(
                SlotEffectContext.Passive,
                SlotEffectTrigger.OnCardPlayed,
                SlotEffectAction.DrawToHandLimit);
            var board = new BoardState(new SlotDefinition[] { null, null, drawSlotDef });
            var turn = MakeTurnWithBoard(board);

            turn.StartTurn();
            var card0 = _playerHand.Cards[0];
            var card1 = _playerHand.Cards[1];
            turn.PlayCard(card0, slotIndex: 0);
            turn.PlayCard(card1, slotIndex: 1);

            var handCard = _playerHand.Cards[0];
            turn.PlayCard(handCard, slotIndex: 2);

            Assert.AreEqual(3, _playerHand.Cards.Count);
        }

        [Test]
        public void StartTurn_IncreaseOpponentCardsOfTypeValue_increases_only_matching_opponent_cards()
        {
            var def = MakeSlotDefinition(
                SlotEffectContext.Passive,
                SlotEffectTrigger.OnPlayerTurnStart,
                SlotEffectAction.IncreaseOpponentCardsOfTypeValue,
                actionValue: 2);
            def.effects[0].targetCardType = CardType.Presence;

            var board = new BoardState(new SlotDefinition[] { def, null, null });
            board.Slots[1].OpponentCard = new CardInstance(MakeCardDef(CardType.Presence, 4));
            board.Slots[2].OpponentCard = new CardInstance(MakeCardDef(CardType.Wit, 5));
            var turn = MakeTurnWithBoard(board);

            turn.StartTurn();

            Assert.AreEqual(6, board.Slots[1].OpponentCard.CurrentValue);
            Assert.AreEqual(5, board.Slots[2].OpponentCard.CurrentValue);
        }

        [Test]
        public void StartTurn_IncreaseOpponentCardsOfTypeValue_increases_matching_card_on_effect_slot_too()
        {
            var def = MakeSlotDefinition(
                SlotEffectContext.Passive,
                SlotEffectTrigger.OnPlayerTurnStart,
                SlotEffectAction.IncreaseOpponentCardsOfTypeValue,
                actionValue: 3);
            def.effects[0].targetCardType = CardType.Presence;

            var board = new BoardState(new SlotDefinition[] { def, null, null });
            board.Slots[0].OpponentCard = new CardInstance(MakeCardDef(CardType.Presence, 2));
            var turn = MakeTurnWithBoard(board);

            turn.StartTurn();

            Assert.AreEqual(5, board.Slots[0].OpponentCard.CurrentValue);
        }

        [Test]
        public void EndTurn_IncreaseAppearingOpponentCardOfTypeValue_buffs_only_newly_placed_matching_card()
        {
            var effectSlotDef = MakeSlotDefinition(
                SlotEffectContext.Passive,
                SlotEffectTrigger.OnOpponentCardPlaced,
                SlotEffectAction.IncreaseAppearingOpponentCardOfTypeValue,
                actionValue: 2);
            effectSlotDef.effects[0].targetCardType = CardType.Presence;

            var board = new BoardState(new SlotDefinition[] { effectSlotDef, null, null });
            var existingCard = new CardInstance(MakeCardDef(CardType.Presence, 7));
            board.Slots[0].OpponentCard = existingCard;
            var turn = MakeTurnWithBoard(board);

            turn.EndTurn();

            Assert.AreEqual(7, board.Slots[0].OpponentCard.CurrentValue);
            Assert.AreEqual(6, board.Slots[1].OpponentCard.CurrentValue);
            Assert.AreEqual(6, board.Slots[2].OpponentCard.CurrentValue);
        }

        [Test]
        public void StartTurn_DrawOpponentCard_can_trigger_IncreaseAppearingOpponentCardOfTypeValue_once()
        {
            var drawSlotDef = MakeSlotDefinition(
                SlotEffectContext.NoOpponentCard,
                SlotEffectTrigger.OnPlayerTurnStart,
                SlotEffectAction.DrawOpponentCard);
            drawSlotDef.hasOpponentCardSpot = true;

            var passiveBuffSlotDef = MakeSlotDefinition(
                SlotEffectContext.Passive,
                SlotEffectTrigger.OnOpponentCardPlaced,
                SlotEffectAction.IncreaseAppearingOpponentCardOfTypeValue,
                actionValue: 3);
            passiveBuffSlotDef.effects[0].targetCardType = CardType.Presence;

            var board = new BoardState(new SlotDefinition[] { drawSlotDef, passiveBuffSlotDef, null });
            var turn = MakeTurnWithBoard(board);

            turn.StartTurn();

            Assert.AreEqual(7, board.Slots[0].OpponentCard.CurrentValue);
        }

        [Test]
        public void EndTurn_IncreaseAppearingOpponentCardOfTypeValue_does_not_buff_non_matching_card()
        {
            var effectSlotDef = MakeSlotDefinition(
                SlotEffectContext.Passive,
                SlotEffectTrigger.OnOpponentCardPlaced,
                SlotEffectAction.IncreaseAppearingOpponentCardOfTypeValue,
                actionValue: 2);
            effectSlotDef.effects[0].targetCardType = CardType.Force;

            var board = new BoardState(new SlotDefinition[] { effectSlotDef, null, null });
            var turn = MakeTurnWithBoard(board);

            turn.EndTurn();

            Assert.AreEqual(4, board.Slots[1].OpponentCard.CurrentValue);
            Assert.AreEqual(4, board.Slots[2].OpponentCard.CurrentValue);
        }

        [Test]
        public void PlayCard_does_not_place_card_on_slot_without_player_spot()
        {
            var def = MakeSlotDefinition(
                SlotEffectContext.Passive,
                SlotEffectTrigger.OnCardPlayed,
                SlotEffectAction.DrawToHandLimit);
            def.hasPlayerCardSpot = false;
            var board = new BoardState(new SlotDefinition[] { def, null, null });
            var turn = MakeTurnWithBoard(board);

            turn.StartTurn();
            var card = _playerHand.Cards[0];

            turn.PlayCard(card, slotIndex: 0);

            Assert.That(_playerHand.Cards, Does.Contain(card));
            Assert.IsNull(board.Slots[0].PlayerCard);
        }

        [Test]
        public void PlayCard_TurnEnd_returns_player_cards_and_places_opponent_cards()
        {
            var turnEndSlotDef = MakeSlotDefinition(
                SlotEffectContext.Passive,
                SlotEffectTrigger.OnCardPlayed,
                SlotEffectAction.TurnEnd);
            var board = new BoardState(new SlotDefinition[] { null, null, turnEndSlotDef });
            var turn = MakeTurnWithBoard(board);

            turn.StartTurn();
            var firstCard = _playerHand.Cards[0];
            var triggerCard = _playerHand.Cards[1];
            turn.PlayCard(firstCard, slotIndex: 0);
            turn.PlayCard(triggerCard, slotIndex: 2);

            Assert.IsTrue(turn.ConsumeTurnEndedByEffect());
            Assert.IsNull(board.Slots[0].PlayerCard);
            Assert.IsNull(board.Slots[2].PlayerCard);
            Assert.That(_playerHand.Cards, Does.Contain(firstCard));
            Assert.That(_playerHand.Cards, Does.Contain(triggerCard));
            Assert.Greater(board.Slots.Count(s => s.HasOpponentCard), 0);
        }

        [Test]
        public void ConsumeTurnEndedByEffect_returns_false_after_state_is_consumed()
        {
            var turnEndSlotDef = MakeSlotDefinition(
                SlotEffectContext.Passive,
                SlotEffectTrigger.OnCardPlayed,
                SlotEffectAction.TurnEnd);
            var board = new BoardState(new SlotDefinition[] { null, null, turnEndSlotDef });
            var turn = MakeTurnWithBoard(board);

            turn.StartTurn();
            turn.PlayCard(_playerHand.Cards[0], slotIndex: 2);

            Assert.IsTrue(turn.ConsumeTurnEndedByEffect());
            Assert.IsFalse(turn.ConsumeTurnEndedByEffect());
        }

        // ── helpers ───────────────────────────────────────────────────────────

        private static ThreatState MakeThreatState(int maxPerBar)
        {
            var tier = ScriptableObject.CreateInstance<ThreatTierDefinition>();
            tier.maxValue = maxPerBar;
            var tiers = new List<ThreatTierDefinition> { tier };
            return new ThreatState(tiers, tiers, tiers);
        }

        private static DeckState MakeDeck(int count, CardType type, int value)
        {
            var deck = new DeckState();
            deck.Initialize(Enumerable.Range(0, count).Select(_ => MakeCardDef(type, value)).ToList());
            return deck;
        }

        private static CardDefinition MakeCardDef(CardType type, int value)
        {
            var def = ScriptableObject.CreateInstance<CardDefinition>();
            def.type = type;
            def.value = value;
            return def;
        }

        private static SlotDefinition MakeSlotDefinition(
            SlotEffectContext context,
            SlotEffectTrigger trigger,
            SlotEffectAction action,
            int actionValue = 1)
        {
            var effect = ScriptableObject.CreateInstance<SlotEffectDefinition>();
            effect.context = context;
            effect.trigger = trigger;
            effect.action = action;
            effect.actionValue = actionValue;

            var def = ScriptableObject.CreateInstance<SlotDefinition>();
            def.effects = new List<SlotEffectDefinition> { effect };
            return def;
        }

        private TurnController MakeTurnWithBoard(BoardState board)
        {
            return new TurnController(
                _playerDeck, _playerHand,
                _opponentDeck, board,
                _threatState, _opponentClock, _progression, _playedCardCounters,
                draftValue: 3,
                rng: new System.Random(42));
        }
    }
}
```

- [ ] **Step 2: Run all tests — expect all pass**

```bash
/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath . \
  -runTests -testPlatform EditMode \
  -testResults TestResults/EditMode.xml
```

Expected: all tests pass including existing suites.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Tests/TurnControllerTests.cs
git commit -m "test: update TurnControllerTests to use ThreatState; add DamageToThreat and progression tests"
```

---

## Task 9: ChallengeController — wire ThreatState, ProgressionState, remove playerClock

**Files:**
- Modify: `Assets/Scripts/Controllers/ChallengeController.cs`

- [ ] **Step 1: Replace the entire file**

Key changes: remove `playerClockView` + `_playerClock`; add `ThreatState _threatState` + `ProgressionState _progression`; subscribe to threat events to spawn slots / trigger game over; `IsGameOver()` checks `_threatState.AnyAtZero`.

```csharp
using System;
using System.Collections.Generic;
using CardsUnity.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CardsUnity
{
    public class ChallengeController : MonoBehaviour
    {
        [SerializeField] private GameConfig config;
        [SerializeField] private BoardView boardView;
        [SerializeField] private HandView handView;
        [SerializeField] private ClockView opponentClockView;
        [SerializeField] private PlayedCardCounterView playedCardCounterView;
        [SerializeField] private EndTurnButton endTurnButton;
        [SerializeField] private GameObject winPanel;
        [SerializeField] private GameObject losePanel;
        [SerializeField] private StoryDeckDefinition storyDeck;
        [SerializeField] private StoryCardView storyCardView;

        private TurnController _turn;
        private HandState _hand;
        private BoardState _board;
        private ClockState _opponentClock;
        private ThreatState _threatState;
        private ProgressionState _progression;
        private PlayedCardCounterState _playedCardCounters;
        private System.Random _rng;
        private StoryDeckState _storyDeck;
        private Action<CardView> _cardDragStartHandler;
        private Action<CardView> _cardDragEndHandler;

        private void Start()
        {
            if (config == null || boardView == null || handView == null ||
                opponentClockView == null || endTurnButton == null)
            {
                Debug.LogError("ChallengeController is missing required references.");
                enabled = false;
                return;
            }

            _rng = new System.Random();

            var playerDeck = BuildDeck(config.startingDeck, _rng);
            var opponentDeck = BuildDeck(config.opponentDeck, _rng);

            _hand = new HandState();
            var slotDefs = ResolveSlotDefinitions();
            var slotActiveStates = ResolveSlotActiveStates(slotDefs);
            _board = slotDefs != null
                ? new BoardState(slotDefs, slotActiveStates)
                : new BoardState(config.boardSlotCount);

            _opponentClock = new ClockState(config.opponentDeck != null ? config.opponentDeck.Count : 0);
            _threatState = BuildThreatState();
            _progression = new ProgressionState(config.progressionXpCap);
            _playedCardCounters = new PlayedCardCounterState();

            _turn = new TurnController(
                playerDeck, _hand,
                opponentDeck, _board,
                _threatState, _opponentClock, _progression, _playedCardCounters,
                config.draftValue, _rng);

            if (storyDeck != null)
                _storyDeck = new StoryDeckState(storyDeck, _rng);

            _cardDragStartHandler = _ => boardView.SetHighlightAll(true);
            _cardDragEndHandler   = _ => boardView.SetHighlightAll(false);

            boardView.OnCardPlayed     += OnCardPlayed;
            handView.OnCardDragStart   += _cardDragStartHandler;
            handView.OnCardDragEnd     += _cardDragEndHandler;
            endTurnButton.OnClicked    += OnEndTurn;
            _turn.OnOpponentCardDestroyed += HandleOpponentCardDestroyed;
            _turn.OnPlayerCardDestroyed   += HandlePlayerCardDestroyed;

            _threatState.OnTierDepleted += HandleThreatTierDepleted;
            _threatState.OnBarAtZero    += HandleThreatBarAtZero;
            _progression.OnLevelUp      += HandleLevelUp;

            if (winPanel != null)  winPanel.SetActive(false);
            if (losePanel != null) losePanel.SetActive(false);

            if (slotDefs != null)
                boardView.ConfigureSlots(slotDefs);

            BeginTurn();
            _turn.EndTurn();
            RefreshUI();
            CheckEndCondition();
        }

        private void OnDestroy()
        {
            if (boardView != null)
                boardView.OnCardPlayed -= OnCardPlayed;

            if (handView != null)
            {
                if (_cardDragStartHandler != null) handView.OnCardDragStart -= _cardDragStartHandler;
                if (_cardDragEndHandler   != null) handView.OnCardDragEnd   -= _cardDragEndHandler;
            }

            if (endTurnButton != null)
                endTurnButton.OnClicked -= OnEndTurn;

            if (_turn != null)
            {
                _turn.OnOpponentCardDestroyed -= HandleOpponentCardDestroyed;
                _turn.OnPlayerCardDestroyed   -= HandlePlayerCardDestroyed;
            }

            if (_threatState != null)
            {
                _threatState.OnTierDepleted -= HandleThreatTierDepleted;
                _threatState.OnBarAtZero    -= HandleThreatBarAtZero;
            }

            if (_progression != null)
                _progression.OnLevelUp -= HandleLevelUp;
        }

        // ── threat ────────────────────────────────────────────────────────────

        private ThreatState BuildThreatState()
        {
            return new ThreatState(
                config.forceThreatTiers,
                config.witThreatTiers,
                config.presenceThreatTiers);
        }

        private void HandleThreatTierDepleted(CardType type, int tierIndex)
        {
            var bar = _threatState.GetBar(type);
            if (bar == null || tierIndex >= config.GetThreatTiers(type).Count)
                return;

            var tierDef = config.GetThreatTiers(type)[tierIndex];
            if (tierDef?.penaltySlot != null)
                AddRuntimeStorySlot(tierDef.penaltySlot);

            RefreshUI();
        }

        private void HandleThreatBarAtZero(CardType type)
        {
            CheckEndCondition();
        }

        // ── progression ───────────────────────────────────────────────────────

        private void HandleLevelUp(StoryEffectResolutionKey dominantType)
        {
            // Reward: log the level-up for now; reward card draw will be wired
            // once ProgressionRewardDeckSet is added in a later task.
            Debug.Log($"Level up! Dominant type: {dominantType}, Tier: {_progression.CurrentTier}");
            RefreshUI();
        }

        // ── turn flow ─────────────────────────────────────────────────────────

        private static DeckState BuildDeck(List<CardDefinition> cards, System.Random rng)
        {
            var deck = new DeckState();
            var shuffledCards = cards != null
                ? new List<CardDefinition>(cards)
                : new List<CardDefinition>();

            if (rng != null)
            {
                for (int i = shuffledCards.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    (shuffledCards[i], shuffledCards[j]) = (shuffledCards[j], shuffledCards[i]);
                }
            }

            deck.Initialize(shuffledCards);
            return deck;
        }

        private SlotDefinition[] ResolveSlotDefinitions()
        {
            if (boardView != null && boardView.HasAnySceneSlotDefinitions())
                return boardView.GetSceneSlotDefinitions();

            return config.slotDefinitions != null && config.slotDefinitions.Count > 0
                ? config.slotDefinitions.ToArray()
                : null;
        }

        private bool[] ResolveSlotActiveStates(SlotDefinition[] slotDefinitions)
        {
            if (slotDefinitions == null)
                return null;

            if (boardView != null)
            {
                var sceneActiveStates = boardView.GetSceneSlotActiveStates();
                if (sceneActiveStates != null && sceneActiveStates.Length == slotDefinitions.Length)
                    return sceneActiveStates;
            }

            var activeStates = new bool[slotDefinitions.Length];
            for (int i = 0; i < activeStates.Length; i++)
                activeStates[i] = true;

            return activeStates;
        }

        private void BeginTurn()
        {
            _turn.StartTurn();
            RefreshUI();
        }

        private void OnCardPlayed(CardInstance card, int slotIndex)
        {
            _turn.PlayCard(card, slotIndex);

            if (_turn.ConsumeTurnEndedByEffect())
            {
                RefreshUI();
                CheckEndCondition();

                if (!IsGameOver())
                    BeginTurn();

                return;
            }

            RefreshUI();
            CheckEndCondition();
        }

        private void HandleOpponentCardDestroyed() =>
            EvaluateStoryEffects(StoryEffectTrigger.OnOpponentCardDestroyed);

        private void HandlePlayerCardDestroyed() =>
            EvaluateStoryEffects(StoryEffectTrigger.OnPlayerCardDestroyed);

        private void EvaluateStoryEffects(StoryEffectTrigger trigger, StoryCardDefinition sourceCard = null)
        {
            var card = sourceCard ?? _storyDeck?.ActiveCard;
            if (card?.effects == null)
                return;

            foreach (var effect in card.effects)
            {
                if (effect == null || effect.trigger != trigger)
                    continue;

                switch (effect.action)
                {
                    case StoryEffectAction.IncrementStoryClock:
                        IncrementStoryClock(effect.actionValue);
                        break;
                    case StoryEffectAction.ResolvePlayedCardCounters:
                        ExecutePlayedCardCounterStoryEffect(effect);
                        break;
                }
            }
        }

        private void OnEndTurn()
        {
            _turn.EndTurn();
            RefreshUI();
            CheckEndCondition();

            if (!IsGameOver())
                BeginTurn();
        }

        private void RefreshUI()
        {
            handView.Refresh(_hand);
            boardView.Refresh(_board);
            opponentClockView.Refresh(_opponentClock);

            if (playedCardCounterView != null)
                playedCardCounterView.Refresh(_playedCardCounters);

            if (storyCardView != null)
                storyCardView.Refresh(_storyDeck?.ActiveCard, _storyDeck?.ActiveCardClock);
        }

        private void CheckEndCondition()
        {
            if (_threatState.AnyAtZero && losePanel != null)
                losePanel.SetActive(true);
        }

        private bool IsGameOver() => _threatState.AnyAtZero;

        public void Restart()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        // ── story ─────────────────────────────────────────────────────────────

        public void IncrementStoryClock(int amount)
        {
            if (_storyDeck == null || _storyDeck.ActiveCard == null)
                return;

            var completedCard = _storyDeck.ActiveCard;
            _storyDeck.ActiveCardClock.Increment(amount);

            if (_storyDeck.ActiveCardClock.IsFull)
            {
                EvaluateStoryEffects(StoryEffectTrigger.OnStoryClockCompleted, completedCard);
                _storyDeck.AdvanceCard();
            }
        }

        private void ExecutePlayedCardCounterStoryEffect(StoryEffectDefinition effect)
        {
            if (effect == null)
                return;

            var resolution = PlayedCardCounterDominanceResolver.Resolve(_playedCardCounters);
            ApplyStoryEffectOutcome(effect.GetOutcome(resolution));
        }

        private void ApplyStoryEffectOutcome(StoryEffectOutcome outcome)
        {
            if (outcome == null)
                return;

            if (outcome.deckMutations != null)
            {
                foreach (var deckMutation in outcome.deckMutations)
                {
                    if (deckMutation?.cards == null) continue;
                    _turn.AddCardsToDeck(deckMutation.cards, deckMutation.targetDeck, deckMutation.placement);
                }
            }

            if (outcome.slotMutations == null)
                return;

            foreach (var slotMutation in outcome.slotMutations)
                ApplySlotMutation(slotMutation);
        }

        private void ApplySlotMutation(StoryEffectSlotMutation slotMutation)
        {
            if (slotMutation == null || _board?.Slots == null)
                return;

            if (slotMutation.mode == StoryEffectSlotMutationMode.Add)
            {
                AddRuntimeStorySlot(slotMutation.slotDefinition);
                return;
            }

            if (slotMutation.slotIndex < 0 || slotMutation.slotIndex >= _board.Slots.Length)
                return;

            var slot = _board.Slots[slotMutation.slotIndex];
            if (slot == null)
                return;

            if (!slotMutation.active)
            {
                ReturnPlayerCardToHand(slot);
                slot.OpponentCard = null;
            }

            slot.SetDefinition(slotMutation.slotDefinition);
            slot.SetActive(slotMutation.active);

            if (slot.HasPlayerCard && slot.Definition != null && !slot.Definition.hasPlayerCardSpot)
                ReturnPlayerCardToHand(slot);

            if (slot.HasOpponentCard && slot.Definition != null && !slot.Definition.hasOpponentCardSpot)
                slot.OpponentCard = null;
        }

        private void AddRuntimeStorySlot(SlotDefinition definition)
        {
            if (definition == null)
                return;

            int slotIndex = _board.AddSlot(definition);
            if (boardView == null)
                return;

            int createdIndex = boardView.CreateRuntimeSlot(definition);
            if (createdIndex >= 0 && createdIndex != slotIndex)
                Debug.LogWarning($"Runtime slot index mismatch. BoardState={slotIndex}, BoardView={createdIndex}.");
        }

        private void ReturnPlayerCardToHand(SlotState slot)
        {
            if (slot?.PlayerCard == null)
                return;

            slot.PlayerCard.IsExhausted = false;
            _hand.Add(slot.PlayerCard);
            slot.PlayerCard = null;
        }
    }
}
```

- [ ] **Step 2: Add `GetThreatTiers` helper to GameConfig**

Open `Assets/Scripts/Config/GameConfig.cs` and add at the end of the class:

```csharp
        public List<ThreatTierDefinition> GetThreatTiers(CardType type) => type switch
        {
            CardType.Force    => forceThreatTiers,
            CardType.Wit      => witThreatTiers,
            CardType.Presence => presenceThreatTiers,
            _                 => new List<ThreatTierDefinition>()
        };
```

- [ ] **Step 3: Compile check in Unity — expect no errors**

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Controllers/ChallengeController.cs Assets/Scripts/Config/GameConfig.cs
git commit -m "feat: wire ThreatState and ProgressionState into ChallengeController; remove player clock"
```

---

## Task 10: ThreatBarView — segmented HP-style UI widget

**Files:**
- Create: `Assets/Scripts/UI/ThreatBarView.cs`

- [ ] **Step 1: Create ThreatBarView**

This MonoBehaviour renders one typed threat bar. Marek sets up the prefab in the Unity Editor: a horizontal parent with a `ThreatBarView` and a prefab slot for segment UI.

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CardsUnity.UI
{
    public class ThreatBarView : MonoBehaviour
    {
        [SerializeField] private CardType type;
        [SerializeField] private RectTransform segmentContainer;
        [SerializeField] private GameObject segmentPrefab; // prefab with Image component for fill
        [SerializeField] private float segmentSpacing = 4f;

        private readonly List<Image> _segmentFills = new();
        private readonly List<RectTransform> _segmentRects = new();

        public CardType Type => type;

        public void Build(ThreatBarState bar)
        {
            foreach (Transform child in segmentContainer)
                Destroy(child.gameObject);

            _segmentFills.Clear();
            _segmentRects.Clear();

            int total = 0;
            foreach (var seg in bar.Segments)
                total += seg.MaxValue;

            if (total == 0)
                return;

            float containerWidth = segmentContainer.rect.width
                - segmentSpacing * (bar.Segments.Count - 1);

            foreach (var seg in bar.Segments)
            {
                var go = Instantiate(segmentPrefab, segmentContainer);
                var rt = go.GetComponent<RectTransform>();
                float width = containerWidth * ((float)seg.MaxValue / total);
                rt.sizeDelta = new Vector2(width, rt.sizeDelta.y);
                _segmentRects.Add(rt);

                var fill = go.GetComponentInChildren<Image>();
                _segmentFills.Add(fill);
            }

            Refresh(bar);
        }

        public void Refresh(ThreatBarState bar)
        {
            for (int i = 0; i < _segmentFills.Count && i < bar.Segments.Count; i++)
            {
                var seg = bar.Segments[i];
                if (_segmentFills[i] != null)
                    _segmentFills[i].fillAmount = seg.MaxValue > 0
                        ? (float)seg.CurrentValue / seg.MaxValue
                        : 0f;
            }
        }
    }
}
```

- [ ] **Step 2: Create the segment prefab in Unity Editor**

In the Unity Editor:
1. Create a new UI Panel child under the `ThreatBarView` hierarchy — name it `SegmentPrefab`.
2. Add an `Image` component inside it (or use a child Image for the fill).
3. Set `Image.type = Filled` and `fillMethod = Horizontal`.
4. Save as a prefab to `Assets/Prefabs/UI/ThreatSegment.prefab`.
5. Assign to `ThreatBarView.segmentPrefab` in the Inspector.

- [ ] **Step 3: Add three ThreatBarView instances to the scene**

In the Unity scene hierarchy under the HUD canvas:
1. Create three horizontal containers — `ThreatBar_Force`, `ThreatBar_Wit`, `ThreatBar_Presence`.
2. Add `ThreatBarView` to each; set `type` to the matching `CardType`.
3. Assign `segmentContainer` to the horizontal container's RectTransform.

- [ ] **Step 4: Wire Build + Refresh in ChallengeController**

Add serialized field references to `ChallengeController`:

```csharp
[SerializeField] private ThreatBarView forceThreatBarView;
[SerializeField] private ThreatBarView witThreatBarView;
[SerializeField] private ThreatBarView presenceThreatBarView;
```

Call `Build` once after `_threatState` is created (add to the `Start()` method after `BuildThreatState()`):

```csharp
forceThreatBarView?.Build(_threatState.GetBar(CardType.Force));
witThreatBarView?.Build(_threatState.GetBar(CardType.Wit));
presenceThreatBarView?.Build(_threatState.GetBar(CardType.Presence));
```

Call `Refresh` inside `RefreshUI()`:

```csharp
forceThreatBarView?.Refresh(_threatState.GetBar(CardType.Force));
witThreatBarView?.Refresh(_threatState.GetBar(CardType.Wit));
presenceThreatBarView?.Refresh(_threatState.GetBar(CardType.Presence));
```

- [ ] **Step 5: Remove the old player clock widget from the scene**

In the Unity Editor, remove (or deactivate) the old player clock `ClockView` GameObject from the scene HUD. Also remove the `playerClockView` field assignment from `ChallengeController` Inspector.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/UI/ThreatBarView.cs Assets/Scripts/Controllers/ChallengeController.cs
git commit -m "feat: add ThreatBarView segmented HP widget and wire to ChallengeController"
```

---

## Task 11: ProgressionView — XP bar UI widget

**Files:**
- Create: `Assets/Scripts/UI/ProgressionView.cs`

- [ ] **Step 1: Create ProgressionView**

```csharp
using MoreMountains.Tools;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardsUnity.UI
{
    public class ProgressionView : MonoBehaviour
    {
        [SerializeField] private MMProgressBar progressBar;
        [SerializeField] private Image fillImage;
        [SerializeField] private TextMeshProUGUI tierLabel;
        [SerializeField] private TextMeshProUGUI forceCountLabel;
        [SerializeField] private TextMeshProUGUI witCountLabel;
        [SerializeField] private TextMeshProUGUI presenceCountLabel;

        public void Refresh(ProgressionState state)
        {
            if (state == null)
                return;

            if (progressBar != null)
                progressBar.UpdateBar(state.CurrentXp, 0f, state.XpCap);
            else if (fillImage != null)
                fillImage.fillAmount = state.Progress;

            if (tierLabel != null)
                tierLabel.text = $"Tier {state.CurrentTier + 1}";

            if (forceCountLabel != null)
                forceCountLabel.text = state.DefeatedCounters.Force.ToString();
            if (witCountLabel != null)
                witCountLabel.text = state.DefeatedCounters.Wit.ToString();
            if (presenceCountLabel != null)
                presenceCountLabel.text = state.DefeatedCounters.Presence.ToString();
        }
    }
}
```

- [ ] **Step 2: Add ProgressionView to scene**

In the Unity Editor, create a `ProgressionBar` UI group in the HUD canvas:
1. Add `ProgressionView` MonoBehaviour.
2. Wire up an `MMProgressBar` (or `Image` fill) for the XP bar.
3. Add three small `TextMeshProUGUI` labels for Force / Wit / Presence defeated counts.
4. Add a tier label.

- [ ] **Step 3: Wire in ChallengeController**

Add serialized field:

```csharp
[SerializeField] private ProgressionView progressionView;
```

Call `Refresh` in `RefreshUI()`:

```csharp
progressionView?.Refresh(_progression);
```

- [ ] **Step 4: Run all tests to confirm no regressions**

```bash
/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath . \
  -runTests -testPlatform EditMode \
  -testResults TestResults/EditMode.xml
```

Expected: all tests pass.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/UI/ProgressionView.cs Assets/Scripts/Controllers/ChallengeController.cs
git commit -m "feat: add ProgressionView XP bar widget and wire to ChallengeController"
```

---

## Task 12: Update game-design-doc.md

**Files:**
- Modify: `docs/game-design-doc.md`

- [ ] **Step 1: Update relevant sections**

Open `docs/game-design-doc.md` and:

1. **Remove** the Player Clock section (or mark as removed).
2. **Add** a Threat System section documenting: three bars (Force/Wit/Presence), drain direction, tier segments, tier penalties (spawn negative slot), game over at zero.
3. **Add** a Player Progression section documenting: one XP bar, fills from defeated opponent card base values, per-type counters reset on level-up, dominant type determines reward, overflow carries to next tier, tier count increments.
4. **Append** two rows to the Changelog table at the bottom:

```
| 2026-05-03 | Replaced Player Clock with Threat System (three segmented HP bars) |
| 2026-05-03 | Added Player Progression XP bar rewarding dominant defeated card type |
```

- [ ] **Step 2: Commit**

```bash
git add docs/game-design-doc.md
git commit -m "docs: update game-design-doc with Threat System and Player Progression"
```

---

## Self-Review Checklist

**Spec coverage:**
- ✅ Player clock removed (`_playerClock` gone from both TurnController and ChallengeController)
- ✅ Three draining threat bars (Force/Wit/Presence) — `ThreatBarState` + `ThreatState`
- ✅ Tier segments with variable max values — `ThreatTierState`, `ThreatTierDefinition.maxValue`
- ✅ Drains outermost first, cascades — `ThreatBarState.TakeDamage()` loop
- ✅ Tier penalty on crossing — `OnTierDepleted` → `HandleThreatTierDepleted` → spawn slot
- ✅ Game over at zero — `OnBarAtZero` / `AnyAtZero`
- ✅ `DamageToThreat` slot effect action wired through `ExecuteSlotEffect`
- ✅ One XP bar — `ProgressionState.CurrentXp`
- ✅ XP from defeated opponent card base values — `AddDefeatedCard(type, def.value)` in `ResolveSlotCombat`
- ✅ Per-type counters for dominance — `DefeatedCounters` (reuses `PlayedCardCounterState`)
- ✅ Level-up resolves dominant type — `PlayedCardCounterDominanceResolver.Resolve`
- ✅ Overflow carries — `CurrentXp = overflow` after level-up
- ✅ Tier increments — `CurrentTier++`
- ✅ Reward hook — `OnLevelUp` event (reward deck wired in future task)
- ✅ UI for threat bars — `ThreatBarView`
- ✅ UI for XP bar — `ProgressionView`
- ✅ `game-design-doc.md` updated — Task 12

**Type consistency check:**
- `ThreatBarState.Segments` → used in `ThreatBarView.Build()` and `Refresh()` ✅
- `ThreatState.GetBar(CardType)` → used in `TurnController.ExecuteSlotEffect` and `ChallengeController` ✅
- `ProgressionState.AddDefeatedCard(CardType, int)` → called with `def.type` and `def.value` ✅
- `GameConfig.GetThreatTiers(CardType)` → called in `HandleThreatTierDepleted` ✅
- `TurnController` constructor parameter order matches `ChallengeController` and test `SetUp` ✅
