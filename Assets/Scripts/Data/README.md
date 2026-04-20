# Data

Plain runtime data and immutable definitions live here.

Expected homes:

- `CardData`: immutable `ScriptableObject` definitions.
- `CardInstance`: mutable runtime copies cloned from definitions.
- `GameState`: plain C# state container with no scene dependencies.
- Shared enums such as RPS, role, owner, and phase.

Runtime mutation must affect only `CardInstance` objects. Card definitions remain immutable.
