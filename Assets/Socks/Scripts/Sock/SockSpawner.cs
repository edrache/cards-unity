using System.Collections;
using System.Collections.Generic;
using CardsUnity;
using Obi;
using Rewired;
using UnityEngine;

public class SockSpawner : MonoBehaviour
{
    [SerializeField] GameObject[] sockPrefabs;
    [SerializeField] BoxCollider[] spawnZones;
    [SerializeField] Material sourceMaterial;
    [Header("Obi")]
    [Tooltip("Scene solver that should own spawned Obi actors.")]
    [SerializeField] ObiSolver spawnSolver;
    [Header("Input")]
    [Tooltip("Rewired action name used to trigger spawning.")]
    [SerializeField] string spawnActionName = "SpawnSock";

    [Header("Pairs")]
    [SerializeField] int pairCount = 5;
    [SerializeField] float spawnInterval = 0.5f;

    [Header("Colors")]
    [SerializeField] Color[] sockColors;

    [Header("Textures")]
    [SerializeField] Texture2D[] albedoTextures;
    [SerializeField] Texture2D[] detailMapTextures;

    static readonly int DetailMapColorId = Shader.PropertyToID("_DetailMapColor");
    static readonly int BaseMapId        = Shader.PropertyToID("_BaseMap");
    static readonly int DetailMapId      = Shader.PropertyToID("_DetailMap");

    Player _player;
    bool _spawning;
    int _spawnActionId = -1;
    string _cachedSpawnActionName;

    public event System.Action OnSpawnComplete;

    void Awake()
    {
        RefreshSpawnActionId();
    }

    public void BeginSpawn()
    {
        if (!_spawning)
            StartCoroutine(SpawnAll());
    }

    [ContextMenu("Spawn Socks")]
    void ContextMenuSpawn()
    {
        BeginSpawn();
    }

    void Update()
    {
        EnsurePlayer();
        RefreshSpawnActionId();

        if (_player != null && _spawnActionId >= 0 && _player.GetButtonDown(_spawnActionId))
            BeginSpawn();
    }

    void OnValidate()
    {
        if (Application.isPlaying)
            RefreshSpawnActionId();
    }

    void RefreshSpawnActionId()
    {
        if (_cachedSpawnActionName == spawnActionName)
            return;

        _cachedSpawnActionName = spawnActionName;

        if (string.IsNullOrWhiteSpace(spawnActionName))
        {
            _spawnActionId = -1;
            return;
        }

        if (!ReInput.isReady)
        {
            _spawnActionId = -1;
            return;
        }

        _spawnActionId = ReInput.mapping.GetActionId(spawnActionName);
    }

    void EnsurePlayer()
    {
        if (_player != null || !ReInput.isReady)
            return;

        _player = ReInput.players.GetPlayer(0);
    }

    IEnumerator SpawnAll()
    {
        _spawning = true;

        if (sockPrefabs == null || sockPrefabs.Length == 0 || spawnZones == null || spawnZones.Length == 0)
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

    List<Material> BuildShuffledQueue()
    {
        var list = new List<Material>(pairCount * 2);

        for (int i = 0; i < pairCount; i++)
        {
            if (sourceMaterial == null)
            {
                list.Add(null);
                list.Add(null);
                continue;
            }

            Material mat = new Material(sourceMaterial);

            if (sockColors != null && sockColors.Length > 0)
                mat.SetColor(DetailMapColorId, sockColors[Random.Range(0, sockColors.Length)]);

            if (albedoTextures != null && albedoTextures.Length > 0)
                mat.SetTexture(BaseMapId, albedoTextures[Random.Range(0, albedoTextures.Length)]);

            if (detailMapTextures != null && detailMapTextures.Length > 0)
                mat.SetTexture(DetailMapId, detailMapTextures[Random.Range(0, detailMapTextures.Length)]);

            list.Add(mat);
            list.Add(mat);
        }

        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }

        return list;
    }

    void SpawnSock(Material mat)
    {
        Bounds b = spawnZones[Random.Range(0, spawnZones.Length)].bounds;
        Vector3 pos = new Vector3(
            Random.Range(b.min.x, b.max.x),
            Random.Range(b.min.y, b.max.y),
            Random.Range(b.min.z, b.max.z)
        );
        Quaternion rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        GameObject prefab = sockPrefabs[Random.Range(0, sockPrefabs.Length)];
        Transform parent = spawnSolver != null ? spawnSolver.transform : null;
        GameObject go    = Instantiate(prefab, pos, rot, parent);

        go = MoveSpawnedObiContentToSceneSolver(go);

        ObiActor spawnedSockActor = go.GetComponentInChildren<ObiActor>(true);
        SockDragController dragController = SockGrabAutoBinder.FindPreferredController();
        if (dragController != null && spawnedSockActor != null)
            dragController.BindSockActorToDragger(spawnedSockActor);

        SockGrabAutoBinder grabBinder = go.GetComponent<SockGrabAutoBinder>();
        if (grabBinder == null)
            grabBinder = go.GetComponentInChildren<SockGrabAutoBinder>(true);

        grabBinder?.BindIfNeeded();

        Sock sock = go.GetComponent<Sock>();
        if (sock != null)
        {
            sock.SourcePrefab = prefab;
            if (mat != null)
                sock.SetMaterial(mat);
        }
    }

    GameObject MoveSpawnedObiContentToSceneSolver(GameObject spawnedRoot)
    {
        if (spawnSolver == null || spawnedRoot == null)
            return spawnedRoot;

        ObiSolver localSolver = spawnedRoot.GetComponent<ObiSolver>();
        if (localSolver == null || localSolver == spawnSolver || !HasOnlyTransformAndSolver(spawnedRoot))
            return spawnedRoot;

        if (spawnedRoot.transform.childCount == 0)
            return spawnedRoot;

        Transform promotedChild = spawnedRoot.transform.GetChild(0);
        promotedChild.SetParent(spawnSolver.transform, true);
        Destroy(spawnedRoot);
        return promotedChild.gameObject;
    }

    static bool HasOnlyTransformAndSolver(GameObject gameObject)
    {
        Component[] components = gameObject.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component is Transform || component is ObiSolver)
                continue;

            return false;
        }

        return true;
    }
}
