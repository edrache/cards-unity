# Game Start System — Design Spec

**Date:** 2026-06-14

## Overview

When Play starts, a KeyArt image is shown on a Canvas. Socks spawn automatically in the background. When spawning finishes, the CanvasGroup fades to alpha 0 and becomes non-interactive.

## Components

### `SockSpawner` (modified)

- Add `public event Action OnSpawnComplete` — fired at the end of `SpawnAll()`.
- Add `public void BeginSpawn()` — public entry point that starts the `SpawnAll()` coroutine if not already spawning.
- `Update()` button trigger calls `BeginSpawn()` (unchanged behavior).

### `GameStarter` (new MonoBehaviour)

Lives on the `GameManager` prefab alongside `SockSpawner`.

| Field | Type | Notes |
|---|---|---|
| `spawner` | `SockSpawner` | Reference to the spawner |
| `keyArtCanvas` | `CanvasGroup` | Canvas showing the KeyArt image |
| `fadeDuration` | `float` | Set in Inspector by designer |

**Flow:**

1. `Start()` — subscribes to `spawner.OnSpawnComplete`, calls `spawner.BeginSpawn()`.
2. `OnSpawnComplete` callback — runs `keyArtCanvas.DOFade(0, fadeDuration)`, on tween complete sets `keyArtCanvas.blocksRaycasts = false`.
3. `OnDestroy()` — unsubscribes from event.

## Canvas Setup (by designer in Editor)

`CanvasGroup` starts with: `alpha = 1`, `interactable = true`, `blocksRaycasts = true`.

## What Is Not Covered

- Re-triggering the intro sequence (e.g. after a restart) — out of scope.
- Button-triggered re-spawn does not re-show the KeyArt canvas — out of scope.
