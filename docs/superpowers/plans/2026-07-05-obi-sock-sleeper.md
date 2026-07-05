# Obi Sock Sleeper Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stop simulating settled Obi Cloth socks by freezing their particles in place (`invMass = 0`) while keeping them collidable and visually unchanged, and wake them only when the sock (or a sock near it) is grabbed.

**Architecture:** A pure, unit-testable state machine (`SockSleepState`) decides Awake/Asleep transitions from particle-speed samples and timers. A thin `MonoBehaviour` (`ObiSockSleeper`) wraps it, reading/writing Obi's `ObiSolver.velocities`/`ObiSolver.invMasses` arrays for its own actor's particles, and maintains a static registry for proximity wake-ups. `SockDragController` gets two small additive hooks to notify grab/release.

**Tech Stack:** Unity 6000.3.10f1, Obi (`Assets/Obi/`), NUnit EditMode tests (`CardsUnity.Tests` assembly).

## Global Constraints

- All code, comments, and commit messages must be in English (per `CLAUDE.md`).
- New runtime code goes under `Assets/Scripts/` (assembly `CardsUnity.Runtime`, namespace `CardsUnity`); new tests go under `Assets/Scripts/Tests/` (assembly `CardsUnity.Tests`, namespace `CardsUnity.Tests`).
- Follow the existing `SockGrabStateMachine` / `SockGrabStateMachineTests` pattern: pure logic class with no Unity/Obi dependency, unit-tested; a separate `MonoBehaviour` wrapper handles Unity/Obi integration and is verified manually in the Editor (no PlayMode test infrastructure exists for Obi actors in this repo, and none is being introduced here).
- Verify EditMode tests with: `/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath . -runTests -testPlatform EditMode -testResults TestResults/EditMode.xml`

---

### Task 1: `SockSleepState` pure logic class

**Files:**
- Create: `Assets/Scripts/Controllers/SockSleep/SockSleepState.cs`
- Test: `Assets/Scripts/Tests/SockSleepStateTests.cs`

**Interfaces:**
- Produces: `CardsUnity.SockSleepState` with:
  - `enum State { Awake, Asleep }`
  - constructor `SockSleepState(float sleepVelocityThreshold, float minRestTimeBeforeSleep, float wakeUpRadius, float minActiveTimeAfterWake)`
  - `State CurrentState { get; }`
  - `float WakeUpRadius { get; }`
  - `bool Tick(float maxParticleSpeed, float deltaTime)` — advances timers by `deltaTime` given the current max particle speed; returns `true` only on the call that transitions `Awake` → `Asleep`.
  - `void WakeUp()` — forces `Awake`, resets the rest timer, and starts a grace period of `minActiveTimeAfterWake` seconds during which `Tick` cannot sleep it (a value of `0` means no grace period).

- [ ] **Step 1: Write the failing tests**

Create `Assets/Scripts/Tests/SockSleepStateTests.cs`:

```csharp
using NUnit.Framework;

namespace CardsUnity.Tests
{
    public class SockSleepStateTests
    {
        [Test]
        public void InitialState_IsAwake()
        {
            var state = new SockSleepState(0.05f, 0.5f, 0.4f, 1f);

            Assert.AreEqual(SockSleepState.State.Awake, state.CurrentState);
        }

        [Test]
        public void Tick_WithSpeedAboveThreshold_NeverSleeps()
        {
            var state = new SockSleepState(0.05f, 0.5f, 0.4f, 0f);

            for (int i = 0; i < 100; i++)
                state.Tick(1f, 0.02f);

            Assert.AreEqual(SockSleepState.State.Awake, state.CurrentState);
        }

        [Test]
        public void Tick_WithSpeedBelowThresholdForFullDuration_TransitionsToAsleep()
        {
            var state = new SockSleepState(0.05f, 0.5f, 0.4f, 0f);

            bool sleptOnThisTick = false;
            for (int i = 0; i < 24; i++)
                sleptOnThisTick = state.Tick(0.01f, 0.02f); // 24 * 0.02s = 0.48s, just under 0.5s

            Assert.IsFalse(sleptOnThisTick);
            Assert.AreEqual(SockSleepState.State.Awake, state.CurrentState);

            sleptOnThisTick = state.Tick(0.01f, 0.02f); // total 0.50s, exactly enough

            Assert.IsTrue(sleptOnThisTick);
            Assert.AreEqual(SockSleepState.State.Asleep, state.CurrentState);
        }

        [Test]
        public void Tick_SpeedSpikeResetsRestTimer()
        {
            var state = new SockSleepState(0.05f, 0.5f, 0.4f, 0f);

            for (int i = 0; i < 24; i++)
                state.Tick(0.01f, 0.02f); // 0.48s of rest accumulated, just under the 0.5s threshold

            state.Tick(1f, 0.02f); // speed spike must reset the rest timer to zero

            for (int i = 0; i < 24; i++)
                state.Tick(0.01f, 0.02f); // another 0.48s — if the reset worked, still short of 0.5s

            Assert.AreEqual(SockSleepState.State.Awake, state.CurrentState);
        }

        [Test]
        public void Tick_WhileAsleep_ReturnsFalseAndStaysAsleep()
        {
            var state = new SockSleepState(0.05f, 0.5f, 0.4f, 0f);

            for (int i = 0; i < 25; i++)
                state.Tick(0.01f, 0.02f);

            Assert.AreEqual(SockSleepState.State.Asleep, state.CurrentState);

            bool result = state.Tick(0.01f, 0.02f);

            Assert.IsFalse(result);
            Assert.AreEqual(SockSleepState.State.Asleep, state.CurrentState);
        }

        [Test]
        public void WakeUp_TransitionsToAwakeAndResetsRestTimer()
        {
            var state = new SockSleepState(0.05f, 0.5f, 0.4f, 0f);

            for (int i = 0; i < 25; i++)
                state.Tick(0.01f, 0.02f);

            Assert.AreEqual(SockSleepState.State.Asleep, state.CurrentState);

            state.WakeUp();

            Assert.AreEqual(SockSleepState.State.Awake, state.CurrentState);
        }

        [Test]
        public void WakeUp_WithGracePeriod_SuppressesSleepUntilElapsed()
        {
            var state = new SockSleepState(0.05f, 0.5f, 0.4f, 0.3f);

            state.WakeUp();

            for (int i = 0; i < 20; i++)
                state.Tick(0.01f, 0.02f); // 0.40s elapsed: 0.30s grace + only 0.10s of counted rest

            Assert.AreEqual(SockSleepState.State.Awake, state.CurrentState);

            for (int i = 0; i < 20; i++)
                state.Tick(0.01f, 0.02f); // another 0.40s, well past the 0.5s rest requirement after grace

            Assert.AreEqual(SockSleepState.State.Asleep, state.CurrentState);
        }

        [Test]
        public void WakeUp_WithZeroGracePeriod_AllowsSleepCheckImmediately()
        {
            var state = new SockSleepState(0.05f, 0.5f, 0.4f, 0f);

            state.WakeUp();

            for (int i = 0; i < 25; i++)
                state.Tick(0.01f, 0.02f); // 0.50s of rest, no grace period to wait out

            Assert.AreEqual(SockSleepState.State.Asleep, state.CurrentState);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath . -runTests -testPlatform EditMode -testResults TestResults/EditMode.xml`
Expected: compilation error — `SockSleepState` does not exist in the current context.

- [ ] **Step 3: Write the implementation**

Create `Assets/Scripts/Controllers/SockSleep/SockSleepState.cs`:

```csharp
namespace CardsUnity
{
    public class SockSleepState
    {
        public enum State
        {
            Awake,
            Asleep
        }

        public State CurrentState { get; private set; } = State.Awake;
        public float WakeUpRadius { get; }

        readonly float _sleepVelocityThreshold;
        readonly float _minRestTimeBeforeSleep;
        readonly float _minActiveTimeAfterWake;

        float _restTime;
        float _graceTime;

        public SockSleepState(float sleepVelocityThreshold, float minRestTimeBeforeSleep, float wakeUpRadius, float minActiveTimeAfterWake)
        {
            _sleepVelocityThreshold = sleepVelocityThreshold;
            _minRestTimeBeforeSleep = minRestTimeBeforeSleep;
            WakeUpRadius = wakeUpRadius;
            _minActiveTimeAfterWake = minActiveTimeAfterWake;
        }

        public bool Tick(float maxParticleSpeed, float deltaTime)
        {
            if (CurrentState == State.Asleep)
                return false;

            if (_graceTime > 0f)
            {
                _graceTime -= deltaTime;
                _restTime = 0f;
                return false;
            }

            if (maxParticleSpeed < _sleepVelocityThreshold)
                _restTime += deltaTime;
            else
                _restTime = 0f;

            if (_restTime < _minRestTimeBeforeSleep)
                return false;

            CurrentState = State.Asleep;
            return true;
        }

        public void WakeUp()
        {
            CurrentState = State.Awake;
            _restTime = 0f;
            _graceTime = _minActiveTimeAfterWake;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath . -runTests -testPlatform EditMode -testResults TestResults/EditMode.xml`
Expected: all `SockSleepStateTests` pass, no `result="Failed"` entries in `TestResults/EditMode.xml`.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Controllers/SockSleep/SockSleepState.cs Assets/Scripts/Tests/SockSleepStateTests.cs Assets/Scripts/Controllers/SockSleep/SockSleepState.cs.meta Assets/Scripts/Tests/SockSleepStateTests.cs.meta
git commit -m "Add SockSleepState timer/threshold state machine"
```

---

### Task 2: `ObiSockSleeper` MonoBehaviour

**Files:**
- Create: `Assets/Scripts/Controllers/SockSleep/ObiSockSleeper.cs`

**Interfaces:**
- Consumes: `CardsUnity.SockSleepState` from Task 1 (constructor, `Tick`, `WakeUp`, `CurrentState`, `WakeUpRadius`); `Obi.ObiActor` (`solver`, `solverIndices`, `particleCount`, `isLoaded`, `GetParticlePosition(int solverIndex)`); `Obi.ObiSolver` (`velocities: ObiNativeVector4List`, `invMasses: ObiNativeFloatList`).
- Produces: `CardsUnity.ObiSockSleeper` with:
  - `static readonly List<ObiSockSleeper> All`
  - `void OnGrabbed()` — marks this sock as held (exempt from sleeping) and wakes it.
  - `void OnReleased()` — clears the held flag so the rest timer resumes.
  - `void WakeUp()` — unfreezes particles (if frozen) and resets the internal state.
  - `static void WakeNearby(ObiSockSleeper source)` — wakes every `Asleep` sleeper within `source`'s `wakeUpRadius`.

- [ ] **Step 1: Write the implementation**

Create `Assets/Scripts/Controllers/SockSleep/ObiSockSleeper.cs`:

```csharp
using System.Collections.Generic;
using Obi;
using UnityEngine;

namespace CardsUnity
{
    public class ObiSockSleeper : MonoBehaviour
    {
        [Tooltip("Particle speed below which it counts as at rest.")]
        [SerializeField] float sleepVelocityThreshold = 0.05f;

        [Tooltip("Seconds all particles must stay below the threshold before sleeping.")]
        [SerializeField] float minRestTimeBeforeSleep = 0.5f;

        [Tooltip("Radius around a newly-grabbed sock's particles in which sleeping socks wake up.")]
        [SerializeField] float wakeUpRadius = 0.4f;

        [Tooltip("Grace period after waking before the sleep check resumes. 0 disables it.")]
        [SerializeField] float minActiveTimeAfterWake = 1f;

        public static readonly List<ObiSockSleeper> All = new List<ObiSockSleeper>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetAll() => All.Clear();

        ObiActor _actor;
        SockSleepState _state;
        float[] _originalInvMasses;
        bool _frozen;
        bool _isHeld;

        void Awake()
        {
            _actor = GetComponent<ObiActor>();
            _state = new SockSleepState(sleepVelocityThreshold, minRestTimeBeforeSleep, wakeUpRadius, minActiveTimeAfterWake);
            All.Add(this);
        }

        void OnDestroy()
        {
            All.Remove(this);
        }

        void FixedUpdate()
        {
            if (_isHeld || _actor == null || !_actor.isLoaded)
                return;

            if (_state.Tick(GetMaxParticleSpeed(), Time.fixedDeltaTime))
                Freeze();
        }

        public void OnGrabbed()
        {
            _isHeld = true;
            WakeUp();
        }

        public void OnReleased()
        {
            _isHeld = false;
        }

        public void WakeUp()
        {
            Unfreeze();
            _state.WakeUp();
        }

        public static void WakeNearby(ObiSockSleeper source)
        {
            if (source == null || source._actor == null || !source._actor.isLoaded)
                return;

            Vector3 sourceCentroid = source.GetCentroid();
            float sqrRadius = source._state.WakeUpRadius * source._state.WakeUpRadius;

            for (int i = 0; i < All.Count; i++)
            {
                ObiSockSleeper sleeper = All[i];
                if (sleeper == source || sleeper._state.CurrentState != SockSleepState.State.Asleep)
                    continue;

                if ((sleeper.GetCentroid() - sourceCentroid).sqrMagnitude <= sqrRadius)
                    sleeper.WakeUp();
            }
        }

        float GetMaxParticleSpeed()
        {
            ObiNativeVector4List velocities = _actor.solver.velocities;
            int count = _actor.particleCount;
            float maxSqrSpeed = 0f;

            for (int i = 0; i < count; i++)
            {
                Vector4 velocity = velocities[_actor.solverIndices[i]];
                float sqrSpeed = velocity.x * velocity.x + velocity.y * velocity.y + velocity.z * velocity.z;
                if (sqrSpeed > maxSqrSpeed)
                    maxSqrSpeed = sqrSpeed;
            }

            return Mathf.Sqrt(maxSqrSpeed);
        }

        Vector3 GetCentroid()
        {
            if (_actor == null || !_actor.isLoaded || _actor.particleCount == 0)
                return transform.position;

            Vector3 sum = Vector3.zero;
            int count = _actor.particleCount;

            for (int i = 0; i < count; i++)
                sum += _actor.GetParticlePosition(_actor.solverIndices[i]);

            return sum / count;
        }

        void Freeze()
        {
            if (_frozen || _actor == null || !_actor.isLoaded)
                return;

            _frozen = true;

            ObiNativeFloatList invMasses = _actor.solver.invMasses;
            int count = _actor.particleCount;
            _originalInvMasses = new float[count];

            for (int i = 0; i < count; i++)
            {
                int solverIndex = _actor.solverIndices[i];
                _originalInvMasses[i] = invMasses[solverIndex];
                invMasses[solverIndex] = 0f;
            }
        }

        void Unfreeze()
        {
            if (!_frozen || _actor == null || !_actor.isLoaded)
                return;

            _frozen = false;

            ObiNativeFloatList invMasses = _actor.solver.invMasses;
            int count = _actor.particleCount;

            for (int i = 0; i < count; i++)
                invMasses[_actor.solverIndices[i]] = _originalInvMasses[i];
        }
    }
}
```

- [ ] **Step 2: Run EditMode tests to verify the project still compiles cleanly**

Run: `/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath . -runTests -testPlatform EditMode -testResults TestResults/EditMode.xml`
Expected: no compiler errors, all existing tests (including `SockSleepStateTests` from Task 1) still pass. `ObiSockSleeper` has no dedicated automated test — it's a thin wrapper around Obi's live solver arrays, verified manually in Task 4 (matches this repo's existing convention of only unit-testing the pure `SockGrab*` logic classes, not their `MonoBehaviour` wrappers).

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Controllers/SockSleep/ObiSockSleeper.cs Assets/Scripts/Controllers/SockSleep/ObiSockSleeper.cs.meta
git commit -m "Add ObiSockSleeper: freeze/unfreeze cloth particles when settled"
```

---

### Task 3: Wire grab/release notifications into `SockDragController`

**Files:**
- Modify: `Assets/Scripts/Controllers/SockGrab/SockDragController.cs:211-232` (`BeginDrag`)
- Modify: `Assets/Scripts/Controllers/SockGrab/SockDragController.cs:256-265` (`EndDrag`)

**Interfaces:**
- Consumes: `CardsUnity.ObiSockSleeper` from Task 2 (`OnGrabbed()`, `OnReleased()`, `static WakeNearby(ObiSockSleeper)`); `Obi.ObiParticleAttachment.actor` (already used elsewhere in this file).

**Important ordering note:** the wake/unfreeze call MUST happen **before** `handle.Attachment.enabled = true`. `ObiParticleAttachment`'s own `OnEnable` pins its particle group by writing `solver.invMasses[...] = 0` for just those particles. If we unfreeze *after* enabling the attachment, `ObiSockSleeper.Unfreeze()` would overwrite that pin back to a non-zero mass, breaking the drag constraint. Waking first means the attachment's own pin logic runs last and wins for its particles.

- [ ] **Step 1: Add the notification calls in `BeginDrag`**

Replace the existing `BeginDrag` method (currently at `Assets/Scripts/Controllers/SockGrab/SockDragController.cs:211-232`):

```csharp
        private void BeginDrag(Vector2 pointerScreenPosition)
        {
            if (!TryGetActiveHandle(out SockGrabAttachmentHandle handle))
                return;

            Vector3 grabWorldPosition = handle.GetWorldPosition();
            dragPlanePoint = grabWorldPosition;
            dragPlaneNormal = -pointerCamera.transform.forward;
            lastPointerCameraPosition = pointerCamera.transform.position;

            dragger.position = grabWorldPosition;
            NotifySockGrabbed(handle.Attachment.actor);
            handle.Attachment.enabled = true;
            if (!stateMachine.TryBeginDrag())
            {
                handle.Attachment.enabled = false;
                return;
            }

            BeginDraggerReposition(pointerScreenPosition, grabWorldPosition);

            DragStarted?.Invoke(pointerScreenPosition);
        }
```

- [ ] **Step 2: Add the notification call in `EndDrag`**

Replace the existing `EndDrag` method (currently at `Assets/Scripts/Controllers/SockGrab/SockDragController.cs:256-265`):

```csharp
        private void EndDrag()
        {
            if (TryGetActiveHandle(out SockGrabAttachmentHandle handle) && handle.Attachment != null)
            {
                handle.Attachment.enabled = false;
                NotifySockReleased(handle.Attachment.actor);
            }

            isRepositioningDragger = false;
            repositionElapsed = 0f;
            stateMachine.EndDrag();
            DragEnded?.Invoke();
        }
```

- [ ] **Step 3: Add the two private helper methods**

Add these methods right after `EndDrag` (after the closing brace added in Step 2), still inside the `SockDragController` class:

```csharp
        private void NotifySockGrabbed(ObiActor actor)
        {
            if (actor == null)
                return;

            ObiSockSleeper sleeper = actor.GetComponent<ObiSockSleeper>();
            if (sleeper == null)
                return;

            sleeper.OnGrabbed();
            ObiSockSleeper.WakeNearby(sleeper);
        }

        private void NotifySockReleased(ObiActor actor)
        {
            if (actor == null)
                return;

            ObiSockSleeper sleeper = actor.GetComponent<ObiSockSleeper>();
            if (sleeper != null)
                sleeper.OnReleased();
        }
```

- [ ] **Step 4: Run EditMode tests to verify the project still compiles cleanly**

Run: `/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath . -runTests -testPlatform EditMode -testResults TestResults/EditMode.xml`
Expected: no compiler errors, all tests still pass.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Controllers/SockGrab/SockDragController.cs
git commit -m "Wake ObiSockSleeper on grab and notify nearby sleepers"
```

---

### Task 4: Manual Editor wiring and Play Mode verification (Marek, Unity Editor)

This task has no code changes — it wires the new component into the prefab and manually verifies the behavior in Play Mode. Per this project's convention, this part is done directly in the Unity Editor rather than by an agent.

**Files:**
- Modify (Unity Editor only): `Assets/Socks/ObiCloth/ObiSock.prefab`

- [ ] **Step 1: Add the component to the prefab**

Open `Assets/Socks/ObiCloth/ObiSock.prefab` in the Unity Editor. On the prefab root (the `ObiSock` GameObject, which already has `ObiCloth` and `ObiClothRenderer`), click **Add Component** and add `ObiSockSleeper`. Leave the default values (`sleepVelocityThreshold = 0.05`, `minRestTimeBeforeSleep = 0.5`, `wakeUpRadius = 0.4`, `minActiveTimeAfterWake = 1`). Save the prefab.

- [ ] **Step 2: Verify socks fall asleep when settled**

Open `Assets/Socks/Scenes/scn_socks_demo.unity` and enter Play Mode. Spawn a batch of socks and let them fall and settle. After they stop moving for about half a second, they should freeze in their current draped shape — no visible change in the mesh, no further wrinkle/jiggle motion.

- [ ] **Step 3: Verify grabbing wakes the sock and dragging still works**

Drag a settled sock with the mouse. It should respond immediately and drag normally, with no visible pop or snap in its mesh at the moment you grab it.

- [ ] **Step 4: Verify nearby socks wake up too**

Let several socks settle close together (within roughly 0.4m of each other). Grab one of them and drag it into/through its neighbors. The settled neighbors should wake up (resume jiggling/reacting) as soon as the dragged sock gets close, rather than staying frozen while being pushed.

- [ ] **Step 5: Verify sleeping socks still block other socks**

Drag one sock so it collides with a different, currently-sleeping sock that is out of `wakeUpRadius` range of the drag path's start (so it doesn't just wake up from proximity first). Confirm the dragged sock visibly collides with/rests against the sleeping one instead of passing through it.

- [ ] **Step 6: Commit the prefab change**

```bash
git add Assets/Socks/ObiCloth/ObiSock.prefab
git commit -m "Add ObiSockSleeper to ObiSock prefab"
```
