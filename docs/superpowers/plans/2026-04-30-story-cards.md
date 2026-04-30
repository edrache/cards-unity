# Story Cards Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `StoryCardDefinition`, `StoryDeckDefinition`, and `StoryDeckState` so a challenge can have one active story card with its own clock that advances to the next card when full.

**Architecture:** Three ScriptableObject data classes (`StoryCardDefinition`, `StoryDeckDefinition`, `StoryDrawMode` enum) plus a plain runtime class `StoryDeckState` that owns an active card and a `ClockState`. `ChallengeController` gets an optional `storyDeck` field and a public `IncrementStoryClock(int)` entry point. No UI in this plan.

**Tech Stack:** Unity 6, C#, Unity Test Framework (EditMode), `ClockState` (existing), `CardEffectTag` (existing), `ScriptableObject.CreateInstance<T>()` for tests.

---

## File Map

| Action | Path | Responsibility |
|---|---|---|
| Create | `Assets/Scripts/Data/StoryDrawMode.cs` | Enum: `Sequential`, `Random` |
| Create | `Assets/Scripts/Data/StoryCardDefinition.cs` | SO: title, content, artwork, effects, clockMaxValue |
| Create | `Assets/Scripts/Data/StoryDeckDefinition.cs` | SO: list of cards, draw mode |
| Create | `Assets/Scripts/Data/StoryDeckState.cs` | Runtime: active card, clock, AdvanceCard() |
| Create | `Assets/Scripts/Tests/StoryDeckStateTests.cs` | EditMode tests for StoryDeckState |
| Modify | `Assets/Scripts/Controllers/ChallengeController.cs` | Add storyDeck field + IncrementStoryClock() |

---

## Task 1: `StoryDrawMode` enum

**Files:**
- Create: `Assets/Scripts/Data/StoryDrawMode.cs`

- [ ] **Step 1: Create the enum**

```csharp
namespace CardsUnity
{
    public enum StoryDrawMode
    {
        Sequential,
        Random
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Assets/Scripts/Data/StoryDrawMode.cs
git commit -m "Add StoryDrawMode enum"
```

---

## Task 2: `StoryCardDefinition` ScriptableObject

**Files:**
- Create: `Assets/Scripts/Data/StoryCardDefinition.cs`

- [ ] **Step 1: Create the ScriptableObject**

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace CardsUnity
{
    [CreateAssetMenu(menuName = "CardsUnity/Story Card Definition", fileName = "NewStoryCard")]
    public class StoryCardDefinition : ScriptableObject
    {
        public string title;
        [TextArea] public string content;
        public Sprite artwork;
        public List<CardEffectTag> effects;
        public int clockMaxValue;
    }
}
```

- [ ] **Step 2: Check Unity console for compilation errors**

Open Unity Editor, check Console window. Expected: no errors.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Data/StoryCardDefinition.cs
git commit -m "Add StoryCardDefinition ScriptableObject"
```

---

## Task 3: `StoryDeckDefinition` ScriptableObject

**Files:**
- Create: `Assets/Scripts/Data/StoryDeckDefinition.cs`

- [ ] **Step 1: Create the ScriptableObject**

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace CardsUnity
{
    [CreateAssetMenu(menuName = "CardsUnity/Story Deck Definition", fileName = "NewStoryDeck")]
    public class StoryDeckDefinition : ScriptableObject
    {
        public List<StoryCardDefinition> cards;
        public StoryDrawMode drawMode;
    }
}
```

- [ ] **Step 2: Check Unity console for compilation errors**

Open Unity Editor, check Console window. Expected: no errors.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Data/StoryDeckDefinition.cs
git commit -m "Add StoryDeckDefinition ScriptableObject"
```

---

## Task 4: `StoryDeckState` runtime class — TDD

**Files:**
- Create: `Assets/Scripts/Data/StoryDeckState.cs`
- Create: `Assets/Scripts/Tests/StoryDeckStateTests.cs`

### Step group A — Sequential initialization

- [ ] **Step 1: Write failing test — constructor sets first card in Sequential mode**

```csharp
using NUnit.Framework;
using UnityEngine;

namespace CardsUnity.Tests
{
    public class StoryDeckStateTests
    {
        private static StoryCardDefinition MakeCard(string title, int clockMax)
        {
            var card = ScriptableObject.CreateInstance<StoryCardDefinition>();
            card.title = title;
            card.clockMaxValue = clockMax;
            return card;
        }

        private static StoryDeckDefinition MakeDeck(StoryDrawMode mode, params StoryCardDefinition[] cards)
        {
            var deck = ScriptableObject.CreateInstance<StoryDeckDefinition>();
            deck.drawMode = mode;
            deck.cards = new System.Collections.Generic.List<StoryCardDefinition>(cards);
            return deck;
        }

        [Test]
        public void Constructor_sequential_sets_first_card_as_active()
        {
            var card1 = MakeCard("A", 3);
            var card2 = MakeCard("B", 5);
            var deckDef = MakeDeck(StoryDrawMode.Sequential, card1, card2);

            var state = new StoryDeckState(deckDef);

            Assert.AreEqual(card1, state.ActiveCard);
        }

        [Test]
        public void Constructor_initializes_clock_with_active_card_clockMaxValue()
        {
            var card = MakeCard("A", 7);
            var deckDef = MakeDeck(StoryDrawMode.Sequential, card);

            var state = new StoryDeckState(deckDef);

            Assert.AreEqual(7, state.ActiveCardClock.MaxValue);
            Assert.AreEqual(0, state.ActiveCardClock.CurrentValue);
        }
    }
}
```

- [ ] **Step 2: Run test to confirm it fails**

```bash
/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath . \
  -runTests -testPlatform EditMode \
  -testResults TestResults/EditMode.xml
```

Expected: FAIL — `StoryDeckState` does not exist yet.

- [ ] **Step 3: Create minimal `StoryDeckState` to make tests pass**

```csharp
using System.Collections.Generic;

namespace CardsUnity
{
    public class StoryDeckState
    {
        private readonly List<StoryCardDefinition> _sequence;
        private int _currentIndex;

        public StoryDeckDefinition Definition { get; }
        public StoryCardDefinition ActiveCard { get; private set; }
        public ClockState ActiveCardClock { get; private set; }

        public StoryDeckState(StoryDeckDefinition definition, System.Random rng = null)
        {
            Definition = definition;
            _sequence = new List<StoryCardDefinition>(definition.cards);

            if (definition.drawMode == StoryDrawMode.Random)
            {
                var r = rng ?? new System.Random();
                for (int i = _sequence.Count - 1; i > 0; i--)
                {
                    int j = r.Next(i + 1);
                    (_sequence[i], _sequence[j]) = (_sequence[j], _sequence[i]);
                }
            }

            _currentIndex = 0;
            SetActiveCard(_currentIndex < _sequence.Count ? _sequence[_currentIndex] : null);
        }

        public void AdvanceCard()
        {
            _currentIndex++;
            SetActiveCard(_currentIndex < _sequence.Count ? _sequence[_currentIndex] : null);
        }

        private void SetActiveCard(StoryCardDefinition card)
        {
            ActiveCard = card;
            ActiveCardClock = card != null ? new ClockState(card.clockMaxValue) : null;
        }
    }
}
```

- [ ] **Step 4: Run tests — confirm the two tests pass**

```bash
/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath . \
  -runTests -testPlatform EditMode \
  -testResults TestResults/EditMode.xml
```

Expected: `Constructor_sequential_sets_first_card_as_active` PASS, `Constructor_initializes_clock_with_active_card_clockMaxValue` PASS.

### Step group B — Sequential AdvanceCard

- [ ] **Step 5: Add tests for AdvanceCard in Sequential mode**

Add these tests to `StoryDeckStateTests`:

```csharp
[Test]
public void AdvanceCard_sequential_moves_to_next_card()
{
    var card1 = MakeCard("A", 3);
    var card2 = MakeCard("B", 5);
    var deckDef = MakeDeck(StoryDrawMode.Sequential, card1, card2);
    var state = new StoryDeckState(deckDef);

    state.AdvanceCard();

    Assert.AreEqual(card2, state.ActiveCard);
}

[Test]
public void AdvanceCard_sequential_resets_clock_to_new_card_max()
{
    var card1 = MakeCard("A", 3);
    var card2 = MakeCard("B", 5);
    var deckDef = MakeDeck(StoryDrawMode.Sequential, card1, card2);
    var state = new StoryDeckState(deckDef);

    state.AdvanceCard();

    Assert.AreEqual(5, state.ActiveCardClock.MaxValue);
    Assert.AreEqual(0, state.ActiveCardClock.CurrentValue);
}

[Test]
public void AdvanceCard_sequential_sets_active_card_null_when_deck_exhausted()
{
    var card = MakeCard("A", 3);
    var deckDef = MakeDeck(StoryDrawMode.Sequential, card);
    var state = new StoryDeckState(deckDef);

    state.AdvanceCard();

    Assert.IsNull(state.ActiveCard);
    Assert.IsNull(state.ActiveCardClock);
}
```

- [ ] **Step 6: Run tests — all should pass (implementation already handles this)**

```bash
/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath . \
  -runTests -testPlatform EditMode \
  -testResults TestResults/EditMode.xml
```

Expected: all 5 tests PASS.

### Step group C — Random mode

- [ ] **Step 7: Add tests for Random mode**

Add these tests to `StoryDeckStateTests`:

```csharp
[Test]
public void Constructor_random_sets_one_of_the_cards_as_active()
{
    var card1 = MakeCard("A", 3);
    var card2 = MakeCard("B", 5);
    var card3 = MakeCard("C", 2);
    var deckDef = MakeDeck(StoryDrawMode.Random, card1, card2, card3);

    var state = new StoryDeckState(deckDef, new System.Random(42));

    Assert.IsTrue(
        state.ActiveCard == card1 ||
        state.ActiveCard == card2 ||
        state.ActiveCard == card3);
}

[Test]
public void AdvanceCard_random_does_not_repeat_cards()
{
    var card1 = MakeCard("A", 1);
    var card2 = MakeCard("B", 1);
    var card3 = MakeCard("C", 1);
    var deckDef = MakeDeck(StoryDrawMode.Random, card1, card2, card3);
    var state = new StoryDeckState(deckDef, new System.Random(42));

    var seen = new System.Collections.Generic.HashSet<StoryCardDefinition>();
    seen.Add(state.ActiveCard);
    state.AdvanceCard();
    seen.Add(state.ActiveCard);
    state.AdvanceCard();
    seen.Add(state.ActiveCard);

    Assert.AreEqual(3, seen.Count);
}

[Test]
public void AdvanceCard_random_sets_active_card_null_when_all_cards_seen()
{
    var card1 = MakeCard("A", 1);
    var card2 = MakeCard("B", 1);
    var deckDef = MakeDeck(StoryDrawMode.Random, card1, card2);
    var state = new StoryDeckState(deckDef, new System.Random(42));

    state.AdvanceCard();
    state.AdvanceCard();

    Assert.IsNull(state.ActiveCard);
}
```

- [ ] **Step 8: Run tests — all should pass**

```bash
/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath . \
  -runTests -testPlatform EditMode \
  -testResults TestResults/EditMode.xml
```

Expected: all 8 tests PASS.

- [ ] **Step 9: Commit**

```bash
git add Assets/Scripts/Data/StoryDeckState.cs Assets/Scripts/Tests/StoryDeckStateTests.cs
git commit -m "Add StoryDeckState with Sequential and Random draw modes"
```

---

## Task 5: `ChallengeController` integration

**Files:**
- Modify: `Assets/Scripts/Controllers/ChallengeController.cs`

- [ ] **Step 1: Add `storyDeck` field and `_storyDeck` state**

In `ChallengeController.cs`, add to the serialized fields block (after `losePanel`):

```csharp
[SerializeField] private StoryDeckDefinition storyDeck;
```

Add to the private fields block (after `_rng`):

```csharp
private StoryDeckState _storyDeck;
```

- [ ] **Step 2: Initialize `_storyDeck` in `Start()`**

At the end of the `Start()` method, before `BeginTurn()`, add:

```csharp
if (storyDeck != null)
    _storyDeck = new StoryDeckState(storyDeck, _rng);
```

- [ ] **Step 3: Add `IncrementStoryClock` public method**

Add this method to `ChallengeController`, after the `Restart()` method:

```csharp
public void IncrementStoryClock(int amount)
{
    if (_storyDeck == null || _storyDeck.ActiveCard == null) return;
    _storyDeck.ActiveCardClock.Increment(amount);
    if (_storyDeck.ActiveCardClock.IsFull)
        _storyDeck.AdvanceCard();
}
```

- [ ] **Step 4: Check Unity console for compilation errors**

Open Unity Editor, check Console. Expected: no errors.

- [ ] **Step 5: Run full EditMode test suite — ensure nothing broke**

```bash
/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath . \
  -runTests -testPlatform EditMode \
  -testResults TestResults/EditMode.xml
```

Expected: all existing tests + 8 new StoryDeckState tests PASS.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Controllers/ChallengeController.cs
git commit -m "Wire StoryDeckState into ChallengeController"
```

---

## Task 6: Update game design doc

**Files:**
- Modify: `docs/game-design-doc.md`

- [ ] **Step 1: Update "Planned but Not Implemented" table**

In the table under `## Implementation Status → Planned but Not Implemented`, change the Story cards row:

From:
```
| Story cards | Global rule-change cards tied to narrative |
```

To:
```
| Story cards — UI | Display active story card (title, content, artwork, clock bar) |
| Story cards — clock wiring | Connect existing cards/effects to IncrementStoryClock |
```

- [ ] **Step 2: Add to "Implemented and Working" section**

Append to the list under `## Implementation Status → Implemented and Working`:

```
- Story card data model: `StoryCardDefinition`, `StoryDeckDefinition` ScriptableObjects with `StoryDrawMode` (Sequential/Random); `StoryDeckState` runtime class with clock-driven `AdvanceCard()`; `ChallengeController.IncrementStoryClock(int)` entry point
```

- [ ] **Step 3: Append changelog entry**

Append to the Changelog table:

```
| 2026-04-30 | Added story card data model: StoryCardDefinition SO, StoryDeckDefinition SO, StoryDeckState runtime class with Sequential/Random draw and per-card ClockState; ChallengeController exposes IncrementStoryClock(int) |
```

- [ ] **Step 4: Commit**

```bash
git add docs/game-design-doc.md
git commit -m "Update design doc: story cards data model implemented"
```
