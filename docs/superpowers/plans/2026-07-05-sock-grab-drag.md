# Sock Grab & Drag Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let the player grab the Obi-simulated sock by the cursor at a few predefined points and drag it around, with Canvas UI icons that show hover/drag affordance.

**Architecture:** A handful of `ObiParticleAttachment` components (one per grab point, `Dynamic` type) live on the sock's `ObiCloth` GameObject and stay disabled until dragged. A `SockDragController` (Controllers layer) polls the mouse each frame, converts each attachment's live particle position to screen space, and runs it through small pure-logic helpers (hover distance check, a state machine, and a ray/plane intersection) to decide when to enable/disable an attachment and where to move a shared "dragger" `Transform` that the attachment targets. A `SockGrabCursorUI` (UI layer) subscribes to the controller's C# events to show/hide/position two separate Canvas icon objects (hover vs. drag).

**Tech Stack:** Unity 6000.3.10f1, C#, Obi Cloth/Common (`Obi` assembly), Unity Test Framework (NUnit) for EditMode tests.

## Global Constraints

- All code, comments, and commit messages: English. Conversation with the user about this plan: Polish.
- Runtime code goes under `Assets/Scripts/`, assembly `CardsUnity.Runtime`. EditMode tests go under `Assets/Scripts/Tests/`, assembly `CardsUnity.Tests`.
- Neither assembly exists yet on this branch (`Assets/Scripts/` is currently empty) — Task 1 creates both `.asmdef` files.
- Namespace convention (matched from the project's other feature branch, `claude/awesome-clarke-c56bff`, which already has real `CardsUnity.Runtime` code): `Data` and `Controllers` code uses flat `namespace CardsUnity`; `UI` code uses `namespace CardsUnity.UI`; tests use `namespace CardsUnity.Tests`.
- `CardsUnity.Runtime.asmdef` must reference the `Obi` assembly (`Assets/Obi/Scripts/Obi.asmdef`, assembly name `"Obi"`) because `SockDragController`/`SockGrabAttachmentHandle` use `Obi.ObiActor`, `Obi.ObiParticleAttachment`, `Obi.ObiParticleGroup`.
- EditMode test command (from `CLAUDE.md`):
  ```bash
  /Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath . -runTests -testPlatform EditMode -testResults TestResults/EditMode.xml
  ```
  This reimports/recompiles the whole project, so it can take a few minutes — use a long timeout when running it.
- Only pure C# logic (no live `ObiSolver`/`ObiActor` simulation) is covered by automated EditMode tests in this plan. Code that reads live particle positions or toggles `ObiParticleAttachment` is verified manually in the Unity Editor (Play Mode), because it depends on a loaded cloth blueprint and a running solver, which is out of scope to fake in EditMode. This is a deliberate scope decision, not an oversight.
- Scope: single-pointer drag only (no multi-touch). Any number of grab points is supported (`SockDragController` discovers all `ObiParticleAttachment` components on the assigned actor at runtime — no per-point wiring needed).

---

## File Structure

| File | Responsibility |
|---|---|
| `Assets/Scripts/CardsUnity.Runtime.asmdef` | Runtime assembly definition, references `Obi`. |
| `Assets/Scripts/Tests/CardsUnity.Tests.asmdef` | EditMode test assembly, references `CardsUnity.Runtime`. |
| `Assets/Scripts/Controllers/SockGrab/SockGrabHoverDetector.cs` | Pure math: which handle (if any) is closest to the pointer, within a pixel radius. |
| `Assets/Scripts/Data/SockGrabState.cs` | `Idle`/`Hovering`/`Dragging` enum. |
| `Assets/Scripts/Controllers/SockGrab/SockGrabStateMachine.cs` | Pure state transitions between those states. |
| `Assets/Scripts/Controllers/SockGrab/SockGrabMath.cs` | Pure math: ray/plane intersection used to move the drag target. |
| `Assets/Scripts/Controllers/SockGrab/SockGrabAttachmentHandle.cs` | Thin wrapper around one `ObiParticleAttachment`; knows how to read its particle's current world position. |
| `Assets/Scripts/Controllers/SockGrab/SockDragController.cs` | MonoBehaviour coordinator: input, hover/drag state, moves the dragger, enables/disables attachments, raises events. |
| `Assets/Scripts/UI/SockGrabCursorUI.cs` | MonoBehaviour view: shows/hides/positions the two Canvas cursor icons based on the controller's events. |
| `Assets/Scripts/Tests/SockGrabHoverDetectorTests.cs` | Tests for hover detection. |
| `Assets/Scripts/Tests/SockGrabStateMachineTests.cs` | Tests for the state machine. |
| `Assets/Scripts/Tests/SockGrabMathTests.cs` | Tests for the ray/plane helper. |

---

### Task 1: Bootstrap script assemblies + hover detector

**Files:**
- Create: `Assets/Scripts/CardsUnity.Runtime.asmdef`
- Create: `Assets/Scripts/Tests/CardsUnity.Tests.asmdef`
- Create: `Assets/Scripts/Controllers/SockGrab/SockGrabHoverDetector.cs`
- Test: `Assets/Scripts/Tests/SockGrabHoverDetectorTests.cs`

**Interfaces:**
- Produces: `CardsUnity.SockGrabHoverDetector.FindClosestHandleWithinRadius(Vector2 pointerScreenPosition, IReadOnlyList<Vector2> handleScreenPositions, float radiusPixels) : int` — returns the index of the closest handle within `radiusPixels`, or `-1` if none qualify.

- [ ] **Step 1: Create the runtime assembly definition**

`Assets/Scripts/CardsUnity.Runtime.asmdef`:
```json
{
    "name": "CardsUnity.Runtime",
    "rootNamespace": "CardsUnity",
    "references": [
        "Obi"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 2: Create the test assembly definition**

`Assets/Scripts/Tests/CardsUnity.Tests.asmdef`:
```json
{
    "name": "CardsUnity.Tests",
    "rootNamespace": "CardsUnity.Tests",
    "references": [
        "CardsUnity.Runtime"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false,
    "optionalUnityReferences": [
        "TestAssemblies"
    ]
}
```

- [ ] **Step 3: Write the failing test**

`Assets/Scripts/Tests/SockGrabHoverDetectorTests.cs`:
```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CardsUnity.Tests
{
    public class SockGrabHoverDetectorTests
    {
        [Test]
        public void FindClosestHandleWithinRadius_ReturnsMinusOne_WhenNoHandles()
        {
            var result = SockGrabHoverDetector.FindClosestHandleWithinRadius(Vector2.zero, new List<Vector2>(), 50f);
            Assert.AreEqual(-1, result);
        }

        [Test]
        public void FindClosestHandleWithinRadius_ReturnsMinusOne_WhenAllHandlesOutsideRadius()
        {
            var handles = new List<Vector2> { new Vector2(200f, 200f) };
            var result = SockGrabHoverDetector.FindClosestHandleWithinRadius(Vector2.zero, handles, 50f);
            Assert.AreEqual(-1, result);
        }

        [Test]
        public void FindClosestHandleWithinRadius_ReturnsIndex_WhenHandleWithinRadius()
        {
            var handles = new List<Vector2> { new Vector2(10f, 0f) };
            var result = SockGrabHoverDetector.FindClosestHandleWithinRadius(Vector2.zero, handles, 50f);
            Assert.AreEqual(0, result);
        }

        [Test]
        public void FindClosestHandleWithinRadius_ReturnsClosest_WhenMultipleWithinRadius()
        {
            var handles = new List<Vector2> { new Vector2(40f, 0f), new Vector2(10f, 0f) };
            var result = SockGrabHoverDetector.FindClosestHandleWithinRadius(Vector2.zero, handles, 50f);
            Assert.AreEqual(1, result);
        }
    }
}
```

- [ ] **Step 4: Run the test suite to verify it fails**

Run:
```bash
/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath . -runTests -testPlatform EditMode -testResults TestResults/EditMode.xml
```
Expected: compile error — `SockGrabHoverDetector` does not exist (`CS0246`). The run aborts before any test executes; this is the expected "red" state.

- [ ] **Step 5: Implement the minimal code to make it pass**

`Assets/Scripts/Controllers/SockGrab/SockGrabHoverDetector.cs`:
```csharp
using System.Collections.Generic;
using UnityEngine;

namespace CardsUnity
{
    public static class SockGrabHoverDetector
    {
        public static int FindClosestHandleWithinRadius(Vector2 pointerScreenPosition, IReadOnlyList<Vector2> handleScreenPositions, float radiusPixels)
        {
            int closestIndex = -1;
            float closestSqrDistance = radiusPixels * radiusPixels;

            for (int i = 0; i < handleScreenPositions.Count; i++)
            {
                float sqrDistance = (handleScreenPositions[i] - pointerScreenPosition).sqrMagnitude;
                if (sqrDistance <= closestSqrDistance)
                {
                    closestSqrDistance = sqrDistance;
                    closestIndex = i;
                }
            }

            return closestIndex;
        }
    }
}
```

- [ ] **Step 6: Run the test suite to verify it passes**

Run the same command as Step 4.
Expected: `TestResults/EditMode.xml` reports 4 tests run, 4 passed, 0 failed.

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/CardsUnity.Runtime.asmdef Assets/Scripts/CardsUnity.Runtime.asmdef.meta \
        Assets/Scripts/Tests/CardsUnity.Tests.asmdef Assets/Scripts/Tests/CardsUnity.Tests.asmdef.meta \
        Assets/Scripts/Controllers/SockGrab/SockGrabHoverDetector.cs Assets/Scripts/Controllers/SockGrab/SockGrabHoverDetector.cs.meta \
        Assets/Scripts/Tests/SockGrabHoverDetectorTests.cs Assets/Scripts/Tests/SockGrabHoverDetectorTests.cs.meta
git commit -m "feat: bootstrap CardsUnity assemblies and add sock grab hover detector"
```
(Unity generates the `.meta` files on next editor import/domain reload if they don't exist yet — the batchmode test run in Step 4/6 already triggers this, so they should be present before this commit. Verify with `git status` first; add only the `.meta` files that actually exist.)

---

### Task 2: Grab state machine

**Files:**
- Create: `Assets/Scripts/Data/SockGrabState.cs`
- Create: `Assets/Scripts/Controllers/SockGrab/SockGrabStateMachine.cs`
- Test: `Assets/Scripts/Tests/SockGrabStateMachineTests.cs`

**Interfaces:**
- Consumes: nothing from Task 1.
- Produces: `CardsUnity.SockGrabState` enum (`Idle`, `Hovering`, `Dragging`); `CardsUnity.SockGrabStateMachine` with `CurrentState : SockGrabState`, `ActiveHandleIndex : int`, `OnPointerMoved(int hoveredHandleIndex) : void`, `TryBeginDrag() : bool`, `EndDrag() : void`.

- [ ] **Step 1: Write the failing tests**

`Assets/Scripts/Tests/SockGrabStateMachineTests.cs`:
```csharp
using NUnit.Framework;

namespace CardsUnity.Tests
{
    public class SockGrabStateMachineTests
    {
        [Test]
        public void InitialState_IsIdle()
        {
            var sm = new SockGrabStateMachine();
            Assert.AreEqual(SockGrabState.Idle, sm.CurrentState);
            Assert.AreEqual(-1, sm.ActiveHandleIndex);
        }

        [Test]
        public void OnPointerMoved_WithHoveredHandle_EntersHovering()
        {
            var sm = new SockGrabStateMachine();
            sm.OnPointerMoved(2);
            Assert.AreEqual(SockGrabState.Hovering, sm.CurrentState);
            Assert.AreEqual(2, sm.ActiveHandleIndex);
        }

        [Test]
        public void OnPointerMoved_WithNoHoveredHandle_ReturnsToIdle()
        {
            var sm = new SockGrabStateMachine();
            sm.OnPointerMoved(2);
            sm.OnPointerMoved(-1);
            Assert.AreEqual(SockGrabState.Idle, sm.CurrentState);
            Assert.AreEqual(-1, sm.ActiveHandleIndex);
        }

        [Test]
        public void TryBeginDrag_WhileHovering_EntersDraggingAndReturnsTrue()
        {
            var sm = new SockGrabStateMachine();
            sm.OnPointerMoved(1);
            bool started = sm.TryBeginDrag();
            Assert.IsTrue(started);
            Assert.AreEqual(SockGrabState.Dragging, sm.CurrentState);
            Assert.AreEqual(1, sm.ActiveHandleIndex);
        }

        [Test]
        public void TryBeginDrag_WhileIdle_ReturnsFalseAndStaysIdle()
        {
            var sm = new SockGrabStateMachine();
            bool started = sm.TryBeginDrag();
            Assert.IsFalse(started);
            Assert.AreEqual(SockGrabState.Idle, sm.CurrentState);
        }

        [Test]
        public void OnPointerMoved_WhileDragging_DoesNotChangeActiveHandle()
        {
            var sm = new SockGrabStateMachine();
            sm.OnPointerMoved(1);
            sm.TryBeginDrag();
            sm.OnPointerMoved(-1);
            Assert.AreEqual(SockGrabState.Dragging, sm.CurrentState);
            Assert.AreEqual(1, sm.ActiveHandleIndex);
        }

        [Test]
        public void EndDrag_WhileDragging_ReturnsToIdle()
        {
            var sm = new SockGrabStateMachine();
            sm.OnPointerMoved(1);
            sm.TryBeginDrag();
            sm.EndDrag();
            Assert.AreEqual(SockGrabState.Idle, sm.CurrentState);
            Assert.AreEqual(-1, sm.ActiveHandleIndex);
        }

        [Test]
        public void EndDrag_WhileNotDragging_DoesNothing()
        {
            var sm = new SockGrabStateMachine();
            sm.OnPointerMoved(1);
            sm.EndDrag();
            Assert.AreEqual(SockGrabState.Hovering, sm.CurrentState);
            Assert.AreEqual(1, sm.ActiveHandleIndex);
        }
    }
}
```

- [ ] **Step 2: Run the test suite to verify it fails**

Run the command from Task 1 Step 4.
Expected: compile error — `SockGrabStateMachine`/`SockGrabState` do not exist.

- [ ] **Step 3: Implement the enum**

`Assets/Scripts/Data/SockGrabState.cs`:
```csharp
namespace CardsUnity
{
    public enum SockGrabState
    {
        Idle,
        Hovering,
        Dragging
    }
}
```

- [ ] **Step 4: Implement the state machine**

`Assets/Scripts/Controllers/SockGrab/SockGrabStateMachine.cs`:
```csharp
namespace CardsUnity
{
    public class SockGrabStateMachine
    {
        public SockGrabState CurrentState { get; private set; } = SockGrabState.Idle;
        public int ActiveHandleIndex { get; private set; } = -1;

        public void OnPointerMoved(int hoveredHandleIndex)
        {
            if (CurrentState == SockGrabState.Dragging)
                return;

            if (hoveredHandleIndex >= 0)
            {
                CurrentState = SockGrabState.Hovering;
                ActiveHandleIndex = hoveredHandleIndex;
            }
            else
            {
                CurrentState = SockGrabState.Idle;
                ActiveHandleIndex = -1;
            }
        }

        public bool TryBeginDrag()
        {
            if (CurrentState != SockGrabState.Hovering)
                return false;

            CurrentState = SockGrabState.Dragging;
            return true;
        }

        public void EndDrag()
        {
            if (CurrentState != SockGrabState.Dragging)
                return;

            CurrentState = SockGrabState.Idle;
            ActiveHandleIndex = -1;
        }
    }
}
```

- [ ] **Step 5: Run the test suite to verify it passes**

Run the command from Task 1 Step 4.
Expected: 8 new tests pass (12 total including Task 1's 4), 0 failed.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Data/SockGrabState.cs Assets/Scripts/Data/SockGrabState.cs.meta \
        Assets/Scripts/Controllers/SockGrab/SockGrabStateMachine.cs Assets/Scripts/Controllers/SockGrab/SockGrabStateMachine.cs.meta \
        Assets/Scripts/Tests/SockGrabStateMachineTests.cs Assets/Scripts/Tests/SockGrabStateMachineTests.cs.meta
git commit -m "feat: add sock grab state machine (idle/hovering/dragging)"
```

---

### Task 3: Drag-plane math

**Files:**
- Create: `Assets/Scripts/Controllers/SockGrab/SockGrabMath.cs`
- Test: `Assets/Scripts/Tests/SockGrabMathTests.cs`

**Interfaces:**
- Consumes: nothing from Tasks 1-2.
- Produces: `CardsUnity.SockGrabMath.TryGetPointOnPlane(Ray ray, Vector3 planePoint, Vector3 planeNormal, out Vector3 worldPoint) : bool`.

- [ ] **Step 1: Write the failing tests**

`Assets/Scripts/Tests/SockGrabMathTests.cs`:
```csharp
using NUnit.Framework;
using UnityEngine;

namespace CardsUnity.Tests
{
    public class SockGrabMathTests
    {
        [Test]
        public void TryGetPointOnPlane_RayHitsPlane_ReturnsTrueAndPoint()
        {
            var ray = new Ray(new Vector3(0f, 0f, -10f), Vector3.forward);
            bool hit = SockGrabMath.TryGetPointOnPlane(ray, Vector3.zero, Vector3.back, out Vector3 point);
            Assert.IsTrue(hit);
            Assert.AreEqual(Vector3.zero, point);
        }

        [Test]
        public void TryGetPointOnPlane_RayParallelToPlane_ReturnsFalse()
        {
            var ray = new Ray(new Vector3(0f, 5f, -10f), Vector3.up);
            bool hit = SockGrabMath.TryGetPointOnPlane(ray, Vector3.zero, Vector3.back, out _);
            Assert.IsFalse(hit);
        }

        [Test]
        public void TryGetPointOnPlane_OffsetPlanePoint_ReturnsCorrectIntersection()
        {
            var ray = new Ray(new Vector3(2f, 3f, -10f), Vector3.forward);
            bool hit = SockGrabMath.TryGetPointOnPlane(ray, new Vector3(0f, 0f, 5f), Vector3.back, out Vector3 point);
            Assert.IsTrue(hit);
            Assert.AreEqual(new Vector3(2f, 3f, 5f), point);
        }
    }
}
```

- [ ] **Step 2: Run the test suite to verify it fails**

Run the command from Task 1 Step 4.
Expected: compile error — `SockGrabMath` does not exist.

- [ ] **Step 3: Implement the minimal code to make it pass**

`Assets/Scripts/Controllers/SockGrab/SockGrabMath.cs`:
```csharp
using UnityEngine;

namespace CardsUnity
{
    public static class SockGrabMath
    {
        public static bool TryGetPointOnPlane(Ray ray, Vector3 planePoint, Vector3 planeNormal, out Vector3 worldPoint)
        {
            var plane = new Plane(planeNormal, planePoint);
            if (plane.Raycast(ray, out float distance))
            {
                worldPoint = ray.GetPoint(distance);
                return true;
            }

            worldPoint = planePoint;
            return false;
        }
    }
}
```

- [ ] **Step 4: Run the test suite to verify it passes**

Run the command from Task 1 Step 4.
Expected: 3 new tests pass (15 total), 0 failed.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Controllers/SockGrab/SockGrabMath.cs Assets/Scripts/Controllers/SockGrab/SockGrabMath.cs.meta \
        Assets/Scripts/Tests/SockGrabMathTests.cs Assets/Scripts/Tests/SockGrabMathTests.cs.meta
git commit -m "feat: add ray/plane intersection helper for sock dragging"
```

---

### Task 4: Attachment handle wrapper + drag controller

**Files:**
- Create: `Assets/Scripts/Controllers/SockGrab/SockGrabAttachmentHandle.cs`
- Create: `Assets/Scripts/Controllers/SockGrab/SockDragController.cs`

**Interfaces:**
- Consumes: `CardsUnity.SockGrabHoverDetector.FindClosestHandleWithinRadius` (Task 1), `CardsUnity.SockGrabStateMachine`/`SockGrabState` (Task 2), `CardsUnity.SockGrabMath.TryGetPointOnPlane` (Task 3), `Obi.ObiActor`, `Obi.ObiParticleAttachment`, `Obi.ObiParticleGroup` (from the `Obi` assembly).
- Produces: `CardsUnity.SockGrabAttachmentHandle` with `Attachment : Obi.ObiParticleAttachment` (get) and `GetWorldPosition() : Vector3`. `CardsUnity.SockDragController` (MonoBehaviour) with serialized fields `sockActor : Obi.ObiActor`, `pointerCamera : Camera`, `dragger : Transform`, `hoverRadiusPixels : float`, and events `HoverStarted`/`HoverUpdated : Action<Vector2>`, `HoverEnded : Action`, `DragStarted`/`DragUpdated : Action<Vector2>`, `DragEnded : Action` — all consumed by Task 5's `SockGrabCursorUI`.

**Why no automated test here:** `SockGrabAttachmentHandle.GetWorldPosition()` only returns real data once `Obi.ObiActor.solver` is loaded and simulating, and `SockDragController` reads live mouse input and mutates a running `ObiParticleAttachment`. Both require a loaded cloth blueprint and a stepping `ObiSolver`, which is Play Mode-only behavior. This task is verified manually in the Editor (Step 3) instead of via EditMode tests — see the constraint about this in the "Global Constraints" section above.

- [ ] **Step 1: Implement the attachment handle wrapper**

`Assets/Scripts/Controllers/SockGrab/SockGrabAttachmentHandle.cs`:
```csharp
using Obi;
using UnityEngine;

namespace CardsUnity
{
    public sealed class SockGrabAttachmentHandle
    {
        public ObiParticleAttachment Attachment { get; }

        public SockGrabAttachmentHandle(ObiParticleAttachment attachment)
        {
            Attachment = attachment;
        }

        public Vector3 GetWorldPosition()
        {
            var actor = Attachment.actor;
            var group = Attachment.particleGroup;

            if (actor == null || actor.solver == null || group == null || group.Count == 0)
                return Attachment.transform.position;

            int localParticleIndex = group.particleIndices[0];
            int solverIndex = actor.solverIndices[localParticleIndex];
            Vector3 solverLocalPosition = actor.solver.positions[solverIndex];
            return actor.solver.transform.TransformPoint(solverLocalPosition);
        }
    }
}
```

- [ ] **Step 2: Implement the drag controller**

`Assets/Scripts/Controllers/SockGrab/SockDragController.cs`:
```csharp
using System;
using Obi;
using UnityEngine;

namespace CardsUnity
{
    public class SockDragController : MonoBehaviour
    {
        [SerializeField] private ObiActor sockActor;
        [SerializeField] private Camera pointerCamera;
        [SerializeField] private Transform dragger;
        [SerializeField] private float hoverRadiusPixels = 60f;

        public event Action<Vector2> HoverStarted;
        public event Action<Vector2> HoverUpdated;
        public event Action HoverEnded;
        public event Action<Vector2> DragStarted;
        public event Action<Vector2> DragUpdated;
        public event Action DragEnded;

        private readonly SockGrabStateMachine stateMachine = new SockGrabStateMachine();
        private SockGrabAttachmentHandle[] handles;
        private Vector2[] handleScreenPositions;
        private Vector3 dragPlanePoint;
        private Vector3 dragPlaneNormal;

        private void Awake()
        {
            var attachments = sockActor.GetComponents<ObiParticleAttachment>();
            handles = new SockGrabAttachmentHandle[attachments.Length];
            for (int i = 0; i < attachments.Length; i++)
                handles[i] = new SockGrabAttachmentHandle(attachments[i]);

            handleScreenPositions = new Vector2[handles.Length];
        }

        private void Update()
        {
            Vector2 pointerScreenPosition = Input.mousePosition;

            if (stateMachine.CurrentState != SockGrabState.Dragging)
                UpdateHover(pointerScreenPosition);

            if (Input.GetMouseButtonDown(0) && stateMachine.CurrentState == SockGrabState.Hovering)
            {
                BeginDrag(pointerScreenPosition);
            }
            else if (stateMachine.CurrentState == SockGrabState.Dragging)
            {
                if (Input.GetMouseButtonUp(0))
                    EndDrag();
                else
                    UpdateDrag(pointerScreenPosition);
            }
        }

        private void UpdateHover(Vector2 pointerScreenPosition)
        {
            for (int i = 0; i < handles.Length; i++)
                handleScreenPositions[i] = pointerCamera.WorldToScreenPoint(handles[i].GetWorldPosition());

            SockGrabState previousState = stateMachine.CurrentState;
            int hoveredIndex = SockGrabHoverDetector.FindClosestHandleWithinRadius(pointerScreenPosition, handleScreenPositions, hoverRadiusPixels);
            stateMachine.OnPointerMoved(hoveredIndex);

            if (previousState != SockGrabState.Hovering && stateMachine.CurrentState == SockGrabState.Hovering)
                HoverStarted?.Invoke(pointerScreenPosition);
            else if (previousState == SockGrabState.Hovering && stateMachine.CurrentState == SockGrabState.Idle)
                HoverEnded?.Invoke();
            else if (stateMachine.CurrentState == SockGrabState.Hovering)
                HoverUpdated?.Invoke(pointerScreenPosition);
        }

        private void BeginDrag(Vector2 pointerScreenPosition)
        {
            SockGrabAttachmentHandle handle = handles[stateMachine.ActiveHandleIndex];
            Vector3 grabWorldPosition = handle.GetWorldPosition();

            dragPlanePoint = grabWorldPosition;
            dragPlaneNormal = -pointerCamera.transform.forward;

            dragger.position = grabWorldPosition;
            handle.Attachment.enabled = true;

            stateMachine.TryBeginDrag();
            DragStarted?.Invoke(pointerScreenPosition);
        }

        private void UpdateDrag(Vector2 pointerScreenPosition)
        {
            Ray ray = pointerCamera.ScreenPointToRay(pointerScreenPosition);
            if (SockGrabMath.TryGetPointOnPlane(ray, dragPlanePoint, dragPlaneNormal, out Vector3 worldPoint))
                dragger.position = worldPoint;

            DragUpdated?.Invoke(pointerScreenPosition);
        }

        private void EndDrag()
        {
            SockGrabAttachmentHandle handle = handles[stateMachine.ActiveHandleIndex];
            handle.Attachment.enabled = false;

            stateMachine.EndDrag();
            DragEnded?.Invoke();
        }
    }
}
```

- [ ] **Step 3: Verify the project compiles**

Run the command from Task 1 Step 4.
Expected: still 15 tests passed, 0 failed, and no compile errors reported in the log (`grep -i "error CS" TestResults/EditMode.xml` or check the Unity log printed to the console — there should be no matches).

- [ ] **Step 4: Manual Editor setup (do this in the Unity Editor, not via script)**

This is Marek's part — the following steps require the Obi Cloth Blueprint editor and Inspector, which aren't scriptable from here:

1. Open the sock's cloth blueprint asset (`Assets/Socks/ObiCloth/ClothBlueprint_sock1 2.asset`) and open its Blueprint Editor. Create 1-2 particle groups at the intended grab points (e.g. `Cuff`, `Toe`), each containing a handful of neighboring particles.
2. Open `Assets/Socks/ObiCloth/ObiSock.prefab` (or its instance in `Assets/Socks/Scenes/scn_obi.unity`). Select the **"Obi Cloth"** child GameObject (the one with the `ObiCloth` component).
3. For each particle group, **Add Component → Obi Particle Attachment**. Set `Particle Group` to the matching group and `Attachment Type` to `Dynamic`. Leave `Target` empty for now.
4. Create an empty GameObject in the scene named `SockDragger`. Add a small `Sphere Collider` to it (radius ~0.01, `Is Trigger` on) and then **Add Component → Obi Collider**. (We'll revisit its collision `Filter` in a follow-up pass once we can see/feel the jitter behavior discussed earlier — leave it at the default `Collide With Everything` for now.)
5. Set every `Obi Particle Attachment`'s `Target` field (from step 3) to the `SockDragger` transform.
6. Add the `SockDragController` component to a scene GameObject (e.g. a new empty `SockGrabManager`, or the `ObiSock` root). Assign: `Sock Actor` = the **"Obi Cloth"** GameObject from step 2, `Pointer Camera` = the scene's main camera, `Dragger` = the `SockDragger` transform, `Hover Radius Pixels` = `60` (default is fine to start).

- [ ] **Step 5: Manual Play Mode verification**

Enter Play Mode. Click near where one of the particle groups sits on screen and drag the mouse — the cloth should visibly follow the cursor. Release the mouse button — the sock should drop and settle back under normal physics (no leftover stiffness). There's no cursor icon yet (that's Task 5), so judge this purely by watching the cloth deform. Check the Console for exceptions (especially `NullReferenceException` from `SockDragController` or `SockGrabAttachmentHandle`) — there should be none.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Controllers/SockGrab/SockGrabAttachmentHandle.cs Assets/Scripts/Controllers/SockGrab/SockGrabAttachmentHandle.cs.meta \
        Assets/Scripts/Controllers/SockGrab/SockDragController.cs Assets/Scripts/Controllers/SockGrab/SockDragController.cs.meta
git commit -m "feat: add sock drag controller wiring pointer input to Obi particle attachments"
```
(The scene/prefab changes from Step 4 are Marek's manual Editor work — commit those separately once he's done and happy with the setup, they're not part of this scripted step.)

---

### Task 5: Cursor feedback UI

**Files:**
- Create: `Assets/Scripts/UI/SockGrabCursorUI.cs`

**Interfaces:**
- Consumes: `CardsUnity.SockDragController`'s events (Task 4): `HoverStarted`, `HoverUpdated`, `HoverEnded`, `DragStarted`, `DragUpdated`, `DragEnded`.
- Produces: nothing consumed by later tasks (this is the last task in the plan).

**Why no automated test here:** this class only calls `RectTransformUtility.ScreenPointToLocalPointInRectangle` and toggles `GameObject.SetActive` in response to controller events — there's no non-trivial logic left to unit test once Task 4's controller owns the state machine; it's a thin view. Verified manually in Play Mode.

- [ ] **Step 1: Implement the cursor UI**

`Assets/Scripts/UI/SockGrabCursorUI.cs`:
```csharp
using UnityEngine;

namespace CardsUnity.UI
{
    public class SockGrabCursorUI : MonoBehaviour
    {
        [SerializeField] private SockDragController dragController;
        [SerializeField] private RectTransform canvasRect;
        [SerializeField] private RectTransform hoverCursorVisual;
        [SerializeField] private RectTransform dragCursorVisual;
        [SerializeField] private Camera uiCamera;

        private void OnEnable()
        {
            dragController.HoverStarted += HandleHoverStarted;
            dragController.HoverUpdated += HandleHoverUpdated;
            dragController.HoverEnded += HandleHoverEnded;
            dragController.DragStarted += HandleDragStarted;
            dragController.DragUpdated += HandleDragUpdated;
            dragController.DragEnded += HandleDragEnded;

            hoverCursorVisual.gameObject.SetActive(false);
            dragCursorVisual.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            dragController.HoverStarted -= HandleHoverStarted;
            dragController.HoverUpdated -= HandleHoverUpdated;
            dragController.HoverEnded -= HandleHoverEnded;
            dragController.DragStarted -= HandleDragStarted;
            dragController.DragUpdated -= HandleDragUpdated;
            dragController.DragEnded -= HandleDragEnded;
        }

        private void HandleHoverStarted(Vector2 screenPosition)
        {
            hoverCursorVisual.gameObject.SetActive(true);
            PositionAt(hoverCursorVisual, screenPosition);
        }

        private void HandleHoverUpdated(Vector2 screenPosition)
        {
            PositionAt(hoverCursorVisual, screenPosition);
        }

        private void HandleHoverEnded()
        {
            hoverCursorVisual.gameObject.SetActive(false);
        }

        private void HandleDragStarted(Vector2 screenPosition)
        {
            hoverCursorVisual.gameObject.SetActive(false);
            dragCursorVisual.gameObject.SetActive(true);
            PositionAt(dragCursorVisual, screenPosition);
        }

        private void HandleDragUpdated(Vector2 screenPosition)
        {
            PositionAt(dragCursorVisual, screenPosition);
        }

        private void HandleDragEnded()
        {
            dragCursorVisual.gameObject.SetActive(false);
        }

        private void PositionAt(RectTransform visual, Vector2 screenPosition)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, uiCamera, out Vector2 localPoint))
                visual.anchoredPosition = localPoint;
        }
    }
}
```

- [ ] **Step 2: Verify the project compiles**

Run the command from Task 1 Step 4.
Expected: still 15 tests passed, 0 failed, no compile errors.

- [ ] **Step 3: Manual Editor setup**

1. Under the scene's Canvas, create two UI GameObjects for the icons (e.g. `HoverCursorIcon`, `DragCursorIcon`) with whatever graphics you want for each state.
2. Add `SockGrabCursorUI` to a UI manager object under the Canvas. Assign: `Drag Controller` = the `SockDragController` from Task 4, `Canvas Rect` = the Canvas's own `RectTransform`, `Hover Cursor Visual` / `Drag Cursor Visual` = the two icons from step 1, `Ui Camera` = leave empty if the Canvas's `Render Mode` is `Screen Space - Overlay`, otherwise assign the camera the Canvas uses.

- [ ] **Step 4: Manual Play Mode verification**

Enter Play Mode. Move the mouse near a grab point — the hover icon should appear and track the cursor. Click and drag — the hover icon should disappear and the drag icon should appear and track the cursor while the sock follows. Release — the drag icon should disappear and the sock should drop back under physics.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/UI/SockGrabCursorUI.cs Assets/Scripts/UI/SockGrabCursorUI.cs.meta
git commit -m "feat: add hover/drag cursor icons for sock grab interaction"
```

---

## Self-Review

**Spec coverage:**
- Particle groups + `ObiParticleAttachment` per grab point → Task 4, Step 4 (manual Editor setup).
- `Dynamic` attachment type + shared collider-bearing `Target` (discussed jitter/collider requirement) → Task 4, Step 4, points 3-5; filter tuning explicitly deferred and called out, not silently dropped.
- Hover detection without physical colliders, using screen-space distance → Task 1 (`SockGrabHoverDetector`) + Task 4 (`SockDragController.UpdateHover`).
- Canvas icon at cursor position, two separate objects for hover/drag, changing on click → Task 5 (`SockGrabCursorUI`).
- Grab → move dragger to particle position → enable attachment → drag on a camera-facing plane → disable on release → Task 4 (`SockDragController.BeginDrag/UpdateDrag/EndDrag`) + Task 3 (`SockGrabMath`).
- Controllers/UI separation per `CLAUDE.md` → Task 4 lives in `Controllers/`, Task 5 lives in `UI/`, connected only through C# events.

**Placeholder scan:** no TODOs, no "add error handling" hand-waves, no repeated-code references — checked.

**Type consistency:** `SockGrabState` (Task 2) used identically in Task 4. `SockGrabHoverDetector.FindClosestHandleWithinRadius` signature (Task 1) matches its call site in Task 4. `SockGrabMath.TryGetPointOnPlane` signature (Task 3) matches its call site in Task 4. `SockDragController` event names (Task 4) match the subscriptions in Task 5 exactly (`HoverStarted`, `HoverUpdated`, `HoverEnded`, `DragStarted`, `DragUpdated`, `DragEnded`) — checked.
