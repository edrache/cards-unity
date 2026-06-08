# Cloth Sock Compute Shader Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Standalone GPU cloth simulation for 30+ sock-shaped planes using Position-Based Dynamics in a Unity 6 compute shader.

**Architecture:** All socks share one global `ComputeBuffer` of 4096 particle slots (64 socks × 64 particles). A single `Dispatch(sockCount, 1, 1)` with `[numthreads(64,1,1)]` runs each kernel — group X = sock index, thread X = local particle index. CPU reads particle data back once per frame to update mesh vertices/normals and bounding spheres.

**Tech Stack:** Unity 6 (6000.3.10f1), HLSL compute shader, URP, `ComputeBuffer`, `Mesh.SetVertices` / `Mesh.SetNormals`.

---

## File Map

| Action | Path | Responsibility |
|--------|------|----------------|
| Create | `Assets/Socks/Shaders/SockCloth.compute` | PBD kernels: Integrate, SolveConstraints, SolveCollisions, UpdateNormals |
| Create | `Assets/Socks/Scripts/ClothSim/ClothSockData.cs` | C# structs: ParticleData, SockData, ColliderData |
| Create | `Assets/Socks/Scripts/ClothSim/ClothSockInstance.cs` | Per-sock MonoBehaviour: procedural mesh, registers with Simulator |
| Create | `Assets/Socks/Scripts/ClothSim/ClothSockSimulator.cs` | ComputeBuffers, simulation loop, collider collection, mesh update |
| Create | `Assets/Socks/Scripts/ClothSim/ClothSockPickup.cs` | Raycast pick-up, pin/unpin particle |
| Create | `Assets/Socks/Scripts/ClothSim/ClothSockSpawner.cs` | Spawns N sock instances on Awake |

---

## Task 1: Data Structs (ClothSockData.cs)

**Files:**
- Create: `Assets/Socks/Scripts/ClothSim/ClothSockData.cs`

Defines three structs shared between C# and HLSL. Sizes must match exactly — a mismatch causes silent corruption in ComputeBuffer.SetData.

- [ ] **Step 1: Create the file**

```csharp
// Assets/Socks/Scripts/ClothSim/ClothSockData.cs
using System.Runtime.InteropServices;
using UnityEngine;

namespace ClothSim
{
    // 48 bytes — matches ParticleData in SockCloth.compute
    [StructLayout(LayoutKind.Sequential)]
    public struct ParticleData
    {
        public Vector3 position;     // 12
        public float   invMass;      //  4 = 16
        public Vector3 prevPosition; // 12
        public float   padding0;     //  4 = 32
        public Vector3 normal;       // 12
        public float   padding1;     //  4 = 48
    }

    // 48 bytes — matches SockData in SockCloth.compute
    [StructLayout(LayoutKind.Sequential)]
    public struct SockData
    {
        public int     startIndex;      //  4
        public int     particleCount;   //  4 =  8
        public Vector3 boundCenter;     // 12 = 20
        public float   boundRadius;     //  4 = 24
        public Vector3 pinnedPosition;  // 12 = 36
        public int     pinnedIndex;     //  4 = 40
        public Vector2 padding;         //  8 = 48
    }

    // 108 bytes — matches ColliderData in SockCloth.compute
    [StructLayout(LayoutKind.Sequential)]
    public struct ColliderData
    {
        public int     type;       //   4
        public Vector3 p0;        //  12 =  16
        public Vector3 p1;        //  12 =  28
        public Vector4 paramsVec; //  16 =  44
        public Matrix4x4 invMatrix; // 64 = 108
    }
}
```

- [ ] **Step 2: Verify struct sizes in Editor (add temporary check in any Awake)**

Open the Unity console after compile. Add this anywhere to verify (e.g. in an empty test script, remove after confirming):

```csharp
using ClothSim;
// In any Awake():
Debug.Assert(System.Runtime.InteropServices.Marshal.SizeOf<ParticleData>() == 48,
    "ParticleData size mismatch");
Debug.Assert(System.Runtime.InteropServices.Marshal.SizeOf<SockData>() == 48,
    "SockData size mismatch");
Debug.Assert(System.Runtime.InteropServices.Marshal.SizeOf<ColliderData>() == 108,
    "ColliderData size mismatch");
```

Expected: no assertion fires.

- [ ] **Step 3: Commit**

```bash
git add Assets/Socks/Scripts/ClothSim/ClothSockData.cs
git commit -m "feat(cloth): add shared C# data structs for cloth simulation"
```

---

## Task 2: Compute Shader (SockCloth.compute)

**Files:**
- Create: `Assets/Socks/Shaders/SockCloth.compute`

The core of the simulation. Four kernels, all dispatched as `(sockCount, 1, 1)` with `[numthreads(64,1,1)]` — one group per sock, one thread per particle.

- [ ] **Step 1: Create the Shaders directory and compute shader**

```bash
mkdir -p Assets/Socks/Shaders
```

- [ ] **Step 2: Write the full compute shader**

```hlsl
// Assets/Socks/Shaders/SockCloth.compute
#pragma kernel Kernel_Integrate
#pragma kernel Kernel_SolveConstraints
#pragma kernel Kernel_SolveCollisions
#pragma kernel Kernel_UpdateNormals

#define GRID_W              8
#define GRID_H              8
#define PARTICLES_PER_SOCK  64
#define COLLIDER_PLANE      0
#define COLLIDER_SPHERE     1
#define COLLIDER_CAPSULE    2
#define COLLIDER_BOX        3

// ── Structs ────────────────────────────────────────────────────────────────

struct ParticleData
{
    float3 position;
    float  invMass;
    float3 prevPosition;
    float  padding0;
    float3 normal;
    float  padding1;
};

struct SockData
{
    int    startIndex;
    int    particleCount;
    float3 boundCenter;
    float  boundRadius;
    float3 pinnedPosition;
    int    pinnedIndex;
    float2 padding;
};

struct ColliderData
{
    int      type;
    float3   p0;
    float3   p1;
    float4   paramsVec;   // sphere/capsule: x=radius; box: xyz=half-extents
    float4x4 invMatrix;   // world-to-local rotation for box (p0 = box center)
};

// ── Buffers ────────────────────────────────────────────────────────────────

RWStructuredBuffer<ParticleData> _Particles;
StructuredBuffer<SockData>       _Socks;
StructuredBuffer<ColliderData>   _Colliders;

// ── Uniforms ───────────────────────────────────────────────────────────────

int    _TotalSocks;
int    _TotalColliders;
float  _DeltaTime;        // Time.deltaTime / substeps
float  _Damping;          // ~0.98
float3 _Gravity;          // (0, -9.81, 0)
float  _CellSize;         // sockWidth / (GRID_W - 1)
float  _ParticleRadius;   // ~0.02

// ── Shared memory ──────────────────────────────────────────────────────────

groupshared float3 s_pos[PARTICLES_PER_SOCK];
groupshared float  s_inv[PARTICLES_PER_SOCK];

// ── Helpers ────────────────────────────────────────────────────────────────

float3 ClosestPointOnSegment(float3 a, float3 b, float3 p)
{
    float3 ab = b - a;
    float  t  = saturate(dot(p - a, ab) / max(dot(ab, ab), 1e-6));
    return a + ab * t;
}

// ── Kernel_Integrate ───────────────────────────────────────────────────────

[numthreads(PARTICLES_PER_SOCK, 1, 1)]
void Kernel_Integrate(uint3 gid : SV_GroupID, uint3 tid : SV_GroupThreadID)
{
    uint sockIdx  = gid.x;
    uint localId  = tid.x;
    SockData sock = _Socks[sockIdx];
    uint pid      = sock.startIndex + localId;

    ParticleData p = _Particles[pid];

    // Pinned particle: snap to hand position, clear velocity
    if (sock.pinnedIndex == (int)localId)
    {
        p.prevPosition = sock.pinnedPosition;
        p.position     = sock.pinnedPosition;
        p.invMass      = 0.0;
        _Particles[pid] = p;
        return;
    }

    if (p.invMass == 0.0) return;

    float dt2       = _DeltaTime * _DeltaTime;
    float3 vel      = (p.position - p.prevPosition) * _Damping;
    p.prevPosition  = p.position;
    p.position      = p.position + vel + _Gravity * dt2;

    _Particles[pid] = p;
}

// ── Kernel_SolveConstraints ────────────────────────────────────────────────

[numthreads(PARTICLES_PER_SOCK, 1, 1)]
void Kernel_SolveConstraints(uint3 gid : SV_GroupID, uint3 tid : SV_GroupThreadID)
{
    uint sockIdx  = gid.x;
    uint localId  = tid.x;
    SockData sock = _Socks[sockIdx];
    uint pid      = sock.startIndex + localId;

    // Load all 64 particles of this sock into shared memory
    s_pos[localId] = _Particles[pid].position;
    s_inv[localId] = _Particles[pid].invMass;
    GroupMemoryBarrierWithGroupSync();

    if (s_inv[localId] == 0.0) return;

    int row = localId / GRID_W;
    int col = localId % GRID_W;
    float3 selfPos  = s_pos[localId];
    float  selfInv  = s_inv[localId];
    float3 totalCorr = 0;

    // Helper macro: apply one distance constraint toward neighbor nid
    #define APPLY_CONSTRAINT(nid, restLen) \
    { \
        float3 nb  = s_pos[nid]; \
        float  ni  = s_inv[nid]; \
        float3 d   = nb - selfPos; \
        float  len = length(d); \
        if (len > 1e-5 && selfInv + ni > 1e-5) \
        { \
            float corr = (len - (restLen)) / len * (selfInv / (selfInv + ni)); \
            totalCorr += d * corr; \
        } \
    }

    float diagLen = _CellSize * 1.41421356;
    float bend    = _CellSize * 2.0;

    // Stretch — horizontal
    if (col + 1 < GRID_W) APPLY_CONSTRAINT(localId + 1,       _CellSize)
    if (col > 0)          APPLY_CONSTRAINT(localId - 1,       _CellSize)
    // Stretch — vertical
    if (row + 1 < GRID_H) APPLY_CONSTRAINT(localId + GRID_W,  _CellSize)
    if (row > 0)          APPLY_CONSTRAINT(localId - GRID_W,  _CellSize)
    // Shear — diagonals
    if (row+1 < GRID_H && col+1 < GRID_W) APPLY_CONSTRAINT(localId + GRID_W + 1, diagLen)
    if (row+1 < GRID_H && col > 0)        APPLY_CONSTRAINT(localId + GRID_W - 1, diagLen)
    if (row > 0 && col+1 < GRID_W)        APPLY_CONSTRAINT(localId - GRID_W + 1, diagLen)
    if (row > 0 && col > 0)               APPLY_CONSTRAINT(localId - GRID_W - 1, diagLen)
    // Bend — horizontal
    if (col + 2 < GRID_W) APPLY_CONSTRAINT(localId + 2,        bend)
    if (col > 1)          APPLY_CONSTRAINT(localId - 2,        bend)
    // Bend — vertical
    if (row + 2 < GRID_H) APPLY_CONSTRAINT(localId + GRID_W*2, bend)
    if (row > 1)          APPLY_CONSTRAINT(localId - GRID_W*2, bend)

    #undef APPLY_CONSTRAINT

    _Particles[pid].position = selfPos + totalCorr;
}

// ── Kernel_SolveCollisions ─────────────────────────────────────────────────

[numthreads(PARTICLES_PER_SOCK, 1, 1)]
void Kernel_SolveCollisions(uint3 gid : SV_GroupID, uint3 tid : SV_GroupThreadID)
{
    uint sockIdx  = gid.x;
    uint localId  = tid.x;
    SockData sock = _Socks[sockIdx];
    uint pid      = sock.startIndex + localId;

    ParticleData p = _Particles[pid];
    if (p.invMass == 0.0) return;

    float r = _ParticleRadius;

    // ── Scene colliders ──────────────────────────────────────────────────
    for (int ci = 0; ci < _TotalColliders; ci++)
    {
        ColliderData col = _Colliders[ci];

        if (col.type == COLLIDER_PLANE)
        {
            float3 n    = normalize(col.p1);
            float  d    = dot(col.p0, n);
            float  dist = dot(p.position, n) - d;
            if (dist < r)
                p.position += n * (r - dist);
        }
        else if (col.type == COLLIDER_SPHERE)
        {
            float3 diff = p.position - col.p0;
            float  dist = length(diff);
            float  minD = col.paramsVec.x + r;
            if (dist < minD && dist > 1e-5)
                p.position = col.p0 + normalize(diff) * minD;
        }
        else if (col.type == COLLIDER_CAPSULE)
        {
            float3 q    = ClosestPointOnSegment(col.p0, col.p1, p.position);
            float3 diff = p.position - q;
            float  dist = length(diff);
            float  minD = col.paramsVec.x + r;
            if (dist < minD && dist > 1e-5)
                p.position = q + normalize(diff) * minD;
        }
        else if (col.type == COLLIDER_BOX)
        {
            // col.p0 = box center, col.invMatrix = world-to-local rotation (no scale)
            float3 localP = mul((float3x3)col.invMatrix, (p.position - col.p0));
            float3 he     = col.paramsVec.xyz;
            float3 inside = he - abs(localP);

            if (all(inside > -r))
            {
                float3 correction = 0;
                if (all(inside > 0))
                {
                    // Fully inside: push out along shortest axis
                    if (inside.x < inside.y && inside.x < inside.z)
                        correction = float3(sign(localP.x) * (inside.x + r), 0, 0);
                    else if (inside.y < inside.z)
                        correction = float3(0, sign(localP.y) * (inside.y + r), 0);
                    else
                        correction = float3(0, 0, sign(localP.z) * (inside.z + r));
                }
                else
                {
                    // Outside but within r of a face/edge/corner
                    float3 clamped = clamp(localP, -he, he);
                    float3 diff    = localP - clamped;
                    float  dist    = length(diff);
                    if (dist < r && dist > 1e-5)
                        correction = normalize(diff) * (r - dist);
                }
                // Transform correction back to world space (invMatrix is orthonormal, so transpose = inverse)
                float3x3 localToWorld = transpose((float3x3)col.invMatrix);
                p.position += mul(localToWorld, correction);
            }
        }
    }

    // ── Cloth-cloth collision ────────────────────────────────────────────
    float ccMinDist = r * 2.0;
    SockData mySock = _Socks[sockIdx];

    for (uint otherSock = 0; otherSock < (uint)_TotalSocks; otherSock++)
    {
        if (otherSock == sockIdx) continue;

        SockData other = _Socks[otherSock];

        // Bounding sphere broadphase
        float3 sphereDiff = other.boundCenter - mySock.boundCenter;
        if (length(sphereDiff) > other.boundRadius + mySock.boundRadius + ccMinDist)
            continue;

        // Particle-vs-particle
        for (uint j = 0; j < PARTICLES_PER_SOCK; j++)
        {
            uint otherPid   = other.startIndex + j;
            float3 diff     = p.position - _Particles[otherPid].position;
            float  dist     = length(diff);
            if (dist < ccMinDist && dist > 1e-5)
                p.position += normalize(diff) * (ccMinDist - dist) * 0.5;
        }
    }

    _Particles[pid] = p;
}

// ── Kernel_UpdateNormals ───────────────────────────────────────────────────

[numthreads(PARTICLES_PER_SOCK, 1, 1)]
void Kernel_UpdateNormals(uint3 gid : SV_GroupID, uint3 tid : SV_GroupThreadID)
{
    uint sockIdx  = gid.x;
    uint localId  = tid.x;
    SockData sock = _Socks[sockIdx];
    uint pid      = sock.startIndex + localId;

    // Load sock positions into shared memory
    s_pos[localId] = _Particles[pid].position;
    GroupMemoryBarrierWithGroupSync();

    int    row    = localId / GRID_W;
    int    col    = localId % GRID_W;
    float3 pos    = s_pos[localId];
    float3 normal = 0;
    int    count  = 0;

    // Accumulate normals from surrounding quad triangles (cross products reversed for +Y default)
    if (col+1 < GRID_W && row+1 < GRID_H) { normal += cross(s_pos[localId+GRID_W]-pos, s_pos[localId+1]-pos); count++; }
    if (row+1 < GRID_H && col > 0)        { normal += cross(s_pos[localId+GRID_W-1]-pos, s_pos[localId+GRID_W]-pos); count++; }
    if (col > 0 && row > 0)               { normal += cross(s_pos[localId-1]-pos, s_pos[localId-GRID_W-1+1]-pos); count++; }
    if (row > 0 && col+1 < GRID_W)        { normal += cross(s_pos[localId+1]-pos, s_pos[localId-GRID_W+1]-pos); count++; }

    _Particles[pid].normal = count > 0 ? normalize(normal) : float3(0, 1, 0);
}
```

- [ ] **Step 3: Commit**

```bash
git add Assets/Socks/Shaders/SockCloth.compute
git commit -m "feat(cloth): add SockCloth compute shader with PBD kernels"
```

---

## Task 3: ClothSockInstance.cs — Procedural Mesh

**Files:**
- Create: `Assets/Socks/Scripts/ClothSim/ClothSockInstance.cs`

Each sock creates an 8×8 quad mesh in `Awake`. The mesh vertex positions and normals are overwritten each frame by the Simulator.

- [ ] **Step 1: Create the script**

```csharp
// Assets/Socks/Scripts/ClothSim/ClothSockInstance.cs
using UnityEngine;
using ClothSim;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ClothSockInstance : MonoBehaviour
{
    const int GridW = 8;
    const int GridH = 8;

    [SerializeField] float sockWidth  = 0.25f;
    [SerializeField] float sockHeight = 0.35f;

    public Mesh      Mesh        { get; private set; }
    public int       StartIndex  { get; private set; } = -1;
    public int       SockIndex   { get; private set; } = -1;

    // Reusable arrays to avoid per-frame allocation
    readonly Vector3[] _positions = new Vector3[GridW * GridH];
    readonly Vector3[] _normals   = new Vector3[GridW * GridH];

    void Awake()
    {
        Mesh = BuildMesh();
        GetComponent<MeshFilter>().sharedMesh = Mesh;
    }

    void Start()
    {
        ClothSockSimulator.Instance.RegisterSock(this);
    }

    void OnDestroy()
    {
        if (ClothSockSimulator.Instance != null)
            ClothSockSimulator.Instance.UnregisterSock(this);
    }

    public void SetRegistration(int sockIndex, int startIndex)
    {
        SockIndex  = sockIndex;
        StartIndex = startIndex;
    }

    // Called by Simulator each frame with the latest particle data
    public void UpdateMesh(ParticleData[] allParticles)
    {
        for (int i = 0; i < GridW * GridH; i++)
        {
            _positions[i] = allParticles[StartIndex + i].position;
            _normals[i]   = allParticles[StartIndex + i].normal;
        }
        Mesh.vertices = _positions;
        Mesh.normals  = _normals;
        Mesh.RecalculateBounds();
    }

    public float SockWidth  => sockWidth;
    public float SockHeight => sockHeight;

    // ── Mesh generation ───────────────────────────────────────────────────

    Mesh BuildMesh()
    {
        var mesh = new Mesh { name = "SockClothMesh" };
        mesh.MarkDynamic();

        var verts = new Vector3[GridW * GridH];
        var uvs   = new Vector2[GridW * GridH];

        for (int r = 0; r < GridH; r++)
        for (int c = 0; c < GridW; c++)
        {
            int i  = r * GridW + c;
            verts[i] = new Vector3(
                (c / (float)(GridW - 1) - 0.5f) * sockWidth,
                0f,
                (r / (float)(GridH - 1) - 0.5f) * sockHeight
            );
            uvs[i] = new Vector2(c / (float)(GridW - 1), 1f - r / (float)(GridH - 1));
        }

        int quadCount = (GridW - 1) * (GridH - 1);
        var tris = new int[quadCount * 6];
        int t = 0;
        for (int r = 0; r < GridH - 1; r++)
        for (int c = 0; c < GridW - 1; c++)
        {
            int tl = r * GridW + c;
            int tr = r * GridW + c + 1;
            int bl = (r + 1) * GridW + c;
            int br = (r + 1) * GridW + c + 1;
            // CCW winding → normal points +Y for flat XZ mesh
            tris[t++] = tl; tris[t++] = bl; tris[t++] = tr;
            tris[t++] = bl; tris[t++] = br; tris[t++] = tr;
        }

        mesh.vertices  = verts;
        mesh.uv        = uvs;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals(); // replaced each frame by Simulator
        return mesh;
    }
}
```

- [ ] **Step 2: Manually verify mesh looks correct**

Add one `ClothSockInstance` to an empty scene, enter Play mode, check Scene view — should see a flat rectangular grid with 8×8 vertices.

- [ ] **Step 3: Commit**

```bash
git add Assets/Socks/Scripts/ClothSim/ClothSockInstance.cs
git commit -m "feat(cloth): add ClothSockInstance with procedural 8x8 mesh"
```

---

## Task 4: ClothSockSimulator.cs — Buffer Management and Registration

**Files:**
- Create: `Assets/Socks/Scripts/ClothSim/ClothSockSimulator.cs`

This task builds only the buffer allocation and registration API — no simulation loop yet.

- [ ] **Step 1: Create the script skeleton with buffer management**

```csharp
// Assets/Socks/Scripts/ClothSim/ClothSockSimulator.cs
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using ClothSim;

public class ClothSockSimulator : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────
    [SerializeField] ComputeShader  shader;
    [SerializeField] int   substeps            = 4;
    [SerializeField] int   constraintIterations = 3;
    [SerializeField] float damping             = 0.98f;
    [SerializeField] float particleRadius      = 0.02f;
    [SerializeField] Vector3 gravity           = new Vector3(0f, -9.81f, 0f);
    [SerializeField] int   maxSocks            = 64;
    [SerializeField] int   maxColliders        = 32;

    // ── Singleton ─────────────────────────────────────────────────────────
    public static ClothSockSimulator Instance { get; private set; }

    // ── Buffers ───────────────────────────────────────────────────────────
    ComputeBuffer _particlesBuf;
    ComputeBuffer _socksBuf;
    ComputeBuffer _collidersBuf;

    // ── CPU-side arrays (uploaded to GPU each frame) ──────────────────────
    ParticleData[]  _particlesCPU;
    SockData[]      _socksCPU;
    ColliderData[]  _collidersCPU;

    // ── Registered socks ──────────────────────────────────────────────────
    readonly List<ClothSockInstance> _socks = new();
    int _nextStartIndex = 0;

    // ── Kernel IDs ────────────────────────────────────────────────────────
    int _kIntegrate;
    int _kConstraints;
    int _kCollisions;
    int _kNormals;

    // ── Scene collider sources ────────────────────────────────────────────
    readonly List<Collider> _sceneColliders = new();

    // ─────────────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        int totalParticles = maxSocks * 64;
        int particleStride = Marshal.SizeOf<ParticleData>();
        int sockStride     = Marshal.SizeOf<SockData>();
        int colliderStride = Marshal.SizeOf<ColliderData>();

        _particlesBuf  = new ComputeBuffer(totalParticles, particleStride);
        _socksBuf      = new ComputeBuffer(maxSocks,       sockStride);
        _collidersBuf  = new ComputeBuffer(maxColliders,   colliderStride);

        _particlesCPU  = new ParticleData[totalParticles];
        _socksCPU      = new SockData[maxSocks];
        _collidersCPU  = new ColliderData[maxColliders];

        _kIntegrate   = shader.FindKernel("Kernel_Integrate");
        _kConstraints = shader.FindKernel("Kernel_SolveConstraints");
        _kCollisions  = shader.FindKernel("Kernel_SolveCollisions");
        _kNormals     = shader.FindKernel("Kernel_UpdateNormals");

        BindBuffers();
    }

    void BindBuffers()
    {
        foreach (int k in new[] { _kIntegrate, _kConstraints, _kCollisions, _kNormals })
        {
            shader.SetBuffer(k, "_Particles",  _particlesBuf);
            shader.SetBuffer(k, "_Socks",      _socksBuf);
            shader.SetBuffer(k, "_Colliders",  _collidersBuf);
        }
    }

    void OnDestroy()
    {
        _particlesBuf?.Release();
        _socksBuf?.Release();
        _collidersBuf?.Release();
        if (Instance == this) Instance = null;
    }

    // ── Registration API ──────────────────────────────────────────────────

    public void RegisterSock(ClothSockInstance sock)
    {
        if (_socks.Count >= maxSocks)
        {
            Debug.LogWarning($"ClothSockSimulator: maxSocks ({maxSocks}) reached.");
            return;
        }

        int sockIndex  = _socks.Count;
        int startIndex = _nextStartIndex;
        _nextStartIndex += 64;

        sock.SetRegistration(sockIndex, startIndex);
        _socks.Add(sock);

        // Initialise particles for this sock in world space
        float cellW = sock.SockWidth  / 7f;
        float cellH = sock.SockHeight / 7f;
        float cellSize = (cellW + cellH) * 0.5f; // used as _CellSize (square approximation)

        Vector3 origin = sock.transform.position;
        for (int r = 0; r < 8; r++)
        for (int c = 0; c < 8; c++)
        {
            int pi = startIndex + r * 8 + c;
            Vector3 localPos = new Vector3(
                (c / 7f - 0.5f) * sock.SockWidth,
                0f,
                (r / 7f - 0.5f) * sock.SockHeight
            );
            _particlesCPU[pi].position     = origin + sock.transform.TransformVector(localPos);
            _particlesCPU[pi].prevPosition = _particlesCPU[pi].position;
            _particlesCPU[pi].invMass      = 1f;
            _particlesCPU[pi].normal       = Vector3.up;
        }

        // SockData entry
        _socksCPU[sockIndex] = new SockData
        {
            startIndex    = startIndex,
            particleCount = 64,
            boundCenter   = origin,
            boundRadius   = Mathf.Max(sock.SockWidth, sock.SockHeight),
            pinnedPosition = origin,
            pinnedIndex   = -1,
        };

        _particlesBuf.SetData(_particlesCPU, startIndex, startIndex, 64);
        _socksBuf.SetData(_socksCPU, sockIndex, sockIndex, 1);
    }

    public void UnregisterSock(ClothSockInstance sock)
    {
        _socks.Remove(sock);
        // Note: does not reclaim the buffer slot in this prototype
    }

    public void RegisterCollider(Collider col) => _sceneColliders.Add(col);
    public void UnregisterCollider(Collider col) => _sceneColliders.Remove(col);

    // ── Getters ───────────────────────────────────────────────────────────

    public int RegisteredSockCount => _socks.Count;

    public void SetPinnedParticle(int sockIndex, int localParticleIndex, Vector3 worldPos)
    {
        if (sockIndex < 0 || sockIndex >= _socks.Count) return;
        ref SockData s = ref _socksCPU[sockIndex];
        s.pinnedIndex    = localParticleIndex;
        s.pinnedPosition = worldPos;
    }

    public void ClearPinnedParticle(int sockIndex)
    {
        if (sockIndex < 0 || sockIndex >= _socks.Count) return;
        _socksCPU[sockIndex].pinnedIndex = -1;
        // Restore invMass for that particle
        int pid = _socksCPU[sockIndex].startIndex + Mathf.Max(0, _socksCPU[sockIndex].pinnedIndex);
        // Will be restored in the next Integrate pass since invMass 0 only when pinnedIndex matches
        _socksCPU[sockIndex].pinnedIndex = -1;
    }
}
```

- [ ] **Step 2: Check it compiles in Unity**

Open Unity. If errors appear, fix them before proceeding. Common issues: missing `using` directives, namespace mismatches.

- [ ] **Step 3: Commit**

```bash
git add Assets/Socks/Scripts/ClothSim/ClothSockSimulator.cs
git commit -m "feat(cloth): add ClothSockSimulator with buffer management and sock registration"
```

---

## Task 5: ClothSockSimulator.cs — Simulation Loop and Mesh Update

**Files:**
- Modify: `Assets/Socks/Scripts/ClothSim/ClothSockSimulator.cs`

Add the `Update()` loop that runs the substep pipeline, collects scene colliders, updates bounding spheres, and writes particle data back to meshes.

- [ ] **Step 1: Add the simulation loop and collider collection to ClothSockSimulator**

Add these methods inside `ClothSockSimulator` (after `ClearPinnedParticle`):

```csharp
    void Update()
    {
        if (_socks.Count == 0) return;

        int sockCount = _socks.Count;
        float dt      = Time.deltaTime / substeps;

        CollectSceneColliders();
        UploadSockData();

        shader.SetInt("_TotalSocks",     sockCount);
        shader.SetInt("_TotalColliders", _activeColliderCount);
        shader.SetFloat("_Damping",        damping);
        shader.SetFloat("_ParticleRadius", particleRadius);
        shader.SetVector("_Gravity",       gravity);

        // Compute average cell size from first sock (prototype: all socks same size)
        if (_socks.Count > 0)
        {
            float cw = _socks[0].SockWidth  / 7f;
            float ch = _socks[0].SockHeight / 7f;
            shader.SetFloat("_CellSize", (cw + ch) * 0.5f);
        }

        for (int s = 0; s < substeps; s++)
        {
            shader.SetFloat("_DeltaTime", dt);

            shader.Dispatch(_kIntegrate,   sockCount, 1, 1);
            for (int ci = 0; ci < constraintIterations; ci++)
                shader.Dispatch(_kConstraints, sockCount, 1, 1);
            shader.Dispatch(_kCollisions,  sockCount, 1, 1);
        }

        shader.Dispatch(_kNormals, sockCount, 1, 1);

        // Read particle data back to CPU (sync — acceptable for prototype)
        int totalParts = sockCount * 64;
        _particlesBuf.GetData(_particlesCPU, 0, 0, totalParts);

        // Update bounding spheres + sock meshes
        for (int si = 0; si < sockCount; si++)
        {
            UpdateBoundingSphere(si);
            _socks[si].UpdateMesh(_particlesCPU);
        }

        // Re-upload updated sock data (bounding spheres changed)
        _socksBuf.SetData(_socksCPU, 0, 0, sockCount);
    }

    int _activeColliderCount;

    void CollectSceneColliders()
    {
        _activeColliderCount = 0;
        foreach (Collider col in _sceneColliders)
        {
            if (!col || !col.enabled) continue;
            if (_activeColliderCount >= maxColliders) break;

            ColliderData cd = default;
            if (col is BoxCollider box)
            {
                Matrix4x4 worldToLocal = box.transform.worldToLocalMatrix;
                cd.type      = 3; // BOX
                cd.p0        = box.transform.TransformPoint(box.center);
                cd.paramsVec = new Vector4(box.size.x * 0.5f * box.transform.lossyScale.x,
                                           box.size.y * 0.5f * box.transform.lossyScale.y,
                                           box.size.z * 0.5f * box.transform.lossyScale.z, 0f);
                cd.invMatrix = box.transform.worldToLocalMatrix;
            }
            else if (col is SphereCollider sphere)
            {
                cd.type       = 1; // SPHERE
                cd.p0         = sphere.transform.TransformPoint(sphere.center);
                cd.paramsVec  = new Vector4(sphere.radius * sphere.transform.lossyScale.x, 0, 0, 0);
            }
            else if (col is CapsuleCollider cap)
            {
                Vector3 dir    = cap.direction == 0 ? Vector3.right :
                                 cap.direction == 1 ? Vector3.up : Vector3.forward;
                float   half   = cap.height * 0.5f - cap.radius;
                Vector3 center = cap.transform.TransformPoint(cap.center);
                cd.type       = 2; // CAPSULE
                cd.p0         = center - cap.transform.TransformDirection(dir) * half;
                cd.p1         = center + cap.transform.TransformDirection(dir) * half;
                cd.paramsVec  = new Vector4(cap.radius * cap.transform.lossyScale.x, 0, 0, 0);
            }
            else if (col is MeshCollider || col is TerrainCollider)
            {
                // Treat as flat plane at the collider's Y position (prototype simplification)
                cd.type = 0; // PLANE
                cd.p0   = col.bounds.min;
                cd.p1   = Vector3.up;
            }

            _collidersCPU[_activeColliderCount++] = cd;
        }
        _collidersBuf.SetData(_collidersCPU, 0, 0, Mathf.Max(1, _activeColliderCount));
    }

    void UploadSockData()
    {
        int sc = _socks.Count;
        if (sc > 0) _socksBuf.SetData(_socksCPU, 0, 0, sc);
    }

    void UpdateBoundingSphere(int si)
    {
        int   start  = _socksCPU[si].startIndex;
        Vector3 center = Vector3.zero;
        for (int i = 0; i < 64; i++)
            center += _particlesCPU[start + i].position;
        center /= 64f;

        float radius = 0f;
        for (int i = 0; i < 64; i++)
            radius = Mathf.Max(radius, Vector3.Distance(center, _particlesCPU[start + i].position));

        _socksCPU[si].boundCenter = center;
        _socksCPU[si].boundRadius = radius + particleRadius;
    }
```

- [ ] **Step 2: Set up a minimal test scene**

1. Create a new scene (or use scn_game)
2. Create `GameObject "ClothSockSystem"`, add `ClothSockSimulator`
3. Drag `SockCloth.compute` into the `Shader` field
4. Create `GameObject "Sock_01"`, add `ClothSockInstance` + `MeshFilter` + `MeshRenderer`
5. Add a `Plane` to the scene, add a `ColliderRegistrar` component that calls `simulator.RegisterCollider(GetComponent<Collider>())` in `Start()`
6. Enter Play — sock should fall and land on the plane

**Expected:** Sock falls, flops onto the plane, cloth folds slightly. If it explodes (flies off), decrease `deltaTime` by increasing substeps.

- [ ] **Step 3: Add a simple ColliderRegistrar helper script**

```csharp
// Assets/Socks/Scripts/ClothSim/ColliderRegistrar.cs
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ColliderRegistrar : MonoBehaviour
{
    void Start()   => ClothSockSimulator.Instance?.RegisterCollider(GetComponent<Collider>());
    void OnDestroy() => ClothSockSimulator.Instance?.UnregisterCollider(GetComponent<Collider>());
}
```

- [ ] **Step 4: Commit**

```bash
git add Assets/Socks/Scripts/ClothSim/ClothSockSimulator.cs \
        Assets/Socks/Scripts/ClothSim/ColliderRegistrar.cs
git commit -m "feat(cloth): add simulation loop, collider collection, and mesh update"
```

---

## Task 6: ClothSockPickup.cs — Pick-Up and Release

**Files:**
- Create: `Assets/Socks/Scripts/ClothSim/ClothSockPickup.cs`

Raycast on mouse click, find nearest particle, pin it. On release, unpin.

- [ ] **Step 1: Create the script**

```csharp
// Assets/Socks/Scripts/ClothSim/ClothSockPickup.cs
using UnityEngine;
using ClothSim;

public class ClothSockPickup : MonoBehaviour
{
    [SerializeField] Camera     cam;
    [SerializeField] float      holdDistance = 0.5f;
    [SerializeField] LayerMask  sockLayer    = ~0;

    ClothSockSimulator _sim;
    ClothSockInstance  _heldSock;
    int                _heldLocalIndex = -1;
    float              _holdZ;         // depth in camera space

    void Awake()
    {
        _sim = ClothSockSimulator.Instance != null
            ? ClothSockSimulator.Instance
            : FindFirstObjectByType<ClothSockSimulator>();
        if (cam == null) cam = Camera.main;
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0)) TryPickUp();
        if (Input.GetMouseButtonUp(0))   Release();
        if (_heldSock != null)           DragPinnedParticle();
    }

    void TryPickUp()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 50f, sockLayer)) return;

        ClothSockInstance sock = hit.collider.GetComponentInParent<ClothSockInstance>();
        if (sock == null) return;

        // Find the nearest particle to the hit point
        int   bestLocal = 0;
        float bestDist  = float.MaxValue;
        int   start     = sock.StartIndex;

        // Ask simulator for current particle positions
        ParticleData[] particles = _sim.GetParticlesCPU();
        for (int i = 0; i < 64; i++)
        {
            float d = Vector3.Distance(particles[start + i].position, hit.point);
            if (d < bestDist) { bestDist = d; bestLocal = i; }
        }

        _heldSock        = sock;
        _heldLocalIndex  = bestLocal;
        _holdZ           = Vector3.Distance(cam.transform.position, hit.point);
        _sim.SetPinnedParticle(sock.SockIndex, bestLocal, hit.point);
    }

    void DragPinnedParticle()
    {
        Ray     ray      = cam.ScreenPointToRay(Input.mousePosition);
        Vector3 worldPos = ray.origin + ray.direction * _holdZ;
        _sim.SetPinnedParticle(_heldSock.SockIndex, _heldLocalIndex, worldPos);
    }

    void Release()
    {
        if (_heldSock == null) return;
        _sim.ClearPinnedParticle(_heldSock.SockIndex);
        _heldSock       = null;
        _heldLocalIndex = -1;
    }
}
```

- [ ] **Step 2: Expose `GetParticlesCPU()` in ClothSockSimulator**

Add this method to `ClothSockSimulator`:

```csharp
    public ParticleData[] GetParticlesCPU() => _particlesCPU;
```

- [ ] **Step 3: Add a MeshCollider to each sock so raycasts hit it**

In `ClothSockInstance.Awake()`, add after `mesh.MarkDynamic()`:

```csharp
var mc = gameObject.AddComponent<MeshCollider>();
mc.sharedMesh = Mesh;
// Refresh mesh collider each frame so raycasts hit the deformed mesh
```

Add to `ClothSockInstance.UpdateMesh()` at the end:

```csharp
GetComponent<MeshCollider>().sharedMesh = Mesh; // update collider shape
```

- [ ] **Step 4: Add `ClothSockPickup` to the scene**

Add `ClothSockPickup` to `ClothSockSystem` or Camera. Set `Cam` reference. Enter Play, click a sock, drag it around.

**Expected:** Clicked sock's nearest vertex follows the mouse, rest of cloth hangs/sways.

- [ ] **Step 5: Commit**

```bash
git add Assets/Socks/Scripts/ClothSim/ClothSockPickup.cs \
        Assets/Socks/Scripts/ClothSim/ClothSockInstance.cs \
        Assets/Socks/Scripts/ClothSim/ClothSockSimulator.cs
git commit -m "feat(cloth): add pick-up system with raycasting and particle pinning"
```

---

## Task 7: ClothSockSpawner.cs and Scene Setup

**Files:**
- Create: `Assets/Socks/Scripts/ClothSim/ClothSockSpawner.cs`
- Create: `Assets/Socks/Scenes/scn_clothtest.unity` (new test scene)

Spawns N socks at randomised positions above the floor.

- [ ] **Step 1: Create the spawner**

```csharp
// Assets/Socks/Scripts/ClothSim/ClothSockSpawner.cs
using UnityEngine;

public class ClothSockSpawner : MonoBehaviour
{
    [SerializeField] GameObject sockPrefab;
    [SerializeField] int   count      = 30;
    [SerializeField] float spawnArea  = 2f;
    [SerializeField] float spawnHeight = 1.5f;
    [SerializeField] Material[] sockMaterials;

    void Start()
    {
        for (int i = 0; i < count; i++)
        {
            Vector3 pos = new Vector3(
                Random.Range(-spawnArea, spawnArea),
                spawnHeight + Random.Range(0f, 0.5f),
                Random.Range(-spawnArea, spawnArea)
            );

            GameObject go = Instantiate(sockPrefab, pos, Quaternion.identity);
            go.name = $"Sock_{i:D2}";

            if (sockMaterials != null && sockMaterials.Length > 0)
                go.GetComponent<MeshRenderer>().material = sockMaterials[i % sockMaterials.Length];
        }
    }
}
```

- [ ] **Step 2: Create the Sock prefab**

In Unity Editor:
1. Create an empty `GameObject`
2. Add `ClothSockInstance`, `MeshFilter`, `MeshRenderer`
3. Assign `cloth1.mat` (from `Assets/Socks/Materials/`) to MeshRenderer
4. Save as `Assets/Socks/Prefabs/SockCloth.prefab`

- [ ] **Step 3: Set up the test scene**

Create scene `Assets/Socks/Scenes/scn_clothtest.unity` with this hierarchy:

```
Main Camera
  ClothSockPickup (component)

ClothSockSystem
  ClothSockSimulator (component) ← assign SockCloth.compute
  ClothSockPickup ← or put on camera (your choice)

Floor (Plane, scale 5×1×5, position 0,0,0)
  ColliderRegistrar (component)

Box_A (Cube, position 1, 0.25, 0)
  ColliderRegistrar (component)

ClothSockSpawner (component) ← assign SockCloth prefab, count=10 to start
```

- [ ] **Step 4: Enter Play and do a smoke test**

1. 10 socks should spawn above the floor
2. They should fall, fold, land on the floor
3. Some may drape over the box
4. Click a sock → vertex follows mouse; release → sock falls

If socks tunnel through floor: increase `substeps` (try 6 or 8).  
If simulation is jittery/explodes: decrease `_CellSize` or increase `damping`.

- [ ] **Step 5: Increase count to 30, verify performance**

Set `count = 30` in `ClothSockSpawner`. Verify it stays above 30fps in Play mode (check Stats window).

- [ ] **Step 6: Commit**

```bash
git add Assets/Socks/Scripts/ClothSim/ClothSockSpawner.cs \
        Assets/Socks/Prefabs/SockCloth.prefab \
        Assets/Socks/Scenes/scn_clothtest.unity
git commit -m "feat(cloth): add spawner and test scene for cloth sock prototype"
```

---

## Self-Review Checklist

| Spec Requirement | Covered By |
|-----------------|------------|
| 30+ socks simultaneously | Task 7 (spawner, count=30) |
| 8×8 grid mesh per sock | Task 3 (ClothSockInstance.BuildMesh) |
| PBD compute shader | Task 2 (SockCloth.compute) |
| Collision: floor/plane | Task 2 (COLLIDER_PLANE) + Task 5 (CollectSceneColliders) |
| Collision: box | Task 2 (COLLIDER_BOX) + Task 5 |
| Collision: capsule | Task 2 (COLLIDER_CAPSULE) + Task 5 |
| Collision: cloth-cloth | Task 2 (Kernel_SolveCollisions cloth-cloth section) |
| Pick-up: one pinned vertex | Task 6 (ClothSockPickup.cs) |
| Standalone prototype | All tasks — no integration with Sock.cs |
| Bounding sphere broadphase for cloth-cloth | Task 2 + Task 5 (UpdateBoundingSphere) |
| Parameters tunable in Inspector | Task 4 (ClothSockSimulator Inspector fields) |

**Known limitation:** `ClothSockSimulator.Update()` calls `GetData()` synchronously — this is correct but adds a GPU-CPU sync point. For >30 socks at 60fps it may become a bottleneck; replace with `AsyncGPUReadback.RequestIntoNativeArray` in a future pass.
