# Game Start System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show a KeyArt CanvasGroup on Play, auto-spawn socks in the background, then fade the canvas to alpha 0 when spawning finishes.

**Architecture:** `SockSpawner` gains a public `BeginSpawn()` entry point and an `OnSpawnComplete` event. A new `GameStarter` MonoBehaviour (on the GameManager prefab) subscribes to that event, triggers spawning on `Start()`, and DOTween-fades the `CanvasGroup` to alpha 0 on completion.

**Tech Stack:** Unity 6 (6000.3.10f1), DOTween, Rewired

---

### Task 1: Refactor SockSpawner

**Files:**
- Modify: `Assets/Socks/Scripts/Sock/SockSpawner.cs`

- [ ] **Step 1: Add `OnSpawnComplete` event and `BeginSpawn()` method**

Replace the current `Update()` and `SpawnAll()` methods with the following. Key changes:
- `BeginSpawn()` is the single public entry point — guards against double-spawn.
- `_spawning = true` moves to the top of `SpawnAll()` so the guard at the bottom also fires `OnSpawnComplete` (canvas always fades, even if setup is missing).
- `Update()` now just delegates to `BeginSpawn()`.

```csharp
public event System.Action OnSpawnComplete;

public void BeginSpawn()
{
    if (!_spawning)
        StartCoroutine(SpawnAll());
}

void Update()
{
    if (_player.GetButtonDown(spawnActionName))
        BeginSpawn();
}

IEnumerator SpawnAll()
{
    _spawning = true;

    if (sockPrefabs == null || sockPrefabs.Length == 0 || spawnZones == null || spawnZones.Length == 0 || sourceMaterial == null)
    {
        _spawning = false;
        OnSpawnComplete?.Invoke();
        yield break;
    }

    var queue = BuildShuffledQueue();

    foreach (Material mat in queue)
    {
        SpawnSock(mat);
        yield return new WaitForSeconds(spawnInterval);
    }

    _spawning = false;
    OnSpawnComplete?.Invoke();
}
```

- [ ] **Step 2: Commit**

```bash
git add Assets/Socks/Scripts/Sock/SockSpawner.cs
git commit -m "feat: add BeginSpawn() and OnSpawnComplete event to SockSpawner"
```

---

### Task 2: Create GameStarter

**Files:**
- Create: `Assets/Socks/Scripts/GameStarter.cs`

- [ ] **Step 1: Create the script**

```csharp
using DG.Tweening;
using UnityEngine;

public class GameStarter : MonoBehaviour
{
    [SerializeField] SockSpawner spawner;
    [SerializeField] CanvasGroup keyArtCanvas;
    [SerializeField] float fadeDuration = 0.5f;

    void Start()
    {
        spawner.OnSpawnComplete += HandleSpawnComplete;
        spawner.BeginSpawn();
    }

    void OnDestroy()
    {
        spawner.OnSpawnComplete -= HandleSpawnComplete;
    }

    void HandleSpawnComplete()
    {
        keyArtCanvas.DOFade(0f, fadeDuration)
            .OnComplete(() => keyArtCanvas.blocksRaycasts = false);
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Assets/Socks/Scripts/GameStarter.cs
git commit -m "feat: add GameStarter — auto-spawn on Start, fade KeyArt canvas on complete"
```

---

### Task 3: Wire up in Unity Editor

**Files:**
- Modify: `Assets/Socks/Prefabs/GameManager.prefab` (w Unity Editorze)

- [ ] **Step 1: Dodaj komponent GameStarter do prefaba GameManager**
  - Otwórz `Assets/Socks/Prefabs/GameManager.prefab`
  - Add Component → `GameStarter`

- [ ] **Step 2: Przypisz pola w Inspectorze**
  - `Spawner` → przeciągnij komponent `SockSpawner`
  - `Key Art Canvas` → przeciągnij `CanvasGroup` na Canvasie z KeyArtem
  - `Fade Duration` → ustaw żądany czas fade

- [ ] **Step 3: Skonfiguruj CanvasGroup**
  - Na `CanvasGroup`: `Alpha = 1`, `Interactable = true`, `Blocks Raycasts = true`
  - Upewnij się że obraz KeyArt jest widoczny przy alpha = 1

- [ ] **Step 4: Weryfikacja w Play Mode**
  - Wciśnij Play — canvas widoczny, skarpety spawnują się jedna po drugiej
  - Po ostatniej skarpecie canvas znika (fade do alpha 0)
  - `Blocks Raycasts` powinno być `false` po zakończeniu fade
