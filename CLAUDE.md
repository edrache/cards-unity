# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Language

All code, documentation, comments, and commit messages must be written in **English**.
Conversation with the user is conducted in **Polish**.

## Project Overview

Unity 6 (6000.3.10f1) port of a browser-based turn-based card combat game. The game logic is fully documented in [`../docs/unity-migration.md`](../docs/unity-migration.md) — read it before implementing any game feature. The original HTML/JS prototype lives in the parent directory.

## Key Dependencies

| Asset | Purpose |
|---|---|
| DOTween (Plugins/) | Tweening / animation |
| Rewired (Rewired/) | Input management |
| Feel / MMFeedbacks | Screen shake, haptics, VFX feedback |
| Quibli | Stylized / toon shaders (URP) |
| TrueShadow | UI drop shadows |

Rendering: URP 17.3.0. PC and Mobile URP variants are in `Assets/Settings/`.

## Architecture

Follow the layered architecture defined in `unity-migration.md §9`:

**Data layer (no MonoBehaviour)**
- `CardData` — ScriptableObject with static card definition
- `CardInstance` — plain C# class, mutable runtime copy (`value` is both HP and dice ceiling — never split these)
- `GameState` — plain C# class mirroring the JS game state
- `CombatResolver` — static class for RPS resolution and damage math
- `DeckFactory` — static class; Fisher-Yates shuffle; always clone `CardData` into `CardInstance`

**Game flow (MonoBehaviour)**
- `GameManager` — owns `GameState`, drives phase transitions, exposes C# events
- `RoundController` — draw, place, unplace, canResolve
- `CombatController` — staged combat resolution (one column at a time)
- `RewardController` — reward draft, new encounter setup

**UI layer (MonoBehaviour)**
- `CardView` — renders `CardInstance`; implements `IBeginDragHandler / IDragHandler / IEndDragHandler`
- `SlotView` — highlights on hover; receives card drop
- `BoardView`, `HandView`, `HUDView`, `DeckPanelView`, `RewardView`, `CombatResultView`, `TooltipView`

## Critical Game Rules

See `unity-migration.md §12` for the full invariant list. The most important:

1. `value` is **both** HP and dice ceiling — never separate them.
2. Damage is **simultaneous** — a dying card still deals its damage.
3. **Placement order = resolution order.**
4. Support bonus applies only to **immediately adjacent** allies.
5. Neutral matchup: both sides take `min(playerRoll, enemyRoll)` as shared damage.
6. Enemy deck **refills after every round**, not per fight.

## Configuration

Map all tuning values to ScriptableObjects (not hardcoded):
- `GameConfig` — slot count (3)
- `AnimConfig` — DOTween durations from `unity-migration.md §8`
- `CardVisualConfig` — card dimensions and corner radius
