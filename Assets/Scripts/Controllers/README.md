# Controllers

MonoBehaviour game-flow controllers live here.

Expected homes:

- `GameManager`: owns `GameState`, initializes runs, and publishes state/phase events.
- `RoundController`: draw, place, unplace, can-resolve, and resolution order.
- `CombatController`: staged column-by-column combat.
- `RewardController`: reward draft and new encounter setup.

Controllers coordinate systems but should not duplicate resolver rules.
