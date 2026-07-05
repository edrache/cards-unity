# Sock Grab & Drag Usage Guide

**Date:** 2026-07-05  
**Related plan:** [2026-07-05-sock-grab-drag.md](/Users/marek/OfflineDocuments/Repo/Antigravity/cards-unity/docs/superpowers/plans/2026-07-05-sock-grab-drag.md)

## Purpose

The sock grab system lets the player grab an Obi-simulated sock at predefined particle groups, drag it with the mouse, and display separate hover/drag cursor visuals on a Canvas.

The runtime implementation is split into:

- `SockDragController` for input, hover detection, drag state, and Obi attachment toggling
- `SockGrabCursorUI` for cursor visuals only
- small pure helpers for hover detection, state transitions, and drag-plane math

## Script Overview

| File | Responsibility |
|---|---|
| `Assets/Scripts/Controllers/SockGrab/SockDragController.cs` | Main runtime controller for hover and drag |
| `Assets/Scripts/Controllers/SockGrab/SockGrabAttachmentHandle.cs` | Reads a live grab point position from `ObiParticleAttachment` |
| `Assets/Scripts/UI/SockGrabCursorUI.cs` | Shows and positions hover/drag UI visuals |
| `Assets/Scripts/Controllers/SockGrab/SockGrabHoverDetector.cs` | Finds the closest handle in screen space |
| `Assets/Scripts/Controllers/SockGrab/SockGrabStateMachine.cs` | `Idle` / `Hovering` / `Dragging` transitions |
| `Assets/Scripts/Controllers/SockGrab/SockGrabMath.cs` | Ray/plane intersection for drag target movement |

## Scene Setup

### 1. Prepare grab particle groups

Open the cloth blueprint used by the sock and create 1 or more particle groups at intended grab areas, for example:

- cuff
- toe
- heel

Each group should contain a small cluster of neighboring particles, not a single isolated particle unless that gives the desired feel.

### 2. Add Obi particle attachments

On the sock's `ObiCloth` GameObject:

1. Add one `Obi Particle Attachment` per particle group.
2. Assign the corresponding `Particle Group`.
3. Set `Attachment Type` to `Dynamic`.
4. Keep them disabled by default during authoring if you want the controller to own activation at runtime.

The runtime code discovers attachments automatically through `sockActor.GetComponents<ObiParticleAttachment>()`, so no per-handle list is needed in the inspector.

### 3. Create the shared drag target

Create a scene object named `SockDragger` and assign it as the `Target` of every `Obi Particle Attachment`.

Recommended setup:

- `Transform` at a neutral scene position
- small `Sphere Collider`
- `Is Trigger` enabled
- `Obi Collider` component added

This object is moved by `SockDragController` during dragging.

### 4. Add the drag controller

Add `SockDragController` to a manager object in the scene and assign:

| Field | What to assign |
|---|---|
| `Sock Actor` | the `ObiCloth` GameObject that owns the `ObiParticleAttachment` components |
| `Pointer Camera` | the camera used for mouse interaction |
| `Dragger` | the `SockDragger` transform |
| `Hover Radius Pixels` | screen-space hover radius, default `60` |

## UI Setup

Add `SockGrabCursorUI` under the scene Canvas and assign:

| Field | What to assign |
|---|---|
| `Drag Controller` | the `SockDragController` instance |
| `Canvas Rect` | the Canvas root `RectTransform` |
| `Hover Cursor Visual` | UI object shown while hovering a handle |
| `Drag Cursor Visual` | UI object shown while dragging |
| `Ui Camera` | leave empty for Overlay canvas, otherwise assign the canvas camera |

Recommended behavior:

- hover icon should be subtle and readable
- drag icon should communicate active grabbing
- both visuals should have anchors/pivots that make cursor alignment feel intentional

## Runtime Behavior

### Hover

Every frame, `SockDragController`:

1. reads each handle's current world position
2. converts it to screen space
3. finds the closest handle within `hoverRadiusPixels`
4. updates hover state and emits UI events

Hover detection does not use physics colliders on the sock itself. It is purely screen-space based.

### Drag start

When the left mouse button is pressed while hovering:

1. the selected handle world position becomes the initial drag point
2. a drag plane is built facing the pointer camera
3. the shared `dragger` is snapped to the grab point
4. the selected `ObiParticleAttachment` is enabled
5. drag events are emitted for UI

### Drag update

While the mouse button is held:

1. the mouse ray is intersected with the drag plane
2. the drag plane follows the pointer camera translation, so player movement carries the drag target along
3. the `dragger` transform is moved to that world point
4. the Obi attachment pulls the particle group toward the dragger

### Drag end

On mouse release:

1. the active `ObiParticleAttachment` is disabled
2. state returns to idle
3. drag UI is hidden

## Notes for Designers

- More grab points increase flexibility, but too many nearby groups can make hover selection noisy.
- A larger `Hover Radius Pixels` makes interaction easier but less precise.
- A smaller `Hover Radius Pixels` feels cleaner but may be harder to use on small or fast-moving cloth regions.
- If dragging feels unstable, first inspect the `SockDragger` collider and Obi collision settings before changing the controller code.

## Limitations

- Single-pointer mouse interaction only
- No multi-touch support
- No automated Play Mode validation of live Obi behavior
- Final feel depends on particle-group placement and Obi solver tuning

## Manual Test Checklist

1. Enter Play Mode.
2. Move the cursor near a configured grab point.
3. Confirm the hover cursor appears.
4. Press and hold the left mouse button.
5. Confirm the drag cursor replaces the hover cursor.
6. Move the mouse and confirm the sock follows.
7. Release the mouse button.
8. Confirm the drag cursor disappears and the sock falls back under normal simulation.
9. Check the Console for `NullReferenceException` or Obi binding errors.

## Troubleshooting

### Hover cursor never appears

- Check that `SockDragController` has valid `Sock Actor`, `Pointer Camera`, and `Dragger` references.
- Check that the assigned `ObiCloth` object actually has `ObiParticleAttachment` components.
- Check whether the grab points are behind the camera or too far from the cursor.
- Increase `Hover Radius Pixels` temporarily.

### Drag starts but the sock does not follow

- Check that each `ObiParticleAttachment` has `Target` set to `SockDragger`.
- Check that `SockDragger` has `Obi Collider`.
- Check that the attachment `Particle Group` is valid and contains particles from the active blueprint.
- Check that the Obi actor is loaded into a running solver.

### UI does not move with the cursor

- Check `Canvas Rect`.
- Check whether `Ui Camera` matches the Canvas render mode.
- Check that both cursor visuals are assigned and active-capable.

## Verification

Pure helper logic is covered by EditMode tests in:

- `Assets/Scripts/Tests/SockGrabHoverDetectorTests.cs`
- `Assets/Scripts/Tests/SockGrabStateMachineTests.cs`
- `Assets/Scripts/Tests/SockGrabMathTests.cs`

Live cloth dragging and UI behavior must still be verified manually in the Unity Editor.
