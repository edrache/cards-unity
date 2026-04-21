# Unity Card Combat Implementation Plan

This plan breaks the Unity migration into independent, trackable work packages so multiple agents can implement the game over time. Keep this file updated after every completed task.

Source of truth: [`unity-migration.md`](unity-migration.md)

## Collaboration Rules

- Before starting a task, read the relevant sections of `unity-migration.md`.
- Keep code, documentation, comments, and commit messages in English.
- Mark tasks as `[x]` only after implementation and verification are complete.
- Add a short entry to the Agent Work Log whenever you finish or intentionally defer work.
- Do not rewrite Unity `.meta` files unless the asset operation genuinely requires it.
- Keep data and resolver logic free of Unity scene dependencies.
- Preserve the key invariants listed below in every implementation decision.

## Status Legend

- `[ ]` Not started
- `[~]` In progress
- `[x]` Done
- `[!]` Blocked or needs decision

## Key Invariants

- `CardInstance.Value` is both current HP and dice ceiling. Do not split HP and roll ceiling.
- Damage is simultaneous. A card that dies in a fight still deals its damage for that fight.
- Placement order is resolution order.
- Support bonus applies only to immediate left and right allies on the same board.
- Neutral matchup uses the lower of the two rolls as shared damage before defense reduction.
- Card definitions are immutable. Runtime cards must be cloned from definitions.
- Enemy deck refills after every round, not after every fight.
- Reward draft resets the encounter with a new enemy deck and board, while the player keeps their deck.

## Existing Unity Assets and Recommended Use

Use the existing imported assets deliberately. Build the first playable milestone with the smallest dependable stack, then layer polish and controller support on top.

### Core Implementation Stack

- **UGUI** (`com.unity.ugui`) should be the default UI system for the first playable version.
  - Use Canvas, Image, Button, layout groups, and Unity EventSystem interfaces.
  - This matches the card/slot/drag-and-drop requirements in `unity-migration.md`.
- **Unity Test Framework** (`com.unity.test-framework`) should be used for EditMode tests around pure data, deck, combat, and round-flow logic.
- **URP 17.3.0** is already present and should remain the rendering baseline.
  - Existing render assets live in `Assets/Settings/`.
  - Use `PC_RPAsset` and `Mobile_RPAsset` as the starting targets for desktop/mobile checks.

### Animation and Feedback Assets

- **DOTween** (`Assets/Plugins/Demigiant/DOTween/`) is the preferred animation library.
  - Use it for card lift, drag tilt, snap-to-slot, return-to-hand, staged combat reveal, defeated-card tilt, cleanup, and reward choice transitions.
  - Keep durations and tuning in `AnimConfig`; do not hardcode DOTween values in views.
- **Feel / MMFeedbacks** (`Assets/Feel/MMFeedbacks/`) should be added after core gameplay works.
  - Recommended uses: hit feedback, defeat feedback, reward selection feedback, game-over feedback, flashes, scale pulses, position shake, camera shake, and floating text.
  - Keep MMFeedbacks as a presentation layer. Gameplay must remain correct if feedbacks are disabled.
- **NiceVibrations** (`Assets/Feel/NiceVibrations/`) is optional mobile polish.
  - Use for light haptics on valid drop, hit, defeat, reward pick, and game over.
  - Do not block desktop playability on haptics.

### UI Visual Assets

- **TrueShadow** (`Assets/Le Tai's Asset/TrueShadow/`) is recommended for card, slot, tooltip, HUD panel, and reward card depth.
  - Use it once the UGUI prefabs exist and the visual hierarchy is stable.
  - Avoid making TrueShadow part of gameplay state or rule logic.
- **Text rendering decision:** TextMeshPro is referenced by imported packages, but it is not explicitly listed in `Packages/manifest.json`.
  - In Phase 0, confirm whether TMP is installed/available in the project.
  - If TMP is available, prefer TMP for card names, values, tooltips, and combat results.
  - If TMP is not available, use UGUI Text first and document the later TMP migration.

### Input Assets

- **Unity EventSystem + UGUI drag interfaces** should be the first implementation path.
  - Implement `IBeginDragHandler`, `IDragHandler`, and `IEndDragHandler` on `CardView`.
  - Implement drop/hover behavior on `SlotView`.
- **Input System asset** (`Assets/InputSystem_Actions.inputactions`) currently looks like a default action map (`Move`, `Look`, `Attack`, `Interact`, etc.).
  - Do not depend on it for first playable drag-and-drop unless it is intentionally redesigned.
- **Rewired** (`Assets/Rewired/`) should be treated as optional controller support.
  - Use `Assets/Rewired/Integration/UnityUI/RewiredEventSystem.prefab` if/when gamepad navigation or remapping becomes a requirement.
  - Do not introduce Rewired into core card rules, deck logic, or combat logic.

### Visual Style Assets

- **Quibli** (`Assets/Quibli/`) is available for stylized URP rendering.
  - Use it for later visual direction, stylized backgrounds, toon-lit 3D elements, skybox, or decorative scene treatment.
  - Do not require Quibli for the first UGUI-only playable milestone.
  - Quibli foliage/grass prefabs are not directly useful for the card game unless a 3D environment backdrop is added.

### Assets to Treat as Reference or Debug Only

- `Assets/Scenes/SampleScene.unity` is a placeholder scene unless converted into the gameplay scene.
- `Assets/TutorialInfo/` is Unity tutorial/readme content and should not drive implementation.
- Rewired DevTools prefabs are for controller diagnostics, not core gameplay UI.
- Feel/MMTools demo scenes and demo prefabs are useful references, but should not be copied into production gameplay without review.

### Missing Game-Specific Assets

Future implementation work still needs to create:

- `CardData` assets for all player and enemy cards.
- `CardView`, `SlotView`, `BoardView`, `HandView`, `HUDView`, `DeckPanelView`, `RewardView`, `CombatResultView`, and `TooltipView` prefabs.
- Main gameplay scene.
- Card icons/sprites or a documented temporary symbol system.
- `GameConfig`, `AnimConfig`, and `CardVisualConfig` assets.
- Test assemblies and test fixtures for deterministic logic verification.

## Phase 0 - Project Setup and Conventions

- [x] Create the main runtime folder structure under `Assets/Scripts/`.
  - Suggested folders: `Data`, `Config`, `Runtime`, `Combat`, `Controllers`, `UI`, `Tests`.
- [x] Add assembly definitions if the project starts using Unity test assemblies.
- [x] Confirm whether TextMeshPro is available and document the chosen UI text stack.
- [x] Define scene bootstrap strategy for the first playable scene.
- [x] Document the first-playable technology choices.
  - Use UGUI for UI.
  - Use Unity EventSystem drag/drop before optional Rewired controller support.
  - Use DOTween for view animation.
  - Keep MMFeedbacks, NiceVibrations, TrueShadow, and Quibli as presentation/polish layers.
- [x] Update `AGENTS.md` with build, test, and verification commands once they exist.

Acceptance criteria:

- New source files have a clear home.
- Future agents can identify where data, controllers, UI, and tests belong.
- No gameplay behavior is implemented in this phase except minimal bootstrapping if needed.
- The first-playable asset stack is documented before UI work begins.

## Phase 1 - Configuration Assets

- [x] Implement `GameConfig` ScriptableObject.
  - Include `SlotCount`, default `3`.
- [x] Implement `AnimConfig` ScriptableObject.
  - Include lift scale, tilt max, snap duration, lift duration, slot snap radius, resolve flash duration, resolve pause duration, resolve tilt angle, and cleanup duration.
- [x] Implement `CardVisualConfig` ScriptableObject.
  - Include card width, height, and corner radius from the migration guide.
- [x] Create default config assets under `Assets/Resources/Config/` or another documented config path.
- [x] Document how runtime systems obtain config references.

Acceptance criteria:

- Tuning values from `unity-migration.md` section 8 are represented as ScriptableObjects.
- Gameplay code does not hardcode slot count or animation tuning values.

## Phase 2 - Core Data Model

- [x] Implement `RpsType` enum.
  - Values: `Pressure`, `Appeal`, `Positioning`.
- [x] Implement `CardRole` enum.
  - Values: `Attack`, `Defense`, `Support`, `None`.
- [x] Implement `CardOwner` enum.
  - Values: `Player`, `Enemy`.
- [x] Implement `GamePhase` enum.
  - Values: `Draw`, `Placement`, `Combat`, `Reward`, `End`.
- [x] Implement `CardData` ScriptableObject for immutable card definitions.
- [x] Implement `CardInstance` as a plain C# runtime class.
  - Include definition reference or copied static fields.
  - Include mutable `Value`.
  - Do not inherit from `MonoBehaviour`.
- [x] Implement `GameState` as a plain C# runtime class.
  - Include phase, round, player deck, player hand, player cemetery, player board, enemy deck, enemy cemetery, enemy board, placement order, and reward choices.
- [x] Add basic data model tests if Unity test setup is available.

Acceptance criteria:

- Runtime state can be represented without scene objects.
- Card definitions are not mutated when runtime cards take damage.
- Board arrays/lists can represent exactly three nullable slots through config-driven sizing.

## Phase 3 - Card Definitions and Deck Factory

- [x] Create all 12 player `CardData` assets from `unity-migration.md` section 3.1.
- [x] Create all 6 enemy `CardData` assets from `unity-migration.md` section 3.2.
- [x] Implement `DeckFactory`.
  - Create player deck from player card definitions.
  - Create enemy deck from enemy card definitions.
  - Clone every `CardData` into a separate `CardInstance`.
  - Shuffle with Fisher-Yates.
- [x] Decide and document RNG injection strategy for deterministic tests.
- [x] Add tests for deck size, ownership, value ranges, cloning, and shuffle determinism where possible.

Acceptance criteria:

- Player deck starts with 12 cloned runtime cards.
- Enemy deck starts with 6 cloned runtime cards.
- Mutating one runtime card never mutates another runtime card or its source `CardData`.
- Tests can run predictable shuffle cases.

## Phase 4 - Combat Resolver

- [x] Implement `CombatResolver.GetRpsResult()`.
- [x] Implement support bonus calculation.
  - Count only immediate left and right allies.
  - Count only allies with `CardRole.Support`.
- [x] Implement effective range calculation.
  - `effectiveRange = card.Value + supportBonus`.
- [x] Implement roll handling for advantage, disadvantage, and neutral matchups.
- [x] Implement simultaneous damage calculation.
  - Attack role adds `+1` to outgoing damage.
  - Defense role subtracts `1` from incoming damage, minimum `0`.
  - Neutral uses shared lower roll before defense reduction.
- [x] Implement `ResolvePair()` result object.
  - Include rolls, chosen rolls, damage dealt, values before and after, and death flags.
- [x] Implement read-only combat preview.
  - Include RPS label, roll mode, effective range, support bonus, damage range, and death risk.
- [x] Add focused tests for every RPS matchup, every role modifier, support adjacency, neutral shared damage, and simultaneous death.

Acceptance criteria:

- Resolver has no `MonoBehaviour`, scene, UI, animation, or asset lookup dependency.
- Tests cover all invariants that could regress combat behavior.
- Preview never mutates card values.

## Phase 5 - Game Manager and Round Flow

- [x] Implement `GameManager`.
  - Own one `GameState`.
  - Initialize a new run.
  - Expose events for phase/state changes.
- [x] Implement `RoundController.DrawCards()`.
  - Draw until hand reaches the number of empty player board slots or deck is empty.
  - Clear `placementOrder`.
  - Move phase to placement.
- [x] Implement `RoundController.PlaceCard()`.
  - Move card from hand to the selected board slot.
  - Append card to `placementOrder`.
- [x] Implement `RoundController.UnplaceCard()`.
  - Return card to hand.
  - Remove card from `placementOrder`.
- [x] Implement `RoundController.CanResolve()`.
  - True when all player slots are filled or hand is empty.
- [x] Implement resolution-order calculation.
  - Use placement order first.
  - Skip slots with no enemy card.
  - Append remaining filled slots in index order for edge cases.
- [x] Add tests for draw, place, unplace, can-resolve, and resolution ordering.

Acceptance criteria:

- A round can progress from draw to placement to combat without UI.
- Placement order survives slot indices correctly.
- Partially empty boards are valid only when the hand is empty.

## Phase 6 - Staged Combat Controller

- [x] Implement `CombatController.PrepareResolveRound()`.
  - Set phase to combat.
  - Build the staged resolution queue.
- [x] Implement `CombatController.ResolveCombatStep()`.
  - Resolve one queued column at a time.
  - Return or publish the combat result for UI.
- [x] Implement `CombatController.FinalizeResolveRound()`.
  - Move dead player cards to player cemetery.
  - Move dead enemy cards to enemy cemetery.
  - Return surviving player cards to hand.
  - Refill enemy board from enemy deck left to right.
  - Increment round.
  - Set next phase to end, reward, or draw.
- [x] Ensure enemy deck refills only during final cleanup, never after each fight.
- [x] Add tests for cleanup, death handling, enemy refill, loss, reward transition, and continue transition.

Acceptance criteria:

- Fights resolve one column at a time.
- Cleanup exactly matches `unity-migration.md` section 5.5.
- End, reward, and continue outcomes are deterministic in tests.

## Phase 7 - Reward and Encounter Loop

- [x] Implement `RewardController.GenerateRewardChoices()`.
  - Shuffle a fresh player deck copy and take three cards.
- [x] Implement reward selection.
  - Add selected card to `playerDeck`.
- [x] Implement new encounter setup.
  - Create new enemy deck and board.
  - Reset round to `1`.
  - Move phase to draw.
- [x] Decide whether the added reward card enters the draw pile immediately or after shuffle, and document the exact behavior.
  - Decision: card is appended to the end of `playerDeck` without reshuffling. It will be drawn in a future round when the draw pile reaches it.
- [x] Add tests for reward choice count, selected card ownership, encounter reset, and player deck persistence.

Acceptance criteria:

- Clearing an encounter reliably opens reward selection.
- Selecting one reward starts a new encounter.
- Player deck persists while enemy state resets.

## Phase 8 - UI Foundation

- [x] Build UI with UGUI as the default first-playable UI stack.
- [x] Create the main gameplay scene layout.
- [x] Implement `CardView`.
  - Render name, RPS type, value, role, owner, icon or sprite, and flavor.
  - Bind to `CardInstance`.
- [x] Implement `SlotView`.
  - Represent player and enemy slots.
  - Expose hover/drop state.
- [x] Implement `BoardView`.
  - Manage three player slots and three enemy slots.
- [x] Implement `HandView`.
  - Render `playerHand`.
- [x] Implement `HUDView`.
  - Show phase, round, resolve/continue action, and game outcome.
- [x] Implement `DeckPanelView`.
  - Show player/enemy deck counts and cemetery counts.
- [x] Connect views to `GameManager` events.
- [x] Create production gameplay prefabs instead of copying demo prefabs from imported packages.
  - First-playable decision: `GameplaySceneBootstrap` builds the UGUI hierarchy and reusable card templates at runtime in `Assets/Scenes/Gameplay.unity`; no imported demo prefabs are copied.

Acceptance criteria:

- Current `GameState` can be inspected visually in play mode.
- UI updates after draw, placement, combat step, cleanup, reward, and end transitions.
- UI does not own gameplay rules.
- The UI can run without Rewired, MMFeedbacks, NiceVibrations, or Quibli enabled.

## Phase 9 - Input and Interaction

- [x] Use Unity EventSystem drag/drop interfaces for first playable card interaction.
- [x] Implement drag begin, drag, and drag end on `CardView`.
- [x] Implement valid player slot drop handling.
- [x] Implement unplace interaction.
- [x] Implement resolve button behavior.
- [x] Implement continue button behavior during staged combat.
- [x] Add slot hover highlights and invalid-drop feedback.
- [x] Confirm keyboard/mouse/touch expectations and document the supported baseline.
  - Baseline: mouse and touch pointer events through Unity `EventSystem` + UGUI drag/drop. Keyboard/gamepad navigation is deferred.
- [x] Defer Rewired integration unless controller navigation/remapping is explicitly required.

Acceptance criteria:

- A full round can be played manually in the Unity editor.
- Invalid actions do not corrupt `GameState`.
- Resolve is only available when `CanResolve()` is true.
- Card interaction works with standard Unity UI pointer events before optional controller support is added.

## Phase 10 - Combat Result, Tooltip, and Preview UI

- [x] Implement `CombatResultView`.
  - Show RPS outcome, rolls, chosen rolls, damage, before/after values, and deaths.
- [x] Implement `TooltipView`.
  - Show preview for placed cards before combat.
- [x] Show preview badges on placed cards.
  - Best-case and worst-case outgoing damage.
  - Player death risk where applicable.
- [x] Ensure combat preview is hidden or disabled during inappropriate phases.

Acceptance criteria:

- Players can understand expected risk before resolving.
- Combat results match resolver output exactly.
- Tooltip and badges use read-only preview data.

## Phase 11 - Animation and Feedback

- [x] Add DOTween-based card lift, drag, snap, and return animations using `AnimConfig`.
- [x] Add staged combat reveal animation.
- [x] Add defeated-card tilt and cleanup animation.
- [x] Add Feel/MMFeedbacks hooks for hits, defeats, reward selection, and game over where appropriate.
  - First-playable hook point: `GameplayUIView` exposes `UnityEvent` feedback hooks without requiring MMFeedbacks at runtime.
- [x] Add NiceVibrations haptics only after desktop interaction is stable.
  - Decision: haptics remain deferred until mobile polish; desktop pointer interaction is the current baseline.
- [x] Add basic audio/VFX placeholders only where they support clear feedback.
  - Decision: DOTween UI feedback is sufficient for first playable; no placeholder audio/VFX assets were added.
- [x] Verify animations do not change gameplay ordering or timing assumptions.

Acceptance criteria:

- Animations communicate state changes without becoming gameplay state.
- Combat remains correct if animations are skipped or sped up.
- All timings come from `AnimConfig`.
- DOTween drives view animation; MMFeedbacks and haptics remain optional presentation hooks.

## Phase 12 - Visual Polish and Responsiveness

- [ ] Apply card dimensions and corner radius from `CardVisualConfig`.
- [ ] Establish a consistent card visual hierarchy.
- [ ] Integrate TrueShadow where it improves card readability.
- [ ] Apply Quibli/URP visual direction if 3D or stylized materials are used.
- [ ] Keep Quibli optional unless a stylized 3D/2.5D scene treatment is intentionally added.
- [ ] Check layout in common desktop and mobile aspect ratios.
- [ ] Ensure text does not overflow cards, buttons, or panels.

Acceptance criteria:

- Cards remain readable at target resolutions.
- Board, hand, HUD, and reward selection are usable on desktop and mobile aspect ratios.
- Visual polish does not introduce gameplay dependencies.
- TrueShadow, Quibli, and URP polish can be adjusted without changing gameplay state or resolver behavior.

## Phase 13 - Tests and Verification

- [ ] Add edit-mode tests for pure data and combat logic.
- [ ] Add play-mode tests for round flow and controller integration.
- [ ] Add a deterministic test scenario covering a complete encounter clear.
- [ ] Add a deterministic test scenario covering player loss.
- [ ] Add a manual QA checklist for editor playthroughs.
- [ ] Document test commands in `AGENTS.md`.

Acceptance criteria:

- Core gameplay behavior is protected by automated tests.
- Manual verification has a repeatable checklist.
- Future agents know which command to run before marking work done.

## Phase 14 - First Playable Milestone

- [ ] Verify a new run starts correctly.
- [ ] Verify player can draw, place, unplace, and resolve cards.
- [ ] Verify combat resolves column by column.
- [ ] Verify dead cards move to cemeteries.
- [ ] Verify surviving player cards return to hand.
- [ ] Verify enemy board refills after each round.
- [ ] Verify reward draft appears after enemy clear.
- [ ] Verify selected reward starts a new encounter.
- [ ] Verify game over appears when player has no cards left.
- [ ] Record known issues and next polish priorities.

Acceptance criteria:

- The game is playable from start to loss or reward loop without console errors.
- The implemented behavior matches the migration guide's rules.
- Remaining work is documented as follow-up tasks, not hidden assumptions.

## Cross-Agent Task Template

Use this template when claiming or finishing a task:

```markdown
### Task

- Plan item:
- Agent:
- Started:
- Finished:
- Files changed:
- Verification:
- Notes / follow-ups:
```

## Agent Work Log

Add entries newest first.

### 2026-04-21 - Codex - Phase 11 Animation and Feedback

- Plan item: Phase 11 - Animation and Feedback.
- Files changed: `Assets/Scripts/CardsUnity.Runtime.asmdef`, `Assets/Scripts/Controllers/CombatController.cs`, `Assets/Scripts/UI/CardView.cs`, `Assets/Scripts/UI/CombatResultView.cs`, `Assets/Scripts/UI/GameplaySceneBootstrap.cs`, `Assets/Scripts/UI/GameplayUIView.cs`, `Assets/Scripts/UI/TooltipView.cs`, `docs/implementation-plan.md`.
- Verification: Compiled through Unity MCP; Play Mode opened `Assets/Scenes/Gameplay.unity` with a clean console; runtime smoke placed cards, resolved a combat step, confirmed active DOTween tweens, and verified `CombatResultView`; all 94 EditMode tests passed after rerunning the MCP test job when the first test job timed out during initialization.
- Notes / follow-ups: `CardsUnity.Runtime` now references the DOTween precompiled assembly. Animation timings are sourced from `AnimConfig`. `CombatController.ResolveCombatStep` now notifies state changes before firing `OnCombatResult` so reveal animations run after UI rebinding. MMFeedbacks can be wired to `GameplayUIView` UnityEvents later; NiceVibrations and audio/VFX assets remain deferred polish.

### 2026-04-21 - Codex - Phase 10 Combat Result, Tooltip, and Preview UI

- Plan item: Phase 10 - Combat Result, Tooltip, and Preview UI.
- Files changed: `Assets/Scripts/UI/CardView.cs`, `Assets/Scripts/UI/BoardView.cs`, `Assets/Scripts/UI/SlotView.cs`, `Assets/Scripts/UI/CombatResultView.cs`, `Assets/Scripts/UI/TooltipView.cs`, `Assets/Scripts/UI/GameplayUIView.cs`, `Assets/Scripts/UI/GameplaySceneBootstrap.cs`, `docs/implementation-plan.md`.
- Verification: Compiled through Unity MCP; Play Mode opened `Assets/Scenes/Gameplay.unity` with a clean console; runtime smoke placed cards, generated preview badges, resolved a combat step, and confirmed `CombatResultView` activated with resolver output; all 94 EditMode tests passed.
- Notes / follow-ups: Preview data is calculated through `CombatResolver.GetCombatPreview` and does not mutate card values. Tooltip and preview badges are visible only during placement, while combat results are visible only during combat.

### 2026-04-21 - Codex - Phase 9 Input and Interaction

- Plan item: Phase 9 - Input and Interaction.
- Files changed: `Assets/Scripts/UI/CardView.cs`, `Assets/Scripts/UI/SlotView.cs`, `Assets/Scripts/UI/BoardView.cs`, `Assets/Scripts/UI/HandView.cs`, `Assets/Scripts/UI/GameplayUIView.cs`, `Assets/Scripts/UI/HUDView.cs`, `docs/implementation-plan.md`.
- Verification: Compiled through Unity MCP; Play Mode opened `Assets/Scenes/Gameplay.unity` with a clean console; runtime smoke verified place/unplace state transitions through the scene's UI/controller wiring; all 94 EditMode tests passed.
- Notes / follow-ups: Card dragging uses UGUI `IBeginDragHandler`, `IDragHandler`, and `IEndDragHandler`; slots use `IDropHandler` and click-to-unplace. Invalid targets highlight red, valid player slots highlight green. Mouse/touch pointer input is the supported baseline; keyboard/gamepad/Rewired remain deferred.

### 2026-04-21 - Codex - Phase 8 UI Foundation

- Plan item: Phase 8 - UI Foundation.
- Files changed: `Assets/Scripts/UI/`, `Assets/Scripts/Controllers/CombatController.cs`, `Assets/Scripts/Controllers/RewardController.cs`, `Assets/Scripts/Controllers/RoundController.cs`, `Assets/Scenes/Gameplay.unity`, `docs/implementation-plan.md`.
- Verification: Compiled through Unity MCP; opened `Assets/Scenes/Gameplay.unity` in Play Mode with a clean console after fixing Unity 6 built-in font usage; all 94 EditMode tests passed.
- Notes / follow-ups: UGUI views now render game state and subscribe through `GameplayUIView`. `GameplaySceneBootstrap` creates the first-playable layout and auto-draws the opening hand for inspection. Phase 9 should add real drag/drop placement, unplace behavior, and invalid-drop feedback. The MCP camera screenshot captured only the Main Camera background because the UI canvas is Screen Space Overlay.

### 2026-04-21 - Claude - Phase 7 Reward and Encounter Loop

- Plan item: Phase 7 - Reward and Encounter Loop.
- Files changed: `Assets/Scripts/Runtime/RewardFlow.cs`, `Assets/Scripts/Controllers/RewardController.cs`, `Assets/Scripts/Tests/RewardFlowTests.cs`, `docs/implementation-plan.md`.
- Verification: Compiled without errors; all 94 EditMode tests passed (17 new in RewardFlowTests, 77 existing).
- Notes / follow-ups: Reward choices pool from all zones (deck + hand + cemetery + board) to avoid empty pool edge cases. Selected card appended to end of `playerDeck` without reshuffle — this is documented in the plan. `RewardController.SelectRewardAndStartEncounter` combines selection + new encounter in one call to keep UI simple.

### 2026-04-21 - Claude - Phase 6 Staged Combat Controller

- Plan item: Phase 6 - Staged Combat Controller.
- Files changed: `Assets/Scripts/Runtime/CombatFlow.cs`, `Assets/Scripts/Controllers/CombatController.cs`, `Assets/Scripts/Tests/CombatFlowTests.cs`, `docs/implementation-plan.md`.
- Verification: Compiled without errors; all 77 EditMode tests passed (19 new in CombatFlowTests, 58 existing).
- Notes / follow-ups: Pure logic lives in `CombatFlow` static class (testable without MonoBehaviour). `CombatController` is the MonoBehaviour integration layer — it holds the `Queue<int>` and `Random` instance, fires `OnCombatResult` event for UI, and delegates cleanup/phase to `CombatFlow`. `DetermineNextPhase` is a separate public method so Phase 7 (`RewardController`) can call it independently if needed.

### 2026-04-21 - Claude - Phase 5 Game Manager and Round Flow

- Plan item: Phase 5 - Game Manager and Round Flow.
- Files changed: `Assets/Scripts/Runtime/RoundFlow.cs`, `Assets/Scripts/Controllers/GameManager.cs`, `Assets/Scripts/Controllers/RoundController.cs`, `Assets/Scripts/Tests/RoundFlowTests.cs`, `docs/implementation-plan.md`.
- Verification: Compiled without errors; all 21 EditMode tests passed (RoundFlowTests suite).
- Notes / follow-ups: Round logic extracted to `RoundFlow` static class for testability. `GameManager` loads card definitions from Resources and fills enemy board on `StartNewRun()`. `RoundFlow.FillEnemyBoard` is also called by Phase 6 CombatController after each round.

### 2026-04-21 - Claude - Phase 4 Combat Resolver

- Plan item: Phase 4 - Combat Resolver.
- Files changed: `Assets/Scripts/Combat/RpsResult.cs`, `Assets/Scripts/Combat/CombatResult.cs`, `Assets/Scripts/Combat/CombatPreview.cs`, `Assets/Scripts/Combat/CombatResolver.cs`, `Assets/Scripts/Tests/CombatResolverTests.cs`, `docs/implementation-plan.md`.
- Verification: Compiled without errors via Unity MCP refresh; all 23 EditMode tests passed (CombatResolverTests suite).
- Notes / follow-ups: Phase 5 can use `CombatResolver.ResolvePair` and `GetCombatPreview` directly. `CombatResolver` has no MonoBehaviour, scene, or asset dependency.

### 2026-04-21 - Codex - Phase 3 Card Definitions and Deck Factory

- Plan item: Phase 3 - Card Definitions and Deck Factory.
- Files changed: `Assets/Scripts/Data/`, `Assets/Scripts/Tests/`, `Assets/Resources/CardDefinitions/`, `docs/implementation-plan.md`.
- Verification: Created player/enemy `CardData` assets from the migration guide/source prototype, compiled through Unity MCP, and ran EditMode tests for deck creation, cloning, ownership, value ranges, and deterministic shuffle.
- Notes / follow-ups: Phase 4 can use `DeckFactory` output directly for combat resolver tests.

### 2026-04-21 - Codex - Phase 2 Core Data Model

- Plan item: Phase 2 - Core Data Model.
- Files changed: `Assets/Scripts/Data/`, `Assets/Scripts/Tests/`, `docs/implementation-plan.md`.
- Verification: Added ScriptableObject/plain C# data model, validated scripts through Unity MCP, and ran EditMode data model tests.
- Notes / follow-ups: Phase 3 should create `CardData` assets and `DeckFactory` cloning/shuffle logic on top of these types.

### 2026-04-20 - Codex - Phase 1 Configuration Assets

- Plan item: Phase 1 - Configuration Assets.
- Files changed: `Assets/Scripts/Config/`, `Assets/Resources/Config/`, `docs/implementation-plan.md`.
- Verification: Created config ScriptableObject classes with defaults from `docs/unity-migration.md` section 8; created default Resources config assets; validated scripts in Unity and checked console after compilation.
- Notes / follow-ups: Runtime systems should use serialized config references where possible, with `Resources.Load<T>("Config/Default...")` as a first-playable fallback.

### 2026-04-20 - Codex - Phase 0 Project Setup

- Plan item: Phase 0 - Project Setup and Conventions.
- Files changed: `Assets/Scripts/`, `docs/phase-0-project-setup.md`, `docs/implementation-plan.md`, `AGENTS.md`.
- Verification: Confirmed UGUI and Unity Test Framework in `Packages/manifest.json`; confirmed TextMeshPro is not present in `Packages/manifest.json` or `Packages/packages-lock.json`; verified Unity 6000.3.10f1 executable path exists; validated new asmdef JSON files. Attempted EditMode batchmode test run, but Unity stopped during licensing initialization before project tests could run.
- Notes / follow-ups: Phase 1 should add config ScriptableObjects and default config assets without hardcoding tuning values.

### 2026-04-20 - Codex - Implementation Plan

- Plan item: Initial planning document.
- Files changed: `docs/implementation-plan.md`.
- Verification: Reviewed `docs/unity-migration.md` and created task breakdown with acceptance criteria.
- Notes / follow-ups: First implementation agent should start with Phase 0 and Phase 1 unless project structure already changes.

### 2026-04-20 - Codex - Existing Asset Review Integrated

- Plan item: Existing Unity assets and implementation usage guidance.
- Files changed: `docs/implementation-plan.md`.
- Verification: Reviewed imported asset folders, package manifest, render settings, input asset, and major plugin assets.
- Notes / follow-ups: First playable should use UGUI, Unity EventSystem, DOTween, and Unity Test Framework. Rewired, MMFeedbacks, NiceVibrations, TrueShadow, and Quibli should be layered in according to the phase guidance.
