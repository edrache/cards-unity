# NPC Deck Setup

This guide describes the editor-side setup required to use the NPC deck system in the project.

## Goal

After completing the steps below, a slot effect will be able to draw an NPC card, show its portrait and narrative text in the HUD, and apply its optional mechanical effect.

## What Was Implemented in Code

The runtime side is already available in the project:

- `NpcCardDefinition` stores character name, portrait, narrative text, and effects
- `NpcDeckDefinition` stores the NPC card list
- `NpcDeckState` manages draw pile, discard pile, and the active NPC card
- `SlotEffectAction.DrawNpcCard` triggers an NPC draw
- `TurnController` fires `OnNpcCardDrawn`
- `ChallengeController` applies the NPC mechanical effects
- `NpcCardView` renders the currently active NPC card in UI

What remains is Unity Editor configuration.

## Editor Setup Checklist

1. Create one or more `NpcCardDefinition` assets
2. Create an `NpcDeckDefinition` asset
3. Assign the NPC deck to `GameConfig`
4. Add a slot effect that uses `DrawNpcCard`
5. Add and wire the `NpcCardView` UI panel in the scene
6. Run the scene and verify the NPC card is drawn and displayed

## Step 1: Create NPC Card Assets

In the Project window:

1. Right-click in the target folder
2. Choose `Create > CardsUnity > NPC Card Definition`
3. Create at least two cards for testing

Suggested test assets:

- `Assets/Data/NpcCards/Npc_Merchant.asset`
- `Assets/Data/NpcCards/Npc_Bandit.asset`

### Example Card 1: Merchant

Suggested values:

- `characterName`: `Merchant`
- `narrativeText`: `A passing merchant slips you a useful card before disappearing into the crowd.`
- `portrait`: optional
- `effects`: add one entry

Effect:

- `action`: `AddCardToPlayerHand`
- `cardToAdd`: choose one existing player card from your starter deck
- `actionValue`: `0`
- `targetCardType`: leave default

### Example Card 2: Bandit

Suggested values:

- `characterName`: `Bandit`
- `narrativeText`: `A lurking bandit rattles your nerves and leaves you exposed.`
- `portrait`: optional
- `effects`: add one entry

Effect:

- `action`: `DamageToThreat`
- `targetCardType`: `Force`
- `actionValue`: `1`
- `cardToAdd`: leave empty

### Optional Third Test Card

If you also want to test clock impact:

- `characterName`: `Witness`
- `narrativeText`: `A witness points you toward the truth, accelerating the confrontation.`
- `effects`: one entry

Effect:

- `action`: `IncrementOpponentClock`
- `actionValue`: `1`

## Step 2: Create the NPC Deck Asset

In the Project window:

1. Right-click in the target folder
2. Choose `Create > CardsUnity > NPC Deck Definition`
3. Save it as `Assets/Data/NpcDecks/SampleNpcDeck.asset`

Then configure:

- add your NPC card assets into `cards`
- set `shuffleOnInit = true` for varied draws

For deterministic debugging you can temporarily set:

- `shuffleOnInit = false`

## Step 3: Assign the Deck to GameConfig

Open your `GameConfig` asset and set:

- `npcDeck` = `SampleNpcDeck`

Without this assignment, `ChallengeController` will not create the runtime NPC deck.

## Step 4: Add a Slot Effect That Draws NPC Cards

Open the `SlotDefinition` used in your test encounter.

Add a `SlotEffectDefinition` with these values:

- `trigger`: `OnPlayerTurnStart`
- `context`: `Passive`
- `action`: `DrawNpcCard`

Recommended first setup:

- use `Passive` so the effect always fires
- use `OnPlayerTurnStart` so you immediately see the system working when the turn begins

If you want stricter behavior, you can also try:

- `context = NoOpponentCard`

That will only draw an NPC card when the slot currently has no opponent card.

## Step 5: Add the NPC Card HUD Panel

In the scene:

1. Create a UI panel under your HUD canvas
2. Name it `NpcCardPanel`
3. Add child UI elements:
   - portrait `Image`
   - name `TextMeshProUGUI`
   - narrative `TextMeshProUGUI`
4. Add the `NpcCardView` component to `NpcCardPanel`
5. Wire the fields in the Inspector:
   - `panel`
   - `portraitImage`
   - `nameLabel`
   - `narrativeLabel`
6. Select the scene object with `ChallengeController`
7. Assign the `NpcCardPanel` object to `ChallengeController.npcCardView`

Behavior:

- when there is no active NPC card, the panel hides itself
- when a card is drawn, the panel shows portrait, name, and narrative text

## Step 6: Minimal Test Flow

Use this simplest possible test:

1. `GameConfig.npcDeck` points to your sample NPC deck
2. One active slot has a `DrawNpcCard` effect on `OnPlayerTurnStart`
3. `NpcCardView` is wired in the scene
4. Start Play Mode

Expected result:

1. The turn begins
2. The slot effect triggers `DrawNpcCard`
3. An NPC card becomes active
4. The UI panel appears
5. The NPC mechanical effect is applied immediately

## What to Verify

### For AddCardToPlayerHand

Verify that:

- a new card appears in the player's hand

### For DamageToThreat

Verify that:

- the selected threat bar loses the expected amount

### For IncrementOpponentClock

Verify that:

- the opponent clock increases by the configured value

### For Deck Cycling

Verify that:

- drawing a second NPC discards the first active NPC
- when the draw pile empties, discard is shuffled back into draw

## Recommended First Debug Setup

For the fastest validation:

- create exactly 2 NPC cards
- set `shuffleOnInit = false`
- use one `DrawNpcCard` passive slot on `OnPlayerTurnStart`
- make one card grant a hand card
- make the other deal `1` damage to a threat bar

This makes it easy to see both UI behavior and mechanical effects across consecutive turns.

## Common Failure Cases

### NPC panel never appears

Check:

- `ChallengeController.npcCardView` is assigned
- the slot effect action is really `DrawNpcCard`
- `GameConfig.npcDeck` is assigned

### NPC draws do nothing

Check:

- the deck contains actual `NpcCardDefinition` assets
- the slot effect trigger is actually firing in that encounter

### Mechanical effect does not apply

Check:

- the `NpcEffect.action` is set correctly
- `cardToAdd` is assigned when using `AddCardToPlayerHand`
- `targetCardType` and `actionValue` are set when using `DamageToThreat`

## Optional Next Step

After the first successful setup, create a dedicated test encounter scene or slot definition specifically for NPC deck validation so future balancing and content work can be tested quickly.
