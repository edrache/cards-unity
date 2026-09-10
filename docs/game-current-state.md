# Game documentation: current playable prototype

Snapshot: 10 September 2026. Primary scene: [`ProceduralCave.unity`](../Assets/Scenes/ProceduralCave.unity).

This document describes the saved scene, its referenced prefabs and the current project-owned runtime code. It records implemented behaviour, rather than a proposed design. Values below are saved scene/prefab settings unless explicitly described as options. Generated population counts are calculated from the generator, not a fresh Play Mode census. Unsaved Unity Editor changes are outside this snapshot.

## 1. Current game experience

The project is a playable third-person cave exploration prototype with an overhead orthographic camera. The player controls a procedurally animated armoured knight carrying a finite-life torch. The cave contains light-sensitive centipedes and fallen knights with additional, independently burning torches.

The implemented interactions create this gameplay sequence:

1. Start in the cave entrance and explore connected chambers and winding passages.
2. Use torch light to see the environment and influence nearby creatures.
3. Encounter centipedes that become alerted, pursue along shadow edges, flee illumination, or become provoked and lunge.
4. Evade lunges; a successful enemy hit forces the knight onto their knees and drops the torch.
5. Move to recover, then retrieve the dropped torch or take one beside a fallen knight.
6. Continue exploring while torch fuel runs down and encounters increase the knight's visible stress.

There is no implemented completion objective, victory sequence, health-based death loop or progression system in the reviewed gameplay code. The scene includes the displayed text **“The Mercy”**, but does not implement a story or quest system around it.

## 2. What the saved cave scene contains

### Authored scene objects

| Object | Role |
| --- | --- |
| Shadow Knight | Player prefab with movement, procedural animation, torch, attack, interaction, knockdown and stress components. |
| Procedural Cave | Generator and references to the player, cave materials, rocks, centipedes and fallen bodies. |
| Main Camera | Orthographic follow camera and AudioListener. |
| Rewired Input Manager | Shared input configuration, using Player0. |
| Music | Automatically started, looping background track. |
| Canvas / Text (TMP) | Displays “The Mercy”. |
| EventSystem | UI event handling. |

The cave floor, walls, residents and bodies are generated children. They are rebuilt for preview and Play Mode instead of being individually stored as authored scene objects. Regeneration replaces these children and places the player in the entrance, facing inward.

### Saved generator configuration

| Setting | Value |
| --- | --- |
| Room Count / Seed | 15 / 1 |
| Room Radius / Room Size Variation | 6.65 m / 1 |
| Extra Connections | 0.074 |
| Corridor Width / Winding | 3.12 m / 0.8 |
| Branch Density / Entrance Length | 0.5 / 17.97 m |
| Irregularity | 1 |
| Elevation | Enabled; range 6 m; maximum slope 15° |
| Visible cutaway wall height / boundary wall height | 1.5 m / 20 m |
| Rock Density | **0: scattered rock spawning is currently disabled** |
| Available rock size range | 1.2–2.6 scale |
| Centipede Room Percentage | 90%: **14 centipedes** calculated for 15 rooms |
| Body Room Percentage | 25%: **4 fallen knights** calculated for 15 rooms |

Population counts use nearest-integer rounding with halves rounded up. Each selected room receives one member of that population; the entrance is not a room. Body and centipede selections use independent seeded shuffles, so a room can contain both. The expected initial torch supply is the player's torch plus four corpse torches; these are already burning, not stored fresh replacements.

### Cave generation and traversal

Chambers are distributed with sunflower packing and varied radii. A distance-based spanning tree connects them, and extra links can introduce loops. Corridors have multiple bends and optional side branches. The floor is a continuous height field: elevation changes remain connected at junctions, with no stacked floors. Disabling elevation produces a level cave.

Walls use generated meshes and collision geometry. Their visible tops are cut away for the overhead view, while taller boundary collision keeps the player inside. Creature surface queries also account for the visible wall geometry.

Four reusable low, broad rock prefabs exist under `Assets/Prefabs/CaveRocks/`. Enabling Rock Density scatters them deterministically, aligns them to slopes and avoids overlaps and cave boundaries. Placement reserves routes along corridor centres and clearance around the spawn and bodies. The saved zero density means this capability is available but contributes no scattered rocks to the current layout.

The Inspector supports 1–24 rooms, a layout seed, room size and variation, connections, corridor shape, branches, entrance length, wall shape, elevation, rock settings and population percentages. Keep the generator at unit scale; use its settings or **Rebuild Cave** to change the layout.

## 3. Player: Shadow Knight

### Movement and controls

Movement is relative to the camera, with acceleration, braking, gravity and CharacterController collision. Saved base speed is 2 m/s and run speed is 7 m/s, with acceleration 9 and braking 7. Sneak selects an animation style; the controller does not define a separate keyboard sneak speed. Analog deflection scales movement speed.

| Input | Implemented action |
| --- | --- |
| WASD / arrows | Move. |
| Left gamepad stick | Move; deflection selects Sneak or Walk. |
| Hold Run (keyboard Left Shift) | Run; overrides the current Sneak/Walk request. |
| C | Toggle Sneak from the keyboard. |
| Hold Space, then release | Raise the torch, hold the preparation pose, then strike. |
| E | Contextual torch drop or pickup. |

The scene uses the shared Rewired configuration. Attack and torch interaction have keyboard bindings; dedicated gamepad bindings for them still need to be authored in the Rewired Inspector. In scenes without a Rewired manager, the controller provides an Input System fallback.

The saved stick Idle Threshold is 0.08 and Walk Threshold is 0.35. A centred stick releases automatic style selection; running is requested by a button, never by stick deflection alone. Releasing Run restores the prior style mix or the current input-requested style.

The CharacterController step limit follows calf length, currently 0.524 m. Additional steep-riser checks prevent the rounded capsule from climbing higher ledges. Foot IK samples supporting ground and the recovery path. There is no player-controlled physical jump.

### Procedural body and animation

The knight is a procedural articulated rig built from meshes, with an optional helmet, independently sized limbs and a three-joint spine plus neck. It is not an imported Humanoid animation setup. `ShadowKnightProportions` exposes body dimensions and updates rig positions, leg IK, collider sizing and the torch grip.

The animation mixer provides twelve styles: Walk, Double Bounce Walk, Strut, Shuffle, Sneak, Run, Jump, Fast Run, Tip Toe, Skip, Knee Walk and Crawl. The saved mix is Walk 0.127 and Double Bounce Walk 0.381, with other weights zero; normalization yields 25% Walk and 75% Double Bounce Walk. All-zero weights fall back to Walk.

Distance actually travelled advances the gait, so standing still or pressing against a wall does not run a full walking cycle. Animation includes two-bone leg IK, body bounce and lean, independent upper-arm/forearm motion, noise, spine deformation and head stabilization. Jump-like styles animate the pose without jumping the controller.

Knee Walk and Crawl are also manually selectable visual styles. Their low poses persist at rest and suppress ordinary footsteps when dominant. They do not shorten the CharacterController or constitute an exhaustion system. Combat knockdown applies its own knee overlay without rewriting the saved style mix.

## 4. Torch: light, fuel, attack and interaction

### Light and fuel

The reusable `Handheld Torch.prefab` contains a light, fire and smoke. Saved brightness is 6.2 and light range is 16.5 m. Flicker, movement response and inertia animate the held torch; world-space particles leave trails as it moves.

| Fuel rule | Current value / behaviour |
| --- | --- |
| Lifetime | 180 gameplay seconds per torch. |
| Burnout phase | Final 40% of lifetime: illumination and emission fade, with stronger flicker. |
| Strike penalty | 5 seconds of lifetime per strike. |
| Drop impact penalty | 10 seconds on the first collision after each drop. |
| Subsequent bounces | No additional impact penalty for the same drop. |
| Pickup or disable/re-enable | Does not refill fuel. |
| Complete burnout | Light and new fire/smoke emission reach zero; existing particles finish. |

Dropped and corpse torches burn independently of being held. Extinguished torches remain pickable. No refuelling or relighting interaction is implemented.

### Player torch strike

Pressing Attack raises the right arm over and behind the head; holding keeps it cocked. Release starts the forward/downward swing after the minimum windup has finished, including for a very short tap. Saved windup/strike/recovery durations are 0.14 / 0.10 / 0.30 seconds.

The pose blends over the torch-holding animation and can twist the chest. Movement and turning continue during the swing. Additional presses during a swing are not buffered. A held torch is required; interaction and knockdown restrict attacks.

**The player strike currently provides animation, a strike event, a hit-window flag and fuel consumption. It does not detect enemy hits, deal damage or kill centipedes.** Holding the charge adds no extra penalty beyond ordinary burning.

### Dropping, retrieving and swapping torches

E drops a held torch or attempts to retrieve the nearest available one with empty hands. There is no inventory: to take another torch, first release the current one. The prompt displays “Upuść” or “Podnieś”. The cave scene overrides Pickup Range to **2 m**, versus 1 m on the player prefab.

Dropping lowers the arm and releases the same object into Rigidbody/capsule-collider physics. Pickup turns toward the target, crouches and uses arm IK to reach the grip before attachment. It supports moving or rolling torches, but rechecks clear reach, distance and hand proximity before transferring ownership. A wall or an unreachable moving torch can prevent pickup.

Movement pauses during this interaction; repeated requests and attacks are rejected. Drop/pickup preparation takes 0.35 / 0.75 seconds, followed by a 0.45-second recovery blend. Successful pickup preserves fuel and binds torch attacks to the newly acquired torch, including one taken from a corpse.

## 5. Cave inhabitants

### Fallen knights

`Fallen Knight.prefab` supplies a static fallen body and a separate torch beside it. Generation varies body proportions and orientation deterministically and aligns the body to the local slope. Bodies are decorative and do not block movement; they do not run player input, gait, stress or combat logic.

Their torches start lit, share the normal 180-second lifetime and participate in creature light reactions. They can alert nearby dormant residents before the player reaches that room. The body itself has no loot menu or other interaction; the torch uses the regular pickup system.

### Centipede body and movement

The current prefab has 14 segments at size 1, with two legs per segment. Editable limits are 3–48 segments and size 0.25–3. Each instance builds its own disposable procedural rig. Body segments follow the recorded head path and legs use a travelling wave driven by distance, with individual foot-contact events.

Base stalking speed is 1.2 m/s and fleeing speed is 4.5 m/s. Behaviour variation adds independent steering, timing and speed differences. Room placement is repeatable from the cave seed; the prefab's zero Random Seed allows per-instance runtime behaviour variation.

Navigation uses local surface probes and steering, not a global pathfinding solution. In caves it stays on interior floors, rocks and inward-facing walls, rejecting outer surfaces, cutaway rims and ceilings. Normal stalking prefers the floor; flight can use inner walls, and safe creatures try to descend. Recovery detects poor progress, tries another heading and can backtrack along recent surface history. Missing adjoining surfaces can still stop movement.

### Awareness and light response

| State | Behaviour |
| --- | --- |
| Dormant | Waits until the player enters the assigned room or faint light reaches any segment. |
| Stalking | Approaches the player along safe shadow edges, including orbiting nearby targets. |
| Fleeing | Moves away from unsafe head illumination. |
| Hiding | Dark cooldown that continues moving and tracking the player. |
| Enraged | Provoked pursuit that approaches despite light. |
| WindingUp | Raises the front body chain before a lunge. |
| Leaping | Commits to a straight attack movement with a visible arc. |

Once alerted, a creature stays alerted after the player leaves its room. Point and spot lights contribute to exposure; directional light is ignored. Shadow-casting lights use collider occlusion, so the player's body can shelter a creature from their own torch. Exposure is a gameplay calculation, not a reading of rendered dither pixels or baked lighting.

Saved tuning includes Dim Light Threshold 0.005, Fear Threshold 0.12, Safe Light Ratio 0.4 and Hide Duration 2.5 seconds. Head exposure drives flight and its exit, while all segments can activate a dormant resident. Pursuit searches for a nearby safe radius and follows shadow edges; this does not guarantee successful routing through every cave obstacle.

### Enemy attacks

An alerted creature can be provoked by either prolonged illumination or darkness around the player:

- Head illumination above the reaction threshold accumulates toward Light Patience, currently 3 seconds. Darkness drains this accumulated timer gradually.
- Player-area exposure at or below 0.02 for 0.75 seconds triggers the darkness route. The brightest of four surrounding samples is used, ignoring the target's own colliders to avoid confusing the knight's capsule shadow with an unlit area.

Provocation leads to Enraged pursuit. Within 3.5 m, on a floor-like surface and with a clear approach, the creature winds up for 0.65 seconds and lunges for 0.45 seconds. The visible arc is 0.8 m high; movement still requires supported terrain and cannot jump gaps. Collision checks can stop the lunge, and a player moving out of its committed path can evade it. The attack cooldown is 4 seconds.

Contact between the attacking head and the player's CharacterController attempts a knockdown. This is an implemented enemy hit consequence, distinct from the player's currently animation-only torch strike.

## 6. Knockdown and stress

### Knockdown recovery

An accepted hit cancels torch charging or interaction and immediately drops the held torch. The knight falls onto their knees over 0.25 seconds and can continue moving at 40% of normal movement speed. Sprint, torch attacks and pickup/drop interaction are unavailable while down.

Recovery requires actual displacement: 1.4 seconds moving on the knees, then 0.65 seconds moving to stand. Standing still or being blocked by a wall pauses progress, including during the rise. The original locomotion mix remains intact. Further hits are ignored while down and for 1.5 seconds after standing.

### Stress

Stress ranges from 0 to 100 and starts at zero. A centipede within 7 m and with clear collider line of sight adds 8 stress on an encounter. Continued presence does not repeatedly add points; 10 seconds unseen allows another encounter. This is omnidirectional proximity awareness, including dormant creatures, independent of illumination or camera visibility. An accepted lunge hit adds 25 stress.

Stress smoothly increases body and arm irregularity, spine hunch and head scanning, including while idle. It does not overwrite style weights. Saved visual limits include 24° hunch, 60° head scanning and Motion Chaos 1.

Automatic stress recovery is configurable but currently **zero**, so accumulated stress does not naturally decay. Stress is presently an animation input, with no implemented health damage, exhaustion drain or game-over threshold.

## 7. Presentation and sound

The camera is orthographic with size 15, follow offset `(0, 16, -7)` and smoothing 8. It follows player elevation. URP renderer assets include the project's **One Bit Dither** effect, producing stylized light/dark presentation.

The cave has no skybox or ambient illumination. Gameplay light comes from torches, including those beside fallen bodies and those dropped on the floor. The player's torch is therefore not the only possible light source in this scene.

Music uses `Stereo_Color_Dark-Background_main.wav`, looping automatically at volume 0.1 with a 4-second fade-in and 2-second fade-out. Footsteps use separate left/right samples with randomized pitch and style-dependent volume. Actual contacts trigger them; standing still produces none.

Centipedes use pooled leg-click audio, audible within 16 m and at full volume within 4 m, with distance falloff. The prefab permits 8 audible legs, 16 voices and at most 12 steps per frame. Footsteps, clicks and music route to the Master group of `Assets/Audio/Mixers/Deep.mixer`.

## 8. Implementation map

All scripts below are under [`Assets/Scripts/Controllers/`](../Assets/Scripts/Controllers/) in `CardsUnity.Controllers`.

| Script | Responsibility |
| --- | --- |
| `ProceduralCave.cs` | Layout, geometry, elevation, rock/body/resident spawning and player entrance placement. |
| `CaveBody.cs` | Fallen-body proportions, static pose and adjacent torch placement. |
| `ProceduralCharacter.cs` | Input, movement, gravity, step restrictions and action requests. |
| `CharacterFollowCamera.cs` | Smoothed overhead following. |
| `ShadowKnightProportions.cs` | Rig dimensions, collider/IK sizing and helmet visibility. |
| `CartoonCharacterGait.cs`, `GaitStylePose.cs` | Style mixing, distance-driven animation, terrain IK and pose overlays. |
| `ProceduralSpine.cs` | Spine/neck motion and runtime torso deformation. |
| `HandheldTorch.cs` | Holding pose, light/particles, fuel and physical drop/pickup lifecycle. |
| `TorchAttack.cs`, `TorchInteraction.cs` | Player swing and contextual torch transfer. |
| `ProceduralCentipede.cs`, `ProceduralCentipede.Attack.cs` | Creature rig, awareness, steering, light response and lunges. |
| `CrawlSurfaceMesh.cs` | Accelerated queries against visible cave surfaces. |
| `CharacterKnockdown.cs`, `CharacterStress.cs` | Hit recovery and threat-driven pose changes. |
| `FootstepAudio.cs`, `CentipedeLegAudio.cs`, `MusicPlayer.cs` | Contact sounds and background music. |

Key prefabs: [`Shadow Knight`](../Assets/Prefabs/Shadow%20Knight.prefab), [`Handheld Torch`](../Assets/Prefabs/Handheld%20Torch.prefab), [`Centipede`](../Assets/Prefabs/Centipede.prefab), [`Fallen Knight`](../Assets/Prefabs/Fallen%20Knight.prefab), and [`Rewired Input Manager`](../Assets/Prefabs/Rewired%20Input%20Manager.prefab).

The project uses Unity **6000.3.10f1** and URP **17.3.0**. Other scenes remain available: `ProceduralMovement` for daytime locomotion, `TorchNight` for the dark obstacle playground, and `SampleScene` as the original scaffold. Installed third-party packages are not evidence that their systems are used by this cave prototype.

## 9. Scope and verification limits

The reviewed implementation does not currently provide player weapon damage, enemy death, a health meter, an inventory, torch refuelling, an exhaustion resource, quests, a completion trigger, save/load progression or a physical jump action. Knee Walk and Crawl do not enable reduced-height collision traversal.

Existing project notes record direct Unity method/physics checks for cave reachability, terrain traversal, population generation, torch burnout and transfer, creature light reactions, lunges, knockdown recovery and stress. These are historical checks, not a persistent automated test suite. They cover selected scenarios; complex cave routing, extreme body/creature proportions and all combinations of slopes, attacks and low postures are not exhaustively verified.

This documentation task inspected saved assets and source code without modifying or running the scene. It did not repeat Play Mode tests or test physical keyboard/gamepad input. The saved configuration above takes precedence over older example settings and historical test-scene counts in [`AGENTS.md`](../AGENTS.md).
