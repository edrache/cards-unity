# Slot Effects System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a data-driven slot effect system — slots optionally host opponent/player cards, carry up to three typed effects (NoOpponentCard, NoPlayerCard, Passive) evaluated at turn start or on card play, and display effect descriptions via TextMeshPro labels in SlotView.

**Architecture:** New ScriptableObjects (`SlotEffectDefinition`, `SlotDefinition`) define slot behaviour; `SlotState` carries an optional `SlotDefinition`; `TurnController` evaluates effects per trigger using a switch dispatch; `SlotView` gains three `TextMeshProUGUI` serialized fields shown/hidden based on definition content.

**Tech Stack:** Unity 6 (6000.3.10f1), C#, NUnit (Unity Test Framework), TextMeshPro UGUI

---

## File Map

| Action | Path | Responsibility |
|--------|------|----------------|
| Create | `Assets/Scripts/Data/SlotEffectContext.cs` | Enum: condition that must hold for effect to fire |
| Create | `Assets/Scripts/Data/SlotEffectTrigger.cs` | Enum: timing of effect evaluation |
| Create | `Assets/Scripts/Data/SlotEffectAction.cs` | Enum: what happens when effect fires |
| Create | `Assets/Scripts/Data/ClockTarget.cs` | Enum: which clock AddToClock modifies |
| Create | `Assets/Scripts/Data/SlotEffectDefinition.cs` | ScriptableObject: one effect entry (context + trigger + action + params + description) |
| Create | `Assets/Scripts/Data/SlotDefinition.cs` | ScriptableObject: slot config (card spots + list of effects) |
| Modify | `Assets/Scripts/Data/SlotState.cs` | Add optional `Definition` property |
| Modify | `Assets/Scripts/Data/BoardState.cs` | Add `BoardState(SlotDefinition[])` constructor |
| Modify | `Assets/Scripts/Config/GameConfig.cs` | Add optional `slotDefinitions` list |
| Modify | `Assets/Scripts/Controllers/TurnController.cs` | Add effect evaluation methods; limit `PlaceOpponentCards` to definition-less slots |
| Modify | `Assets/Scripts/UI/SlotView.cs` | Add three TMP labels + `SetEffectDescriptions(SlotDefinition)` |
| Modify | `Assets/Scripts/UI/BoardView.cs` | Add `ConfigureSlots(SlotDefinition[])` |
| Modify | `Assets/Scripts/Controllers/ChallengeController.cs` | Wire slot definitions from config to board and view |
| Modify | `Assets/Scripts/Tests/TurnControllerTests.cs` | Add tests for each effect action |
| Modify | `docs/game-design-doc.md` | Document slot effect system and append changelog entry |

---

## Task 1: Effect Enums and SlotEffectDefinition ScriptableObject

**Files:**
- Create: `Assets/Scripts/Data/SlotEffectContext.cs`
- Create: `Assets/Scripts/Data/SlotEffectTrigger.cs`
- Create: `Assets/Scripts/Data/SlotEffectAction.cs`
- Create: `Assets/Scripts/Data/ClockTarget.cs`
- Create: `Assets/Scripts/Data/SlotEffectDefinition.cs`

- [ ] **Step 1: Create SlotEffectContext enum**

```csharp
// Assets/Scripts/Data/SlotEffectContext.cs
namespace CardsUnity
{
    public enum SlotEffectContext
    {
        NoOpponentCard,
        NoPlayerCard,
        Passive
    }
}
```

- [ ] **Step 2: Create SlotEffectTrigger enum**

```csharp
// Assets/Scripts/Data/SlotEffectTrigger.cs
namespace CardsUnity
{
    public enum SlotEffectTrigger
    {
        OnPlayerTurnStart,
        OnCardPlayed
    }
}
```

- [ ] **Step 3: Create SlotEffectAction enum**

```csharp
// Assets/Scripts/Data/SlotEffectAction.cs
namespace CardsUnity
{
    public enum SlotEffectAction
    {
        DrawOpponentCard,
        AddToClock,
        ReturnCardsFromSlots,
        DrawToHandLimit
    }
}
```

- [ ] **Step 4: Create ClockTarget enum**

```csharp
// Assets/Scripts/Data/ClockTarget.cs
namespace CardsUnity
{
    public enum ClockTarget
    {
        PlayerClock,
        OpponentClock
    }
}
```

- [ ] **Step 5: Create SlotEffectDefinition ScriptableObject**

```csharp
// Assets/Scripts/Data/SlotEffectDefinition.cs
using UnityEngine;

namespace CardsUnity
{
    [CreateAssetMenu(menuName = "CardsUnity/Slot Effect Definition", fileName = "NewSlotEffect")]
    public class SlotEffectDefinition : ScriptableObject
    {
        public SlotEffectContext context;
        public SlotEffectTrigger trigger;
        public SlotEffectAction action;
        public int actionValue;
        public ClockTarget clockTarget;
        [TextArea] public string description;
    }
}
```

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Data/SlotEffectContext.cs \
        Assets/Scripts/Data/SlotEffectTrigger.cs \
        Assets/Scripts/Data/SlotEffectAction.cs \
        Assets/Scripts/Data/ClockTarget.cs \
        Assets/Scripts/Data/SlotEffectDefinition.cs
git commit -m "feat: add slot effect enums and SlotEffectDefinition ScriptableObject"
```

---

## Task 2: SlotDefinition ScriptableObject

**Files:**
- Create: `Assets/Scripts/Data/SlotDefinition.cs`

- [ ] **Step 1: Create SlotDefinition**

```csharp
// Assets/Scripts/Data/SlotDefinition.cs
using System.Collections.Generic;
using UnityEngine;

namespace CardsUnity
{
    [CreateAssetMenu(menuName = "CardsUnity/Slot Definition", fileName = "NewSlot")]
    public class SlotDefinition : ScriptableObject
    {
        public bool hasOpponentCardSpot = true;
        public bool hasPlayerCardSpot = true;
        public List<SlotEffectDefinition> effects = new List<SlotEffectDefinition>();
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Assets/Scripts/Data/SlotDefinition.cs
git commit -m "feat: add SlotDefinition ScriptableObject"
```

---

## Task 3: Update SlotState and BoardState

**Files:**
- Modify: `Assets/Scripts/Data/SlotState.cs`
- Modify: `Assets/Scripts/Data/BoardState.cs`

No test changes — existing tests use `BoardState(int)` which creates slots with `null` Definition. Null Definition means the slot uses legacy default behaviour (PlaceOpponentCards at EndTurn).

- [ ] **Step 1: Update SlotState — add Definition property and new constructor**

Full file after edit:

```csharp
// Assets/Scripts/Data/SlotState.cs
namespace CardsUnity
{
    public class SlotState
    {
        public SlotDefinition Definition { get; }
        public CardInstance OpponentCard { get; set; }
        public CardInstance PlayerCard { get; set; }

        public bool HasOpponentCard => OpponentCard != null;
        public bool HasPlayerCard => PlayerCard != null;

        public SlotState() { }

        public SlotState(SlotDefinition definition)
        {
            Definition = definition;
        }
    }
}
```

- [ ] **Step 2: Update BoardState — add SlotDefinition[] constructor**

Full file after edit:

```csharp
// Assets/Scripts/Data/BoardState.cs
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

        public BoardState(SlotDefinition[] definitions)
        {
            Slots = new SlotState[definitions.Length];
            for (int i = 0; i < definitions.Length; i++)
                Slots[i] = new SlotState(definitions[i]);
        }
    }
}
```

- [ ] **Step 3: Run existing tests to confirm no regressions**

```bash
/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath . \
  -runTests -testPlatform EditMode \
  -testResults TestResults/EditMode.xml
```

Expected: all pre-existing tests PASS.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Data/SlotState.cs \
        Assets/Scripts/Data/BoardState.cs
git commit -m "feat: extend SlotState and BoardState to support SlotDefinition"
```

---

## Task 4: Update GameConfig, BoardView, and ChallengeController

**Files:**
- Modify: `Assets/Scripts/Config/GameConfig.cs`
- Modify: `Assets/Scripts/UI/BoardView.cs`
- Modify: `Assets/Scripts/Controllers/ChallengeController.cs`

- [ ] **Step 1: Add slotDefinitions list to GameConfig**

Full file after edit:

```csharp
// Assets/Scripts/Config/GameConfig.cs
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
    }
}
```

- [ ] **Step 2: Add ConfigureSlots to BoardView**

Add this method to `BoardView` (inside the class, after `SetHighlightAll`):

```csharp
public void ConfigureSlots(SlotDefinition[] definitions)
{
    if (slotViews == null || definitions == null)
        return;

    int count = Math.Min(slotViews.Length, definitions.Length);
    for (int i = 0; i < count; i++)
    {
        if (slotViews[i] != null)
            slotViews[i].SetEffectDescriptions(definitions[i]);
    }
}
```

(`SlotView.SetEffectDescriptions` is added in Task 6; the code compiles after that task.)

- [ ] **Step 3: Update ChallengeController.Start to use slotDefinitions**

Replace the line `_board = new BoardState(config.boardSlotCount);` with:

```csharp
bool hasDefinitions = config.slotDefinitions != null && config.slotDefinitions.Count > 0;
_board = hasDefinitions
    ? new BoardState(config.slotDefinitions.ToArray())
    : new BoardState(config.boardSlotCount);
```

Add slot view configuration immediately before `BeginTurn()`:

```csharp
if (hasDefinitions)
    boardView.ConfigureSlots(config.slotDefinitions.ToArray());
```

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Config/GameConfig.cs \
        Assets/Scripts/UI/BoardView.cs \
        Assets/Scripts/Controllers/ChallengeController.cs
git commit -m "feat: wire SlotDefinition list from GameConfig into board and view"
```

---

## Task 5: TurnController Slot Effect Evaluation (TDD)

**Files:**
- Modify: `Assets/Scripts/Tests/TurnControllerTests.cs` (tests first)
- Modify: `Assets/Scripts/Controllers/TurnController.cs` (implementation second)

### Step group A — write failing tests

- [ ] **Step 1: Add helper MakeSlotDefinition to TurnControllerTests**

Add these private helpers at the bottom of the `TurnControllerTests` class (before the closing brace):

```csharp
private static SlotDefinition MakeSlotDefinition(
    SlotEffectContext context,
    SlotEffectTrigger trigger,
    SlotEffectAction action,
    int actionValue = 1,
    ClockTarget clockTarget = ClockTarget.PlayerClock)
{
    var effect = ScriptableObject.CreateInstance<SlotEffectDefinition>();
    effect.context = context;
    effect.trigger = trigger;
    effect.action = action;
    effect.actionValue = actionValue;
    effect.clockTarget = clockTarget;

    var def = ScriptableObject.CreateInstance<SlotDefinition>();
    def.effects = new System.Collections.Generic.List<SlotEffectDefinition> { effect };
    return def;
}

private TurnController MakeTurnWithBoard(BoardState board)
{
    return new TurnController(
        _playerDeck, _playerHand,
        _opponentDeck, board,
        _playerClock, _opponentClock,
        draftValue: 3,
        rng: new System.Random(42));
}
```

- [ ] **Step 2: Add test — DrawOpponentCard fills empty slot at turn start**

```csharp
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
```

- [ ] **Step 3: Add test — DrawOpponentCard does not replace existing opponent card**

```csharp
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
```

- [ ] **Step 4: Add test — AddToClock increments player clock when no player card**

```csharp
[Test]
public void StartTurn_AddToClock_increments_player_clock_when_no_player_card()
{
    var def = MakeSlotDefinition(
        SlotEffectContext.NoPlayerCard,
        SlotEffectTrigger.OnPlayerTurnStart,
        SlotEffectAction.AddToClock,
        actionValue: 2,
        clockTarget: ClockTarget.PlayerClock);
    var board = new BoardState(new SlotDefinition[] { def, null, null });
    var turn = MakeTurnWithBoard(board);

    turn.StartTurn();

    Assert.AreEqual(2, _playerClock.CurrentValue);
}
```

- [ ] **Step 5: Add test — AddToClock does NOT fire when player card is present**

```csharp
[Test]
public void StartTurn_AddToClock_does_not_fire_when_player_card_present()
{
    var def = MakeSlotDefinition(
        SlotEffectContext.NoPlayerCard,
        SlotEffectTrigger.OnPlayerTurnStart,
        SlotEffectAction.AddToClock,
        actionValue: 2,
        clockTarget: ClockTarget.PlayerClock);
    var board = new BoardState(new SlotDefinition[] { def, null, null });
    board.Slots[0].PlayerCard = new CardInstance(MakeCardDef(CardType.Force, 3));
    var turn = MakeTurnWithBoard(board);

    turn.StartTurn();

    Assert.AreEqual(0, _playerClock.CurrentValue);
}
```

- [ ] **Step 6: Add test — ReturnCardsFromSlots returns all board cards to hand**

```csharp
[Test]
public void PlayCard_ReturnCardsFromSlots_returns_all_board_cards_to_hand()
{
    var drawSlotDef = MakeSlotDefinition(
        SlotEffectContext.Passive,
        SlotEffectTrigger.OnCardPlayed,
        SlotEffectAction.ReturnCardsFromSlots);
    var board = new BoardState(new SlotDefinition[] { null, null, drawSlotDef });
    // manually place a card in slot 0 (simulating a card already on board)
    board.Slots[0].PlayerCard = new CardInstance(MakeCardDef(CardType.Force, 3));
    var turn = MakeTurnWithBoard(board);

    turn.StartTurn(); // draws 3 cards
    var handCard = _playerHand.Cards[0];
    turn.PlayCard(handCard, slotIndex: 2);

    // slot 0 card returned, slot 2 played card returned
    Assert.IsNull(board.Slots[0].PlayerCard);
    Assert.IsNull(board.Slots[2].PlayerCard);
    // hand: 2 remaining + 1 from slot 0 + 1 played card returned = 4
    Assert.AreEqual(4, _playerHand.Cards.Count);
}
```

- [ ] **Step 7: Add test — DrawToHandLimit draws cards up to draft value**

```csharp
[Test]
public void PlayCard_DrawToHandLimit_draws_up_to_draft_value()
{
    var drawSlotDef = MakeSlotDefinition(
        SlotEffectContext.Passive,
        SlotEffectTrigger.OnCardPlayed,
        SlotEffectAction.DrawToHandLimit);
    var board = new BoardState(new SlotDefinition[] { null, null, drawSlotDef });
    var turn = MakeTurnWithBoard(board);

    turn.StartTurn(); // draws 3 cards
    // consume 2 cards to lower hand count
    var card0 = _playerHand.Cards[0];
    var card1 = _playerHand.Cards[1];
    turn.PlayCard(card0, slotIndex: 0);
    turn.PlayCard(card1, slotIndex: 1);
    // hand now has 1 card

    var handCard = _playerHand.Cards[0];
    turn.PlayCard(handCard, slotIndex: 2); // plays to draw slot

    // hand had 0 after playing, DrawToHandLimit draws 3
    Assert.AreEqual(3, _playerHand.Cards.Count);
}
```

- [ ] **Step 8: Run tests — expect all new tests to FAIL**

```bash
/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath . \
  -runTests -testPlatform EditMode \
  -testResults TestResults/EditMode.xml
```

Expected: 7 new tests FAIL; all pre-existing tests PASS.

### Step group B — implement effect evaluation

- [ ] **Step 9: Update TurnController — add private effect evaluation methods**

Add these private methods to `TurnController` (before the closing brace):

```csharp
private void EvaluateSlotEffects(SlotEffectTrigger trigger)
{
    foreach (var slot in _board.Slots)
        EvaluateSlotEffectsForSlot(slot, trigger);
}

private void EvaluateSlotEffectsForSlot(SlotState slot, SlotEffectTrigger trigger)
{
    if (slot.Definition?.effects == null)
        return;

    foreach (var effect in slot.Definition.effects)
    {
        if (effect == null || effect.trigger != trigger)
            continue;

        if (!IsContextConditionMet(effect.context, slot))
            continue;

        ExecuteSlotEffect(effect, slot);
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

private void ExecuteSlotEffect(SlotEffectDefinition effect, SlotState slot)
{
    switch (effect.action)
    {
        case SlotEffectAction.DrawOpponentCard:
            DrawOpponentCardToSlot(slot);
            break;
        case SlotEffectAction.AddToClock:
            var clock = effect.clockTarget == ClockTarget.PlayerClock ? _playerClock : _opponentClock;
            clock.Increment(effect.actionValue);
            break;
        case SlotEffectAction.ReturnCardsFromSlots:
            ReturnAllPlayerCardsToHand();
            break;
        case SlotEffectAction.DrawToHandLimit:
            DrawPlayerCardsToHandLimit();
            break;
    }
}

private void DrawOpponentCardToSlot(SlotState slot)
{
    if (slot.HasOpponentCard)
        return;

    if (_opponentDeck.DrawCount == 0 && _opponentDeck.CanDraw)
        _opponentDeck.ShuffleDiscardIntoDrawPile(_rng);

    if (!_opponentDeck.CanDraw)
        return;

    var card = _opponentDeck.Draw();
    if (card != null)
        slot.OpponentCard = new CardInstance(card);
}

private void ReturnAllPlayerCardsToHand()
{
    foreach (var slot in _board.Slots)
    {
        if (slot.PlayerCard == null)
            continue;

        _playerHand.Add(slot.PlayerCard.Definition);
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
            _playerHand.Add(card);
    }
}
```

- [ ] **Step 10: Update StartTurn — call EvaluateSlotEffects at end**

After the existing draw loop in `StartTurn`, add:

```csharp
EvaluateSlotEffects(SlotEffectTrigger.OnPlayerTurnStart);
```

Full `StartTurn` after edit:

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
            _playerHand.Add(card);
    }

    EvaluateSlotEffects(SlotEffectTrigger.OnPlayerTurnStart);
}
```

- [ ] **Step 11: Update PlayCard — call EvaluateSlotEffectsForSlot after placing card**

After `ResolveSlotCombat(slot)` (or after placing the card if no combat), add effect evaluation. Full `PlayCard` after edit:

```csharp
public void PlayCard(CardDefinition card, int slotIndex)
{
    if (card == null)
        throw new ArgumentNullException(nameof(card));

    if (slotIndex < 0 || slotIndex >= _board.Slots.Length)
        throw new ArgumentOutOfRangeException(nameof(slotIndex));

    var slot = _board.Slots[slotIndex];
    if (slot.PlayerCard != null)
        return;

    if (!_playerHand.Remove(card))
        return;

    slot.PlayerCard = new CardInstance(card);

    if (slot.HasOpponentCard)
        ResolveSlotCombat(slot);

    EvaluateSlotEffectsForSlot(slot, SlotEffectTrigger.OnCardPlayed);
}
```

- [ ] **Step 12: Update PlaceOpponentCards — skip slots that have a Definition**

Definition-bearing slots manage their own opponent cards via effects. Full `PlaceOpponentCards` after edit:

```csharp
private void PlaceOpponentCards()
{
    foreach (var slot in _board.Slots)
    {
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
            slot.OpponentCard = new CardInstance(card);
    }
}
```

- [ ] **Step 13: Run all tests — expect everything to PASS**

```bash
/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath . \
  -runTests -testPlatform EditMode \
  -testResults TestResults/EditMode.xml
```

Expected: ALL tests PASS (pre-existing + 7 new).

- [ ] **Step 14: Commit**

```bash
git add Assets/Scripts/Controllers/TurnController.cs \
        Assets/Scripts/Tests/TurnControllerTests.cs
git commit -m "feat: implement slot effect evaluation in TurnController (TDD)"
```

---

## Task 6: Update SlotView with Effect Description Labels

**Files:**
- Modify: `Assets/Scripts/UI/SlotView.cs`

SlotView is a MonoBehaviour — not unit-testable in EditMode. Verification is visual (Marek wires TMP labels in the Unity prefab and observes descriptions appear at runtime).

- [ ] **Step 1: Add TMPro using directive and three label fields**

At the top of `SlotView.cs`, the `using` block already lacks `TMPro`. Add it alongside the others. Then add three serialized fields after `highlightImage`:

```csharp
[SerializeField] private TextMeshProUGUI noOpponentCardEffectLabel;
[SerializeField] private TextMeshProUGUI noPlayerCardEffectLabel;
[SerializeField] private TextMeshProUGUI passiveEffectLabel;
```

- [ ] **Step 2: Add SetEffectDescriptions method**

Add inside the `SlotView` class (before the closing brace):

```csharp
public void SetEffectDescriptions(SlotDefinition definition)
{
    ApplyEffectLabel(noOpponentCardEffectLabel, null);
    ApplyEffectLabel(noPlayerCardEffectLabel, null);
    ApplyEffectLabel(passiveEffectLabel, null);

    if (definition?.effects == null)
        return;

    foreach (var effect in definition.effects)
    {
        if (effect == null)
            continue;

        switch (effect.context)
        {
            case SlotEffectContext.NoOpponentCard:
                ApplyEffectLabel(noOpponentCardEffectLabel, effect.description);
                break;
            case SlotEffectContext.NoPlayerCard:
                ApplyEffectLabel(noPlayerCardEffectLabel, effect.description);
                break;
            case SlotEffectContext.Passive:
                ApplyEffectLabel(passiveEffectLabel, effect.description);
                break;
        }
    }
}

private static void ApplyEffectLabel(TextMeshProUGUI label, string text)
{
    if (label == null)
        return;

    label.text = text ?? string.Empty;
    label.gameObject.SetActive(!string.IsNullOrEmpty(text));
}
```

Full resulting `SlotView.cs`:

```csharp
using System;
using CardsUnity;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CardsUnity.UI
{
    public class SlotView : MonoBehaviour, IDropHandler
    {
        [SerializeField] private Transform playerCardAnchor;
        [SerializeField] private Transform opponentCardAnchor;
        [SerializeField] private Image highlightImage;
        [SerializeField] private TextMeshProUGUI noOpponentCardEffectLabel;
        [SerializeField] private TextMeshProUGUI noPlayerCardEffectLabel;
        [SerializeField] private TextMeshProUGUI passiveEffectLabel;

        public int SlotIndex { get; set; }
        public Action<CardView, int> OnCardDropped;

        private CardView _playerView;
        private CardView _opponentView;
        private bool CanAcceptPlayerCard => _playerView == null;

        public void ShowPlayerCard(CardInstance card, CardView prefab)
        {
            if (_playerView != null)
                Destroy(_playerView.gameObject);

            if (card == null || prefab == null || playerCardAnchor == null)
            {
                _playerView = null;
                return;
            }

            _playerView = Instantiate(prefab, playerCardAnchor);
            _playerView.SetDraggable(false);
            _playerView.SetCardInstance(card);
        }

        public void UpdatePlayerValue(CardInstance card)
        {
            if (_playerView != null)
                _playerView.SetCardInstance(card);
        }

        public void ClearPlayerCard()
        {
            if (_playerView != null)
                Destroy(_playerView.gameObject);

            _playerView = null;
        }

        public void ShowOpponentCard(CardInstance card, CardView prefab)
        {
            if (_opponentView != null)
                Destroy(_opponentView.gameObject);

            if (card == null || prefab == null || opponentCardAnchor == null)
            {
                _opponentView = null;
                return;
            }

            _opponentView = Instantiate(prefab, opponentCardAnchor);
            _opponentView.SetDraggable(false);
            _opponentView.SetCardInstance(card);
        }

        public void UpdateOpponentValue(CardInstance card)
        {
            if (_opponentView != null)
                _opponentView.SetCardInstance(card);
        }

        public void ClearOpponentCard()
        {
            if (_opponentView != null)
                Destroy(_opponentView.gameObject);

            _opponentView = null;
        }

        public void SetHighlight(bool active)
        {
            if (highlightImage != null)
                highlightImage.enabled = active && CanAcceptPlayerCard;
        }

        public void SetEffectDescriptions(SlotDefinition definition)
        {
            ApplyEffectLabel(noOpponentCardEffectLabel, null);
            ApplyEffectLabel(noPlayerCardEffectLabel, null);
            ApplyEffectLabel(passiveEffectLabel, null);

            if (definition?.effects == null)
                return;

            foreach (var effect in definition.effects)
            {
                if (effect == null)
                    continue;

                switch (effect.context)
                {
                    case SlotEffectContext.NoOpponentCard:
                        ApplyEffectLabel(noOpponentCardEffectLabel, effect.description);
                        break;
                    case SlotEffectContext.NoPlayerCard:
                        ApplyEffectLabel(noPlayerCardEffectLabel, effect.description);
                        break;
                    case SlotEffectContext.Passive:
                        ApplyEffectLabel(passiveEffectLabel, effect.description);
                        break;
                }
            }
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (!CanAcceptPlayerCard)
                return;

            var dragged = eventData.pointerDrag?.GetComponent<CardView>();
            if (dragged != null)
                OnCardDropped?.Invoke(dragged, SlotIndex);
        }

        private static void ApplyEffectLabel(TextMeshProUGUI label, string text)
        {
            if (label == null)
                return;

            label.text = text ?? string.Empty;
            label.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }
    }
}
```

- [ ] **Step 3: Run EditMode tests to confirm no compilation errors**

```bash
/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath . \
  -runTests -testPlatform EditMode \
  -testResults TestResults/EditMode.xml
```

Expected: ALL tests PASS.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/UI/SlotView.cs
git commit -m "feat: add TextMeshPro effect description labels to SlotView"
```

---

## Task 7: Update Game Design Document

**Files:**
- Modify: `docs/game-design-doc.md`

- [ ] **Step 1: Update the Effects System section — SlotEffectDefinition fields**

In the `## Effects System` section, under `### Current Implementation`, replace the existing description with:

```markdown
### Current Implementation

**Card effects** — `CardEffectTag` (ScriptableObject) fields:
- `tagName` — string
- `trigger` — `EffectTrigger` enum: `OnPlay`, `OnEndTurn`, `AfterAttack`, `OnDestroyed`
- `description` — string

These tags exist on `CardDefinition.effects` but **are not evaluated** by any runtime code. They are data-only placeholders.

**Slot effects** — `SlotEffectDefinition` (ScriptableObject) fields:
- `context` — `SlotEffectContext`: `NoOpponentCard`, `NoPlayerCard`, `Passive`
- `trigger` — `SlotEffectTrigger`: `OnPlayerTurnStart`, `OnCardPlayed`
- `action` — `SlotEffectAction`: `DrawOpponentCard`, `AddToClock`, `ReturnCardsFromSlots`, `DrawToHandLimit`
- `actionValue` — `int` (used by `AddToClock`: the amount to add)
- `clockTarget` — `ClockTarget`: `PlayerClock`, `OpponentClock`
- `description` — `string` (shown in `SlotView` as a TextMeshPro label)

Slot effects **are evaluated** at runtime by `TurnController`. The four implemented actions:

| Action | Trigger | Context | Behaviour |
|---|---|---|---|
| `DrawOpponentCard` | `OnPlayerTurnStart` | `NoOpponentCard` | Draw from opponent deck; place card on this slot |
| `AddToClock` | `OnPlayerTurnStart` | `NoPlayerCard` | Increment the target clock by `actionValue` |
| `ReturnCardsFromSlots` | `OnCardPlayed` | `Passive` | Move all player board cards back to hand |
| `DrawToHandLimit` | `OnCardPlayed` | `Passive` | Draw from player deck until hand reaches `draftValue` |
```

- [ ] **Step 2: Update the Board and Slots section — SlotDefinition**

In the `## Board and Slots` section, under `### Current Implementation`, add after the existing paragraph:

```markdown
Each `SlotState` may hold an optional `SlotDefinition` (ScriptableObject). When a `SlotDefinition` is present:
- `hasOpponentCardSpot` / `hasPlayerCardSpot` — declare which card anchors this slot uses.
- `effects` — list of `SlotEffectDefinition` entries evaluated by `TurnController`.
- Slot views display effect descriptions via three `TextMeshProUGUI` labels (one per context: NoOpponentCard, NoPlayerCard, Passive).

Slots with no `SlotDefinition` fall back to legacy behaviour: opponent cards are auto-placed at the end of each turn by `PlaceOpponentCards`.
```

- [ ] **Step 3: Update Implementation Status — move slot effects from Planned to Implemented**

In `### Defined but Not Evaluated`, remove (or keep) the `CardEffectTag` note as-is (it still applies to card effects).

In `### Planned but Not Implemented`, remove the row:
```
| Functional slot effects | Effect evaluation at runtime |
```

In `### Implemented and Working`, add:
```
- Slot effect system: `SlotEffectDefinition`, `SlotDefinition` ScriptableObjects; `DrawOpponentCard`, `AddToClock`, `ReturnCardsFromSlots`, `DrawToHandLimit` actions evaluated by `TurnController`
- `SlotView` TextMeshPro effect description labels (one per context)
```

- [ ] **Step 4: Append changelog entry**

Append to the Changelog table:

```markdown
| 2026-04-26 | Slot effect system implemented: SlotEffectDefinition/SlotDefinition ScriptableObjects, four actions (DrawOpponentCard, AddToClock, ReturnCardsFromSlots, DrawToHandLimit), two triggers (OnPlayerTurnStart, OnCardPlayed), SlotView TMP description labels |
```

- [ ] **Step 5: Commit**

```bash
git add docs/game-design-doc.md
git commit -m "docs: update game-design-doc for slot effect system"
```

---

## Self-Review Checklist

**Spec coverage:**
- [x] Slot optionally has opponent card spot → `SlotDefinition.hasOpponentCardSpot` (used to declare intent; anchors remain null-safe in SlotView)
- [x] Slot optionally has player card spot → `SlotDefinition.hasPlayerCardSpot`
- [x] Three TMP effect description fields → `noOpponentCardEffectLabel`, `noPlayerCardEffectLabel`, `passiveEffectLabel`
- [x] Three effect contexts (NoOpponentCard, NoPlayerCard, Passive) → `SlotEffectContext` enum
- [x] Two triggers (OnPlayerTurnStart, OnCardPlayed) → `SlotEffectTrigger` enum
- [x] DrawOpponentCard action → `DrawOpponentCardToSlot()`
- [x] AddToClock action → `clock.Increment(effect.actionValue)` with `ClockTarget`
- [x] ReturnCardsFromSlots action → `ReturnAllPlayerCardsToHand()`
- [x] DrawToHandLimit action → `DrawPlayerCardsToHandLimit()`
- [x] Design doc updated

**Type consistency check:**
- `SlotDefinition.effects` is `List<SlotEffectDefinition>` — used as such in tests (`.effects = new List<SlotEffectDefinition>{ effect }`)
- `SlotState.Definition` — accessed via `?.effects` in `EvaluateSlotEffectsForSlot`
- `BoardState(SlotDefinition[])` — called in tests as `new BoardState(new SlotDefinition[] { def, null, null })`
- `SlotView.SetEffectDescriptions(SlotDefinition)` — called from `BoardView.ConfigureSlots(SlotDefinition[])`
- `TurnController` private methods (`DrawOpponentCardToSlot`, `ReturnAllPlayerCardsToHand`, `DrawPlayerCardsToHandLimit`) are consistent across Tasks 5 and self-contained

**Placeholder scan:** No TBD / TODO / "implement later" present in any step.
