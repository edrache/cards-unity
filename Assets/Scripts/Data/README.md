# Data

Plain runtime data and immutable definitions live here.

Expected homes:

- `CardData`: immutable `ScriptableObject` definitions.
- `CardInstance`: mutable runtime copies cloned from definitions.
- `GameState`: plain C# state container with no scene dependencies.
- `DeckFactory`: clones `CardData` definitions into runtime decks and shuffles them.
- Shared enums such as RPS, role, owner, and phase.

Runtime mutation must affect only `CardInstance` objects. Card definitions remain immutable.

`GameState` is constructed with a slot count, usually from `GameConfig.SlotCount`, so board sizes stay config-driven while the state object remains free of scene dependencies.

Default card definition assets are stored under `Assets/Resources/CardDefinitions/Player/` and `Assets/Resources/CardDefinitions/Enemy/`. Runtime composition code should load or reference those assets, then pass them into `DeckFactory`.

`DeckFactory` accepts an optional `System.Random`. Runtime callers may omit it for a fresh random shuffle. Tests should pass a seeded `Random` so Fisher-Yates order is deterministic.
