# Card Game Design Summary

## Overview

This project is a turn-based card game prototype built in Unity 6 on a Canvas-based interface. The current design focuses on a compact single-challenge loop where the player draws cards, commits them to board slots, resolves combat against enemy cards, and tries to fill the opponent's clock before their own clock is filled.

The prototype is intentionally narrow in scope. It exists to validate the core combat language, turn rhythm, and board interaction before expanding into a larger campaign or deckbuilding structure.

## Core Fantasy

The player navigates a sequence of abstract confrontations using cards that represent tactical approaches rather than literal attacks. Each card belongs to one of three strategic archetypes:

- `Force`
- `Presence`
- `Wit`

These archetypes form the core identity of the game. They create readable matchups, support prediction and counterplay, and make even a small deck feel expressive.

## High-Level Structure

The intended larger game structure is a run made of multiple challenges. Each challenge is resolved through cards and two opposing clocks. In the current prototype, only a single self-contained challenge is implemented.

Within a challenge:

- The player has a deck and a hand.
- The opponent has a deck that automatically populates the board.
- The board contains a fixed number of slots.
- Each side has a clock that acts as challenge progress and health.
- Destroying enemy cards fills the opponent clock.
- Losing your own cards fills the player clock.

When one clock reaches its maximum, the challenge ends immediately.

## Card Model

Each card definition currently contains:

- `title`
- `type`
- `value`
- `effects`
- `artwork`
- `flavorText`

Runtime cards are represented as instances with mutable `CurrentValue`, which allows damage to persist while a card remains on the board during a combat exchange.

### Current Sample Cards

The repository currently includes three sample card definitions:

- `Bold Move` (`Force`, value `4`)
- `Sweet Talk` (`Presence`, value `3`)
- `Maneuver` (`Wit`, value `5`)

These are starter prototype assets used to validate the loop rather than a final content set.

## Card Types and Matchups

The game uses a rock-paper-scissors relationship:

- `Force` beats `Presence`
- `Presence` beats `Wit`
- `Wit` beats `Force`
- Matching types result in a draw

This relationship is the main source of deterministic advantage in combat.

## Combat Rules

Combat happens when the player plays a card into a slot that already contains an opponent card.

### Outcome Evaluation

For each attacking side, the game first determines the matchup result:

- `Win`
- `Draw`
- `Lose`

### Damage Rule

Damage depends on matchup outcome:

- On `Win`, damage equals the attacker's current value.
- On `Draw` or `Lose`, damage is a random value between `1` and the attacker's current value.

### Exchange Rule

The implemented prototype resolves combat as a simultaneous exchange:

- The player card deals damage to the opponent card.
- The opponent card deals damage back in the same combat step.
- Both damage values are calculated before either card is removed.

This means mutual destruction is possible.

### Destruction Rule

If a card's current value is reduced to `0`, it is destroyed.

In the current implementation:

- Destroying an opponent card increments the opponent clock by `1`.
- Losing a player card increments the player clock by `1`.

The original predesign described destroyed cards moving to discard. That discard loop is only implemented for deck piles, not yet for board casualties or returned hand cards.

## Turn Structure

The prototype uses a simple player-driven turn loop.

At challenge start, the scene controller performs an initial setup pass that gives the player an opening hand and then populates the opponent side of the board before the first real decision.

### Start of Turn

At the start of the turn, the player draws until hand size reaches the `Draft Value`.

Current default:

- `Draft Value = 3`

If the draw pile is empty but the discard pile contains cards, the discard pile is shuffled back into the draw pile.

### Player Action Phase

The player may drag a card from hand onto any empty player slot.

If the slot contains an opponent card:

- combat resolves immediately
- clocks may advance
- one or both cards may be destroyed

If the slot does not contain an opponent card:

- the player card simply occupies that slot for the rest of the turn

The current prototype allows one player card per slot and does not include costs, action points, or combo chains.

### End Turn

When the player presses `End Turn`:

- all surviving player cards on the board return to the player's hand as their original card definitions
- all player board slots are cleared
- the opponent fills empty opponent slots by drawing from its deck

If the opponent draw pile is empty and discard exists, the discard pile is shuffled back in using the same deck logic.

## Board and Slot Logic

The default prototype board has:

- `3` slots

Each slot supports:

- one player card position
- one opponent card position

The broader design allows slots to have special effects or conditional behaviors. That concept exists in the predesign, but slot effects are not implemented yet in runtime code.

## Clocks and Win Conditions

Each challenge uses two clocks:

- player clock
- opponent clock

In the current prototype, each clock's maximum value is derived from deck size:

- player clock max = number of cards in the player's starting deck
- opponent clock max = number of cards in the opponent deck

A side loses when its clock becomes full.

That means:

- if the opponent clock fills first, the player wins
- if the player clock fills first, the player loses

UI panels for win and loss states are already supported by the scene controller.

## Current Prototype Configuration

The default game configuration in the repository currently uses:

- `Draft Value = 3`
- `Board Slot Count = 3`
- player starting deck size = `9`
- opponent deck size = `5`

The player starting deck is a repeated mix of all three sample cards. The opponent deck is a smaller mixed set.

## Effects System

The data model already supports effect tags through `CardEffectTag`.

Current trigger types:

- `OnPlay`
- `OnEndTurn`
- `AfterAttack`
- `OnDestroyed`

At the moment, these tags are descriptive only. They are stored in card data but do not yet drive gameplay behavior. They exist as a foundation for future mechanics.

## Implemented Scope vs Planned Scope

### Implemented in the Prototype

- single-challenge loop
- deck, hand, board, and clock state
- draw-to-draft turn start
- drag-and-drop card play
- automatic combat on contested slots
- simultaneous combat exchange
- end turn flow
- win/lose state detection
- ScriptableObject-based card and config data

### Planned but Not Yet Implemented

- challenge series or run structure
- deck growth between challenges
- actual gameplay resolution for card effect tags
- slot-specific gameplay effects
- discard handling for destroyed board cards
- richer opponent behavior and encounter scripting
- additional card fields, content variety, and balancing

## Design Intent

The prototype is built around a few clear principles:

- fast readable turns
- low rules overhead
- meaningful type-based prediction
- partial randomness that keeps weak matchups viable
- enough board state to create tension without slowing pacing

The most important design choice is the combination of deterministic RPS advantage with non-zero random damage on losing or neutral matchups. This prevents combat from becoming completely binary while still rewarding informed placement.

## Summary

At its current stage, the game is a compact tactical card battler prototype. The player cycles a small hand, contests three slots, and tries to destroy enemy cards faster than the opponent destroys theirs. The design already establishes the core verbs and data model, while leaving room for future expansion through card effects, slot rules, and multi-challenge progression.
