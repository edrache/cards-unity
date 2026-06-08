# Cloth Sock Compute Shader — Design Spec
**Date:** 2026-06-08  
**Branch:** game/socks-cloth  
**Scope:** Standalone prototype (not integrated into existing Sock.cs system)

---

## Overview

GPU-based cloth simulation for sock objects represented as flat 8×8 quad planes.  
Up to 30+ socks simulated in a single compute dispatch using Position-Based Dynamics (PBD).

---

## Requirements

- 30+ socks simultaneously on scene
- Each sock: procedural 8×8 grid mesh (64 particles)
- Collision: floor/plane, box, capsule, other socks (cloth-cloth)
- Pick-up: one pinned vertex follows hand position, rest hangs freely
- Standalone prototype — no integration with existing Sock.cs / SockPhysicsBuilder.cs

---

## Architecture

### Scene hierarchy

```
GameObject "ClothSockSystem"
  ClothSockSimulator.cs
  ClothSockPickup.cs

GameObject "Sock_01" ... "Sock_N"  (prefab)
  ClothSockInstance.cs
  MeshFilter
  MeshRenderer

GameObject "ClothSockSpawner"
  ClothSockSpawner.cs
```

### Files to create

| File | Purpose |
|------|---------|
| `Assets/Socks/Scripts/ClothSim/ClothSockSimulator.cs` | Main MonoBehaviour, owns all ComputeBuffers, dispatches |
| `Assets/Socks/Scripts/ClothSim/ClothSockInstance.cs` | Per-sock MonoBehaviour, registers with Simulator |
| `Assets/Socks/Scripts/ClothSim/ClothSockPickup.cs` | Raycast pick-up, sets pinned vertex |
| `Assets/Socks/Scripts/ClothSim/ClothSockSpawner.cs` | Spawns N sock instances |
| `Assets/Socks/Shaders/SockCloth.compute` | PBD compute shader |

---

## Data Layout

### ParticleData (48 bytes, in global buffer)

```hlsl
float3 position;
float  invMass;       // 0 = pinned
float3 prevPosition;
float  sockIndex;     // which sock owns this particle
float3 normal;
float  padding;
```

### SockData

```hlsl
int    startIndex;    // offset into global particle buffer
int    particleCount; // always 64
float3 capsuleA;      // bounding capsule point A (cloth-cloth broadphase)
float3 capsuleB;      // bounding capsule point B
float  capsuleRadius;
float3 pinnedPosition;// world-space pin target (from CPU each frame)
int    pinnedIndex;   // local index of pinned particle (-1 = not held)
```

### ColliderData

```hlsl
int      type;        // 0=plane, 1=sphere, 2=capsule, 3=box
float3   p0, p1;      // position / capsule points
float4   params;      // x=radius or half-extents.x, yz=half-extents.yz
float4x4 invMatrix;   // world-to-local for box collision
```

---

## Simulation Pipeline

Each frame (CPU loop):

```
for (int s = 0; s < substeps; s++)   // substeps = 4
{
    Dispatch Kernel_Integrate          // Verlet + gravity
    Dispatch Kernel_SolveConstraints   // 2–3 iterations
    Dispatch Kernel_SolveCollisions    // scene colliders + cloth-cloth
}
Dispatch Kernel_UpdateNormals
Dispatch Kernel_UpdateMeshes          // writes to GraphicsBuffer → Mesh
```

### Kernel_Integrate

Verlet integration with damping:

```hlsl
float3 vel  = (pos - prevPos) * damping;   // damping ~0.98
prevPos     = pos;
pos         = pos + vel + gravity * dt2;   // dt2 = (deltaTime/substeps)²
if (invMass == 0) pos = pinnedPosition;    // pinned override
```

### Kernel_SolveConstraints

Distance constraints on 8×8 grid:

| Type    | Direction          | Rest distance |
|---------|--------------------|---------------|
| Stretch | horizontal/vertical | 1 cell        |
| Shear   | diagonal           | √2 cells      |
| Bend    | horizontal/vertical | 2 cells       |

Constraint resolution:
```hlsl
float3 delta = pos_b - pos_a;
float  dist  = length(delta);
float  corr  = (dist - restLen) / dist;
pos_a += corr * (invMass_a / (invMass_a + invMass_b)) * delta;
pos_b -= corr * (invMass_b / (invMass_a + invMass_b)) * delta;
```

### Kernel_SolveCollisions

**Scene colliders:** each particle tested against all ColliderData entries.  
**Cloth-cloth broadphase:** capsule-vs-capsule check (CPU updates bounding capsules after each substep). If capsules overlap → particle-vs-particle sphere collision with radius = half cell size.

### Kernel_UpdateMeshes

Writes position + normal from global particle buffer into per-sock `GraphicsBuffer`.  
CPU calls `mesh.SetVertexBufferData()` — zero-copy path when buffer is on GPU.

---

## Pick-up System

`ClothSockPickup.cs`:
- On click/grab: raycast → find nearest particle to hit point → store `(sockIndex, localParticleIndex)`
- Each frame: `simulator.SetPinnedParticle(sockIndex, localIndex, handTransform.position)`
- This sets `invMass = 0` and `pinnedPosition` in SockData uploaded to GPU
- On release: `invMass` restored to `1.0`, particle falls freely

---

## Collider Collection

`ClothSockSimulator` collects Unity colliders each frame:
- `Physics.OverlapSphere` or maintain a registered collider list
- Pack into `ColliderData[]`, upload to `collidersBuf` via `SetData`
- Max colliders: 32 (configurable constant in shader)

---

## Mesh Setup

Each `ClothSockInstance` creates a procedural `Mesh` on `Awake()`:
- 8×8 = 64 vertices, (7×7×2) = 98 triangles
- `Mesh.SetVertexBufferParams` with `VertexAttributeDescriptor` for position + normal
- Triangle indices are static (set once, never change)
- `GraphicsBuffer` created with `Mesh.GetVertexBuffer(0)`

---

## Parameters (tunable in Inspector)

| Parameter | Default | Notes |
|-----------|---------|-------|
| substeps | 4 | Higher = more stable, more expensive |
| constraintIterations | 3 | Per substep |
| gravity | (0, -9.81, 0) | World gravity |
| damping | 0.98 | Velocity damping per step |
| particleRadius | 0.02 | For cloth-cloth collision |
| maxColliders | 32 | Shader constant |
| maxSocks | 64 | Shader constant, buffer size |

---

## Out of Scope (prototype)

- Integration with existing Sock.cs / SockSelector.cs / SockPair.cs
- Spatial hash GPU broadphase for cloth-cloth (capsule approximation is sufficient for prototype)
- Two-point pick-up (hanging from both ends)
- Mobile/PC URP shader variants
