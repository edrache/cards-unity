# Card Game Prototype — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a fully playable single-challenge card game prototype on Unity Canvas — player draws cards, plays them to board slots, RPS combat resolves automatically, clocks track HP, End Turn advances the loop.

**Architecture:** Pure-C# logic layer (Data + Combat + Controllers) tested with EditMode tests, thin MonoBehaviour UI layer wiring Unity Canvas to the logic. No persistent save state, no scene transitions — one scene, one challenge.

**Tech Stack:** Unity 6 (6000.3.10f1), URP, UGUI Canvas, DOTween (animations), Unity Test Framework (EditMode), ScriptableObjects.

---

## Implementation Status

Last updated: 2026-04-22

| Task | Owner | Status | Notes |
|---|---|---|---|
| Task 1 — Assembly Definitions | James | Done | Verified by Unity MCP EditMode test run. |
| Task 2 — Core Data Types | James | Done | Verified by Unity MCP EditMode test run. |
| Task 3 — Combat Logic with Tests | James | Done | Verified by Unity MCP EditMode test run. |
| Task 4 — Deck, Hand, Board, Clock with Tests | Galileo | Done | Verified by Unity MCP EditMode test run. |
| Task 5 — TurnController with Tests | Socrates | Done | Verified by Unity MCP EditMode test run. |
| Task 6 — UI Scripts + GameConfig | Socrates | Done | Verified by Unity MCP EditMode test run. |
| Task 7 — Card Prefab | Marek | Not started | Requires Unity Editor prefab work after scripts compile. |
| Task 8 — Sample Card ScriptableObjects + GameConfig | Marek | Not started | Requires Unity Editor asset work after scripts compile. |
| Task 9 — Game Scene | Marek | Not started | Requires Unity Editor scene work after scripts compile. |
| Task 10 — First Playtest | Marek | Not started | Depends on Tasks 7–9. |

Latest verification: Unity MCP EditMode test job `e8a424d2c201411fa53b2e4d145af306` passed 27/27 tests on 2026-04-22.

---

## Role Split

Every task is labeled:

- **[AGENT]** — agent writes C# code and commits. Marek does not touch the code.
- **[MAREK]** — Marek works in Unity Editor: creates prefabs, builds UI hierarchy, configures components and ScriptableObjects, decides layout and visuals.
- **[REVIEW]** — Marek presses Play and verifies the result before the next task begins.

Handoffs are explicit. The agent never assumes Marek will write code. Marek never needs to type C# — only attach scripts and fill Inspector fields.

---

## File Map

```
Assets/Scripts/
  CardsUnity.Runtime.asmdef

  Data/
    CardType.cs
    CardDefinition.cs     ← ScriptableObject Marek fills in Unity
    CardEffectTag.cs      ← ScriptableObject Marek fills in Unity
    CardInstance.cs
    DeckState.cs
    HandState.cs
    SlotState.cs
    BoardState.cs
    ClockState.cs

  Config/
    GameConfig.cs         ← ScriptableObject Marek fills in Unity

  Combat/
    BattleOutcome.cs
    CombatResult.cs
    CombatResolver.cs

  Controllers/
    TurnController.cs
    ChallengeController.cs ← MonoBehaviour Marek attaches in Unity

  UI/
    CardView.cs           ← Marek builds the prefab, attaches this script
    HandView.cs           ← Marek builds the UI panel, attaches this script
    SlotView.cs           ← Marek builds each slot, attaches this script
    BoardView.cs          ← Marek builds the board area, attaches this script
    ClockView.cs          ← Marek builds clock UI, attaches this script
    EndTurnButton.cs      ← Marek places button, attaches this script

  Tests/
    CardsUnity.Tests.asmdef
    CombatResolverTests.cs
    DeckStateTests.cs
    TurnControllerTests.cs
```

---

## Task 1 [AGENT] — Assembly Definitions

**Files:**
- Create: `Assets/Scripts/CardsUnity.Runtime.asmdef`
- Create: `Assets/Scripts/Tests/CardsUnity.Tests.asmdef`

- [ ] **Step 1: Create runtime asmdef**

`Assets/Scripts/CardsUnity.Runtime.asmdef`:
```json
{
    "name": "CardsUnity.Runtime",
    "rootNamespace": "CardsUnity",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 2: Create tests asmdef**

`Assets/Scripts/Tests/CardsUnity.Tests.asmdef`:
```json
{
    "name": "CardsUnity.Tests",
    "rootNamespace": "CardsUnity.Tests",
    "references": [
        "CardsUnity.Runtime",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/CardsUnity.Runtime.asmdef Assets/Scripts/Tests/CardsUnity.Tests.asmdef
git commit -m "feat: add assembly definitions for runtime and tests"
```

---

## Task 2 [AGENT] — Core Data Types

**Files:**
- Create: `Assets/Scripts/Data/CardType.cs`
- Create: `Assets/Scripts/Data/CardEffectTag.cs`
- Create: `Assets/Scripts/Data/CardDefinition.cs`
- Create: `Assets/Scripts/Data/CardInstance.cs`

- [ ] **Step 1: CardType enum**

`Assets/Scripts/Data/CardType.cs`:
```csharp
namespace CardsUnity
{
    public enum CardType
    {
        Force,
        Presence,
        Wit
    }
}
```

- [ ] **Step 2: EffectTrigger enum + CardEffectTag ScriptableObject**

`Assets/Scripts/Data/CardEffectTag.cs`:
```csharp
using UnityEngine;

namespace CardsUnity
{
    public enum EffectTrigger
    {
        OnPlay,
        OnEndTurn,
        AfterAttack,
        OnDestroyed
    }

    [CreateAssetMenu(menuName = "CardsUnity/Card Effect Tag", fileName = "NewEffectTag")]
    public class CardEffectTag : ScriptableObject
    {
        public string tagName;
        public EffectTrigger trigger;
        [TextArea] public string description;
    }
}
```

- [ ] **Step 3: CardDefinition ScriptableObject**

`Assets/Scripts/Data/CardDefinition.cs`:
```csharp
using System.Collections.Generic;
using UnityEngine;

namespace CardsUnity
{
    [CreateAssetMenu(menuName = "CardsUnity/Card Definition", fileName = "NewCard")]
    public class CardDefinition : ScriptableObject
    {
        public string title;
        public CardType type;
        public int value;
        public List<CardEffectTag> effects;
        public Sprite artwork;
        [TextArea] public string flavorText;
    }
}
```

- [ ] **Step 4: CardInstance runtime class**

`Assets/Scripts/Data/CardInstance.cs`:
```csharp
namespace CardsUnity
{
    public class CardInstance
    {
        public CardDefinition Definition { get; }
        public int CurrentValue { get; set; }

        public CardInstance(CardDefinition definition)
        {
            Definition = definition;
            CurrentValue = definition.value;
        }
    }
}
```

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Data/
git commit -m "feat: add core card data types (CardType, CardDefinition, CardInstance)"
```

---

## Task 3 [AGENT] — Combat Logic with Tests

**Files:**
- Create: `Assets/Scripts/Combat/BattleOutcome.cs`
- Create: `Assets/Scripts/Combat/CombatResult.cs`
- Create: `Assets/Scripts/Combat/CombatResolver.cs`
- Create: `Assets/Scripts/Tests/CombatResolverTests.cs`

- [ ] **Step 1: Write failing tests**

`Assets/Scripts/Tests/CombatResolverTests.cs`:
```csharp
using NUnit.Framework;
using UnityEngine;

namespace CardsUnity.Tests
{
    public class CombatResolverTests
    {
        [Test] public void Force_vs_Presence_returns_Win() =>
            Assert.AreEqual(BattleOutcome.Win, CombatResolver.DetermineOutcome(CardType.Force, CardType.Presence));

        [Test] public void Presence_vs_Wit_returns_Win() =>
            Assert.AreEqual(BattleOutcome.Win, CombatResolver.DetermineOutcome(CardType.Presence, CardType.Wit));

        [Test] public void Wit_vs_Force_returns_Win() =>
            Assert.AreEqual(BattleOutcome.Win, CombatResolver.DetermineOutcome(CardType.Wit, CardType.Force));

        [Test] public void Force_vs_Wit_returns_Lose() =>
            Assert.AreEqual(BattleOutcome.Lose, CombatResolver.DetermineOutcome(CardType.Force, CardType.Wit));

        [Test] public void Force_vs_Force_returns_Draw() =>
            Assert.AreEqual(BattleOutcome.Draw, CombatResolver.DetermineOutcome(CardType.Force, CardType.Force));

        [Test]
        public void Win_deals_full_card_value_as_damage()
        {
            var attacker = MakeCard(CardType.Force, value: 5);
            var defender = MakeCard(CardType.Presence, value: 10);
            var result = CombatResolver.ResolveCombat(attacker, defender, new System.Random(0));
            Assert.AreEqual(5, result.DamageDealt);
        }

        [Test]
        public void Lose_deals_damage_between_1_and_card_value()
        {
            for (int seed = 0; seed < 20; seed++)
            {
                var a = MakeCard(CardType.Force, value: 6);
                var d = MakeCard(CardType.Wit, value: 10);
                var result = CombatResolver.ResolveCombat(a, d, new System.Random(seed));
                Assert.GreaterOrEqual(result.DamageDealt, 1);
                Assert.LessOrEqual(result.DamageDealt, 6);
            }
        }

        [Test]
        public void Combat_reduces_defender_CurrentValue()
        {
            var attacker = MakeCard(CardType.Force, value: 5);
            var defender = MakeCard(CardType.Presence, value: 10);
            CombatResolver.ResolveCombat(attacker, defender, new System.Random(0));
            Assert.AreEqual(5, defender.CurrentValue);
        }

        [Test]
        public void Combat_sets_Destroyed_when_defender_value_reaches_zero()
        {
            var result = CombatResolver.ResolveCombat(
                MakeCard(CardType.Force, value: 10),
                MakeCard(CardType.Presence, value: 5),
                new System.Random(0));
            Assert.IsTrue(result.Destroyed);
        }

        [Test]
        public void Combat_does_not_set_Destroyed_when_defender_survives()
        {
            var result = CombatResolver.ResolveCombat(
                MakeCard(CardType.Force, value: 3),
                MakeCard(CardType.Presence, value: 10),
                new System.Random(0));
            Assert.IsFalse(result.Destroyed);
        }

        private static CardInstance MakeCard(CardType type, int value)
        {
            var def = ScriptableObject.CreateInstance<CardDefinition>();
            def.type = type;
            def.value = value;
            return new CardInstance(def);
        }
    }
}
```

- [ ] **Step 2: Run tests — expect compile errors (types not yet defined)**

```bash
/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath . \
  -runTests -testPlatform EditMode \
  -testResults TestResults/EditMode.xml
```

Expected: compile error.

- [ ] **Step 3: Implement BattleOutcome**

`Assets/Scripts/Combat/BattleOutcome.cs`:
```csharp
namespace CardsUnity
{
    public enum BattleOutcome { Win, Draw, Lose }
}
```

- [ ] **Step 4: Implement CombatResult**

`Assets/Scripts/Combat/CombatResult.cs`:
```csharp
namespace CardsUnity
{
    public readonly struct CombatResult
    {
        public int DamageDealt { get; }
        public bool Destroyed { get; }

        public CombatResult(int damage, bool destroyed)
        {
            DamageDealt = damage;
            Destroyed = destroyed;
        }
    }
}
```

- [ ] **Step 5: Implement CombatResolver**

`Assets/Scripts/Combat/CombatResolver.cs`:
```csharp
namespace CardsUnity
{
    public static class CombatResolver
    {
        // Force(0) > Presence(1) > Wit(2) > Force(0)
        private static readonly CardType[] Beats = { CardType.Presence, CardType.Wit, CardType.Force };

        public static BattleOutcome DetermineOutcome(CardType attacker, CardType defender)
        {
            if (attacker == defender) return BattleOutcome.Draw;
            return Beats[(int)attacker] == defender ? BattleOutcome.Win : BattleOutcome.Lose;
        }

        public static CombatResult ResolveCombat(CardInstance attacker, CardInstance defender, System.Random rng)
        {
            var outcome = DetermineOutcome(attacker.Definition.type, defender.Definition.type);
            int damage = outcome == BattleOutcome.Win
                ? attacker.CurrentValue
                : rng.Next(1, attacker.CurrentValue + 1);

            defender.CurrentValue -= damage;
            return new CombatResult(damage, destroyed: defender.CurrentValue <= 0);
        }
    }
}
```

- [ ] **Step 6: Run tests — expect 9 PASS**

```bash
/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath . \
  -runTests -testPlatform EditMode \
  -testResults TestResults/EditMode.xml
```

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/Combat/ Assets/Scripts/Tests/CombatResolverTests.cs
git commit -m "feat: combat resolver with RPS logic and damage calculation"
```

---

## Task 4 [AGENT] — Deck, Hand, Board, Clock with Tests

**Files:**
- Create: `Assets/Scripts/Data/DeckState.cs`
- Create: `Assets/Scripts/Data/HandState.cs`
- Create: `Assets/Scripts/Data/SlotState.cs`
- Create: `Assets/Scripts/Data/BoardState.cs`
- Create: `Assets/Scripts/Data/ClockState.cs`
- Create: `Assets/Scripts/Tests/DeckStateTests.cs`

- [ ] **Step 1: Write failing tests**

`Assets/Scripts/Tests/DeckStateTests.cs`:
```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace CardsUnity.Tests
{
    public class DeckStateTests
    {
        [Test]
        public void Draw_returns_card()
        {
            Assert.IsNotNull(MakeDeck(3).Draw());
        }

        [Test]
        public void Draw_reduces_draw_pile_count()
        {
            var deck = MakeDeck(3);
            deck.Draw();
            Assert.AreEqual(2, deck.DrawCount);
        }

        [Test]
        public void Draw_returns_null_when_both_piles_empty()
        {
            var deck = new DeckState();
            deck.Initialize(new List<CardDefinition>());
            Assert.IsNull(deck.Draw());
        }

        [Test]
        public void Discard_adds_to_discard_pile()
        {
            var deck = MakeDeck(2);
            deck.Discard(deck.Draw());
            Assert.AreEqual(1, deck.DiscardCount);
        }

        [Test]
        public void ShuffleDiscard_moves_all_cards_to_draw_pile()
        {
            var deck = MakeDeck(3);
            deck.Discard(deck.Draw());
            deck.Discard(deck.Draw());
            deck.Discard(deck.Draw());
            deck.ShuffleDiscardIntoDrawPile(new System.Random(0));
            Assert.AreEqual(3, deck.DrawCount);
            Assert.AreEqual(0, deck.DiscardCount);
        }

        [Test]
        public void CanDraw_false_when_both_piles_empty()
        {
            var deck = new DeckState();
            deck.Initialize(new List<CardDefinition>());
            Assert.IsFalse(deck.CanDraw);
        }

        [Test]
        public void CanDraw_true_when_only_discard_has_cards()
        {
            var deck = MakeDeck(1);
            deck.Discard(deck.Draw());
            Assert.AreEqual(0, deck.DrawCount);
            Assert.IsTrue(deck.CanDraw);
        }

        private static DeckState MakeDeck(int count)
        {
            var cards = Enumerable.Range(0, count)
                .Select(_ => ScriptableObject.CreateInstance<CardDefinition>())
                .ToList();
            var deck = new DeckState();
            deck.Initialize(cards);
            return deck;
        }
    }
}
```

- [ ] **Step 2: Run tests — expect compile errors**

```bash
/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath . \
  -runTests -testPlatform EditMode \
  -testResults TestResults/EditMode.xml
```

- [ ] **Step 3: Implement DeckState**

`Assets/Scripts/Data/DeckState.cs`:
```csharp
using System.Collections.Generic;

namespace CardsUnity
{
    public class DeckState
    {
        private readonly List<CardDefinition> _drawPile = new();
        private readonly List<CardDefinition> _discardPile = new();

        public int DrawCount => _drawPile.Count;
        public int DiscardCount => _discardPile.Count;
        public bool CanDraw => _drawPile.Count > 0 || _discardPile.Count > 0;

        public void Initialize(IEnumerable<CardDefinition> cards)
        {
            _drawPile.Clear();
            _discardPile.Clear();
            _drawPile.AddRange(cards);
        }

        public CardDefinition Draw()
        {
            if (_drawPile.Count == 0) return null;
            var card = _drawPile[_drawPile.Count - 1];
            _drawPile.RemoveAt(_drawPile.Count - 1);
            return card;
        }

        public void Discard(CardDefinition card) => _discardPile.Add(card);

        public void ShuffleDiscardIntoDrawPile(System.Random rng)
        {
            _drawPile.AddRange(_discardPile);
            _discardPile.Clear();
            for (int i = _drawPile.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (_drawPile[i], _drawPile[j]) = (_drawPile[j], _drawPile[i]);
            }
        }
    }
}
```

- [ ] **Step 4: Implement HandState**

`Assets/Scripts/Data/HandState.cs`:
```csharp
using System.Collections.Generic;

namespace CardsUnity
{
    public class HandState
    {
        private readonly List<CardDefinition> _cards = new();
        public IReadOnlyList<CardDefinition> Cards => _cards;

        public void Add(CardDefinition card) => _cards.Add(card);
        public bool Remove(CardDefinition card) => _cards.Remove(card);
        public void Clear() => _cards.Clear();
    }
}
```

- [ ] **Step 5: Implement SlotState, BoardState, ClockState**

`Assets/Scripts/Data/SlotState.cs`:
```csharp
namespace CardsUnity
{
    public class SlotState
    {
        public CardInstance OpponentCard { get; set; }
        public CardInstance PlayerCard { get; set; }
        public bool HasOpponentCard => OpponentCard != null;
        public bool HasPlayerCard => PlayerCard != null;
    }
}
```

`Assets/Scripts/Data/BoardState.cs`:
```csharp
namespace CardsUnity
{
    public class BoardState
    {
        public SlotState[] Slots { get; }

        public BoardState(int slotCount)
        {
            Slots = new SlotState[slotCount];
            for (int i = 0; i < slotCount; i++)
                Slots[i] = new SlotState();
        }
    }
}
```

`Assets/Scripts/Data/ClockState.cs`:
```csharp
namespace CardsUnity
{
    public class ClockState
    {
        public int MaxValue { get; }
        public int CurrentValue { get; private set; }
        public bool IsFull => CurrentValue >= MaxValue;
        public float Progress => MaxValue > 0 ? (float)CurrentValue / MaxValue : 0f;

        public ClockState(int maxValue) { MaxValue = maxValue; }
        public void Increment(int amount = 1) => CurrentValue += amount;
    }
}
```

- [ ] **Step 6: Run tests — expect 16 PASS (9 combat + 7 deck)**

```bash
/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath . \
  -runTests -testPlatform EditMode \
  -testResults TestResults/EditMode.xml
```

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/Data/ Assets/Scripts/Tests/DeckStateTests.cs
git commit -m "feat: deck, hand, board, slot and clock state"
```

---

## Task 5 [AGENT] — TurnController with Tests

**Files:**
- Create: `Assets/Scripts/Controllers/TurnController.cs`
- Create: `Assets/Scripts/Tests/TurnControllerTests.cs`

- [ ] **Step 1: Write failing tests**

`Assets/Scripts/Tests/TurnControllerTests.cs`:
```csharp
using System.Linq;
using NUnit.Framework;
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
        private ClockState _playerClock;
        private ClockState _opponentClock;

        [SetUp]
        public void SetUp()
        {
            _playerDeck = MakeDeck(6, CardType.Force, value: 3);
            _playerHand = new HandState();
            _opponentDeck = MakeDeck(4, CardType.Presence, value: 4);
            _board = new BoardState(slotCount: 3);
            _playerClock = new ClockState(maxValue: 6);
            _opponentClock = new ClockState(maxValue: 4);

            _turn = new TurnController(
                _playerDeck, _playerHand,
                _opponentDeck, _board,
                _playerClock, _opponentClock,
                draftValue: 3,
                rng: new System.Random(42));
        }

        [Test]
        public void StartTurn_draws_up_to_draft_value()
        {
            _turn.StartTurn();
            Assert.AreEqual(3, _playerHand.Cards.Count);
        }

        [Test]
        public void StartTurn_does_not_exceed_draft_value_when_hand_already_has_cards()
        {
            _playerHand.Add(ScriptableObject.CreateInstance<CardDefinition>());
            _turn.StartTurn();
            Assert.AreEqual(3, _playerHand.Cards.Count);
        }

        [Test]
        public void StartTurn_shuffles_discard_when_draw_pile_empty()
        {
            for (int i = 0; i < 6; i++) _playerDeck.Discard(_playerDeck.Draw());
            _turn.StartTurn();
            Assert.AreEqual(3, _playerHand.Cards.Count);
        }

        [Test]
        public void PlayCard_places_card_in_slot()
        {
            _turn.StartTurn();
            var card = _playerHand.Cards[0];
            _turn.PlayCard(card, slotIndex: 0);
            Assert.AreEqual(card, _board.Slots[0].PlayerCard?.Definition);
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
        public void PlayCard_to_occupied_slot_deals_full_damage_on_win()
        {
            _turn.StartTurn();
            var opponentCard = new CardInstance(MakeCardDef(CardType.Presence, value: 10));
            _board.Slots[1].OpponentCard = opponentCard;

            var playerCard = _playerHand.Cards.First(c => c.type == CardType.Force);
            _turn.PlayCard(playerCard, slotIndex: 1);

            // Force beats Presence → full damage = 3 → 10 - 3 = 7
            Assert.AreEqual(7, opponentCard.CurrentValue);
        }

        [Test]
        public void PlayCard_increments_opponent_clock_when_card_destroyed()
        {
            _turn.StartTurn();
            _board.Slots[0].OpponentCard = new CardInstance(MakeCardDef(CardType.Presence, value: 1));
            var playerCard = _playerHand.Cards.First(c => c.type == CardType.Force);
            _turn.PlayCard(playerCard, slotIndex: 0);
            Assert.AreEqual(1, _opponentClock.CurrentValue);
        }

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
    }
}
```

- [ ] **Step 2: Run tests — expect compile errors**

```bash
/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath . \
  -runTests -testPlatform EditMode \
  -testResults TestResults/EditMode.xml
```

- [ ] **Step 3: Implement TurnController**

`Assets/Scripts/Controllers/TurnController.cs`:
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
        private readonly ClockState _playerClock;
        private readonly ClockState _opponentClock;
        private readonly int _draftValue;
        private readonly Random _rng;

        public TurnController(
            DeckState playerDeck, HandState playerHand,
            DeckState opponentDeck, BoardState board,
            ClockState playerClock, ClockState opponentClock,
            int draftValue, Random rng)
        {
            _playerDeck = playerDeck;
            _playerHand = playerHand;
            _opponentDeck = opponentDeck;
            _board = board;
            _playerClock = playerClock;
            _opponentClock = opponentClock;
            _draftValue = draftValue;
            _rng = rng;
        }

        public void StartTurn()
        {
            int toDraw = _draftValue - _playerHand.Cards.Count;
            for (int i = 0; i < toDraw; i++)
            {
                if (_playerDeck.DrawCount == 0 && _playerDeck.CanDraw)
                    _playerDeck.ShuffleDiscardIntoDrawPile(_rng);
                if (!_playerDeck.CanDraw) break;
                var card = _playerDeck.Draw();
                if (card != null) _playerHand.Add(card);
            }
        }

        public void PlayCard(CardDefinition card, int slotIndex)
        {
            var slot = _board.Slots[slotIndex];
            var instance = new CardInstance(card);
            _playerHand.Remove(card);
            slot.PlayerCard = instance;

            if (slot.HasOpponentCard)
                ResolveSlotCombat(slot);
        }

        public void EndTurn()
        {
            foreach (var slot in _board.Slots)
            {
                if (slot.PlayerCard != null)
                {
                    _playerHand.Add(slot.PlayerCard.Definition);
                    _playerDeck.Discard(slot.PlayerCard.Definition);
                    slot.PlayerCard = null;
                }
            }
            PlaceOpponentCards();
        }

        private void ResolveSlotCombat(SlotState slot)
        {
            var result = CombatResolver.ResolveCombat(slot.PlayerCard, slot.OpponentCard, _rng);
            if (result.Destroyed)
            {
                _opponentClock.Increment();
                slot.OpponentCard = null;
            }
        }

        private void PlaceOpponentCards()
        {
            foreach (var slot in _board.Slots)
            {
                if (!slot.HasOpponentCard && _opponentDeck.CanDraw)
                {
                    if (_opponentDeck.DrawCount == 0)
                        _opponentDeck.ShuffleDiscardIntoDrawPile(_rng);
                    var card = _opponentDeck.Draw();
                    if (card != null) slot.OpponentCard = new CardInstance(card);
                }
            }
        }
    }
}
```

- [ ] **Step 4: Run tests — expect 26 PASS**

```bash
/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath . \
  -runTests -testPlatform EditMode \
  -testResults TestResults/EditMode.xml
```

Expected: 26 tests pass (9 combat + 7 deck + 10 turn).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Controllers/TurnController.cs Assets/Scripts/Tests/TurnControllerTests.cs
git commit -m "feat: turn controller - draw, play, combat, end turn"
```

---

## Task 6 [AGENT] — UI Scripts + GameConfig

Agent writes all C# for UI. Marek will use these scripts in Tasks 7–9 when building prefabs and the scene.

**Files:**
- Create: `Assets/Scripts/Config/GameConfig.cs`
- Create: `Assets/Scripts/UI/CardView.cs`
- Create: `Assets/Scripts/UI/HandView.cs`
- Create: `Assets/Scripts/UI/SlotView.cs`
- Create: `Assets/Scripts/UI/BoardView.cs`
- Create: `Assets/Scripts/UI/ClockView.cs`
- Create: `Assets/Scripts/UI/EndTurnButton.cs`
- Create: `Assets/Scripts/Controllers/ChallengeController.cs`

- [ ] **Step 1: GameConfig**

`Assets/Scripts/Config/GameConfig.cs`:
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
        public List<CardDefinition> startingDeck;
        public List<CardDefinition> opponentDeck;
    }
}
```

- [ ] **Step 2: CardView**

`Assets/Scripts/UI/CardView.cs`:
```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

namespace CardsUnity.UI
{
    public class CardView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI valueText;
        [SerializeField] private TextMeshProUGUI typeText;
        [SerializeField] private Image artworkImage;
        [SerializeField] private Image cardBackground;

        public static readonly Color[] TypeColors =
        {
            new Color(0.85f, 0.3f, 0.3f),  // Force
            new Color(0.3f, 0.7f, 0.85f),  // Presence
            new Color(0.3f, 0.85f, 0.5f),  // Wit
        };

        public CardDefinition Card { get; private set; }
        public System.Action<CardView> OnDragStart;
        public System.Action<CardView> OnDragEnd;

        private Transform _originalParent;
        private int _originalSiblingIndex;
        private Canvas _rootCanvas;

        private void Awake() =>
            _rootCanvas = GetComponentInParent<Canvas>(includeInactive: true);

        public void SetCard(CardDefinition card)
        {
            Card = card;
            if (titleText) titleText.text = card.title;
            if (valueText) valueText.text = card.value.ToString();
            if (typeText) typeText.text = card.type.ToString();
            if (artworkImage && card.artwork) artworkImage.sprite = card.artwork;
            if (cardBackground) cardBackground.color = TypeColors[(int)card.type];
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _originalParent = transform.parent;
            _originalSiblingIndex = transform.GetSiblingIndex();
            transform.SetParent(_rootCanvas.transform, worldPositionStays: true);
            transform.SetAsLastSibling();
            OnDragStart?.Invoke(this);
        }

        public void OnDrag(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToWorldPointInRectangle(
                _rootCanvas.GetComponent<RectTransform>(),
                eventData.position, _rootCanvas.worldCamera, out var worldPos);
            transform.position = worldPos;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            transform.SetParent(_originalParent, worldPositionStays: true);
            transform.SetSiblingIndex(_originalSiblingIndex);
            OnDragEnd?.Invoke(this);
        }
    }
}
```

- [ ] **Step 3: HandView**

`Assets/Scripts/UI/HandView.cs`:
```csharp
using System.Collections.Generic;
using UnityEngine;

namespace CardsUnity.UI
{
    public class HandView : MonoBehaviour
    {
        [SerializeField] private CardView cardPrefab;
        [SerializeField] private Transform cardContainer;

        public System.Action<CardView> OnCardDragStart;
        public System.Action<CardView> OnCardDragEnd;

        private readonly List<CardView> _views = new();

        public void Refresh(HandState hand)
        {
            foreach (var v in _views) Destroy(v.gameObject);
            _views.Clear();

            foreach (var card in hand.Cards)
            {
                var view = Instantiate(cardPrefab, cardContainer);
                view.SetCard(card);
                view.OnDragStart += cv => OnCardDragStart?.Invoke(cv);
                view.OnDragEnd += cv => OnCardDragEnd?.Invoke(cv);
                _views.Add(view);
            }
        }
    }
}
```

- [ ] **Step 4: SlotView**

`Assets/Scripts/UI/SlotView.cs`:
```csharp
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace CardsUnity.UI
{
    public class SlotView : MonoBehaviour, IDropHandler
    {
        [SerializeField] private Transform playerCardAnchor;
        [SerializeField] private Transform opponentCardAnchor;
        [SerializeField] private Image highlightImage;

        public int SlotIndex { get; set; }
        public System.Action<CardView, int> OnCardDropped;

        private CardView _opponentView;

        public void ShowOpponentCard(CardInstance card, CardView prefab)
        {
            if (_opponentView) Destroy(_opponentView.gameObject);
            _opponentView = Instantiate(prefab, opponentCardAnchor);
            _opponentView.SetCard(card.Definition);
        }

        public void UpdateOpponentValue(CardInstance card)
        {
            if (_opponentView) _opponentView.SetCard(card.Definition);
        }

        public void ClearOpponentCard()
        {
            if (_opponentView) Destroy(_opponentView.gameObject);
            _opponentView = null;
        }

        public void SetHighlight(bool active)
        {
            if (highlightImage) highlightImage.enabled = active;
        }

        public void OnDrop(PointerEventData eventData)
        {
            var dragged = eventData.pointerDrag?.GetComponent<CardView>();
            if (dragged != null) OnCardDropped?.Invoke(dragged, SlotIndex);
        }
    }
}
```

- [ ] **Step 5: BoardView**

`Assets/Scripts/UI/BoardView.cs`:
```csharp
using UnityEngine;

namespace CardsUnity.UI
{
    public class BoardView : MonoBehaviour
    {
        [SerializeField] private SlotView[] slotViews;
        [SerializeField] private CardView opponentCardPrefab;

        public System.Action<CardDefinition, int> OnCardPlayed;

        private void Awake()
        {
            for (int i = 0; i < slotViews.Length; i++)
            {
                int idx = i;
                slotViews[idx].SlotIndex = idx;
                slotViews[idx].OnCardDropped += (cv, si) => OnCardPlayed?.Invoke(cv.Card, si);
            }
        }

        public void Refresh(BoardState board)
        {
            for (int i = 0; i < slotViews.Length && i < board.Slots.Length; i++)
            {
                var slot = board.Slots[i];
                if (slot.HasOpponentCard)
                    slotViews[i].ShowOpponentCard(slot.OpponentCard, opponentCardPrefab);
                else
                    slotViews[i].ClearOpponentCard();
            }
        }

        public void SetHighlightAll(bool active)
        {
            foreach (var sv in slotViews) sv.SetHighlight(active);
        }
    }
}
```

- [ ] **Step 6: ClockView**

`Assets/Scripts/UI/ClockView.cs`:
```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CardsUnity.UI
{
    public class ClockView : MonoBehaviour
    {
        [SerializeField] private Image fillImage;
        [SerializeField] private TextMeshProUGUI label;

        public void Refresh(ClockState clock)
        {
            if (fillImage) fillImage.fillAmount = clock.Progress;
            if (label) label.text = $"{clock.CurrentValue} / {clock.MaxValue}";
        }
    }
}
```

- [ ] **Step 7: EndTurnButton**

`Assets/Scripts/UI/EndTurnButton.cs`:
```csharp
using UnityEngine;
using UnityEngine.UI;

namespace CardsUnity.UI
{
    public class EndTurnButton : MonoBehaviour
    {
        public System.Action OnClicked;

        private void Awake() =>
            GetComponent<Button>().onClick.AddListener(() => OnClicked?.Invoke());
    }
}
```

- [ ] **Step 8: ChallengeController**

`Assets/Scripts/Controllers/ChallengeController.cs`:
```csharp
using System.Collections.Generic;
using UnityEngine;
using CardsUnity.UI;

namespace CardsUnity
{
    public class ChallengeController : MonoBehaviour
    {
        [SerializeField] private GameConfig config;
        [SerializeField] private BoardView boardView;
        [SerializeField] private HandView handView;
        [SerializeField] private ClockView playerClockView;
        [SerializeField] private ClockView opponentClockView;
        [SerializeField] private EndTurnButton endTurnButton;
        [SerializeField] private GameObject winPanel;
        [SerializeField] private GameObject losePanel;

        private TurnController _turn;
        private HandState _hand;
        private BoardState _board;
        private ClockState _playerClock;
        private ClockState _opponentClock;

        private void Start()
        {
            var rng = new System.Random();

            var playerDeck = BuildDeck(config.startingDeck, rng);
            var opponentDeck = BuildDeck(config.opponentDeck, rng);

            _hand = new HandState();
            _board = new BoardState(config.boardSlotCount);
            _playerClock = new ClockState(config.startingDeck.Count);
            _opponentClock = new ClockState(config.opponentDeck.Count);

            _turn = new TurnController(
                playerDeck, _hand, opponentDeck, _board,
                _playerClock, _opponentClock,
                config.draftValue, rng);

            boardView.OnCardPlayed += OnCardPlayed;
            handView.OnCardDragStart += _ => boardView.SetHighlightAll(true);
            handView.OnCardDragEnd += _ => boardView.SetHighlightAll(false);
            endTurnButton.OnClicked += OnEndTurn;

            if (winPanel) winPanel.SetActive(false);
            if (losePanel) losePanel.SetActive(false);

            BeginTurn();
        }

        private static DeckState BuildDeck(List<CardDefinition> cards, System.Random rng)
        {
            var deck = new DeckState();
            deck.Initialize(cards);
            deck.ShuffleDiscardIntoDrawPile(rng);
            return deck;
        }

        private void BeginTurn()
        {
            _turn.StartTurn();
            RefreshUI();
        }

        private void OnCardPlayed(CardDefinition card, int slotIndex)
        {
            _turn.PlayCard(card, slotIndex);
            RefreshUI();
            CheckEndCondition();
        }

        private void OnEndTurn()
        {
            _turn.EndTurn();
            RefreshUI();
            CheckEndCondition();
            if (!IsGameOver()) BeginTurn();
        }

        private void RefreshUI()
        {
            handView.Refresh(_hand);
            boardView.Refresh(_board);
            playerClockView.Refresh(_playerClock);
            opponentClockView.Refresh(_opponentClock);
        }

        private void CheckEndCondition()
        {
            if (_opponentClock.IsFull && winPanel) winPanel.SetActive(true);
            if (_playerClock.IsFull && losePanel) losePanel.SetActive(true);
        }

        private bool IsGameOver() => _playerClock.IsFull || _opponentClock.IsFull;

        public void Restart() =>
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }
}
```

- [ ] **Step 9: Commit all UI scripts**

```bash
git add Assets/Scripts/Config/ Assets/Scripts/UI/ Assets/Scripts/Controllers/
git commit -m "feat: UI MonoBehaviours and ChallengeController"
```

---

## Task 7 [MAREK] — Card Prefab

Marek builds the card prefab that will be used for both player cards and opponent cards. The visual design and layout is entirely up to Marek — the scripts only need the named fields filled in.

**What to build in Unity Editor:**

- [ ] **Step 1: Create sample card artwork (optional)**

If you have artwork, import it to `Assets/Art/Cards/`. If not, use colored solid squares for now (Image with a solid color is fine).

- [ ] **Step 2: Create the Card prefab structure**

In the Hierarchy, right-click → UI → Image. Rename it `CardPrefab`. This is the root of the card.

Suggested layout (you decide exact sizes and positions):
```
CardPrefab  [Image — cardBackground, RectTransform ~120×180]
  ├── TypeStripe  [Image — thin colored strip at top, optional]
  ├── TitleText  [TextMeshPro — card name, top area]
  ├── Artwork  [Image — middle area, shows artwork sprite]
  ├── ValueText  [TextMeshPro — big number, center or bottom-center]
  └── TypeText  [TextMeshPro — type label, small, bottom]
```

You can rearrange, add borders, shadows (TrueShadow), or any visual elements you want. The script only cares about the named references.

- [ ] **Step 3: Attach CardView script**

Select the root `CardPrefab` object. In the Inspector → Add Component → `CardView`.

Fill in the serialized fields:
| Field | Which child object |
|---|---|
| Title Text | the TextMeshPro with the card name |
| Value Text | the TextMeshPro with the number |
| Type Text | the TextMeshPro with the type label |
| Artwork Image | the Image in the middle |
| Card Background | the root Image component |

- [ ] **Step 4: Save as prefab**

Create folder `Assets/Prefabs/`. Drag `CardPrefab` from Hierarchy into that folder → "Original Prefab". Delete from Hierarchy.

- [ ] **Step 5: Verify in Play Mode (quick test)**

Temporarily add a `CardPrefab` to the scene, add a small test script or just use the CardView's `SetCard` via the Inspector to confirm text and color show correctly. Then remove it.

---

## Task 8 [MAREK] — Sample Card ScriptableObjects + GameConfig

Marek creates the actual card data in Unity Editor.

- [ ] **Step 1: Create card folders**

In Project window: `Assets/Resources/Cards/`

- [ ] **Step 2: Create 3 card definitions**

Right-click → Create → CardsUnity → Card Definition:

| Asset name | title | type | value | flavorText |
|---|---|---|---|---|
| `Card_Force_01` | "Bold Move" | Force | 4 | "Go in hard." |
| `Card_Presence_01` | "Sweet Talk" | Presence | 3 | "Charm wins." |
| `Card_Wit_01` | "Maneuver" | Wit | 5 | "Stay sharp." |

Create 2–3 copies of each (duplicate with Ctrl+D, adjust names/values slightly) for deck variety.

- [ ] **Step 3: Create GameConfig**

Create folder `Assets/Resources/Config/`.

Right-click → Create → CardsUnity → Game Config → name `DefaultGameConfig`.

Fill in:
- draftValue: **3**
- boardSlotCount: **3**
- startingDeck: add your card assets (6–9 total, mix of types)
- opponentDeck: add a different mix (4–6 cards, heavier on one type for testing)

- [ ] **Step 4: Commit**

```bash
git add Assets/Resources/
git commit -m "feat: sample card ScriptableObjects and GameConfig"
```

---

## Task 9 [MAREK] — Game Scene

Marek builds the full scene layout. There's no code to write — only Unity Editor work.

**What the scene needs:**

- [ ] **Step 1: Create the scene**

File → New Scene → Basic (URP). Save as `Assets/Scenes/Game.unity`.

- [ ] **Step 2: Canvas setup**

In Hierarchy: right-click → UI → Canvas.

Inspector settings:
- Render Mode: **Screen Space — Overlay**
- UI Scale Mode: **Scale With Screen Size**, Reference Resolution: **1920 × 1080**

An EventSystem is created automatically — leave it.

- [ ] **Step 3: Build the UI hierarchy**

Create this structure inside the Canvas (use right-click → UI → Panel / Image / Button as needed):

```
Canvas
  ├── OpponentArea  [top 1/3 of screen]
  │   └── OpponentClock  [Image with Image Type: Filled, Method: Horizontal]
  │       └── ClockLabel  [TextMeshPro]
  │
  ├── Board  [middle of screen, horizontal layout]
  │   ├── Slot_0  [Panel or Image, ~150×250]
  │   │   ├── OpponentAnchor  [empty RectTransform, top half of slot]
  │   │   ├── PlayerAnchor   [empty RectTransform, bottom half of slot]
  │   │   └── Highlight      [Image, semi-transparent yellow, disabled by default]
  │   ├── Slot_1  [same structure]
  │   └── Slot_2  [same structure]
  │
  ├── Hand  [bottom 1/4 of screen, horizontal layout]
  │   └── CardContainer  [Horizontal Layout Group]
  │
  ├── HUD
  │   ├── PlayerClock  [Image with Image Type: Filled, Method: Horizontal]
  │   │   └── ClockLabel  [TextMeshPro]
  │   └── EndTurnButton  [Button]
  │       └── Label  [TextMeshPro: "End Turn"]
  │
  ├── WinPanel  [full-screen overlay, green tint, disabled]
  │   ├── WinText  [TextMeshPro: "You Win!"]
  │   └── RestartButton  [Button: "Play Again"]
  │
  └── LosePanel  [full-screen overlay, red tint, disabled]
      ├── LoseText  [TextMeshPro: "You Lose"]
      └── RestartButton  [Button: "Play Again"]
```

The visual style (colors, sizes, fonts, spacing) is entirely your decision.

- [ ] **Step 4: Attach scripts to the objects**

| Object | Script to attach | Notes |
|---|---|---|
| Each Slot_N | `SlotView` | Fill: PlayerCardAnchor, OpponentCardAnchor, HighlightImage |
| Board | `BoardView` | Fill: SlotViews array (drag all 3), OpponentCardPrefab (drag CardPrefab asset) |
| Hand | `HandView` | Fill: CardPrefab (drag CardPrefab asset), CardContainer |
| PlayerClock | `ClockView` | Fill: FillImage, Label |
| OpponentClock | `ClockView` | Fill: FillImage, Label |
| EndTurnButton | `EndTurnButton` | (no fields) |

- [ ] **Step 5: Create the GameController object**

In Hierarchy: right-click → Create Empty → rename `GameController`.

Attach `ChallengeController` component. Fill in all Inspector fields:

| Field | Object |
|---|---|
| Config | `DefaultGameConfig` asset |
| Board View | `Board` |
| Hand View | `Hand` |
| Player Clock View | `PlayerClock` |
| Opponent Clock View | `OpponentClock` |
| End Turn Button | `EndTurnButton` |
| Win Panel | `WinPanel` |
| Lose Panel | `LosePanel` |

- [ ] **Step 6: Wire the Restart buttons**

Select `WinPanel/RestartButton` → Inspector → Button → OnClick → drag `GameController` → choose `ChallengeController.Restart`.

Repeat for `LosePanel/RestartButton`.

- [ ] **Step 7: Commit**

```bash
git add Assets/Scenes/
git commit -m "feat: Game scene layout with board, hand, clocks and HUD"
```

---

## Task 10 [REVIEW] — First Playtest

- [ ] **Step 1: Press Play in Unity Editor**

Expected behavior:
- 3 cards appear in the hand area at the bottom, each showing title / type color / value
- Opponent cards appear in some slots
- Clock labels show `0 / N`

- [ ] **Step 2: Drag a card from hand to an empty slot**

Expected: card appears in the slot's PlayerAnchor. Hand updates (card removed).

- [ ] **Step 3: Drag a card onto a slot that has an opponent card**

Expected: opponent card's value decreases. If it reaches 0, it disappears and the OpponentClock increments.

- [ ] **Step 4: Click End Turn**

Expected: played cards return to hand, opponent deck fills empty slots, new cards drawn up to 3.

- [ ] **Step 5: Play until a clock fills**

Expected: Win or Lose panel appears with a "Play Again" button that restarts the scene.

- [ ] **Step 6: Note any issues**

Write down anything that looks or feels wrong. Bring those notes to the next session for fixes.

---

## Coverage Checklist

| Requirement from pre-design | Status |
|---|---|
| Card fields: type, value, title, effects, artwork, flavor text | ✅ Task 2 |
| 3 card types with RPS | ✅ Task 3 |
| Effects as tags with trigger moments | ⏳ Data model only — runtime dispatch in next phase |
| Player deck | ✅ Task 4 |
| 2 clocks per challenge | ✅ Task 4 |
| Clock = number of cards (HP) | ✅ Task 6 |
| Draft draw mechanic | ✅ Task 5 |
| Shuffle discard when draw empty | ✅ Task 4 |
| Board slots | ✅ Task 4 |
| Slot effects | ⏳ Data model only — runtime dispatch in next phase |
| Opponent card per slot | ✅ Task 5 |
| RPS combat on card play | ✅ Task 3 |
| Win = full damage / Lose or Draw = random damage | ✅ Task 3 |
| Card destroyed at ≤0 value | ✅ Task 3 |
| End Turn button | ✅ Task 6 |
| Cards return to hand on end turn | ✅ Task 5 |
| Opponent cards fill empty slots on end turn | ✅ Task 5 |
| Canvas-based rendering | ✅ Task 9 |
| Cards as prefab + ScriptableObjects | ✅ Tasks 7–8 |
| Player deck development (add/remove cards) | ⏳ Next phase |
| Player clock increment (player loses cards) | ⏳ Next phase |
